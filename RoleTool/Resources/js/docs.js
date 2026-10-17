// 应用内文档：每个节点（代码块）的作用 / 端口含义 / 用法 + 使用教程
// 端口列表从已注册节点自动读取（不会与实现脱节），此处只补充文字说明。

var NODE_DOC = {
    // ---------- 生命周期 ----------
    'life/onActivated': {
        desc: '职业被激活时执行一次：开局分配到本职业、或换职业到本职业时触发。',
        usage: '最常用的入口。通常在这里"创建技能按钮"。生成的代码会自动加上 if (!AmOwner) return;，只有本人会真正执行挂按钮的逻辑。',
        out: { '执行': '接后续执行节点（按钮、判断、动作…）' }
    },
    'life/onInactivated': {
        desc: '职业失活时执行（换职业、换局清理时）。',
        usage: '一般用于收尾。技能按钮框架会自动回收，多数情况留空即可。',
        out: { '执行': '接后续执行节点' }
    },
    'life/checkWin': {
        desc: '本职业（或本队）的胜利条件：主机每帧轮询，条件为真即结束本局并判胜。',
        usage: '把一个布尔值连到"条件"输入。不接则固定为 false。',
        in: { '条件': '布尔表达式；true = 本职业获胜' }
    },
    'life/onDied': {
        desc: '任意玩家死亡时触发（不只是自己死）。',
        usage: '要筛选"是不是自己/某目标"，接"玩家是否死亡""玩家名字"等节点做判断。',
        out: { '执行': '事件处理逻辑', '事件': '本次死亡事件对象' }
    },
    'life/onRevived': {
        desc: '任意玩家复活时触发。',
        usage: '同"死亡时"，用于复活相关的职业效果。',
        out: { '执行': '事件处理逻辑', '事件': '本次复活事件对象' }
    },
    'life/onExiled': {
        desc: '任意玩家被会议投票放逐时触发。',
        usage: '如"某人被放逐则我获胜"这类胜利条件可写在这里做记录。',
        out: { '执行': '事件处理逻辑', '事件': '本次放逐事件对象' }
    },
    'life/onMeetingStarted': {
        desc: '会议开始时触发。',
        usage: '适合做会议期间的准备（重置状态、显示提示）。',
        out: { '执行': '事件处理逻辑', '事件': '会议事件对象' }
    },
    'life/onMeetingEnded': {
        desc: '会议结束时触发。',
        usage: '适合做会议后的收尾。',
        out: { '执行': '事件处理逻辑', '事件': '会议事件对象' }
    },
    'life/onRoleData': {
        desc: '职业数据位被同步写入时触发（配合"注册数据位"使用）。',
        usage: '数据位变化时刷新界面/按钮提示，例如票数变化后更新可用次数图标。',
        out: { '执行': '事件处理逻辑', 'dataId': '被写入的数据位 id', 'value': '写入的新值' }
    },

    // ---------- 技能按钮 ----------
    'btn/ability': {
        desc: '创建一个普通技能按钮（点击后进入冷却）。只有本人（AmOwner）会创建。',
        usage: '三个要点：①"执行"接到"激活时"这类入口；②"点击"里写按下去之后干什么；③"下一"用于在它之后继续串别的节点（比如再挂第二个按钮）。参数：label 翻译键、热键、冷却秒数、是否放到左侧。',
        in: { '执行': '从入口接进来才会创建按钮' },
        out: { '点击': '按钮被点击时执行', '下一': '继续往下串执行链' }
    },
    'btn/kill': {
        desc: '杀手刀按钮：自动锁定最近的合法目标（排除队友/隔墙），点击即完成一次击杀。',
        usage: '击杀本身由框架完成，你只需决定"击杀成功之后做什么"。注意"目标"这个输出只有接在"点击"分支里才有意义（它代表刚被击杀的那名玩家）。',
        in: { '执行': '从入口接进来才会创建按钮' },
        out: {
            '点击': '击杀成功后的回调（可在此使用"目标"）',
            '下一': '继续往下串执行链（与按钮点击无关的另一条链）',
            '目标': '本次被击杀的玩家，只能在"点击"分支里使用'
        }
    },

    // ---------- 动作 ----------
    'act/emergencyMeeting': {
        desc: '立即召开紧急会议。',
        usage: '常接到技能按钮的"点击"里。'
    },
    'act/setRoleByCode': {
        desc: '把指定玩家设成某个职业（按内部名 CodeName 匹配）。',
        usage: '在"目标职业内部名"里填职业的 CodeName（必填且全局唯一）。'
    },
    'act/systemMessage': {
        desc: '给指定玩家弹一条系统消息（仅该玩家可见）。',
        usage: '文本可用"文本"节点拼接，或直接留空由别的节点提供。'
    },
    'act/kill': {
        desc: '让"凶手"击杀"受害者"（通用击杀，不限于按钮）。',
        usage: '与"击杀按钮"不同：这里可指定任意凶手/受害者，适合由事件触发的击杀。'
    },
    'act/suicide': {
        desc: '让指定玩家自杀。',
        usage: '不接"玩家"时默认对自己操作。'
    },
    'act/revive': {
        desc: '复活指定玩家（仅房主生效）。',
        usage: '不接"玩家"时默认对自己操作。'
    },
    'act/invisible': {
        desc: '设置指定玩家隐身或显形。',
        usage: '"开关"接布尔值：true=隐身，false=显形；不接则默认 true。'
    },
    'act/teleport': {
        desc: '把指定玩家传送到某个坐标（仅房主生效）。',
        usage: '"坐标"接"坐标"节点。不接时传送到 (0,0)。'
    },
    'act/forceEndMeeting': {
        desc: '强制结束当前会议（仅房主生效）。',
        usage: '用于结束拖沓的会议。'
    },
    'act/breakEmergencyButton': {
        desc: '破坏紧急按钮，本局不能再按（仅房主生效）。',
        usage: '配合"能否按紧急按钮"的职业设定使用。'
    },
    'act/showChat': {
        desc: '显示聊天框（自由聊天）。',
        usage: '让玩家能在非会议时间打字。'
    },
    'act/hideChat': {
        desc: '隐藏聊天框。',
        usage: '与"显示聊天"成对使用。'
    },
    'act/sendChat': {
        desc: '以指定玩家的身份发送一条聊天消息。',
        usage: '"文本"要接文本节点或留空。'
    },
    'act/kick': {
        desc: '把指定玩家踢出房间（仅房主生效）。',
        usage: '谨慎使用；建议先给"系统消息"提示原因。'
    },
    'act/flash': {
        desc: '全屏颜色闪烁，用于受击/提示反馈。',
        usage: '"颜色"接"颜色"节点；不接则默认红色。'
    },
    'act/log': {
        desc: '写一条日志到 LightLog.log，便于调试。',
        usage: '调试用，正式发布前建议移除或保留少量。'
    },

    // ---------- 数值 / 文本 ----------
    'val/text': {
        desc: '一个固定文本值。',
        usage: '给需要文本的输入端口提供内容。'
    },
    'val/number': {
        desc: '一个固定数值。',
        usage: '给需要数值的输入端口提供内容（如距离、冷却、比较）。'
    },
    'val/player': {
        desc: '一个玩家引用。',
        usage: '可选：我自己 / 目标 / 凶手 / 受害者 / 报告人。「凶手」「受害者」「报告人」要在事件或生命周期处理里用（生成代码取 e.Killer / e.Victim / e.Reporter）；「目标」只在击杀按钮的「点击」分支里有效。'
    },
    'val/bool': {
        desc: '一个固定布尔值（真/假）。',
        usage: '给需要布尔判断的输入端口提供内容。'
    },
    'val/color': {
        desc: '一个颜色值（R/G/B/A，0-255）。',
        usage: '给"屏幕闪烁"等需要颜色的端口使用。'
    },
    'val/playerName': {
        desc: '取某个玩家的名字（字符串）。',
        usage: '拼接消息、做名字判断时使用。'
    },
    'val/isDead': {
        desc: '判断某个玩家是否已经死亡（布尔）。',
        usage: '接到"条件分支"做死活判断。'
    },
    'val/isImpostor': {
        desc: '判断某个玩家是否属于内鬼阵营（布尔）。',
        usage: '按阵营做分支。'
    },
    'val/randomAlive': {
        desc: '随机取一个当前存活的玩家。',
        usage: '随机选人时使用（如随机传唤、随机奖励）。'
    },
    'val/aliveCount': {
        desc: '当前存活人数（数值）。',
        usage: '配合"数值比较"做人数条件。'
    },
    'val/distance': {
        desc: '两名玩家之间的距离（数值）。',
        usage: '不接 A/B 时默认都取自己（结果为 0）。'
    },
    'val/inRange': {
        desc: '判断两名玩家是否在指定距离之内（布尔）。',
        usage: '"距离"接数值节点，不接默认 2。'
    },
    'val/vector2': {
        desc: '一组 X/Y 坐标值。',
        usage: '配合"传送玩家"的"坐标"端口使用。'
    },
    'val/gameState': {
        desc: '判断当前游戏状态（阶段/环境）是否为选中项，实时读取、不是缓存值。',
        usage: '常用：IsLobby 在大厅、IsInGame 局内、IsInMeeting 会议中、IsVoting 投票中、IsInTask 任务阶段、IsInIntro 开场动画、IsInExile 放逐动画、IsInMinigame 小游戏、IsMapOpen 地图开着、IsChatFocused 聊天输入中、IsCanMove 能移动、IsInVent 在管道、IsCountDown 倒计时、IsDead 我死了、AmHost 我是房主、IsModHost 房主且跑着本模组、IsFreePlay/IsOnlineGame/IsLocalGame 连接方式、IsNormalGame/IsHideNSeek 模式、IsInitGame 开局建表中、IsMeetingResults 会议结果、IsJoined/IsNotJoined 是否在房间。'
    },
    'val/isMap': {
        desc: '判断当前地图是否为指定的一张。',
        usage: '可选 Skeld / MiraHQ / Polus / Dleks / Airship / Fungle。'
    },
    'val/hostPlayer': {
        desc: '房主玩家。',
        usage: '取房主的玩家引用（大厅玩家表建好之后才有值）。'
    },
    'val/isHost': {
        desc: '判断某个玩家是否为房主。',
        usage: '"玩家"不接时默认判断自己。'
    },
    'val/playerById': {
        desc: '按 PlayerId 取玩家。',
        usage: '"id"接数值。取不到时结果为 null，要使用它的字段前建议先做判断。'
    },
    'val/allPlayerCount': {
        desc: '当前玩家总数（含已死亡）。',
        usage: '与"存活人数"区分：这个把死人也算进去。'
    },

    // ---------- 逻辑 ----------
    'logic/if': {
        desc: '条件分支：条件为真走"真"分支，否则走"假"分支。',
        usage: '"条件"接布尔值（如"是否在范围内""数值比较"）。"假"分支可以不接。'
    },
    'logic/compare': {
        desc: '比较两个数并输出布尔结果。',
        usage: '选择比较符（== != > >= < <=）。'
    },
    'logic/and': {
        desc: '逻辑与：两个条件都为真才为真。',
        usage: '组合多个条件。'
    },
    'logic/or': {
        desc: '逻辑或：任一条件为真即为真。',
        usage: '组合多个条件。'
    },
    'logic/not': {
        desc: '逻辑非：把真变假、假变真。',
        usage: '取反一个布尔条件。'
    },
    'logic/cancelEvent': {
        desc: '取消当前可取消事件（如阻止一次击杀、阻止会议结束）。',
        usage: '接到可取消事件（如"尝试击杀"）的处理链里，把"事件"接进来。'
    },
    'logic/math': {
        desc: '对两个数做 + - * / %（取余）运算。',
        usage: '计算冷却、票数、伤害等数值。'
    },

    // ---------- 数据位 ----------
    'data/registerId': {
        desc: '注册一个"每玩家数据位"，得到一个 id。',
        usage: '配合"读取/写入/累加数据"保存每玩家状态（票数、剩余次数…）。框架负责房主权威同步，不必自己写 RPC。Key 是标识名，同职业内唯一。'
    },
    'data/get': {
        desc: '读取某玩家某个数据位的当前值。',
        usage: '"id"接"注册数据位"的输出。'
    },
    'data/set': {
        desc: '写入某玩家某个数据位的值（房主权威）。',
        usage: '"id"接"注册数据位"；"值"接数值。'
    },
    'data/add': {
        desc: '给某玩家的数据位累加一个数，并输出累加后的新值。',
        usage: '常用于"票数 +1""剩余次数 -1"。'
    },

    // ---------- 目标追踪 ----------
    'target/closest': {
        desc: '寻找离自己最近的合法目标（按谓词过滤：标准/可击杀/内鬼目标/排除队友）。',
        usage: '输出玩家引用，可接到"设为职业""传送"等需要玩家的端口。'
    },
    'target/createTracker': {
        desc: '创建一个每帧自动锁定最近目标的追踪器并注册到框架（会高亮目标）。',
        usage: '创建后可用"追踪器"输出做进一步判断；一般配合技能按钮使用。'
    },

    // ---------- 事件（私有监听）----------
    'ev/playerMurder': {
        desc: '监听"玩家击杀"事件（击杀发生后）。',
        usage: '这些事件是全局的，所有玩家都会进来。要限定"是否与我有关"，请用玩家相关节点判断。开关：仅房主 / 仅本玩家 / 仅本客户端 / 优先级（数值大者先执行）。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },
    'ev/tryMurder': {
        desc: '监听"尝试击杀"事件（可取消：用"取消事件"阻止这次击杀）。',
        usage: '常用于"免疫一次攻击""反弹伤害"等。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象（可取消）' }
    },
    'ev/playerDeath': {
        desc: '监听"玩家死亡"事件。',
        usage: '记录死亡、触发胜负判定等。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },
    'ev/vote': {
        desc: '监听"玩家投票"事件（可修改票数权重/目标）。',
        usage: '做投票相关的职业效果。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },
    'ev/ventUsed': {
        desc: '监听"使用管道"事件。',
        usage: '按管道使用做触发条件。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },
    'ev/meetingStart': {
        desc: '监听"会议开始"事件。',
        usage: '与生命周期里的"会议开始"等价，按习惯二选一即可。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },
    'ev/meetingEnd': {
        desc: '监听"会议结束"事件。',
        usage: '会议后的状态重置。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },
    'ev/playerRevive': {
        desc: '监听"玩家复活"事件。',
        usage: '复活相关效果。',
        out: { '执行': '事件处理逻辑', '事件': '事件对象' }
    },

    // ---------- 自定义 RPC ----------
    'rpc/customDefinition': {
        desc: '定义一个自定义同步 RPC（标 [LidRPC]：调用即全网广播并本地执行）。',
        usage: '"方法名"填 C# 方法名，"参数"填 C# 形参列表（如：byte playerId, int value）。方法体里接执行节点。接收端会执行同一段逻辑。'
    },
    'rpc/call': {
        desc: '调用已定义的自定义 RPC。',
        usage: '"方法名"要与定义处的名称一致；"实参"填 C# 实参列表。'
    },

    // ---------- 配置 ----------
    'cfg/get': {
        desc: '读取本职业的配置项数值（在"职业配置"里添加的项）。',
        usage: '"键"填配置项的 Key（不含前缀），生成 role.<职业内部名>.<键>。'
    }
};

// ---------- 使用教程 ----------
var DOC_TUTORIAL = [
    {
        title: '1. 三种端口：执行(exec)、数据(data)、事件(event)',
        html: '<p>节点下方的圆点就是端口，颜色区分类型，只有<b>同类型</b>才能相连（连线时会自动拒绝不匹配的类型）。</p>' +
            '<ul>' +
            '<li><b>执行(exec)</b>：决定"先后顺序"。从入口一路串下去，像流程图的箭头。</li>' +
            '<li><b>数据(data)</b>：决定"用什么值"。把"数值/文本/玩家/颜色/坐标"喂给需要它的输入端口（如"距离""文本""玩家"）。</li>' +
            '<li><b>事件(event)</b>：代表一个事件对象，可接到"取消事件"等处。</li>' +
            '</ul>'
    },
    {
        title: '2. 一条最小可用的职业逻辑',
        html: '<p>① 在「逻辑蓝图」里，从"激活时"开始；② 把它的<b>执行</b>连到"技能按钮"的<b>执行</b>；' +
            '③ 把"技能按钮"的<b>点击</b>连到一个动作（如"召开紧急会议"）；④ 到「职业配置」填写名字/阵营/颜色；' +
            '⑤ 在「源码预览」确认 C#；⑥ 点「写入主插件源码树」。</p>'
    },
    {
        title: '3. "点击"和"下一"有什么区别？',
        html: '<p>按钮类节点（技能按钮 / 击杀按钮）有两个执行输出：</p>' +
            '<ul>' +
            '<li><b>点击</b>：按钮被按下时执行的内容 —— 相当于"按钮的回调函数"。</li>' +
            '<li><b>下一</b>：这条链<b>之外</b>的后续执行 —— 用于在按钮之后继续挂别的节点（例如再创建第二个按钮）。' +
            '它和"点击"是两条平行的链，互不影响。</li>' +
            '</ul>'
    },
    {
        title: '4. "击杀按钮"的三个端口到底连什么？',
        html: '<p>击杀按钮的击杀动作由框架自动完成（自动锁定最近合法目标、排除队友与隔墙），所以你不需要自己写"怎么杀"。三个输出端口分别是：</p>' +
            '<ul>' +
            '<li><b>点击</b>（执行）：<b>击杀成功之后</b>要做的事。例如"给目标发系统消息""把自己设为另一个职业""播放闪烁"。</li>' +
            '<li><b>下一</b>（执行）：继续往下串执行链（比如再创建一个技能按钮），与"点击"平行。</li>' +
            '<li><b>目标</b>（玩家）：本次被击杀的那名玩家。接到需要玩家的地方（如"设为职业""传送"）即可。</li>' +
            '</ul>' +
            '<p><b>重要：</b>"目标"只在"点击"分支里有效 —— 因为它就是击杀回调的参数。若把它接到"下一"链路上，生成代码会引用到作用域外的变量而编译失败。工具会在校验里提示。</p>'
    },
    {
        title: '5. 事件节点（"玩家击杀""尝试击杀"…）怎么用',
        html: '<p>事件节点是"当某事发生时"的入口，输出<b>执行</b>和<b>事件</b>。左侧开关决定触发范围：</p>' +
            '<ul>' +
            '<li><b>仅房主</b>：只有主机会执行（适合需要权威判定的逻辑）。</li>' +
            '<li><b>仅本玩家</b>：只有当"事件里的玩家"是你自己时才执行。</li>' +
            '<li><b>仅本客户端</b>：只在本机执行（适合纯界面表现）。</li>' +
            '<li><b>优先级</b>：数值越大越先执行。</li>' +
            '</ul>' +
            '<p>"尝试击杀"这类事件是<b>可取消</b>的：把它的"事件"接到"取消事件"上即可阻止本次行为。</p>'
    },
    {
        title: '6. 数据位：给每个玩家存状态',
        html: '<p>三步：① 放一个"注册数据位"（填 Key，如 Votes）；② 用"写入/累加数据"改值、"读取数据"取值；' +
            '③ "id"端口接"注册数据位"的输出。</p>' +
            '<p>数据位的同步由框架负责（房主权威），你不需要写 RPC。数据在换局时自动清空。</p>'
    },
    {
        title: '7. 追踪与索敌',
        html: '<p>"最近目标"返回离你最近的合法玩家（可选过滤：标准/可击杀/内鬼目标/排除队友）。' +
            '"创建追踪器"会注册一个每帧自动锁定的追踪器（可用于高亮目标）。</p>'
    },
    {
        title: '8. 从蓝图到游戏',
        html: '<ol>' +
            '<li>搭好蓝图、填好配置；</li>' +
            '<li>点「写入主插件源码树」——会把生成的 .cs 写进 Light 主插件对应阵营目录，并把翻译键合并进语言文件；</li>' +
            '<li>点「一键编译」重新构建主插件（产物输出到 Output）；</li>' +
            '<li>把产物放进游戏的 BepInEx/plugins 后启动游戏，职业会自动被扫描注册。</li>' +
            '</ol>'
    },
    {
        title: '9. 常见问题',
        html: '<ul>' +
            '<li><b>端口连不上？</b> 类型不匹配会被拒绝，请确认端口类型一致（如"文本"只能接文本类输入）。</li>' +
            '<li><b>提示"内部名与已有职业重复"？</b> 换个 CodeName，或删掉旧职业文件。</li>' +
            '<li><b>按钮不出现？</b> 确认"技能按钮/击杀按钮"的"执行"已接到"激活时"。</li>' +
            '<li><b>玩法没生效？</b> 写入后需要"一键编译"并替换游戏里的插件文件。</li>' +
            '</ul>'
    }
];

// ---------- 渲染 ----------

// RoleTool 自己的节点分类（LiteGraph 自带节点不纳入文档）
var DOC_CATEGORIES = ['life', 'btn', 'act', 'data', 'ev', 'target', 'logic', 'val', 'rpc', 'cfg'];

function docIsOurs(type) {
    return DOC_CATEGORIES.indexOf(String(type || '').split('/')[0]) !== -1;
}

// 读取某节点类型的端口（实例化后取，保证与实现一致）
function docPortsOf(type) {
    var n = null;
    if (docIsOurs(type)) {
        try { n = LiteGraph.createNode(type); } catch (e) { n = null; }
    }
    if (!n) return { inputs: [], outputs: [], widgets: [] };
    return {
        inputs: (n.inputs || []).map(function (p) { return p.name; }),
        outputs: (n.outputs || []).map(function (p) { return p.name; }),
        widgets: (n.widgets || []).map(function (w) { return w.name; })
    };
}

function docEscape(s) {
    return String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function docRenderTutorial() {
    var html = ['<div class="doc-tutorial">'];
    html.push('<h3>使用教程</h3>');
    DOC_TUTORIAL.forEach(function (sec) {
        html.push('<details class="doc-tut-item"><summary>' + docEscape(sec.title) + '</summary>');
        html.push('<div class="doc-tut-body">' + sec.html + '</div>');
        html.push('</details>');
    });
    html.push('</div>');
    return html.join('');
}

function docPortTable(ports, doc) {
    var rows = [];
    (ports.inputs || []).forEach(function (name) {
        var m = (doc.in && doc.in[name]) || '';
        rows.push('<tr><td class="pdir pin">输入</td><td class="pname">' + docEscape(name) +
            '</td><td class="pmean">' + docEscape(m) + '</td></tr>');
    });
    (ports.outputs || []).forEach(function (name) {
        var m = (doc.out && doc.out[name]) || '';
        rows.push('<tr><td class="pdir pout">输出</td><td class="pname">' + docEscape(name) +
            '</td><td class="pmean">' + docEscape(m) + '</td></tr>');
    });
    if (!rows.length) rows.push('<tr><td class="pdir">-</td><td class="pname">-</td><td class="pmean">无端口</td></tr>');
    return '<table class="doc-ports">' + rows.join('') + '</table>';
}

function docNodeCard(type, title, doc) {
    var ports = docPortsOf(type);
    var d = doc || {};
    var cat = type.split('/')[0];
    var color = (typeof C !== 'undefined' && C[cat]) ? C[cat].fg : '#4a90d9';
    var widgets = (ports.widgets || []).length
        ? '<div class="doc-widgets">参数：' + ports.widgets.map(function (w) { return '<code>' + docEscape(w) + '</code>'; }).join(' ') + '</div>'
        : '';
    var hay = (title + ' ' + type + ' ' + (d.desc || '') + ' ' + (d.usage || '')).toLowerCase();

    return '<details class="doc-card" data-hay="' + docEscape(hay) + '">' +
        '<summary><span class="doc-dot" style="background:' + color + '"></span>' +
        '<span class="doc-title">' + docEscape(title) + '</span>' +
        '<code class="doc-type">' + docEscape(type) + '</code></summary>' +
        '<div class="doc-body">' +
        '<p class="doc-desc">' + docEscape(d.desc || '（暂无说明）') + '</p>' +
        docPortTable(ports, d) +
        widgets +
        (d.usage ? '<p class="doc-usage"><b>用法：</b>' + docEscape(d.usage) + '</p>' : '') +
        '</div></details>';
}

function renderDocs() {
    var host = document.getElementById('docs-body');
    if (!host) return;

    var html = [docRenderTutorial()];
    html.push('<h3 class="doc-nodes-title">节点手册</h3>');

    var cats = {};
    Object.keys(LiteGraph.registered_node_types).forEach(function (type) {
        if (!docIsOurs(type)) return;
        var cat = type.split('/')[0];
        (cats[cat] = cats[cat] || []).push(type);
    });

    DOC_CATEGORIES.forEach(function (cat) {
        if (!cats[cat]) return;
        var label = (typeof CN_CATEGORY !== 'undefined' && CN_CATEGORY[cat]) ? CN_CATEGORY[cat] : cat;
        html.push('<div class="doc-group"><div class="doc-group-title">' + label +
            '<span class="doc-group-count">' + cats[cat].length + '</span></div>');
        cats[cat].forEach(function (type) {
            var title = LiteGraph.registered_node_types[type].title || type;
            html.push(docNodeCard(type, title, NODE_DOC[type]));
        });
        html.push('</div>');
    });

    host.innerHTML = html.join('');
}

function filterDocs(query) {
    var q = (query || '').trim().toLowerCase();
    var host = document.getElementById('docs-body');
    if (!host) return;
    host.querySelectorAll('.doc-card').forEach(function (card) {
        var hay = card.getAttribute('data-hay') || '';
        card.classList.toggle('hidden', !!q && hay.indexOf(q) === -1);
    });
    host.querySelectorAll('.doc-group').forEach(function (g) {
        var visible = 0;
        g.querySelectorAll('.doc-card').forEach(function (c) { if (!c.classList.contains('hidden')) visible++; });
        g.classList.toggle('hidden', visible === 0);
    });
}

function initDocs() {
    if (typeof LiteGraph === 'undefined') { setTimeout(initDocs, 100); return; }
    renderDocs();
}
