namespace RoleTool.Bridge;

/// <summary>仓库路径解析：从程序目录向上定位仓库根，并提供职业/语言等常用路径。</summary>
internal static class ProjectPaths
{
    public static string RepoRoot { get; } = LocateRepoRoot();

    public static string DefaultOutDir => Path.Combine(RepoRoot, "Light", "Roles");
    public static string LanguageDir => Path.Combine(RepoRoot, "Light", "Resources", "Language");
    public static string MainProject => Path.Combine(RepoRoot, "Light", "LightPluginMain.csproj");

    /// <summary>向上查找：优先 slnx，其次 Light 主工程文件。</summary>
    private static string LocateRepoRoot()
    {
        var slnx = WalkUp(dir => File.Exists(Path.Combine(dir, "LightInDark.slnx")));
        if (slnx != null) return slnx;

        var fallback = WalkUp(dir => File.Exists(Path.Combine(dir, "Light", "LightPluginMain.csproj")));
        return fallback ?? AppContext.BaseDirectory;
    }

    private static string? WalkUp(Func<string, bool> match)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (match(dir.FullName)) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>阵营（大小写不敏感）→ 源码子目录名。</summary>
    public static string RoleCategoryDir(string? category) => (category ?? "").Trim().ToLowerInvariant() switch
    {
        "crewmate" => "Crewmates",
        "impostor" => "Impostors",
        "neutral" => "Neutrals",
        "ghost" => "Ghosts",
        _ => "Crewmates"
    };
}
