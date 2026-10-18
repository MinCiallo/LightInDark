using LightInDark;
using LightInDark.Configuration;
using LightInDark.Roles;
using UnityEngine;

namespace Light.Roles.Neutrals;

public class Sidekick : RoleTemplate
{
    public const string Code = "Sidekick";

    public static readonly Sidekick MyRole = new();

    public override string CodeName => Code;
    public override LightInDark.Color Color => Jackal.MyRole.Color;
    public override RoleCategory RoleCategory => RoleCategory.Neutral;
    public override NeutralType NeutralType => NeutralType.Evil;
    public override string TeamCode => Jackal.Code;

    public override AllocationParameters Allocation => default;
    public override bool CanBeAssigned => false;

    public override RuntimeRoleTemplate CreateRuntime(PlayerControl owner)
        => new RuntimeInstance(owner, this);

    public class RuntimeInstance : RuntimeRoleTemplate
    {
        // Role 由基类提供（ctor 存下 template）—— 审查 A2：不再各处硬写 `=> MyRole`（两个真相）

        public RuntimeInstance(PlayerControl owner, RoleTemplate template) : base(owner, template) { }

        protected override void OnActivated()
        {
            try
            {
                if (!AmOwner) return;

                // （不再需要 NormalizeVanillaRole ✓ —— 分配机已被完全接管，
                //   原版技能职业从根源上不会出现，见 RuntimeRoleTemplate ✓）
            }
            catch (System.Exception ex)
            {
                LightCoreLog(ex);
            }
        }

        private static void LightCoreLog(System.Exception ex)
        {
            try { LightInDark.Core.LightLogger.LogWarning($"[Sidekick] OnActivated 失败: {ex.Message}"); } catch { }
        }
    }
}
