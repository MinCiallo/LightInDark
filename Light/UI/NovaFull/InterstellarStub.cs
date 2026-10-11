using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using Virial;
using Virial.Components;
using System;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
//  **外部依赖垫片** —— 2026-10-11
//
//  为什么需要：
//    NOS(Nova)的**语音聊天**依赖另一个模组 **Interstellar** ✗
//    （Nova 的 csproj：`<Reference Include="$(AmongUsRelease)\Interstellar.dll" />` ✓）
//    我们没搬那个 DLL ✓，也**不需要**它 ✓（目标是"设置界面一模一样" ✓，语音无关 ✓）
//    它的 VoiceChat 目录已排除编译 ✗ → 但别处仍引用其类型 ✓ → 这里补类型壳 ✓
//
//  ⚠️ 本文件只为**让编译通过** ✓；语音聊天**不会真的工作** ✗（也不需要 ✓）。
//     将来要接语音时：把 Interstellar 的 DLL 拿来引用 ✓，删掉本文件即可 ✓。
// ═══════════════════════════════════════════════════════════════════════════

namespace Interstellar
{
    public interface IVoiceComponent
    {
        void Update();
        void Destroy();
        float Radious { get; }
        float Volume { get; }
        Vector3 Position { get; }
        bool CanPlaySoundFrom(IVoiceComponent other);
    }

    public interface IMicrophone { bool IsRecording { get; } void StartRecording(); void StopRecording(); }
    public interface ISpeaker { float Volume { get; set; } void SetVolume(float volume); }

    public class AudioRoutingInstance : IVoiceComponent
    {
        public float Radious => 1f;
        public float Volume => 1f;
        public Vector3 Position => Vector3.zero;
        public void Update() { }
        public void Destroy() { }
        public bool CanPlaySoundFrom(IVoiceComponent other) => false;
    }

    public enum RadioKind { None = 0, Normal = 1, Radio = 2 }
}

namespace Interstellar.Routing
{
    public class BaseRouter { public float Volume = 1f; }

    public class VolumeRouter : BaseRouter { public class Property { public float Volume = 1f; } }
    public class StereoRouter : BaseRouter { public class Property { public float Left = 1f; public float Right = 1f; } }
    public class LevelMeterRouter : BaseRouter { public class Property { public float Level = 0f; } }
    public class FilterRouter : BaseRouter { public class Property { public float Frequency = 1f; } }
    public class ReverbRouter : BaseRouter { }
    public class SimpleRouter : BaseRouter { }
    public class SimpleEndpoint : BaseRouter { }
    public class DistortionFilter : BaseRouter { }
    public class AudioDevices { public static string[] Names = Array.Empty<string>(); }
    public class WindowsSpeaker : ISpeaker { public float Volume { get; set; } = 1f; public void SetVolume(float v) { Volume = v; } }
    public class WindowsMicrophone : IMicrophone { public bool IsRecording => false; public void StartRecording() { } public void StopRecording() { } }
}

namespace Interstellar.Routing.Router
{
    // 留空：只为让 NOS 代码里的 `using Interstellar.Routing.Router;` 合法 ✓
    //（真正的路由类型统一放在 `Interstellar.Routing` ✓ —— 放两处会造成 CS0104 歧义 ✗）
}

namespace Interstellar.VoiceChat
{
    public class VCRoomParameters { public string Name = ""; }
    public class InterstellarLoader
    {
        public static bool Loaded = false;
        /// <summary>照 `Nebula.cs:136 InterstellarLoader.Load();` 的调用补 ✓</summary>
        public static void Load() { }
    }

    public class VCRoom
    {
        public static VCRoom? Current;
        public string Name = "";
        public bool IsOpen = false;
        public IMicrophone? Microphone;
        public ISpeaker? Speaker;
        public void Open() { IsOpen = true; }
        public void Close() { IsOpen = false; }
    }
}

namespace Nebula.VoiceChat
{
    /// <summary>照 Nova 用法补的"可 Invoke 的 RPC 盒子" ✓（`CameraVC.RpcUseCamera.Invoke((player, id))` ✓）</summary>
    public class MetaRpcBox
    {
        public void Invoke((Virial.Game.Player player, int index) arg) { }
    }

    // ⚠️ 2026-10-11：Nova 自己的 VoiceChat 目录**已排除编译** ✗
    //    （`NoSVCRoom.cs` 789 行，是对着 **Interstellar 真实 API** 写的完整语音实现 ✗，
    //      没有那个 DLL 就无法忠实复刻它的接口形状 ✓）
    //    → 这里补同名件，让**别处**引用它的代码能编译 ✓；语音功能不会真的工作 ✗（也不需要 ✓）
    //    ⚠️ 成员列表是**按编译报错逐个补出来**的 ✓（见 NOS移植状态.md ✓）
    public interface IVoiceComponent : Interstellar.IVoiceComponent { }

    public enum RadioKind
    {
        None = 0, Normal = 1, Radio = 2,
        Impostor = 3, Jackal = 4, Lovers = 5,
    }

    public class RadioChannel
    {
        public string Name = "";
        public string LocalizedName { get; set; } = "";
        public RadioKind Kind = RadioKind.None;
        public bool IsDead = false;
        public int GetHearableMask() => 0;
    }

    public class RadioManager
    {
        public static RadioManager? Instance;
        public IReadOnlyList<RadioChannel> AllRadios => Array.Empty<RadioChannel>();

        /// <summary>★ 照 Nova 自己的 `VoiceChat\RadioManager.cs:70` 的原型抄 ✓
        /// （那个文件因依赖 Interstellar 被排除编译 ✗ → 把它这个公开签名搬过来 ✓）</summary>
        public RadioChannel Register(RadioKind kind, string localizedName,
                                     Func<GamePlayer, bool> canHear, ILifespan lifespan, VColor color)
            => new RadioChannel { Kind = kind, LocalizedName = localizedName };
    }

    public class CameraVC : Interstellar.IVoiceComponent
    {
        public float Radious => 1f;
        public float Volume => 1f;
        public Vector3 Position => Vector3.zero;
        public void Update() { }
        public void Destroy() { }
        public bool CanPlaySoundFrom(Interstellar.IVoiceComponent other) => false;
        public static void Activate(params (Vector2 pos, int index)[] cameras) { }
        public static readonly MetaRpcBox RpcUseCamera = new();
    }

    public class NoSVCRoom
    {
        public static NoSVCRoom? Current;
        public float LocalMicLevel = 0f;
        public static MetaVCSettings VCSettings = new();

        public class VCPlayer { public byte PlayerId; public float Level; }

        public static void Update() { }
        public void OnGameStart() { }
        public static void CloseCurrentRoom() { }
        public void Rejoin() { }
        public void ToggleDebugInfo() { }
        public bool TryGetPlayer(byte id, out VCPlayer player) { player = new VCPlayer(); return false; }
    }
}

namespace Nebula
{
    // ⚠️ `Nebula.cs:136` 里是**不带限定**地调用 `InterstellarLoader.Load();` ✓
    //    而那个文件在 `namespace Nebula` ✓ → 所以在同一个命名空间里补一份 ✓
    public class InterstellarLoader
    {
        public static bool Loaded = false;
        public static void Load() { }
    }
}

// ⚠️ 2026-10-11：`Nebula.cs` 里 `#if PC` 打开后会引用 `NebulaLoader.*` ✓
//    那个类型在 Nova 的**另一个工程**（`NebulaLoader\NebulaLoader.csproj` ✗）里 → 补壳 ✓
namespace NebulaLoader
{
    public class NebulaLoader
    {
        // 照调用点补：`typeof(NebulaLoader.NebulaLoader).GetProperty(name)?.GetValue(null) as ConfigEntryBase` ✓
        // 以及 `nameof(NebulaLoader.NebulaLoader.AllowHttpCommunication / AutoUpdate / UseSnapshot)` ✓
        public static BepInEx.Configuration.ConfigEntryBase? AllowHttpCommunication = null;
        public static BepInEx.Configuration.ConfigEntryBase? AutoUpdate = null;
        public static BepInEx.Configuration.ConfigEntryBase? UseSnapshot = null;
        public static bool Loaded = false;
    }

    public class LoaderConfigEntry<T>
    {
        public T Value = default!;
    }

    public static class LoaderConfig
    {
        public static LoaderConfigEntry<T>? Get<T>(string name) => null;
    }
}

namespace Nebula.VoiceChat
{
    /// <summary>照 `ClientOption.cs:423/428` 的调用补 ✓（`NoSVCRoom.VCSettings.OpenSettingScreen(__instance)` ✓）</summary>
    public class MetaVCSettings
    {
        public void OpenSettingScreen(object? menu) { }
        public void OpenServerSettingScreen(object? menu) { }
    }
}
