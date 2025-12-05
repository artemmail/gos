import json
import pathlib
import re
from typing import Dict, Any

swagger_path = pathlib.Path('swagger_v2.json')
out_path = pathlib.Path('src/Zakupki.MosApi/MosSwaggerClient.V2.cs')

swagger = json.loads(swagger_path.read_text(encoding='utf-8'))

definition_map: Dict[str, str] = {}


def sanitize_name(name: str) -> str:
    base = re.split(r'[.`\[]', name)[-1]
    base = re.sub(r'`\d+', '', base)
    base = re.sub(r'[^0-9A-Za-z_]', '', base)
    if not base:
        base = 'Anonymous'
    if base[0].isdigit():
        base = '_' + base
    return base


def get_type(schema: Dict[str, Any] | None) -> str:
    if schema is None:
        return 'object?'
    if '$ref' in schema:
        ref_name = schema['$ref'].split('/')[-1]
        return definition_map.setdefault(ref_name, sanitize_name(ref_name)) + '?'
    schema_type = schema.get('type')
    fmt = schema.get('format')
    if schema_type == 'array':
        return f"List<{get_type(schema.get('items')).rstrip('?')}>?"
    if schema_type == 'integer':
        return 'int' + ('' if schema.get('required_flag') else '?')
    if schema_type == 'number':
        return 'double' + ('' if schema.get('required_flag') else '?')
    if schema_type == 'boolean':
        return 'bool' + ('' if schema.get('required_flag') else '?')
    if schema_type == 'file':
        return 'byte[]?'
    if schema_type == 'string':
        if fmt == 'date-time':
            return 'DateTimeOffset' + ('' if schema.get('required_flag') else '?')
        return 'string?'
    if schema_type == 'object':
        return 'Dictionary<string, object>?'
    return 'object?'


def collect_definitions():
    for key in swagger.get('definitions', {}):
        sanitized = sanitize_name(key)
        if sanitized in definition_map.values():
            suffix = 2
            candidate = f"{sanitized}{suffix}"
            while candidate in definition_map.values():
                suffix += 1
                candidate = f"{sanitized}{suffix}"
            sanitized = candidate
        definition_map[key] = sanitized


def render_enum(name: str, schema: Dict[str, Any]) -> str:
    values = schema.get('enum', [])
    lines = [f"    public enum {name}", "    {"]
    for value in values:
        member = sanitize_name(str(value))
        if member[0].isdigit():
            member = '_' + member
        lines.append(f"        {member},")
    lines.append("    }")
    return '\n'.join(lines)


def render_class(name: str, schema: Dict[str, Any]) -> str:
    required = set(schema.get('required', []))
    properties = schema.get('properties', {})
    lines = [f"    public class {name}", "    {"]
    for prop_name, prop_schema in properties.items():
        prop_schema = dict(prop_schema)
        prop_schema['required_flag'] = prop_name in required
        prop_type = get_type(prop_schema)
        cs_name = sanitize_name(prop_name)
        attr = f"        [JsonPropertyName(\"{prop_name}\")]"
        initializer = ''
        if not prop_type.endswith('?'):
            initializer = ' = default!;'
        lines.extend([
            attr,
            f"        public {prop_type} {cs_name} {{ get; set; }}{initializer}",
            "",
        ])
    if len(lines) > 2 and lines[-1] == "":
        lines.pop()
    lines.append("    }")
    return '\n'.join(lines)


def render_definitions() -> str:
    sections = []
    for key, schema in swagger.get('definitions', {}).items():
        name = definition_map[key]
        if 'enum' in schema:
            sections.append(render_enum(name, schema))
        else:
            sections.append(render_class(name, schema))
        sections.append('')
    if sections:
        sections.pop()
    return '\n\n'.join(sections)


def map_parameter_type(param: Dict[str, Any]) -> str:
    schema = param.get('schema') or param
    schema = dict(schema)
    schema['required_flag'] = param.get('required', False)
    return get_type(schema)


def sanitize_method(name: str) -> str:
    parts = re.split(r'[^0-9A-Za-z]+', name)
    pascal = ''.join(part.capitalize() for part in parts if part)
    if not pascal:
        pascal = 'Call'
    if pascal[0].isdigit():
        pascal = '_' + pascal
    return pascal


def render_operations() -> str:
    op_lines = [
        "    public partial class MosSwaggerClientV2",
        "    {",
        "        private readonly HttpClient _httpClient;",
        "        private readonly JsonSerializerOptions _serializerOptions;",
        "        private readonly string _baseUrl;",
        "        private string? _apiToken;",
        "",
        "        public MosSwaggerClientV2(HttpClient httpClient, string baseUrl, string? apiToken = null)",
        "        {",
        "            _httpClient = httpClient;",
        "            _baseUrl = baseUrl.TrimEnd('/');",
        "            _serializerOptions = new JsonSerializerOptions",
        "            {",
        "                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,",
        "                PropertyNameCaseInsensitive = true,",
        "                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull",
        "            };",
        "",
        "            ApiToken = apiToken;",
        "        }",
        "",
        "        public string? ApiToken",
        "        {",
        "            get => _apiToken;",
        "            set",
        "            {",
        "                _apiToken = value;",
        "                if (string.IsNullOrWhiteSpace(value))",
        "                {",
        "                    _httpClient.DefaultRequestHeaders.Authorization = null;",
        "                }",
        "                else",
        "                {",
        "                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(\"Bearer\", value);",
        "                }",
        "            }",
        "        }",
        "",
        "        private string? FormatGetQuery(object? query)",
        "        {",
        "            switch (query)",
        "            {",
        "                case null:",
        "                    return null;",
        "                case string queryString:",
        "                    var trimmed = queryString.TrimStart('?');",
        "                    return trimmed.StartsWith(\"query=\", StringComparison.OrdinalIgnoreCase)",
        "                        ? trimmed",
        "                        : $\"query={trimmed}\";",
        "                default:",
        "                    var serialized = JsonSerializer.Serialize(query, _serializerOptions);",
        "                    return $\"query={Uri.EscapeDataString(serialized)}\";",
        "            }",
        "        }",
        "",
    ]

    for path, methods in swagger.get('paths', {}).items():
        for http_method, operation in methods.items():
            if not isinstance(operation, dict):
                continue
            op_id = operation.get('operationId') or f"{http_method}_{path}"
            method_name = sanitize_method(op_id)
            parameters = operation.get('parameters', [])
            param_defs = []
            url_path = path
            query_params = []
            body_param_name = None
            body_type = None
            for param in parameters:
                param_name = sanitize_name(param.get('name', 'param'))
                param_type = map_parameter_type(param)
                param_defs.append((param_name, param_type, param))
                if param.get('in') == 'path':
                    url_path = url_path.replace('{' + param['name'] + '}', '{' + param_name + '}')
                if param.get('in') == 'query':
                    query_params.append((param_name, param_type))
                if param.get('in') == 'body':
                    body_param_name = param_name
                    body_type = param_type
            response = operation.get('responses', {}).get('200') or {}
            response_type = get_type(response.get('schema')).rstrip('?') if response.get('schema') else 'void'
            return_type = 'Task' if response_type == 'void' else f"Task<{response_type}?>"
            param_signature = ', '.join([
                f"{ptype} {pname}" for pname, ptype, _ in param_defs
            ] + ['CancellationToken cancellationToken = default'])
            op_lines.append(f"        public async {return_type} {method_name}Async({param_signature})")
            op_lines.append("        {")
            op_lines.append("            var urlBuilder = new StringBuilder();")
            op_lines.append("            urlBuilder.Append(_baseUrl);")
            op_lines.append(f"            urlBuilder.Append(\"{url_path}\");")
            for pname, ptype, param in param_defs:
                if param.get('in') == 'path':
                    op_lines.append(f"            urlBuilder.Replace(\"{{{pname}}}\", Uri.EscapeDataString({pname}.ToString() ?? string.Empty));")
            if query_params:
                if len(query_params) == 1 and query_params[0][0].lower() == 'query':
                    op_lines.append(f"            var queryString = FormatGetQuery({query_params[0][0]});")
                    op_lines.append("            if (queryString != null)")
                    op_lines.append("            {")
                    op_lines.append("                urlBuilder.Append('?');")
                    op_lines.append("                urlBuilder.Append(queryString);")
                    op_lines.append("            }")
                else:
                    op_lines.append("            var hasQuery = false;")
                    for pname, ptype in query_params:
                        op_lines.append(f"            if ({pname} != null)")
                        op_lines.append("            {")
                        op_lines.append("                urlBuilder.Append(hasQuery ? '&' : '?');")
                        op_lines.append(f"                urlBuilder.Append(Uri.EscapeDataString(\"{pname}\"));")
                        op_lines.append("                urlBuilder.Append('=');")
                        op_lines.append(f"                urlBuilder.Append(Uri.EscapeDataString({pname}.ToString()!));")
                        op_lines.append("                hasQuery = true;")
                        op_lines.append("            }")
            op_lines.append(f"            using var request = new HttpRequestMessage(new HttpMethod(\"{http_method.upper()}\"), urlBuilder.ToString());")
            if body_param_name:
                op_lines.append(f"            var json = JsonSerializer.Serialize({body_param_name}, _serializerOptions);")
                op_lines.append("            request.Content = new StringContent(json, Encoding.UTF8, \"application/json\");")
            op_lines.append("            using var response = await _httpClient.SendAsync(request, cancellationToken);")
            op_lines.append("            response.EnsureSuccessStatusCode();")
            if response_type == 'void':
                op_lines.append("            return;")
            elif response_type == 'byte[]':
                op_lines.append("            return await response.Content.ReadAsByteArrayAsync(cancellationToken);")
            else:
                op_lines.append("            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);")
                op_lines.append(f"            return await JsonSerializer.DeserializeAsync<{response_type}>(stream, _serializerOptions, cancellationToken);")
            op_lines.append("        }")
            op_lines.append("")
    op_lines.append("    }")
    return '\n'.join(op_lines)


collect_definitions()
namespace = "Zakupki.MosApi.V2"
header = "// <auto-generated />\n" + "using System;\nusing System.Collections.Generic;\nusing System.Net.Http;\nusing System.Net.Http.Headers;\nusing System.Text;\nusing System.Text.Json;\nusing System.Text.Json.Serialization;\nusing System.Threading;\nusing System.Threading.Tasks;\n\nnamespace " + namespace + "\n{\n"
body = render_operations() + "\n\n" + render_definitions()
footer = "\n}\n"
out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(header + body + footer, encoding='utf-8')
