namespace LightInDark.UI.Ability
{
    /// <summary>
    /// HUD 布局设置的运行时值。由主插件在读取 / 重载设置时写入，<see cref="HudGrid"/> 排版时读取。
    /// </summary>
    public static class HudLayoutSettings
    {
        /// <summary>HUD 紧凑化：按钮阵缩到 0.72 倍。</summary>
        public static bool Compact { get; set; }

        /// <summary>按钮排列档位：0=默认 1=只抬高左侧 2=全部抬高。</summary>
        public static int Arrangement
        {
            get => _arrangement;
            set => _arrangement = value < 0 ? 0 : (value > 2 ? 2 : value);
        }

        private static int _arrangement;
    }
}
