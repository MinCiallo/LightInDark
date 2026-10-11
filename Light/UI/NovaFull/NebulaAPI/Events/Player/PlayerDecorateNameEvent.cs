using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Events.Player;

public class PlayerDecorateNameEvent : AbstractPlayerEvent
{
    public string Name { get; set; }
    public Virial.Color? Color { get; set; } = null;
    public bool CanSeeAllInfo { get; }

    internal PlayerDecorateNameEvent(Virial.Game.Player player, string name, bool canSeeAllInfo) : base(player)
    {
        Name = name;
        CanSeeAllInfo = canSeeAllInfo;
    }
}
