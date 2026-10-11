using System;
using Virial;
using Virial.Configuration;

namespace Nebula.Roles;

/// <summary>
/// ★★★ 2026-10-11：**只保留配置面板**（用户："我只想搬运NOS的那套配置面板。你只保留这个，剩下的删了吧" ✓）
///
/// 为什么需要这个文件 ✓：
///   那 6 个配置页签（`ConfigurationTab.tabSetting` 等）原本是在
///   **`Roles\Roles.cs:22-33` 的 `NoSRoleSetUp.SetTabs()`** 里赋值的 ✓，
///   而 `Roles\` 那 **147 个文件 / 35,282 行**（约 100 个职业 ✓）用户**不要** ✗。
///
///   → 于是把那**十几行赋值搬到这里** ✓✓，`Roles\` 就能整目录删掉 ✓，
///     而配置面板的左侧页签**照样有值** ✓（不再依赖那 100 个职业 ✓）。
///
/// ⚠️ 这是**从 Nova 源码里抄过来的**（`Roles.cs:22-33` ✓），数值/键名**一字未改** ✓
///    —— 改了会让页面和原版 NOS 不一样 ✗。
/// </summary>
internal static class LidConfigTabs
{
    private static bool _done;

    /// <summary>建那 6 个配置页签（照抄 `Roles.cs:24-32` ✓）</summary>
    public static void SetUp()
    {
        if (_done) return;
        _done = true;

        try
        {
            // 照抄 `Roles.cs:24` ✓：页签的可见性取决于"当前游戏模式是否带角色设置" ✓
            Func<bool> roleVisibility = () => Nebula.Configuration.GeneralConfigurations.CurrentGameMode.WithRoleSettings;

            // 照抄 `Roles.cs:26-31` ✓（位掩码与颜色一字未改 ✓）
            ConfigurationTab.tabSetting = new ConfigurationTab(0x01, "options.tab.setting", new(0.75f, 0.75f, 0.75f));
            ConfigurationTab.tabCrewmate = new ConfigurationTab(0x02, "options.tab.crewmate", VColor.CrewmateColor, roleVisibility);
            ConfigurationTab.tabImpostor = new ConfigurationTab(0x04, "options.tab.impostor", VColor.ImpostorColor, roleVisibility);
            ConfigurationTab.tabNeutral = new ConfigurationTab(0x08, "options.tab.neutral", new(244f / 255f, 211f / 255f, 53f / 255f), roleVisibility);
            ConfigurationTab.tabGhost = new ConfigurationTab(0x10, "options.tab.ghost", new(150f / 255f, 150f / 255f, 150f / 255f), roleVisibility);
            ConfigurationTab.tabModifier = new ConfigurationTab(0x20, "options.tab.modifier", new(255f / 255f, 255f / 255f, 243f / 255f), roleVisibility);

            // 照抄 `Roles.cs:32` ✓
            ConfigurationTab.allTab =
            [
                ConfigurationTab.tabSetting, ConfigurationTab.tabCrewmate, ConfigurationTab.tabImpostor,
                ConfigurationTab.tabNeutral, ConfigurationTab.tabGhost, ConfigurationTab.tabModifier
            ];

            LightInDark.Core.LightLogger.Log("[LidConfigTabs] ★ 6 个配置页签已建 ✓（不依赖 Roles 那 147 个职业文件 ✓）");
        }
        catch (Exception ex)
        {
            LightInDark.Core.LightLogger.LogError("[LidConfigTabs.SetUp]", ex);
        }
    }
}
