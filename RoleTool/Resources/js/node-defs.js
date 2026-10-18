// 仅在代码生成模块可用时触发重新生成
function regenerate() {
    if (typeof regenerateAll === 'function') regenerateAll();
}

function registerAllNodes() {
    // 清空已注册节点
    for (const k of Object.keys(LiteGraph.registered_node_types)) {
        delete LiteGraph.registered_node_types[k];
    }

    // 快速定义并注册一个节点类型
    function defNode(typeStr, title, buildFn, c, bg) {
        function N() { buildFn.call(this); this.shape = LiteGraph.BOX_SHAPE; }
        N.title = title;
        N.prototype.color = c;
        N.prototype.bgcolor = bg;
        N.prototype.onConfigure = function () {
            if (this.widgets && this.properties) {
                if (this._propKeys) {
                    this._propKeys.forEach((propKey, i) => {
                        if (this.widgets[i] && propKey && this.properties[propKey] != null) this.widgets[i].value = this.properties[propKey];
                    });
                } else {
                    this.widgets.forEach(w => { if (w.name && this.properties[w.name] != null) w.value = this.properties[w.name]; });
                }
            }
            if (typeof regenerateAll === 'function') setTimeout(regenerateAll, 80);
        };
        LiteGraph.registerNodeType(typeStr, N);
    }

    // ---------- 生命周期 ----------
    defNode("life/onActivated", "激活时", function () {
        this.addOutput("执行", "exec");
        this.size = [170, 52];
    }, C.life.fg, C.life.bg);

    defNode("life/onInactivated", "失活时", function () {
        this.addOutput("执行", "exec");
        this.size = [170, 52];
    }, C.life.fg, C.life.bg);

    defNode("life/checkWin", "胜负判定", function () {
        this.addInput("条件", "bool");
        this.size = [180, 52];
    }, C.life.fg, C.life.bg);

    // ---------- 技能按钮 ----------
    defNode("btn/ability", "技能按钮", function () {
        this.addInput("执行", "exec");
        this.addProperty("Button.<Role>.label", "");
        this.addWidget("text", "Button.<Role>.label", "", (v) => { this.properties["Button.<Role>.label"] = v; setTimeout(regenerate, 80); });
        this.addProperty("Hotkey", "E");
        this.addWidget("combo", "Hotkey", "E", (v) => { this.properties["Hotkey"] = v; setTimeout(regenerate, 80); },
            { values: ["A", "B", "C", "D", "E", "F", "Q", "R", "Space"] });
        this.addProperty("Cooldown", 20);
        this.addWidget("number", "Cooldown", 20, (v) => { this.properties["Cooldown"] = v; setTimeout(regenerate, 80); });
        this.addProperty("LeftSide", false);
        this.addWidget("toggle", "LeftSide", false, (v) => { this.properties["LeftSide"] = v; setTimeout(regenerate, 80); });
        this.addProperty("MaxUses", 0);
        this.addWidget("number", "MaxUses", 0, (v) => { this.properties["MaxUses"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["Button.<Role>.label", "Hotkey", "Cooldown", "LeftSide", "MaxUses"];
        this.addOutput("点击", "exec");
        this.addOutput("下一", "exec");
        this.size = [240, 172];
    }, C.btn.fg, C.btn.bg);

    // ---------- 动作 ----------
    defNode("act/emergencyMeeting", "召开紧急会议", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [190, 60];
    }, C.act.fg, C.act.bg);

    defNode("act/setRoleByCode", "设为职业(按CodeName)", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addProperty("TargetRoleCode", "");
        this.addWidget("text", "TargetRoleCode", "", (v) => { this.properties["TargetRoleCode"] = v; setTimeout(regenerate, 80); });
        this.addOutput("下一", "exec");
        this.size = [220, 92];
    }, C.act.fg, C.act.bg);

    defNode("act/systemMessage", "系统消息", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addInput("文本", "string");
        this.addOutput("下一", "exec");
        this.size = [200, 104];
    }, C.act.fg, C.act.bg);

    // ---------- 数值 / 文本 ----------
    defNode("val/text", "文本", function () {
        this.addProperty("text", "");
        this.addWidget("text", "text", "", (v) => { this.properties["text"] = v; setTimeout(regenerate, 80); });
        this.addOutput("值", "string");
        this.size = [180, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/number", "数值", function () {
        this.addProperty("number", 0);
        this.addWidget("number", "number", 0, (v) => { this.properties["number"] = v; setTimeout(regenerate, 80); });
        this.addOutput("值", "number");
        this.size = [180, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/player", "玩家", function () {
        this.addProperty("MyPlayer", "MyPlayer");
        this.addWidget("combo", "MyPlayer", "MyPlayer", (v) => { this.properties["MyPlayer"] = v; setTimeout(regenerate, 80); },
            { values: ["MyPlayer", "Target", "Killer", "Victim", "Reporter"] });
        this.addOutput("值", "player");
        this.size = [180, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/bool", "布尔", function () {
        this.addProperty("true", true);
        this.addWidget("toggle", "true", true, (v) => { this.properties["true"] = v; setTimeout(regenerate, 80); });
        this.addOutput("值", "bool");
        this.size = [170, 60];
    }, C.val.fg, C.val.bg);

    // ---------- 逻辑 ----------
    defNode("logic/if", "条件分支", function () {
        this.addInput("执行", "exec");
        this.addInput("条件", "bool");
        this.addOutput("真", "exec");
        this.addOutput("假", "exec");
        this.size = [190, 84];
    }, C.logic.fg, C.logic.bg);

    // ---------- 生命周期（事件钩子）----------
    defNode("life/onDied", "死亡时", function () {
        this.addOutput("执行", "exec");
        this.addOutput("事件", "event");
        this.size = [170, 64];
    }, C.life.fg, C.life.bg);

    defNode("life/onRevived", "复活时", function () {
        this.addOutput("执行", "exec");
        this.addOutput("事件", "event");
        this.size = [170, 64];
    }, C.life.fg, C.life.bg);

    defNode("life/onExiled", "被放逐时", function () {
        this.addOutput("执行", "exec");
        this.addOutput("事件", "event");
        this.size = [170, 64];
    }, C.life.fg, C.life.bg);

    defNode("life/onMeetingStarted", "会议开始", function () {
        this.addOutput("执行", "exec");
        this.addOutput("事件", "event");
        this.size = [170, 64];
    }, C.life.fg, C.life.bg);

    defNode("life/onMeetingEnded", "会议结束", function () {
        this.addOutput("执行", "exec");
        this.addOutput("事件", "event");
        this.size = [170, 64];
    }, C.life.fg, C.life.bg);

    defNode("life/onRoleData", "数据位变化", function () {
        this.addOutput("执行", "exec");
        this.addOutput("dataId", "number");
        this.addOutput("value", "number");
        this.size = [180, 76];
    }, C.life.fg, C.life.bg);

    // ---------- 击杀按钮 ----------
    defNode("btn/kill", "击杀按钮", function () {
        this.addInput("执行", "exec");
        this.addProperty("Cooldown", 20);
        this.addWidget("number", "Cooldown", 20, (v) => { this.properties["Cooldown"] = v; setTimeout(regenerate, 80); });
        this.addProperty("LeftSide", false);
        this.addWidget("toggle", "LeftSide", false, (v) => { this.properties["LeftSide"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["Cooldown", "LeftSide"];
        this.addOutput("点击", "exec");
        this.addOutput("下一", "exec");
        this.addOutput("目标", "player");
        this.size = [220, 150];
    }, C.btn.fg, C.btn.bg);

    // ---------- 数据位 ----------
    defNode("data/registerId", "注册数据位", function () {
        this.addProperty("Key", "Counter");
        this.addWidget("text", "Key", "Counter", (v) => { this.properties["Key"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["Key"];
        this.addOutput("id", "number");
        this.size = [190, 60];
    }, C.data.fg, C.data.bg);

    defNode("data/get", "读取数据", function () {
        this.addInput("玩家", "player");
        this.addInput("id", "number");
        this.addOutput("值", "number");
        this.size = [190, 76];
    }, C.data.fg, C.data.bg);

    defNode("data/set", "写入数据", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addInput("id", "number");
        this.addInput("值", "number");
        this.addOutput("下一", "exec");
        this.size = [200, 100];
    }, C.data.fg, C.data.bg);

    defNode("data/add", "累加数据", function () {
        this.addInput("玩家", "player");
        this.addInput("id", "number");
        this.addInput("增量", "number");
        this.addOutput("值", "number");
        this.size = [200, 88];
    }, C.data.fg, C.data.bg);

    // ---------- 目标追踪 ----------
    defNode("target/closest", "最近目标", function () {
        this.addProperty("Predicate", "Killable");
        this.addWidget("combo", "Predicate", "Killable", (v) => { this.properties["Predicate"] = v; setTimeout(regenerate, 80); },
            { values: ["Standard", "Killable", "ImpostorTarget", "ExcludeTeammates"] });
        this.addProperty("Distance", 2);
        this.addWidget("number", "Distance", 2, (v) => { this.properties["Distance"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["Predicate", "Distance"];
        this.addOutput("玩家", "player");
        this.size = [200, 90];
    }, C.target.fg, C.target.bg);

    defNode("target/createTracker", "创建追踪器", function () {
        this.addInput("执行", "exec");
        this.addProperty("Predicate", "Killable");
        this.addWidget("combo", "Predicate", "Killable", (v) => { this.properties["Predicate"] = v; setTimeout(regenerate, 80); },
            { values: ["Standard", "Killable", "ImpostorTarget", "ExcludeTeammates"] });
        this.addProperty("Distance", 2);
        this.addWidget("number", "Distance", 2, (v) => { this.properties["Distance"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["Predicate", "Distance"];
        this.addOutput("下一", "exec");
        this.addOutput("追踪器", "tracker");
        this.size = [210, 100];
    }, C.target.fg, C.target.bg);

    // ---------- 事件（私有监听）----------
    var EV_LIST = [
        ["ev/playerMurder", "玩家击杀"],
        ["ev/tryMurder", "尝试击杀"],
        ["ev/playerDeath", "玩家死亡"],
        ["ev/vote", "玩家投票"],
        ["ev/ventUsed", "使用管道"],
        ["ev/meetingStart", "会议开始"],
        ["ev/meetingEnd", "会议结束"],
        ["ev/playerRevive", "玩家复活"],
    ];
    EV_LIST.forEach(function (pair) {
        defNode(pair[0], pair[1], function () {
            this.addProperty("OnlyHost", false);
            this.addWidget("toggle", "OnlyHost", false, (v) => { this.properties["OnlyHost"] = v; setTimeout(regenerate, 80); });
            this.addProperty("OnlyMyPlayer", false);
            this.addWidget("toggle", "OnlyMyPlayer", false, (v) => { this.properties["OnlyMyPlayer"] = v; setTimeout(regenerate, 80); });
            this.addProperty("Local", false);
            this.addWidget("toggle", "Local", false, (v) => { this.properties["Local"] = v; setTimeout(regenerate, 80); });
            this.addProperty("Priority", 0);
            this.addWidget("number", "Priority", 0, (v) => { this.properties["Priority"] = v; setTimeout(regenerate, 80); });
            this._propKeys = ["OnlyHost", "OnlyMyPlayer", "Local", "Priority"];
            this.addOutput("执行", "exec");
            this.addOutput("事件", "event");
            this.size = [200, 150];
        }, C.ev.fg, C.ev.bg);
    });

    // ---------- 逻辑 ----------
    defNode("logic/compare", "数值比较", function () {
        this.addInput("A", "number");
        this.addInput("B", "number");
        this.addProperty("Op", "==");
        this.addWidget("combo", "Op", "==", (v) => { this.properties["Op"] = v; setTimeout(regenerate, 80); },
            { values: ["==", "!=", ">", ">=", "<", "<="] });
        this._propKeys = ["Op"];
        this.addOutput("结果", "bool");
        this.size = [190, 84];
    }, C.logic.fg, C.logic.bg);

    defNode("logic/and", "与", function () {
        this.addInput("A", "bool");
        this.addInput("B", "bool");
        this.addOutput("结果", "bool");
        this.size = [170, 76];
    }, C.logic.fg, C.logic.bg);

    defNode("logic/or", "或", function () {
        this.addInput("A", "bool");
        this.addInput("B", "bool");
        this.addOutput("结果", "bool");
        this.size = [170, 76];
    }, C.logic.fg, C.logic.bg);

    defNode("logic/not", "非", function () {
        this.addInput("A", "bool");
        this.addOutput("结果", "bool");
        this.size = [160, 64];
    }, C.logic.fg, C.logic.bg);

    defNode("logic/cancelEvent", "取消事件", function () {
        this.addInput("执行", "exec");
        this.addInput("事件", "event");
        this.addOutput("下一", "exec");
        this.size = [190, 84];
    }, C.logic.fg, C.logic.bg);

    defNode("logic/onlyOwner", "仅本人执行", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [180, 72];
    }, C.logic.fg, C.logic.bg);

    defNode("logic/onlyHost", "仅房主执行", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [180, 72];
    }, C.logic.fg, C.logic.bg);

    // ---------- 颜色 ----------
    defNode("val/color", "颜色", function () {
        ["R", "G", "B", "A"].forEach((k) => {
            this.addProperty(k, 255);
            this.addWidget("number", k, 255, (v) => { this.properties[k] = v; setTimeout(regenerate, 80); });
        });
        this._propKeys = ["R", "G", "B", "A"];
        this.addOutput("值", "color");
        this.size = [190, 130];
    }, C.val.fg, C.val.bg);

    // ---------- 自定义 RPC ----------
    defNode("rpc/customDefinition", "自定义RPC", function () {
        this.addProperty("MethodName", "SyncValue");
        this.addWidget("text", "MethodName", "SyncValue", (v) => { this.properties["MethodName"] = v; setTimeout(regenerate, 80); });
        this.addProperty("Params", "byte playerId, int value");
        this.addWidget("text", "Params", "byte playerId, int value", (v) => { this.properties["Params"] = v; setTimeout(regenerate, 80); });
        this.addProperty("Reliable", true);
        this.addWidget("toggle", "Reliable", true, (v) => { this.properties["Reliable"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["MethodName", "Params", "Reliable"];
        this.addOutput("方法体", "exec");
        this.size = [240, 130];
    }, C.rpc.fg, C.rpc.bg);

    defNode("rpc/call", "调用RPC", function () {
        this.addInput("执行", "exec");
        this.addProperty("MethodName", "SyncValue");
        this.addWidget("text", "MethodName", "SyncValue", (v) => { this.properties["MethodName"] = v; setTimeout(regenerate, 80); });
        this.addProperty("Args", "");
        this.addWidget("text", "Args", "", (v) => { this.properties["Args"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["MethodName", "Args"];
        this.addOutput("下一", "exec");
        this.size = [220, 92];
    }, C.rpc.fg, C.rpc.bg);

    // ---------- 配置 ----------
    defNode("cfg/get", "读取配置", function () {
        this.addProperty("Key", "");
        this.addWidget("text", "Key", "", (v) => { this.properties["Key"] = v; setTimeout(regenerate, 80); });
        this.addProperty("Type", "Float");
        this.addWidget("combo", "Type", "Float", (v) => { this.properties["Type"] = v; setTimeout(regenerate, 80); },
            { values: ["Float", "Int", "Bool"] });
        this._propKeys = ["Key", "Type"];
        this.addOutput("值", "number");
        this.size = [190, 84];
    }, C.cfg.fg, C.cfg.bg);

    // ---------- 动作（扩展）----------
    defNode("act/kill", "击杀目标", function () {
        this.addInput("执行", "exec");
        this.addInput("凶手", "player");
        this.addInput("受害者", "player");
        this.addOutput("下一", "exec");
        this.size = [200, 96];
    }, C.act.fg, C.act.bg);

    defNode("act/suicide", "自杀", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addOutput("下一", "exec");
        this.size = [190, 84];
    }, C.act.fg, C.act.bg);

    defNode("act/revive", "复活玩家", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addOutput("下一", "exec");
        this.size = [190, 84];
    }, C.act.fg, C.act.bg);

    defNode("act/invisible", "隐身/显形", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addInput("开关", "bool");
        this.addOutput("下一", "exec");
        this.size = [200, 96];
    }, C.act.fg, C.act.bg);

    defNode("act/teleport", "传送玩家", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addInput("坐标", "vector2");
        this.addOutput("下一", "exec");
        this.size = [200, 96];
    }, C.act.fg, C.act.bg);

    defNode("act/forceEndMeeting", "强制结束会议", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [190, 72];
    }, C.act.fg, C.act.bg);

    defNode("act/breakEmergencyButton", "破坏紧急按钮", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [200, 72];
    }, C.act.fg, C.act.bg);

    defNode("act/showChat", "显示聊天", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [180, 72];
    }, C.act.fg, C.act.bg);

    defNode("act/hideChat", "隐藏聊天", function () {
        this.addInput("执行", "exec");
        this.addOutput("下一", "exec");
        this.size = [180, 72];
    }, C.act.fg, C.act.bg);

    defNode("act/sendChat", "发送聊天", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addInput("文本", "string");
        this.addOutput("下一", "exec");
        this.size = [200, 96];
    }, C.act.fg, C.act.bg);

    defNode("act/kick", "踢出玩家", function () {
        this.addInput("执行", "exec");
        this.addInput("玩家", "player");
        this.addOutput("下一", "exec");
        this.size = [190, 84];
    }, C.act.fg, C.act.bg);

    defNode("act/flash", "屏幕闪烁", function () {
        this.addInput("执行", "exec");
        this.addInput("颜色", "color");
        this.addOutput("下一", "exec");
        this.size = [190, 84];
    }, C.act.fg, C.act.bg);

    defNode("act/log", "日志输出", function () {
        this.addInput("执行", "exec");
        this.addInput("文本", "string");
        this.addOutput("下一", "exec");
        this.size = [190, 84];
    }, C.act.fg, C.act.bg);

    defNode("act/guesserShoot", "赌怪开枪", function () {
        this.addInput("执行", "exec");
        this.addInput("凶手", "player");
        this.addInput("目标", "player");
        this.addProperty("GuessedRole", "");
        this.addWidget("text", "GuessedRole", "", (v) => { this.properties["GuessedRole"] = v; setTimeout(regenerate, 80); });
        this._propKeys = ["GuessedRole"];
        this.addOutput("下一", "exec");
        this.size = [220, 108];
    }, C.act.fg, C.act.bg);

    defNode("act/playTone", "播放音效", function () {
        this.addInput("执行", "exec");
        this.addProperty("Tone", "Gunfire");
        this.addWidget("combo", "Tone", "Gunfire", (v) => { this.properties["Tone"] = v; setTimeout(regenerate, 80); },
            { values: ["Gunload", "Gunfire"] });
        this._propKeys = ["Tone"];
        this.addOutput("下一", "exec");
        this.size = [190, 76];
    }, C.act.fg, C.act.bg);

    // ---------- 数值 / 文本（扩展）----------
    defNode("val/playerName", "玩家名字", function () {
        this.addInput("玩家", "player");
        this.addOutput("值", "string");
        this.size = [190, 72];
    }, C.val.fg, C.val.bg);

    defNode("val/isDead", "玩家是否死亡", function () {
        this.addInput("玩家", "player");
        this.addOutput("值", "bool");
        this.size = [200, 72];
    }, C.val.fg, C.val.bg);

    defNode("val/isImpostor", "玩家是否内鬼", function () {
        this.addInput("玩家", "player");
        this.addOutput("值", "bool");
        this.size = [200, 72];
    }, C.val.fg, C.val.bg);

    defNode("val/randomAlive", "随机存活玩家", function () {
        this.addOutput("值", "player");
        this.size = [200, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/aliveCount", "存活人数", function () {
        this.addOutput("值", "number");
        this.size = [180, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/distance", "两点距离", function () {
        this.addInput("A", "player");
        this.addInput("B", "player");
        this.addOutput("值", "number");
        this.size = [180, 76];
    }, C.val.fg, C.val.bg);

    defNode("val/inRange", "是否在范围内", function () {
        this.addInput("A", "player");
        this.addInput("B", "player");
        this.addInput("距离", "number");
        this.addOutput("值", "bool");
        this.size = [190, 88];
    }, C.val.fg, C.val.bg);

    defNode("val/vector2", "坐标", function () {
        ["X", "Y"].forEach((k) => {
            this.addProperty(k, 0);
            this.addWidget("number", k, 0, (v) => { this.properties[k] = v; setTimeout(regenerate, 80); });
        });
        this._propKeys = ["X", "Y"];
        this.addOutput("值", "vector2");
        this.size = [180, 90];
    }, C.val.fg, C.val.bg);

    // ---------- 游戏状态 / 玩家查询（对齐 GameState / Player 静态 API）----------
    var GAME_STATES = [
        "IsJoined", "IsNotJoined", "IsFreePlay", "IsOnlineGame", "IsLocalGame", "IsLobby",
        "AmHost", "IsInGame", "IsInitGame", "IsInMeeting", "IsVoting", "IsMeetingResults",
        "IsInTask", "IsInIntro", "IsInExile", "IsInMinigame", "IsMapOpen", "IsChatFocused",
        "IsCountDown", "IsDead", "IsCanMove", "IsInVent", "IsModHost", "IsNormalGame", "IsHideNSeek"
    ];
    defNode("val/gameState", "游戏状态", function () {
        this.addProperty("State", "IsInGame");
        this.addWidget("combo", "State", "IsInGame", (v) => { this.properties["State"] = v; setTimeout(regenerate, 80); },
            { values: GAME_STATES });
        this._propKeys = ["State"];
        this.addOutput("值", "bool");
        this.size = [210, 64];
    }, C.val.fg, C.val.bg);

    defNode("val/isMap", "是否某张地图", function () {
        this.addProperty("Map", "Skeld");
        this.addWidget("combo", "Map", "Skeld", (v) => { this.properties["Map"] = v; setTimeout(regenerate, 80); },
            { values: ["Skeld", "MiraHQ", "Polus", "Dleks", "Airship", "Fungle"] });
        this._propKeys = ["Map"];
        this.addOutput("值", "bool");
        this.size = [200, 64];
    }, C.val.fg, C.val.bg);

    defNode("val/hostPlayer", "房主玩家", function () {
        this.addOutput("值", "player");
        this.size = [180, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/isHost", "玩家是否房主", function () {
        this.addInput("玩家", "player");
        this.addOutput("值", "bool");
        this.size = [200, 72];
    }, C.val.fg, C.val.bg);

    defNode("val/playerById", "按ID取玩家", function () {
        this.addInput("id", "number");
        this.addOutput("值", "player");
        this.size = [190, 72];
    }, C.val.fg, C.val.bg);

    defNode("val/allPlayerCount", "玩家总数", function () {
        this.addOutput("值", "number");
        this.size = [180, 60];
    }, C.val.fg, C.val.bg);

    defNode("val/isCategory", "玩家是否某阵营", function () {
        this.addInput("玩家", "player");
        this.addProperty("Category", "Crewmate");
        this.addWidget("combo", "Category", "Crewmate", (v) => { this.properties["Category"] = v; setTimeout(regenerate, 80); },
            { values: ["Crewmate", "Impostor", "Neutral", "Ghost"] });
        this._propKeys = ["Category"];
        this.addOutput("值", "bool");
        this.size = [210, 88];
    }, C.val.fg, C.val.bg);

    // ---------- 逻辑（扩展）----------
    defNode("logic/math", "数学运算", function () {
        this.addInput("A", "number");
        this.addInput("B", "number");
        this.addProperty("Op", "+");
        this.addWidget("combo", "Op", "+", (v) => { this.properties["Op"] = v; setTimeout(regenerate, 80); },
            { values: ["+", "-", "*", "/", "%"] });
        this._propKeys = ["Op"];
        this.addOutput("结果", "number");
        this.size = [190, 84];
    }, C.logic.fg, C.logic.bg);
}
