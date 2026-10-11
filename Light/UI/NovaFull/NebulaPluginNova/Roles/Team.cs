using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓

using Virial.Assignable;

namespace Nebula.Roles;

public class Team : RoleTeam
{
    public string TranslationKey { get; private init; }
    public string DisplayName => Language.Translate(TranslationKey);
    public UnityEngine.Color UnityColor { get; private init; }
    public Virial.Color Color { get;private init; }
    public int Id { get; set; }
    public TeamRevealType RevealType { get; set; }
    private Func<float>? killCooldownSupplier;
    public float KillCooldown => killCooldownSupplier?.Invoke() ?? AmongUsLLImpl.Instance.VanillaKillCooldown;
    public Team(string translationKey, Virial.Color color, TeamRevealType revealType, Func<float> killCooldown = null)
    {
        TranslationKey = translationKey;
        UnityColor = color.ToUnityColor();
        Color = color;
        RevealType = revealType;
        this.killCooldownSupplier = killCooldown;

        Roles.Register(this);
    }
}
