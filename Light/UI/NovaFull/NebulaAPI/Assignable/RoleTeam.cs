using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
namespace Virial.Assignable;

/// <summary>
/// 陣営の開示バリエーションを表します。
/// </summary>
public enum TeamRevealType
{
    /// <summary>
    /// 自分自身のみ表示されます。主に第三陣営で使用します。
    /// </summary>
    OnlyMe,
    /// <summary>
    /// ゲーム中の全プレイヤーが表示されます。クルーメイト陣営で使用します。
    /// </summary>
    Everyone,
    /// <summary>
    /// チームのメンバー全員が表示されます。
    /// </summary>
    Teams,
}

public interface RoleTeam
{
    public string TranslationKey { get; }
    public string DisplayName { get; }
    internal UnityEngine.Color UnityColor { get; }
    public Virial.Color Color { get; }
    public int Id { get; }
    public TeamRevealType RevealType { get; }

    /// <summary>
    /// 陣営ベースのキルクールダウンを取得します。
    /// </summary>
    public float KillCooldown { get; }
}

public static class NebulaTeams
{
    public static RoleTeam CrewmateTeam { get; internal set; } = null!;
    public static RoleTeam ImpostorTeam { get; internal set; } = null!;
    public static RoleTeam JackalTeam { get; internal set; } = null!;
    public static RoleTeam JesterTeam { get; internal set; } = null!;
    public static RoleTeam VultureTeam { get; internal set; } = null!;
    public static RoleTeam ArsonistTeam { get; internal set; } = null!;
    public static RoleTeam PaparazzoTeam { get; internal set; } = null!;
    public static RoleTeam ChainShifterTeam { get; internal set; } = null!;
}
