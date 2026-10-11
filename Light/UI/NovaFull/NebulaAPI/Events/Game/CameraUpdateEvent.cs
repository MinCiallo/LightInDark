using Color = UnityEngine.Color;   // 2026-10-11 合并准备：Nova 原本靠 Nebula.cs 的 global using ✓，那行与我们的 LightInDark.Color 冲突 ✗ → 逐文件补 ✓
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;

namespace Virial.Events.Game;

/// <summary>
/// カメラが更新される際に呼び出されます。
/// カメラの色調を変更できます。
/// </summary>
[RecyclableEvent]
public class CameraUpdateEvent : Event
{
    private float saturation = 1f;
    public void UpdateSaturation(float value, bool multiply)
    {
        if (multiply)
            saturation *= value;
        else
            saturation += value;
    }

    private float hue = 0f;
    public void UpdateHue(float value)
    {
            hue += value;
    }

    private float brightness = 1f;
    public void UpdateBrightness(float value, bool multiply)
    {
        if (multiply)
            brightness *= value;
        else
            brightness += value;
    }

    private Virial.Color color = Virial.Color.White;
    internal Virial.Color Color { get =>  color; set => color = value; }

    internal float GetSaturation() { return saturation; }
    internal float GetHue() { return hue; }
    internal float GetBrightness() { return brightness; }

    private CameraUpdateEvent() { }
    static private CameraUpdateEvent ev = new();
    static internal CameraUpdateEvent Get()
    {
        ev.saturation = 1f;
        ev.hue = 0f;
        ev.brightness = 1f;
        ev.color = Virial.Color.White;
        return ev;
    }
}
