using LightInDark.Game;
using LightInDark.Visual;

namespace LightInDark.Modifiers.Buffs.Examples
{
    /// <summary>示例：15 秒小黑人（全场玩家清空装扮、身体变灰、名字隐藏）。</summary>
    public sealed class ShadowBuff : BuffDefinition
    {
        public static readonly ShadowBuff MyBuff = new();

        private ShadowBuff()
            : base("shadow", "小黑人", LightInDark.Color.Gray, 15f, canPassMeeting: true,
                   description: "全场玩家清空装扮、身体变灰并隐藏名字") { }

        public override RuntimeBuff CreateInstance(Player player) => new Instance(player);

        private sealed class Instance : RuntimeBuff
        {
            public Instance(Player player) : base(MyBuff, player, MyBuff.DefaultDuration) { }

            protected override void OnApply() => PlayerAppearance.SetAnonymousAll(true);

            protected override void OnExpire() => PlayerAppearance.SetAnonymousAll(false);
        }
    }
}
