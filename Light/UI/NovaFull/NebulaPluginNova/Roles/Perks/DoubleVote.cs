using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;

namespace Nebula.Roles.Perks;

internal class DoubleVote : PerkFunctionalInstance
{
    bool used = false;
    static PerkFunctionalDefinition Def = new("doubleVote", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("doubleVote", 2, 32, Crewmate.Mayor.MyRole.Color), (def, instance) => new DoubleVote(def, instance));


    private DoubleVote(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
    }

    [EventPriority(EventPriority.Low)]
    void OnVoted(PlayerVoteCastLocalEvent ev)
    {
        //スキップ、白票、0票投票は対象外
        if (used || ev.VoteFor == null || ev.Vote == 0) return;

        ev.Vote *= 2;
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(!used ? Color.white : Color.gray);
    }
}
