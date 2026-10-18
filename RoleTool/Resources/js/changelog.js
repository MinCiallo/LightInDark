// 更新日志：每次更新在数组**顶部**追加一条（约定见 .trae/documents/RoleTool_节点与生成契约.md）
var CHANGELOG = [
    {
        date: '2026-10-17',
        title: '同步警长 / 赌怪用到的新 API',
        items: [
            '新增 3 个节点：玩家是否某阵营、赌怪开枪（按猜测处决）、播放音效（装弹 / 枪声）',
            '技能按钮新增「最大使用次数」参数（0 = 不限），对应 SetMaxUses',
            '读取配置新增类型选择：浮点 / 整数 / 布尔（对应 GetFloat / GetInt / GetBool）',
            '修复：内鬼阵营职业的原版底色被归一成船员，现在按阵营传 true'
        ]
    },
    {
        date: '2026-10-17',
        title: '同步游戏状态 / 玩家查询 API',
        items: [
            '新增 6 个节点：游戏状态、是否某张地图、房主玩家、玩家是否房主、按ID取玩家、玩家总数（节点总数 70）',
            '校验新增「事件上下文」检查：击杀者/受害者/报告人按所处事件类型校验，不匹配直接报错并列出该事件可用的成员',
            '修复：事件里的击杀者/受害者/报告人是原版 PlayerControl，需经 Player.Get 转换成本层 Player，否则生成代码编译失败'
        ]
    },
    {
        date: '2026-10-17',
        title: '修复节点拖拽、新增文档页签与 22 个节点',
        items: [
            '修复：节点拖不到画布（画布容器高度被内联样式压成 0；拖拽监听在列表填充前绑定）',
            '左侧节点面板改为按分类折叠收纳，支持搜索过滤',
            '新增「文档」页签：每个节点的作用/端口含义/用法手册 + 使用教程',
            '新增 22 个节点：击杀/自杀/复活/隐身传送/强制结束会议/破坏紧急按钮/显示隐藏聊天/发送聊天/踢人/屏幕闪烁/日志，玩家名字/是否死亡/是否内鬼/随机存活玩家/存活人数/两点距离/是否在范围内/坐标，数学运算',
            '校验新增「击杀按钮目标端口只能在点击分支内使用」检查'
        ]
    },
    {
        date: '2026-10-16',
        title: '首个可用版本',
        items: [
            '桌面外壳（.NET + WebView2）与前端基座，LiteGraph 节点图 + Ace 代码预览',
            '节点图编辑：拖拽添加、右键菜单、连线类型校验、节点属性随蓝图持久化',
            '42 个职业节点：生命周期、技能/击杀按钮、动作、数据位、事件监听、目标追踪、逻辑、数值、自定义 RPC、配置',
            '节点图 → C# 职业源码生成（RoleTemplate + RuntimeInstance），实时预览 C# / 翻译 / 文档',
            '蓝图 .rtp 导出与导入',
            '静态校验（命名、分配、配色、生命周期唯一性、悬空连线、数据位引用、翻译完整性等）',
            '写入主插件源码树：落到 Light\\Roles\\<阵营>\\，并合并语言键',
            '一键编译主插件并回显构建输出'
        ]
    }
];

// 渲染「关于」页签的版本号与更新日志
function renderChangelog() {
    var box = document.getElementById('changelog');
    var verEl = document.getElementById('about-version');
    if (verEl && typeof APP_VERSION !== 'undefined') verEl.textContent = '当前版本：v' + APP_VERSION;
    if (!box) return;

    var html = ['<h2 class="cl-title">更新日志</h2>'];
    CHANGELOG.forEach(function (e) {
        html.push('<div class="cl-entry">');
        html.push('<div class="cl-head"><span class="cl-date">' + e.date +
            '</span><span class="cl-name">' + e.title + '</span></div>');
        html.push('<ul class="cl-items">');
        (e.items || []).forEach(function (t) { html.push('<li>' + t + '</li>'); });
        html.push('</ul></div>');
    });
    box.innerHTML = html.join('');
}
