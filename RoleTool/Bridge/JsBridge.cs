using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RoleTool.Bridge;

/// <summary>IPC 命令分发：接收前端 JSON 请求，返回 JSON 响应字符串。</summary>
internal static class JsBridge
{
    private static readonly Regex CodeRegex =
        new(@"const\s+string\s+Code\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    private static readonly Regex ClassRegex =
        new(@"\bpublic\s+(?:sealed\s+|abstract\s+)?class\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    /// <summary>处理一条请求，永不抛异常。</summary>
    public static string Handle(string rawJson)
    {
        long id = 0;
        try
        {
            MessageProtocol.Request? req;
            try
            {
                req = JsonSerializer.Deserialize<MessageProtocol.Request>(rawJson, MessageProtocol.Options);
            }
            catch (Exception ex)
            {
                return MessageProtocol.ReplyError(null, "请求解析失败：" + ex.Message);
            }

            if (req == null) return MessageProtocol.ReplyError(null, "空请求");
            id = req.Id;
            string cmd = req.Cmd ?? "";

            object? data = cmd switch
            {
                "getPaths" => GetPaths(),
                "pickFolder" => PickFolder(),
                "pickSavePath" => PickSavePath(),
                "readText" => ReadText(Payload(req, cmd)),
                "scanExistingRoles" => ScanExistingRoles(),
                "writeFiles" => WriteFiles(Payload(req, cmd)),
                "openInExplorer" => OpenInExplorer(Payload(req, cmd)),
                "buildPlugin" => BuildPlugin(Payload(req, cmd)),
                _ => throw new InvalidOperationException("unknown cmd: " + cmd)
            };

            return MessageProtocol.ReplyOk(id, data);
        }
        catch (Exception ex)
        {
            return MessageProtocol.ReplyError(id, ex.Message);
        }
    }

    private static JsonElement Payload(MessageProtocol.Request req, string cmd)
        => req.Payload ?? throw new InvalidOperationException($"命令 {cmd} 缺少 payload");

    // ---------- 路径信息 ----------
    private static object GetPaths() => new
    {
        repoRoot = ProjectPaths.RepoRoot,
        defaultOutDir = ProjectPaths.DefaultOutDir,
        languageDir = ProjectPaths.LanguageDir,
        mainProject = ProjectPaths.MainProject
    };

    // ---------- 文件对话框 ----------
    private static object PickFolder()
    {
        using var dlg = new FolderBrowserDialog();
        return new { path = dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedPath : "" };
    }

    private static object PickSavePath()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "蓝图 (*.rtp)|*.rtp",
            DefaultExt = "rtp",
            FileName = "role.rtp"
        };
        return new { path = dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : "" };
    }

    // ---------- 读取文本 ----------
    private static object ReadText(JsonElement p)
    {
        string path = Str(p, "path");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("缺少 path");
        return new { text = File.ReadAllText(path, Encoding.UTF8) };
    }

    // ---------- 扫描已有职业 ----------
    private static object ScanExistingRoles()
    {
        string root = Path.Combine(ProjectPaths.RepoRoot, "Light", "Roles");
        var roles = new List<object>();
        if (!Directory.Exists(root)) return new { roles };

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildDir(file)) continue;

            string text = File.ReadAllText(file);
            var codeMatch = CodeRegex.Match(text);
            var classMatch = ClassRegex.Match(text);

            roles.Add(new
            {
                code = codeMatch.Success ? codeMatch.Groups[1].Value : "",
                className = classMatch.Success ? classMatch.Groups[1].Value : "",
                file
            });
        }
        return new { roles };
    }

    private static bool IsInBuildDir(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
            if (part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("bin", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // ---------- 写入源码树 + 合并语言 ----------
    private static object WriteFiles(JsonElement p)
    {
        string repoRoot = Str(p, "repoRoot");
        if (string.IsNullOrWhiteSpace(repoRoot)) repoRoot = ProjectPaths.RepoRoot;
        string rootFull = Path.GetFullPath(repoRoot);

        var written = new List<string>();
        if (p.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in filesEl.EnumerateArray())
            {
                string rel = Str(f, "relPath");
                if (string.IsNullOrWhiteSpace(rel)) continue;

                string full = ResolveSafe(rootFull, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, Str(f, "content"), new UTF8Encoding(false));
                written.Add(full);
            }
        }

        var merged = new List<string>();
        if (p.TryGetProperty("language", out var langEl) && langEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var langFile in langEl.EnumerateObject())
            {
                string full = ResolveSafe(rootFull, langFile.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                bool hadBom = HasBom(full);

                JsonObject obj = File.Exists(full)
                    ? (JsonNode.Parse(File.ReadAllText(full)) as JsonObject ?? new JsonObject())
                    : new JsonObject();

                // 仅覆盖 payload 提供的键，保留原有键
                if (langFile.Value.ValueKind == JsonValueKind.Object)
                    foreach (var kv in langFile.Value.EnumerateObject())
                        obj[kv.Name] = JsonValue.Create(kv.Value.GetString());

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                File.WriteAllText(full, obj.ToJsonString(opts), new UTF8Encoding(hadBom));
                merged.Add(full);
            }
        }

        return new { written, mergedLanguage = merged };
    }

    /// <summary>把相对路径解析到根目录下，拒绝越界（路径穿越）。</summary>
    private static string ResolveSafe(string rootFull, string rel)
    {
        if (rel.Contains("..")) throw new InvalidOperationException("非法路径（含 ..）：" + rel);

        string full = Path.GetFullPath(Path.Combine(rootFull, rel.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("非法路径（超出仓库根）：" + rel);
        return full;
    }

    private static bool HasBom(string path)
    {
        if (!File.Exists(path)) return false;
        using var fs = File.OpenRead(path);
        Span<byte> b = stackalloc byte[3];
        int n = fs.Read(b);
        return n == 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
    }

    // ---------- 资源管理器 ----------
    private static object OpenInExplorer(JsonElement p)
    {
        string path = Str(p, "path");
        if (!string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)))
        {
            var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            psi.ArgumentList.Add(path);
            Process.Start(psi);
        }
        return new { };
    }

    // ---------- 构建主插件 ----------
    private static object BuildPlugin(JsonElement p)
    {
        string conf = Str(p, "configuration");
        if (string.IsNullOrWhiteSpace(conf)) conf = "Release";

        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add(ProjectPaths.MainProject);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(conf);

        var output = new StringBuilder();
        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(300_000))
        {
            try { proc.Kill(true); } catch { /* 忽略 */ }
            throw new TimeoutException("构建超时（300 秒）");
        }
        proc.WaitForExit(); // 等待异步输出刷新完毕

        return new { exitCode = proc.ExitCode, output = output.ToString() };
    }

    // ---------- JSON 取值 ----------
    private static string Str(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var el))
        {
            if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? "";
            if (el.ValueKind == JsonValueKind.Null) return "";
            return el.ToString();
        }
        return "";
    }
}
