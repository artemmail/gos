using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.EF2020;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public class ZakupkiClient
{
    private const string ServiceUrl = "https://int44.zakupki.gov.ru/eis-integration/services/getDocsIP";
    private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ZakupkiClient> _logger;
    private readonly ZakupkiOptions _options;

    public ZakupkiClient(HttpClient httpClient, IOptions<ZakupkiOptions> options, ILogger<ZakupkiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<NoticeDocument>> FetchByOrgRegionAsync(byte region, string documentType, string subsystem, DateTime date, CancellationToken cancellationToken)
    {
        var requestXml = SoapBuilder.BuildGetDocsByOrgRegion(_options.Token ?? string.Empty, region, subsystem, documentType, date);
        var response = await PostSoapAsync(requestXml, cancellationToken);
        var archiveUrl = SoapResponseParser.ParseArchiveUrl(response, out var faultMessage);
        if (!string.IsNullOrEmpty(faultMessage))
        {
            throw new InvalidOperationException($"SOAP fault: {faultMessage}");
        }

        if (string.IsNullOrWhiteSpace(archiveUrl))
        {
            _logger.LogInformation("No archive returned for date {Date}, region {Region}, document type {DocumentType}", date, region, documentType);
            return Array.Empty<NoticeDocument>();
        }

        return await DownloadArchiveAsync(archiveUrl!, documentType, region, date, cancellationToken);
    }

    public async Task<IReadOnlyList<NoticeDocument>> FetchByReestrNumberAsync(string purchaseNumber, CancellationToken cancellationToken)
    {
        var requestXml = SoapBuilder.BuildGetDocsByReestrNumber(_options.Token ?? string.Empty, purchaseNumber);
        var response = await PostSoapAsync(requestXml, cancellationToken);
        var archiveUrl = SoapResponseParser.ParseArchiveUrl(response, out var faultMessage);
        if (!string.IsNullOrEmpty(faultMessage))
        {
            throw new InvalidOperationException($"SOAP fault: {faultMessage}");
        }

        if (string.IsNullOrWhiteSpace(archiveUrl))
        {
            _logger.LogInformation("No archive returned for purchase {PurchaseNumber}", purchaseNumber);
            return Array.Empty<NoticeDocument>();
        }

        return await DownloadArchiveAsync(archiveUrl!, "package", 0, DateTime.UtcNow.Date, cancellationToken);
    }

    private async Task<byte[]> PostSoapAsync(string requestXml, CancellationToken cancellationToken)
    {
        using var content = new StringContent(requestXml, Encoding.UTF8, "text/xml");
        using var response = await _httpClient.PostAsync(ServiceUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<NoticeDocument>> DownloadArchiveAsync(string archiveUrl, string documentType, byte region, DateTime period, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, archiveUrl);
        request.Headers.TryAddWithoutValidation("individualPerson_token", _options.Token);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var bufferStream = new MemoryStream();
        var maxBytes = _options.MaxArchiveMegabytes > 0 ? _options.MaxArchiveMegabytes * 1024L * 1024L : long.MaxValue;
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException($"Archive size {total} exceeds configured limit of {_options.MaxArchiveMegabytes} MB");
            }

            await bufferStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        bufferStream.Position = 0;

        using var archive = new ZipArchive(bufferStream, ZipArchiveMode.Read, leaveOpen: true);
        var result = new List<NoticeDocument>();
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            await using var entryStream = entry.Open();
            using var entryBuffer = new MemoryStream();
            await entryStream.CopyToAsync(entryBuffer, cancellationToken);
            var content = entryBuffer.ToArray();

            Export? exportModel = null;
            try
            {
                exportModel = TryDeserialize(content);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to deserialize entry {EntryName} as Zakupki export", entry.FullName);
            }

            result.Add(new NoticeDocument(archiveUrl, documentType, region, period, entry.FullName, content, exportModel));
        }

        return result;
    }

    private static Export? TryDeserialize(byte[] content)
    {
        using var xmlStream = new MemoryStream(content);
        return ZakupkiLoader.LoadFromStream(xmlStream);
    }

    private static class SoapBuilder
    {
        private const string WsNs = "http://zakupki.gov.ru/fz44/get-docs-ip/ws";

        public static string BuildGetDocsByOrgRegion(string token, int region, string subsystem, string documentType, DateTime date)
        {
            var now = DateTime.UtcNow;
            var exactDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var createDate = now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var escapedToken = SecurityElement.Escape(token) ?? string.Empty;
            var escapedSubsystem = SecurityElement.Escape(subsystem) ?? string.Empty;
            var escapedDocType = SecurityElement.Escape(documentType) ?? string.Empty;
            var regionString = region.ToString("D2", CultureInfo.InvariantCulture);

            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""{SoapNs}"" xmlns:ws=""{WsNs}"">
  <soapenv:Header>
    <individualPerson_token>{escapedToken}</individualPerson_token>
  </soapenv:Header>
  <soapenv:Body>
    <ws:getDocsByOrgRegionRequest>
      <index>
        <id>{Guid.NewGuid()}</id>
        <createDateTime>{createDate}</createDateTime>
        <mode>PROD</mode>
      </index>
      <selectionParams>
        <orgRegion>{regionString}</orgRegion>
        <subsystemType>{escapedSubsystem}</subsystemType>
        <documentType44>{escapedDocType}</documentType44>
        <periodInfo>
          <exactDate>{exactDate}</exactDate>
        </periodInfo>
      </selectionParams>
    </ws:getDocsByOrgRegionRequest>
  </soapenv:Body>
</soapenv:Envelope>";
        }

        public static string BuildGetDocsByReestrNumber(string token, string reestrNumber)
        {
            var now = DateTime.UtcNow;
            var createDate = now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var escapedToken = SecurityElement.Escape(token) ?? string.Empty;
            var escapedReestr = SecurityElement.Escape(reestrNumber) ?? string.Empty;

            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""{SoapNs}"" xmlns:ws=""{WsNs}"">
  <soapenv:Header>
    <individualPerson_token>{escapedToken}</individualPerson_token>
  </soapenv:Header>
  <soapenv:Body>
    <ws:getDocsByReestrNumberRequest>
      <index>
        <id>{Guid.NewGuid()}</id>
        <createDateTime>{createDate}</createDateTime>
        <mode>PROD</mode>
      </index>
      <selectionParams>
        <subsystemType>PRIZ</subsystemType>
        <reestrNumber>{escapedReestr}</reestrNumber>
      </selectionParams>
    </ws:getDocsByReestrNumberRequest>
  </soapenv:Body>
</soapenv:Envelope>";
        }
    }

    private static class SoapResponseParser
    {
        public static string? ParseArchiveUrl(byte[] response, out string? faultMessage)
        {
            faultMessage = null;
            if (response.Length == 0)
            {
                return null;
            }

            var xml = Encoding.UTF8.GetString(response);
            var doc = XDocument.Parse(xml);

            var fault = doc.Descendants(SoapNs + "Fault").FirstOrDefault();
            if (fault != null)
            {
                faultMessage = fault.Element("faultstring")?.Value ?? fault.Value;
                return null;
            }

            var archive = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "archiveUrl");
            return archive?.Value?.Trim();
        }
    }
}
