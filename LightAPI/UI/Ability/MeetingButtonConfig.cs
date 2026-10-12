using System;
using UnityEngine;

namespace LightInDark.UI.Ability;

public enum MeetingCorner { BottomLeft, BottomRight }

/// <summary>Per-nameplate meeting controls with shared cooldown/uses and target-specific gates.</summary>
public sealed class MeetingTargetButtonConfig : RoleButtonConfig
{
    public Vector3 Position = new(-0.95f, 0.03f, -1.31f);
    public Vector3 Scale = Vector3.one;
    public Sprite HoverIcon;
    public bool IncludeSelf;
    public bool AutoArrange = true;
    public Func<PlayerControl, bool> CanAddTarget = p => p != null && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected;
    public Func<PlayerControl, bool> CanUseTarget = p => p != null && p.Data != null && !p.Data.IsDead && !p.Data.Disconnected;
    public MeetingTargetButtonConfig()
    {
        CanUse = () => true;
        CanRunCooldown = _ => true;
        AllowDuringMeeting = true;
        StartOnCooldown = false;
    }
}
