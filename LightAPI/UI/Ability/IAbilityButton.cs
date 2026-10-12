using UnityEngine;

namespace LightInDark.UI.Ability;

/// <summary>Public button control surface for roles and addons.</summary>
public interface IAbilityButton
{
    bool IsVisible { get; }
    bool IsInCooldown { get; }
    float CooldownRemaining { get; }
    int UsesLeft { get; }
    bool IsBroken { get; }
    IAbilityTimer CurrentTimer { get; }
    bool TryClick();
    void StartCooldown();
    void StopCooldown();
    void SetUses(int remaining);
    void SetLabel(string label);
    void SetIcon(Sprite icon);
    void Release();
}
