using System.Text.Json;

namespace RoleTool.Bridge;

/// <summary>IPC 消息协议：请求/响应结构与统一 JSON 选项。</summary>
internal static class MessageProtocol
{
    /// <summary>线上统一 camelCase，读取时忽略大小写。</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>前端请求：{ id, cmd, payload }。</summary>
    public sealed class Request
    {
        public long Id { get; set; }
        public string? Cmd { get; set; }
        public JsonElement? Payload { get; set; }
    }

    /// <summary>成功响应。</summary>
    public static string ReplyOk(object? id, object? data)
        => JsonSerializer.Serialize(new { id, ok = true, data, error = (string?)null }, Options);

    /// <summary>失败响应。</summary>
    public static string ReplyError(object? id, string error)
        => JsonSerializer.Serialize(new { id, ok = false, data = (object?)null, error }, Options);
}
