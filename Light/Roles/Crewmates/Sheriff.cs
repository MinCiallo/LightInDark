using LightInDark.Configuration;
using LightInDark.Game;
using LightInDark.Roles;
using LightInDark.RPCs;
using LightInDark.UI.Ability;
using UnityEngine;

namespace Light.Roles.Crewmates;

public class Sheriff : RoleTemplate
{
    public const string Code = "Sheriff";

    public static readonly Sheriff MyRole = new();

    public override string CodeName => Code;
    public override LightInDark.Color Color => LightInDark.Color.Yellow;
    public override RoleCategory RoleCategory => RoleCategory.Crewmate;
    public override bool CanKill => true;
    public override float KillCooldown => ConfigRegistry.GetFloat($"role.{Code}.killCooldown");
    public override AllocationParameters Allocation => new() { MaxCount = 1, Chance = 30 };

    public override RoleConfigItem[] RoleConfiguration => new[]
    {
        new RoleConfigItem { Key = "killCooldown", Type = ConfigType.Float, Default = 20f, Min = 5f, Max = 60f, Step = 2.5f, Suffix = ConfigSuffix.Second },
        new RoleConfigItem { Key = "kills", Type = ConfigType.Int, Default = 3, Min = 1, Max = 15, Step = 1 },
    };

    public override RuntimeRoleTemplate CreateRuntime(PlayerControl owner) => new RuntimeInstance(owner, this);

    public class RuntimeInstance : RuntimeRoleTemplate
    {
        private LightInDark.UI.Ability.AbilityButton _button;
        private PlayerTracker _tracker;
        private int _killsLeft;

        public RuntimeInstance(PlayerControl owner, RoleTemplate template) : base(owner, template) { }

        protected override void OnActivated()
        {
            if (!AmOwner) return;

            // （不再需要 NormalizeVanillaRole ✓ —— 分配机已被完全接管，见 RuntimeRoleTemplate ✓）

            _killsLeft = ConfigRegistry.GetInt($"role.{CodeName}.kills");
            _tracker = new PlayerTracker(MyPlayer, RoleUtils.KillDistance(), p => !p.IsDead);
            RoleButtonManager.RegisterTracker(_tracker);

            _button = AbilityButtonFactory.Create(this, new RoleButtonConfig
            {
                IsKillButton = true,
                ArrangedAsKillButton = true,
                LabelType = ButtonLabelType.Impostor,
                Hotkey = KeyCode.Q,
                Label = "击杀",
                Cooldown = Role.KillCooldown,
                CanShow = () => _killsLeft > 0,
                CanUse = () => _killsLeft > 0 && _tracker?.CurrentTarget != null && PlayerControl.LocalPlayer.CanMove,
            }, TryKill);

            RefreshUses();
        }

        private void TryKill()
        {
            var target = _tracker?.CurrentTarget;
            var me = MyPlayer?.Control;
            if (me == null || target?.Control == null || _killsLeft <= 0) return;

            var category = FactionOf(target);

            _killsLeft--;
            RefreshUses();

            if (category == RoleCategory.Crewmate)
                RpcDefinitions.Suicide(me, true, "sheriff", PlayerState.GoOff);
            else
                RpcDefinitions.MurderPlayer(me, target.Control, PlayerState.BeKilled);
        }

        private static RoleCategory FactionOf(Player player)
        {
            var template = player.Role?.Role;
            if (template != null) return template.RoleCategory;

            var data = player.Control?.Data;
            return data?.Role != null && data.Role.IsImpostor ? RoleCategory.Impostor : RoleCategory.Crewmate;
        }

        private void RefreshUses()
        {
            if (_button == null) return;
            if (_killsLeft > 0) _button.ShowUsesIcon(_killsLeft.ToString());
            else _button.HideUsesIcon();
        }
    }
}
