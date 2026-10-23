using System;
using System.Collections;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using UnityEngine;

namespace Light.Utilities;

/// <summary>假人生成器：房主本地生成 Dummy，补足人数便于测试。</summary>
public static class DummySpawner
{
    /// <summary>逐帧生成 count 个假人，完成时回调提示消息。</summary>
    public static IEnumerator CoSpawn(int count, Action<string> onMessage)
    {
        yield return null;                 // 等一帧，让大厅/世界稳定 ✓
        int ok = SpawnNow(count) ;
        yield return null;                 // 让 Spawn 的 RPC 落地一帧 ✓
        onMessage?.Invoke($"已添加 {ok} 个假人");
    }

    /// <summary>
    /// **同步**生成 count 个假人（全部在同一帧内完成 ✓）。
    ///
    /// ⚠️⚠️ 2026-10-10 用户报「自由模式的假人生成不对，怎么调（甚至调成 0）都是 1 个」——
    ///   根因：原来的 `CoSpawn` **每个假人之间 `yield return null`** ✗，
    ///   而它是从 `GameStartManager.BeginGame` 的 Prefix 里 `StartCoroutine` 起来的 ✗ →
    ///   `BeginGame` **不会等它** ✓，游戏立刻开始 ✗ → 只有**第 1 个**假人赶在世界生成前出现 ✓，
    ///   后面 2…N 个还没轮到就被开局流程清掉了 ✗✗（所以"无论调多少都只有 1 个" ✓）
    ///   → 现在开局前**一口气全部生成** ✓（`FreePlayDummyPatch` 调这个 ✓）
    /// </summary>
    public static int SpawnNow(int count)
    {
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            try { SpawnOne(); ok++; }
            catch (Exception ex)
            {
                LightInDark.Core.LightLogger.LogError("[DummySpawner]", ex);
                break;
            }
        }
        LightInDark.Core.LightLogger.Log($"[DummySpawner] 同步生成假人 {ok}/{count} 个");
        return ok;
    }

    /// <summary>生成单个假人（参考 Nebula AmongUsUtil.SpawnDummy）。</summary>
    private static void SpawnOne()
    {
        var pc = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
        byte id = (byte)GameData.Instance.GetAvailableId();
        pc.PlayerId = id;
        pc.isDummy = true;

        var info = GameData.Instance.AddDummy(pc);
        pc.transform.position = PlayerControl.LocalPlayer.transform.position;
        pc.GetComponent<DummyBehaviour>().enabled = true;

        pc.SetName(AccountManager.Instance.GetRandomName());
        pc.SetColor(GetUnusedColor());
        pc.SetHat(CosmeticsLayer.EMPTY_HAT_ID, id);
        pc.SetVisor(CosmeticsLayer.EMPTY_VISOR_ID, id);
        pc.SetSkin(CosmeticsLayer.EMPTY_SKIN_ID, id);
        pc.SetPet(CosmeticsLayer.EMPTY_PET_ID, id);

        AmongUsClient.Instance.Spawn(pc, -2, SpawnFlags.None);
        info.RpcSetTasks(new Il2CppStructArray<byte>(0));
    }

    /// <summary>取一个未被占用的颜色，全部占用时随机。</summary>
    private static byte GetUnusedColor()
    {
        int count = Palette.PlayerColors.Length;
        for (byte c = 0; c < count; c++)
        {
            bool used = false;
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p.cosmetics != null && p.cosmetics.ColorId == c) { used = true; break; }
            if (!used) return c;
        }
        return (byte)UnityEngine.Random.Range(0, count);
    }
}
