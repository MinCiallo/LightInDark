using System;
using System.Collections.Generic;
using System.Linq;
using LightInDark.Configuration;
using LightInDark.Game;
using LightInDark.Roles;
using LightInDark.RPCs;
using LightInDark.UI.Ability;
using UnityEngine;

namespace GameplayApiExample;

// Compile-only addon example referencing the delivered DLL; it is not loaded into the game.
public sealed class ExampleNeutral : RoleTemplate
{
    public static readonly ExampleNeutral MyRole = new();
    public override string CodeName => "example.neutral";
    public override RoleCategory RoleCategory => RoleCategory.Neutral;
    public override NeutralType NeutralType => NeutralType.Evil;
    public override string TeamCode => "example.team";
    public override string VictoryTeamCode => "example.team";
    public override bool CountsTasksTowardVictory => false;
    public override NeutralWinCondition NeutralVictory => NeutralWinCondition.Parity;
    public override RuntimeRoleTemplate CreateRuntime(PlayerControl owner) => new Instance(owner,this);

    private sealed class Instance : RuntimeRoleTemplate
    {
        public Instance(PlayerControl owner,RoleTemplate role) : base(owner,role) { }
        protected override void OnActivated()
        {
            if (!AmOwner) return;
            var config = new RoleButtonConfig
            {
                Label = "能力", Hotkey = KeyCode.F, Cooldown = 10,
                MaxUses = 3, StartOnCooldown = false, ResetCooldownAfterMeeting = true
            };
            var effect = AbilityButtonFactory.CreateEffect(this, config, () => { });
            effect.EffectDuration = 2;
            effect.OnEffectEnd = () => { };
            effect.CooldownTimer.CanProgress = () => MyPlayer.Control != null;
            IAbilityButton control = effect;
            control.SetLabel("新标签");

            AbilityButtonFactory.CreateMeetingTarget(this,
                new MeetingTargetButtonConfig { MaxUses = 1, Cooldown = 2, CanUseTarget = p => p.Data != null && !p.Data.Disconnected },
                (_, target) => { });
            AbilityButtonFactory.CreateMeetingAbility(this,
                new RoleButtonConfig { Label = "会议能力", StartOnCooldown = false },
                () => { }, MeetingCorner.BottomLeft);
        }
    }
}

public static class Registration
{
    public static void Install()
    {
        RoleRegistry.RegisterAssembly(typeof(Registration).Assembly);
        GameModeRegistry.Register(new GameModeDefinition("example.mode", "示例模式",
            () => new ExampleAllocator(), allowAutomaticEnd:false, allowRoleChanges:true, minimumPlayers:1));
        VictoryRules.Register("example.rule", context =>
        {
            // Example condition: the custom faction is the only living faction.
            if (context.Alive.Count == 0 || context.Alive.Any(p => p.TeamCode != "example.team")) return null;
            return VictoryRules.ForTeam(context,"example.team");
        }, priority:200);
        LightInDark.Game.EndGameManager.ResultReceived += result =>
        {
            bool won = PlayerControl.LocalPlayer != null && result.IsWinner(PlayerControl.LocalPlayer.PlayerId);
        };
    }
    private sealed class ExampleAllocator : IRoleAllocator
    {
        public void Assign(List<byte> impostors,List<byte> others)
        {
            foreach (var id in impostors.Concat(others))
                RpcDefinitions.SetRoleByCode(id, ExampleNeutral.MyRole.CodeName, null);
        }
    }
}
