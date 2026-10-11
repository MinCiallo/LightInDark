using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Components;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class SniperIcon : PerkFunctionalInstance
{
    const float CoolDown = 10f;
    static PerkFunctionalDefinition Def = new("sniperIcon", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("sniperIcon", 3, 1, Virial.Color.ImpostorColor, Virial.Color.ImpostorColor).CooldownText("%CD%", ()=>CoolDown), (def, instance) => new SniperIcon(def, instance));

    public SniperIcon(PerkDefinition def, PerkInstance instance) : base(def, instance) {
        cooldownTimer = NebulaAPI.Modules.Timer(this, CoolDown);
        cooldownTimer.Start();
        PerkInstance.BindTimer(cooldownTimer);
    }

    private GameTimer cooldownTimer;
    
    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing) return;
        if (MyPlayer.IsDead) return;

        NebulaAsset.PlaySE(NebulaAudioClip.SniperShot, true);
        Impostor.Sniper.RpcShowNotice.Invoke(MyPlayer.Position);

        cooldownTimer.Start();

        RegisterAchievementToken(MyPlayer);
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        cooldownTimer.Start();
    }

    static public void RegisterAchievementToken(GamePlayer player)
    {
        new StaticAchievementToken("perk.blank");
        new StaticAchievementToken("perk.manyPerks1.blankShot");
        if (player.Role.Role == Neutral.Jester.MyRole) GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev => {
            if (ev.EndState.EndCondition == NebulaGameEnd.JesterWin && ev.CheckWin(player)) new StaticAchievementToken("jester.common2");
        }, player.Role);
    }
}
