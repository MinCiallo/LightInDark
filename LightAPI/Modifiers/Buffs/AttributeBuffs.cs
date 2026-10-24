using System.Collections.Generic;
using UnityEngine;
using LightInDark.Game;

namespace LightInDark.Modifiers.Buffs
{
    /// <summary>速度倍率（0.5 = 半速，1.3 = 加速）。</summary>
    public class SpeedBuff : BuffDefinition
    {
        public float Multiplier { get; }

        public SpeedBuff(float multiplier = 1.3f, float duration = 10f,
            string key = null, string name = "加速", bool canPassMeeting = false)
            : base(key ?? $"speed_x{multiplier:0.##}", name, LightInDark.Color.Cyan, duration, canPassMeeting)
        {
            Multiplier = multiplier;
        }

        public override RuntimeBuff CreateInstance(Player player) => new SimpleRuntimeBuff(this, player, DefaultDuration);
    }

    /// <summary>视野距离倍率（作用于本地玩家的灯光半径）。</summary>
    public class ViewDistanceBuff : BuffDefinition
    {
        public float Multiplier { get; }

        public ViewDistanceBuff(float multiplier = 1.5f, float duration = 10f,
            string key = null, string name = "视野扩大", bool canPassMeeting = false)
            : base(key ?? $"view_x{multiplier:0.##}", name, LightInDark.Color.ModGolden, duration, canPassMeeting)
        {
            Multiplier = multiplier;
        }

        public override RuntimeBuff CreateInstance(Player player) => new SimpleRuntimeBuff(this, player, DefaultDuration);
    }

    /// <summary>体型倍率。</summary>
    public class ScaleBuff : BuffDefinition
    {
        public float Factor { get; }

        public ScaleBuff(float factor = 0.6f, float duration = 10f,
            string key = null, string name = "缩小", bool canPassMeeting = false)
            : base(key ?? $"scale_x{factor:0.##}", name, LightInDark.Color.Magenta, duration, canPassMeeting)
        {
            Factor = factor;
        }

        public override RuntimeBuff CreateInstance(Player player) => new SimpleRuntimeBuff(this, player, DefaultDuration);
    }
}
