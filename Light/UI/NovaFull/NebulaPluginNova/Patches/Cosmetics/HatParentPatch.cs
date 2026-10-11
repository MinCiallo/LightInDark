using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using Nebula.Modules.Cosmetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Patches.Cosmetics;

[HarmonyPatch(typeof(HatParent), nameof(HatParent.SetHat), typeof(string), typeof(int))]
public class HatParentSetHatPatch
{
    public static bool Prefix(HatParent __instance, [HarmonyArgument(0)] string hatId, [HarmonyArgument(1)] int color)
    {
        if (!DestroyableSingleton<HatManager>.InstanceExists) return false;
        
        HatData hatById = DestroyableSingleton<HatManager>.Instance.GetHatById(hatId);
        __instance.SetHat(hatById, color);
        return false;
    }
}
