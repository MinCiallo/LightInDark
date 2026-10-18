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

        Dispatcher.Instance?.StartCoroutine(DummySpawner.CoSpawn(count, null).WrapToIl2Cpp());
    }
}
