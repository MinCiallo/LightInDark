using LightInDark.Game;

namespace LightInDark.Modifiers.Buffs
{
    /// <summary>
    /// Buff 定义（静态数据）：限时属性效果的模板。
    /// 与 <see cref="Modifier"/> 的区别：Buff 有时长、可叠加、可随时增删，不改变职业。
    /// </summary>
    public abstract class BuffDefinition
    {
        /// <summary>唯一键（RPC 同步 / 去重 / 注册表用）。</summary>
        public string Key { get; }

        /// <summary>显示名（语言键 buff.&lt;Key&gt;.name，缺失时用构造传入的文本）。</summary>
        public string Name => LightInDark.Language.Language.GetStringOrKey($"buff.{Key}.name", NameFallback);

        /// <summary>主题色（UI / 日志用）。</summary>
        public LightInDark.Color Color { get; }

        /// <summary>默认时长（秒）；&lt; 0 表示整局有效。</summary>
        public float DefaultDuration { get; }

        /// <summary>会议结束后是否保留。</summary>
        public bool CanPassMeeting { get; }

        /// <summary>叠加排序（同属性多个 Buff 时仅用于展示顺序）。</summary>
        public int Priority { get; }

        /// <summary>描述（语言键 buff.&lt;Key&gt;.describe）。</summary>
        public virtual string Description =>
            LightInDark.Language.Language.GetStringOrKey($"buff.{Key}.describe", DescriptionFallback);

        protected string NameFallback { get; }
        protected string DescriptionFallback { get; }

        protected BuffDefinition(string key, string name, LightInDark.Color color,
            float defaultDuration = -1f, bool canPassMeeting = false, int priority = 0, string description = "")
        {
            Key = key;
            NameFallback = name;
            Color = color;
            DefaultDuration = defaultDuration;
            CanPassMeeting = canPassMeeting;
            Priority = priority;
            DescriptionFallback = description;
        }

        /// <summary>创建绑定到指定玩家的运行时实例。</summary>
        public abstract RuntimeBuff CreateInstance(Player player);
    }
}
