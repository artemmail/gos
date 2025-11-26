using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Options;
using System.Text.RegularExpressions;

namespace Zakupki.Fetcher.Services
{
    public class AttachmentMarkdownService
    {
        private static readonly Dictionary<string, string> FormatMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            [".doc"] = "docx",
            [".docx"] = "docx",
            [".rtf"] = "html",
            [".pdf"] = "html",   // PDF → HTML → Markdown
            [".html"] = "html",
            [".htm"] = "html",
            [".xls"] = "html",   // XLS/XLSX → HTML → Markdown
            [".xlsx"] = "html"
        };

        private const string LuaFilterFileName = "parse-html.lua";

        /// <summary>
        /// Lua-фильтр:
        /// 1) перепарсивает raw HTML-блоки (в т.ч. таблицы) в нормальные pandoc-блоки;
        /// 2) любые таблицы превращает в одну JSON-строку (Paragraph) без лишних управляющих последовательностей.
        /// </summary>
        private const string LuaFilterContent = @"
local dq = string.char(34)

-- минимальное экранирование для JSON:
--  - убираем переводы строк (меняем на пробелы)
--  - экранируем только обратный слэш
local function esc(s)
  s = s:gsub('\\', '\\\\')
  s = s:gsub('\r', ' ')
  s = s:gsub('\n', ' ')
  return s
end

local function json_array_of_strings(list)
  local out = {}
  for i, v in ipairs(list) do
    out[i] = dq .. esc(v) .. dq
  end
  return '[' .. table.concat(out, ',') .. ']'
end

-- Перепарс сырых HTML-блоков, чтобы таблицы из raw html стали нормальными Table
function RawBlock(raw)
  if raw.format and raw.format:match('html') then
    local doc = pandoc.read(raw.text, 'html')
    return doc.blocks
  end
  return raw
end

function RawInline(raw)
  if raw.format and raw.format:match('html') then
    local doc = pandoc.read(raw.text, 'html')
    return doc.blocks
  end
  return raw
end

-- Любую таблицу превращаем в одну JSON-строку
function Table(tbl)
  local simple = pandoc.utils.to_simple_table(tbl)
  if not simple then
    return tbl
  end

  -- заголовки
  local headers = {}
  for i, cell in ipairs(simple.headers) do
    headers[i] = pandoc.utils.stringify(cell)
  end

  -- строки
  local rowJsonParts = {}
  for r, row in ipairs(simple.rows) do
    local cols = {}
    for c, cell in ipairs(row) do
      cols[c] = pandoc.utils.stringify(cell)
    end
    rowJsonParts[r] = json_array_of_strings(cols)
  end

  local headerJson = json_array_of_strings(headers)
  local rowsJson = '[' .. table.concat(rowJsonParts, ',') .. ']'

  local json = '{'
    .. dq .. 'type' .. dq .. ':' .. dq .. 'table' .. dq
    .. ',' .. dq .. 'header' .. dq .. ':' .. headerJson
    .. ',' .. dq .. 'rows' .. dq .. ':' .. rowsJson
    .. '}'

  -- возвращаем как обычный параграф с одной строкой JSON
  return pandoc.Para({ pandoc.Str(json) })
end
";

        private readonly AttachmentConversionOptions _options;
        private readonly ILogger<AttachmentMarkdownService> _logger;

        public AttachmentMarkdownService(
            IOptions<AttachmentConversionOptions> options,
            ILogger<AttachmentMarkdownService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public bool IsSupported(NoticeAttachment attachment)
        {
            if (attachment is null)
            {
                throw new ArgumentNullException(nameof(attachment));
            }

            if (string.IsNullOrWhiteSpace(attachment.FileName))
            {
                return false;
            }

            var extension = Path.GetExtension(attachment.FileName);
            return !string.IsNullOrWhiteSpace(extension) && FormatMappings.ContainsKey(extension);
        }

        public static string RemoveControlChars(string? input, bool removeJsonSymbols = false)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            var sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                // 1) отбрасываем все управляющие символы (в т.ч. \r, \n, \t)
                if (char.IsControl(c))
                    continue;

                // 2) при необходимости убираем "служебные" знаки JSON
                if (removeJsonSymbols)
                {
                    if (c == '\\' || c == '"' || c == '[' || c == ']')
                        continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        public async Task<string> ConvertToMarkdownAsync(NoticeAttachment attachment, CancellationToken cancellationToken)
        {
            if (attachment is null)
            {
                throw new ArgumentNullException(nameof(attachment));
            }

            if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
            {
                throw new InvalidOperationException("Attachment does not contain binary content for conversion.");
            }

            if (string.IsNullOrWhiteSpace(attachment.FileName))
            {
                throw new InvalidOperationException("Attachment file name is missing.");
            }

            var extension = Path.GetExtension(attachment.FileName);

            if (string.IsNullOrWhiteSpace(extension) || !FormatMappings.TryGetValue(extension, out var format))
            {
                throw new NotSupportedException($"Conversion for files with extension '{extension}' is not supported.");
            }

            var pandocPath = _options.PandocPath;

            if (string.IsNullOrWhiteSpace(pandocPath))
            {
                throw new InvalidOperationException("PandocPath is not configured. Conversion cannot be performed.");
            }

            var tempDirectory = Path.GetTempPath();
            var tempFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var tempFilePath = Path.Combine(tempDirectory, tempFileName);

            await File.WriteAllBytesAsync(tempFilePath, attachment.BinaryContent, cancellationToken).ConfigureAwait(false);

            var inputFilePath = tempFilePath;
            var filesToCleanup = new List<string> { tempFilePath };

            // DOC → DOCX через LibreOffice
            if (string.Equals(extension, ".doc", StringComparison.OrdinalIgnoreCase))
            {
                inputFilePath = await ConvertToDocxAsync(tempFilePath, cancellationToken).ConfigureAwait(false);
                filesToCleanup.Add(inputFilePath);
            }
            // PDF / XLS / XLSX → HTML через LibreOffice
            else if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(extension, ".rtf", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                inputFilePath = await ConvertToHtmlAsync(tempFilePath, cancellationToken).ConfigureAwait(false);
                filesToCleanup.Add(inputFilePath);
            }
            // DOCX / HTML / HTM идут напрямую в Pandoc

            try
            {
                var workingDirectory = GetWorkingDirectory();

                if (!Directory.Exists(workingDirectory))
                {
                    throw new InvalidOperationException($"Указанный рабочий каталог Pandoc '{workingDirectory}' не существует.");
                }

                // Гарантируем наличие Lua-фильтра в папке с exe
                var luaFilterPath = EnsureLuaFilterFileInAppDirectory();

                var arguments = BuildPandocArguments(inputFilePath, format, luaFilterPath);

                var consoleCommand = $"\"{pandocPath}\" {arguments}";
                _logger.LogInformation("Pandoc console command: {Command}", consoleCommand);

                var startInfo = new ProcessStartInfo
                {
                    FileName = pandocPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                };

                using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start Pandoc process for attachment {AttachmentId}", attachment.Id);
                    throw new InvalidOperationException("Не удалось запустить процесс Pandoc для конвертации файла.", ex);
                }

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                var markdown = await outputTask.ConfigureAwait(false);
                var errors = await errorTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _logger.LogError(
                        "Pandoc conversion failed for attachment {AttachmentId} with exit code {ExitCode}. Errors: {Errors}",
                        attachment.Id,
                        process.ExitCode,
                        errors);

                    throw new InvalidOperationException("Конвертация файла в Markdown завершилась с ошибкой.");
                }

                if (!string.IsNullOrWhiteSpace(errors))
                {
                    _logger.LogWarning(
                        "Pandoc reported warnings during conversion of attachment {AttachmentId}: {Errors}",
                        attachment.Id,
                        errors);
                }


                //var unescaped = Regex.Unescape(markdown);
                var cleaned = RemoveControlChars(markdown, removeJsonSymbols: true);


                

                //var t = markdown.Replace("\\_","_").Replace("\\\"\"","\"");
                return cleaned;
            }
            catch(Exception er)
            {
                int a = 0;
                return ";";
            }
            finally
            {
                foreach (var filePath in filesToCleanup)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch (Exception cleanupException)
                    {
                        _logger.LogWarning(cleanupException, "Failed to delete temporary file {TempFilePath}", filePath);
                    }
                }
            }
        }

        private static string BuildPandocArguments(string inputFilePath, string format, string luaFilterPath)
        {
            var builder = new StringBuilder();

            // исходный формат (html/docx)
            builder.Append("--from=").Append(format).Append(' ');

            // Lua-фильтр — полный путь к файлу в папке с exe
            builder.Append("--lua-filter=\"").Append(luaFilterPath).Append("\" ");

            // Обычный markdown. Таблицы уже переписаны в JSON-фрагменты.
            builder.Append("--to=markdown ");
            builder.Append("--standalone ");
            builder.Append("--wrap=none ");

            builder.Append('"').Append(inputFilePath).Append('"');
            return builder.ToString();
        }

        private string GetWorkingDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_options.PandocWorkingDirectory))
            {
                return _options.PandocWorkingDirectory!;
            }

            return Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Гарантирует, что Lua-фильтр лежит в папке с исполняемым файлом приложения.
        /// </summary>
        private string EnsureLuaFilterFileInAppDirectory()
        {
            var appDirectory = AppContext.BaseDirectory;
            var path = Path.Combine(appDirectory, LuaFilterFileName);

            try
            {
                path = EnsureLuaFilter(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create Lua filter in app directory {Path}. Using temporary directory instead.", path);

                try
                {
                    var fallbackDirectory = Path.Combine(Path.GetTempPath(), "pandoc-lua-filter");
                    Directory.CreateDirectory(fallbackDirectory);
                    var fallbackPath = Path.Combine(fallbackDirectory, LuaFilterFileName);

                    path = EnsureLuaFilter(fallbackPath);
                    _logger.LogInformation(
                        "Lua filter could not be created in app directory, fallback path used: {Path}",
                        fallbackPath);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Failed to create Lua filter file {Path}", path);
                    throw new InvalidOperationException("Не удалось подготовить Lua-фильтр для Pandoc.", fallbackEx);
                }
            }

            return path;
        }

        private string EnsureLuaFilter(string path)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, LuaFilterContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                _logger.LogInformation("Lua filter for Pandoc created at {Path}", path);
            }

            return path;
        }

        /// <summary>
        /// DOC → DOCX через LibreOffice (Writer).
        /// </summary>
        private async Task<string> ConvertToDocxAsync(string sourceFilePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.LibreOfficePath))
            {
                throw new InvalidOperationException("LibreOfficePath is not configured. Conversion to .docx cannot be performed.");
            }

            var libreOfficePath = _options.LibreOfficePath!;
            if (!File.Exists(libreOfficePath))
            {
                throw new InvalidOperationException(
                    $"LibreOffice executable not found at '{libreOfficePath}'. Проверь путь в настройках.");
            }

            var libreOfficeDir = Path.GetDirectoryName(libreOfficePath)
                                 ?? throw new InvalidOperationException(
                                     $"Не удалось определить каталог LibreOffice по пути '{libreOfficePath}'.");

            var outputDirectory = Path.GetDirectoryName(sourceFilePath) ?? Path.GetTempPath();
            var outputFilePath = Path.ChangeExtension(sourceFilePath, ".docx");

            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            var arguments =
                "--headless --nologo --norestore " +
                "--convert-to \"docx:MS Word 2007 XML\" " +
                $"--outdir \"{outputDirectory}\" " +
                $"\"{sourceFilePath}\"";

            var consoleCommand = $"\"{libreOfficePath}\" {arguments}";
            _logger.LogInformation("LibreOffice DOC→DOCX console command: {Command}", consoleCommand);

            var startInfo = new ProcessStartInfo
            {
                FileName = libreOfficePath,          // soffice.exe
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = libreOfficeDir    // каталог установки LO
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start LibreOffice process for conversion to DOCX for file {SourceFile}", sourceFilePath);
                throw new InvalidOperationException("Не удалось запустить LibreOffice для конвертации файла в .docx.", ex);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdOutput = await outputTask.ConfigureAwait(false);
            var stdError = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "LibreOffice conversion to DOCX failed for file {SourceFile} with exit code {ExitCode}. Output: {Output}. Errors: {Errors}",
                    sourceFilePath,
                    process.ExitCode,
                    stdOutput,
                    stdError);
                throw new InvalidOperationException("Конвертация файла в .docx завершилась с ошибкой.");
            }

            if (!File.Exists(outputFilePath))
            {
                _logger.LogError(
                    "LibreOffice conversion to DOCX did not produce the expected file for {SourceFile}. Output: {Output}. Errors: {Errors}",
                    sourceFilePath,
                    stdOutput,
                    stdError);
                throw new InvalidOperationException("Не удалось получить .docx файл после конвертации документа.");
            }

            return outputFilePath;
        }

        /// <summary>
        /// PDF / XLS / XLSX → HTML через LibreOffice.
        /// </summary>
        private async Task<string> ConvertToHtmlAsync(string sourceFilePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.LibreOfficePath))
            {
                throw new InvalidOperationException("LibreOfficePath is not configured. Conversion of files cannot be performed.");
            }

            var libreOfficePath = _options.LibreOfficePath!;
            if (!File.Exists(libreOfficePath))
            {
                throw new InvalidOperationException(
                    $"LibreOffice executable not found at '{libreOfficePath}'. Проверь путь в настройках.");
            }

            var libreOfficeDir = Path.GetDirectoryName(libreOfficePath)
                                 ?? throw new InvalidOperationException(
                                     $"Не удалось определить каталог LibreOffice по пути '{libreOfficePath}'.");

            var outputDirectory = Path.GetDirectoryName(sourceFilePath) ?? Path.GetTempPath();
            var outputFilePath = Path.ChangeExtension(sourceFilePath, ".html");

            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            var arguments =
                "--headless --nologo --norestore " +
                "--convert-to html " +
                $"--outdir \"{outputDirectory}\" " +
                $"\"{sourceFilePath}\"";

            var consoleCommand = $"\"{libreOfficePath}\" {arguments}";
            _logger.LogInformation("LibreOffice →HTML console command: {Command}", consoleCommand);

            var startInfo = new ProcessStartInfo
            {
                FileName = libreOfficePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = libreOfficeDir
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start LibreOffice process for HTML conversion (file {SourceFile})", sourceFilePath);
                throw new InvalidOperationException("Не удалось запустить LibreOffice для конвертации файла в HTML.", ex);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdOutput = await outputTask.ConfigureAwait(false);
            var stdError = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "LibreOffice HTML conversion failed for file {SourceFile} with exit code {ExitCode}. Output: {Output}. Errors: {Errors}",
                    sourceFilePath,
                    process.ExitCode,
                    stdOutput,
                    stdError);
                throw new InvalidOperationException("Конвертация файла в HTML завершилась с ошибкой.");
            }

            if (!File.Exists(outputFilePath))
            {
                _logger.LogError(
                    "LibreOffice HTML conversion did not produce the expected file for {SourceFile}. Output: {Output}. Errors: {Errors}",
                    sourceFilePath,
                    stdOutput,
                    stdError);
                throw new InvalidOperationException("Не удалось получить HTML файл после конвертации файла.");
            }

            return outputFilePath;
        }
    }
}
