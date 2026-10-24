using System;
using HarmonyLib;
using LightInDark.Core;

namespace Light.Patches;

/// <summary>
/// HudManager.Update 异常诊断：把这个方法（含原生实现与整条补丁链）抛出的异常连完整堆栈记下来，
/// 并吞掉异常避免每帧刷屏卡死。定位完成后应移除本补丁。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HudUpdateTrapPatch
{
    private const int MaxLogs = 3;
    private static int _logged;

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (__exception == null) return null;

        // 前几次保留原行为（方便对照原版堆栈），之后吞掉以免每帧刷屏卡死
        if (_logged < MaxLogs)
        {
            _logged++;
            LightLogger.LogError($"[HudUpdate] 第 {_logged} 次捕获每帧异常（含完整堆栈）", __exception);
            return __exception;
        }

        return null;
    }
}
