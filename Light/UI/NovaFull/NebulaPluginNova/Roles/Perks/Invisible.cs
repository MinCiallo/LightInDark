using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class Invisible : PerkFunctionalInstance
{
    const float Duration = 15f;
    static PerkFunctionalDefinition Def = new("invisible", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("invisible", 4, 48, Crewmate.Comet.MyRole.Color).DurationText("%D%", () => Duration), (def, instance) => new Invisible(def, instance));

    bool used = false;
    public Invisible(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        var durationTimer = NebulaAPI.Modules.Timer(this,Duration).SetTime(0f);
        PerkInstance.BindTimer(durationTimer);
    }


    public override bool HasAction => true;
    public override void OnClick()
    {
        if (used) return;

        MyPlayer.GainAttribute(PlayerAttributes.Invisible, Duration, false, 0);
        PerkInstance.MyTimer?.Start();
        used = true;
        new StaticAchievementToken("perk.manyPerks1.invisible");
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor((used) ? Color.gray : Color.white);
    }
}
