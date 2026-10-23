using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Light.Config;
using Light.Utilities;
using LightInDark.Configuration;
using LightInDark.Game;

namespace Light.Patches;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
public static class FreePlayDummyPatch
{
    public static void Prefix()
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost) return;
        if (!GameModeConfigRegistrar.SelectedMode.AllowRoleChanges) return;

        int count = ConfigRegistry.GetInt(GameModeConfigRegistrar.DummyKey);
        if (count <= 0) return;
        if (PlayerControl.AllPlayerControls.Count != 1) return;

        // ⚠️⚠️ 2026-10-10 用户报「怎么调（甚至调成 0）都是 1 个假人」——
        //   根因是这里原来用 `StartCoroutine(CoSpawn(...))` ✗：
        //   `BeginGame` 不会等协程 ✓ → 游戏立刻开始 ✗ → 只有第一个假人赶在世界生成前出现 ✓
        //   → 改成**同步**一口气全部生成 ✓（见 DummySpawner.SpawnNow 的说明 ✓）
        DummySpawner.SpawnNow(count);
    }
}
