// 应用版本号 (YYYYMMDD)
const APP_VERSION = 20261018;

// 节点配色：按分类分组，{ bg 背景, fg 边框/标题色 }
const C = {
  life:   { bg: "#3a2c2c", fg: "#e0a07a" },
  btn:    { bg: "#26332b", fg: "#7fd0a0" },
  act:    { bg: "#26313e", fg: "#7ab6e0" },
  data:   { bg: "#302a3a", fg: "#c090e0" },
  ev:     { bg: "#3a3018", fg: "#e0c070" },
  target: { bg: "#3a2634", fg: "#e07aa0" },
  logic:  { bg: "#243044", fg: "#80a8e0" },
  val:    { bg: "#263a36", fg: "#70d0c8" },
  rpc:    { bg: "#3a2626", fg: "#e08080" },
  cfg:    { bg: "#33332a", fg: "#d0d070" },
};

// 职业分类 → 源码目录与命名空间
const CATEGORY_MAP = {
  Crewmate: { dir: "Crewmates", ns: "Light.Roles.Crewmates" },
  Impostor: { dir: "Impostors", ns: "Light.Roles.Impostors" },
  Neutral:  { dir: "Neutrals",  ns: "Light.Roles.Neutrals" },
  Ghost:    { dir: "Ghosts",    ns: "Light.Roles.Ghosts" },
};

// 节点分类前缀 → 中文标签
const CN_CATEGORY = {
  life:   "🔄 生命周期",
  btn:    "🔘 技能按钮",
  act:    "⚡ 动作",
  data:   "📦 数据位",
  ev:     "📡 事件",
  target: "🎯 目标追踪",
  logic:  "🔀 逻辑",
  val:    "🔢 数值/文本",
  rpc:    "📶 自定义RPC",
  cfg:    "⚙️ 配置",
};
