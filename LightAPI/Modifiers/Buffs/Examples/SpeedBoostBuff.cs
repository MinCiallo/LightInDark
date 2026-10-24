using LightInDark.Game;

namespace LightInDark.Modifiers.Buffs.Examples
{
    /// <summary>示例：10 秒内移动速度 ×1.3，会议开始即结束。</summary>
    public sealed class SpeedBoostBuff : SpeedBuff
    {
        public static readonly SpeedBoostBuff MyBuff = new();

        private SpeedBoostBuff()
            : base(1.3f, 10f, "speed_boost", "加速", canPassMeeting: false) { }

        public override RuntimeBuff CreateInstance(Player player)
            => new SimpleRuntimeBuff(this, player, DefaultDuration);
    }
}
