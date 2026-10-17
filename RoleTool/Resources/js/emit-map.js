// 节点类型 → C# 代码 的声明式映射表（统一元素，由 codegen.js 消费）
// category: life/btn/act/val/logic…
// kind: life=顶层方法 / exec=执行链语句 / data=表达式生产者

// C# 字符串字面量转义
function csString(v) {
    return '"' + String(v == null ? '' : v)
        .replace(/\\/g, '\\\\')
        .replace(/"/g, '\\"')
        .replace(/\r/g, '\\r')
        .replace(/\n/g, '\\n') + '"';
}

// C# 数字字面量
function csNumber(v) {
    var n = Number(v);
    if (!isFinite(n)) n = 0;
    return String(n);
}

// C# float 实参：字面量加 f 后缀，其它表达式则强转（不能给表达式直接加 f）
function csFloat(expr) {
    var e = String(expr == null ? '' : expr).trim();
    if (/^-?[0-9]+(\.[0-9]+)?$/.test(e)) return e + 'f';
    return '(float)(' + e + ')';
}

// 读取节点属性（带默认值）
function nodeProp(node, key, def) {
    if (node && node.properties && node.properties[key] != null) return node.properties[key];
    return def;
}

// 标识符净化（用于字段名 / RPC 类名）
function sanitizeId(s) {
    return String(s == null ? '' : s).replace(/[^A-Za-z0-9_]/g, '_');
}

// 目标谓词表达式
function targetPredicate(node) {
    var p = nodeProp(node, 'Predicate', 'Killable');
    if (p === 'Standard') return 'TrackerPredicates.Standard(MyPlayer)';
    if (p === 'ImpostorTarget') return 'TrackerPredicates.ImpostorTarget(MyPlayer)';
    if (p === 'ExcludeTeammates') return 'p => !RoleTeam.IsTeammate(MyPlayer, p)';
    return 'TrackerPredicates.Killable(MyPlayer)';
}

const EMIT = {
    // ---------- 生命周期 ----------
    'life/onActivated': {
        category: 'life', kind: 'life',
        method: 'protected override void OnActivated()',
        imports: []
    },
    'life/onInactivated': {
        category: 'life', kind: 'life',
        method: 'protected override void OnInactivated()',
        imports: []
    },
    'life/checkWin': {
        category: 'life', kind: 'life',
        method: 'public override bool CheckWin()',
        imports: [],
        body: function (node, ctx) {
            return ['return ' + ctx.resolveBoolean(node, '条件', 'false') + ';'];
        }
    },

    // ---------- 技能按钮（点击/下一 双输出由 codegen 特殊处理）----------
    'btn/ability': {
        category: 'btn', kind: 'exec',
        imports: ['using LightInDark.UI.Ability;']
    },

    // ---------- 动作 ----------
    'act/emergencyMeeting': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function () { return ['RpcDefinitions.RpcStartMeeting();']; }
    },
    'act/setRoleByCode': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            var code = nodeProp(node, 'TargetRoleCode', '');
            return ['RpcDefinitions.SetRoleByCode(' +
                ctx.resolvePlayer(node, '玩家', 'MyPlayer') + '.Control.PlayerId, ' +
                csString(code) + ', null);'];
        }
    },
    'act/systemMessage': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.ShowSystemMessage(' +
                ctx.resolvePlayer(node, '玩家', 'MyPlayer') + '.Control.PlayerId, ' +
                ctx.resolveString(node, '文本', '""') + ');'];
        }
    },

    // ---------- 数值 / 文本（表达式）----------
    'val/text': {
        category: 'val', kind: 'data',
        expr: function (node) { return csString(nodeProp(node, 'text', '')); }
    },
    'val/number': {
        category: 'val', kind: 'data',
        expr: function (node) { return csNumber(nodeProp(node, 'number', 0)); }
    },
    'val/player': {
        category: 'val', kind: 'data',
        expr: function (node) {
            // 事件/生命周期处理方法的形参统一叫 e；btn/kill 的点击回调形参叫 target。
            // ⚠️ e.Killer / e.Victim / e.Reporter 是原版 PlayerControl，需用 Player.Get 转成本层的 Player
            //   （下游要取 .Control.PlayerId，直接给 PlayerControl 会编译不过）
            var v = String(nodeProp(node, 'MyPlayer', 'MyPlayer'));
            if (v === 'Killer') return 'LightInDark.Game.Player.Get(e.Killer)';
            if (v === 'Victim') return 'LightInDark.Game.Player.Get(e.Victim)';
            if (v === 'Reporter') return 'LightInDark.Game.Player.Get(e.Reporter)';
            if (v === 'Target') return 'target';
            return 'MyPlayer';
        }
    },
    'val/bool': {
        category: 'val', kind: 'data',
        expr: function (node) { return nodeProp(node, 'true', true) ? 'true' : 'false'; }
    },

    // ---------- 逻辑 ----------
    'logic/if': {
        category: 'logic', kind: 'exec'
    },

    // ---------- 生命周期（事件钩子）----------
    'life/onDied': {
        category: 'life', kind: 'life',
        method: 'protected override void OnDied(PlayerDeathEvent e)',
        imports: ['using LightInDark.Events;'],
        dataOut: function () { return 'e'; }
    },
    'life/onRevived': {
        category: 'life', kind: 'life',
        method: 'protected override void OnRevived(PlayerReviveEvent e)',
        imports: ['using LightInDark.Events;'],
        dataOut: function () { return 'e'; }
    },
    'life/onExiled': {
        category: 'life', kind: 'life',
        method: 'protected override void OnExiled(PlayerExileEvent e)',
        imports: ['using LightInDark.Events;'],
        dataOut: function () { return 'e'; }
    },
    'life/onMeetingStarted': {
        category: 'life', kind: 'life',
        method: 'protected override void OnMeetingStarted(MeetingStartEvent e)',
        imports: ['using LightInDark.Events;'],
        dataOut: function () { return 'e'; }
    },
    'life/onMeetingEnded': {
        category: 'life', kind: 'life',
        method: 'protected override void OnMeetingEnded(MeetingEndEvent e)',
        imports: ['using LightInDark.Events;'],
        dataOut: function () { return 'e'; }
    },
    'life/onRoleData': {
        category: 'life', kind: 'life',
        // 基类是 protected internal：跨程序集重写只能声明为 protected（否则 CS0507）
        method: 'protected override void OnRoleData(int dataId, int value)',
        imports: [],
        dataOut: function (outName) { return outName === 'value' ? 'value' : 'dataId'; }
    },

    // ---------- 击杀按钮 ----------
    'btn/kill': {
        category: 'btn', kind: 'exec',
        imports: ['using LightInDark.UI.Ability;'],
        dataOut: function () { return 'target'; }
    },

    // ---------- 数据位 ----------
    'data/registerId': {
        category: 'data', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function (node) { return '_' + sanitizeId(nodeProp(node, 'Key', 'Counter')) + 'Id'; }
    },
    'data/get': {
        category: 'data', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function (node, ctx) {
            return 'RoleData.Get(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') +
                '.Control.PlayerId, ' + ctx.resolveNumber(node, 'id', '0') + ')';
        }
    },
    'data/set': {
        category: 'data', kind: 'exec',
        imports: ['using LightInDark.Roles;'],
        lines: function (node, ctx) {
            return ['RoleData.Set(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') +
                '.Control.PlayerId, ' + ctx.resolveNumber(node, 'id', '0') +
                ', ' + ctx.resolveNumber(node, '值', '0') + ');'];
        }
    },
    'data/add': {
        category: 'data', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        // RoleData.Add 返回 void：用内联 lambda 先累加再读回新值，才能作为表达式使用
        expr: function (node, ctx) {
            var pid = ctx.resolvePlayer(node, '玩家', 'MyPlayer') + '.Control.PlayerId';
            var id = ctx.resolveNumber(node, 'id', '0');
            var delta = ctx.resolveNumber(node, '增量', '0');
            return '((System.Func<int>)(() => { RoleData.Add(' + pid + ', ' + id + ', ' + delta +
                '); return RoleData.Get(' + pid + ', ' + id + '); }))()';
        }
    },

    // ---------- 目标追踪 ----------
    'target/closest': {
        category: 'target', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function (node) {
            return 'RoleUtils.FindClosestPlayer(MyPlayer, ' + targetPredicate(node) +
                ', ' + csNumber(nodeProp(node, 'Distance', 2)) + 'f)';
        }
    },
    'target/createTracker': {
        category: 'target', kind: 'exec',
        imports: ['using LightInDark.Roles;', 'using LightInDark.UI.Ability;'],
        dataOut: function () { return '_tracker'; },
        lines: function (node) {
            return [
                '_tracker = new PlayerTracker(MyPlayer, ' + csNumber(nodeProp(node, 'Distance', 2)) +
                    'f, ' + targetPredicate(node) + ');',
                'RoleButtonManager.RegisterTracker(_tracker);'
            ];
        }
    },

    // ---------- 事件（私有监听）----------
    'ev/playerMurder': {
        category: 'ev', kind: 'ev', event: 'PlayerMurderEvent', name: 'PlayerMurder',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/tryMurder': {
        category: 'ev', kind: 'ev', event: 'PlayerTryMurderEvent', name: 'PlayerTryMurder',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/playerDeath': {
        category: 'ev', kind: 'ev', event: 'PlayerDeathEvent', name: 'PlayerDeath',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/vote': {
        category: 'ev', kind: 'ev', event: 'PlayerVoteEvent', name: 'PlayerVote',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/ventUsed': {
        category: 'ev', kind: 'ev', event: 'VentUsedEvent', name: 'VentUsed',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/meetingStart': {
        category: 'ev', kind: 'ev', event: 'MeetingStartEvent', name: 'MeetingStart',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/meetingEnd': {
        category: 'ev', kind: 'ev', event: 'MeetingEndEvent', name: 'MeetingEnd',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },
    'ev/playerRevive': {
        category: 'ev', kind: 'ev', event: 'PlayerReviveEvent', name: 'PlayerRevive',
        imports: ['using LightInDark.Events;'], dataOut: function () { return 'e'; }
    },

    // ---------- 逻辑 ----------
    'logic/compare': {
        category: 'logic', kind: 'data',
        expr: function (node, ctx) {
            return '(' + ctx.resolveNumber(node, 'A', '0') + ' ' + nodeProp(node, 'Op', '==') +
                ' ' + ctx.resolveNumber(node, 'B', '0') + ')';
        }
    },
    'logic/and': {
        category: 'logic', kind: 'data',
        expr: function (node, ctx) {
            return '(' + ctx.resolveBoolean(node, 'A', 'false') + ' && ' +
                ctx.resolveBoolean(node, 'B', 'false') + ')';
        }
    },
    'logic/or': {
        category: 'logic', kind: 'data',
        expr: function (node, ctx) {
            return '(' + ctx.resolveBoolean(node, 'A', 'false') + ' || ' +
                ctx.resolveBoolean(node, 'B', 'false') + ')';
        }
    },
    'logic/not': {
        category: 'logic', kind: 'data',
        expr: function (node, ctx) { return '(!' + ctx.resolveBoolean(node, 'A', 'false') + ')'; }
    },
    'logic/cancelEvent': {
        category: 'logic', kind: 'exec',
        imports: ['using LightInDark.Events;'],
        lines: function (node, ctx) {
            return ['(' + ctx.resolveEvent(node, '事件', 'e') + ').IsCanceled = true;'];
        }
    },

    // ---------- 颜色 ----------
    'val/color': {
        category: 'val', kind: 'data',
        expr: function (node) {
            return 'new LightInDark.Color(' + csNumber(nodeProp(node, 'R', 255)) + '/255f, ' +
                csNumber(nodeProp(node, 'G', 255)) + '/255f, ' +
                csNumber(nodeProp(node, 'B', 255)) + '/255f, ' +
                csNumber(nodeProp(node, 'A', 255)) + '/255f)';
        }
    },

    // ---------- 自定义 RPC ----------
    'rpc/customDefinition': {
        category: 'rpc', kind: 'rpcdef',
        imports: ['using LightInDark.RPCs;']
    },
    'rpc/call': {
        category: 'rpc', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return [ctx.className + 'Rpcs.' + sanitizeId(nodeProp(node, 'MethodName', 'SyncValue')) +
                '(' + String(nodeProp(node, 'Args', '')) + ');'];
        }
    },

    // ---------- 配置 ----------
    'cfg/get': {
        category: 'cfg', kind: 'data',
        imports: ['using LightInDark.Configuration;'],
        expr: function (node) {
            return 'ConfigRegistry.GetFloat($"role.{Code}.' + String(nodeProp(node, 'Key', '')) + '")';
        }
    },

    // ---------- 动作（扩展）----------
    'act/kill': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.MurderPlayer(' + ctx.resolvePlayer(node, '凶手', 'MyPlayer') +
                '.Control, ' + ctx.resolvePlayer(node, '受害者', 'target') + '.Control);'];
        }
    },
    'act/suicide': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.Suicide(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + '.Control);'];
        }
    },
    'act/revive': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.RevivePlayer(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + '.Control.PlayerId);'];
        }
    },
    'act/invisible': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.SetPlayerInvisible(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') +
                '.Control.PlayerId, ' + ctx.resolveBoolean(node, '开关', 'true') + ');'];
        }
    },
    'act/teleport': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;', 'using UnityEngine;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.TeleportPlayer(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') +
                '.Control.PlayerId, ' + ctx.resolveExpr(node, '坐标', 'Vector2.zero') + ');'];
        }
    },
    'act/forceEndMeeting': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function () { return ['RpcDefinitions.ForceEndMeeting();']; }
    },
    'act/breakEmergencyButton': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function () { return ['RpcDefinitions.BreakEmergencyButton();']; }
    },
    'act/showChat': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function () { return ['RpcDefinitions.ShowChat();']; }
    },
    'act/hideChat': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function () { return ['RpcDefinitions.HideChat();']; }
    },
    'act/sendChat': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.SendChatMessage(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') +
                '.Control.PlayerId, ' + ctx.resolveString(node, '文本', '""') + ');'];
        }
    },
    'act/kick': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.RPCs;'],
        lines: function (node, ctx) {
            return ['RpcDefinitions.KickPlayer(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + '.Control.PlayerId);'];
        }
    },
    'act/flash': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.Utilities;'],
        lines: function (node, ctx) {
            // 注意：LightUtils 位于 LightInDark.Utilities，其 Color 会就近解析为 LightInDark.Color
            var c = ctx.resolveExpr(node, '颜色', 'new LightInDark.Color(1f, 0f, 0f, 1f)');
            return ['LightUtils.PlayFlash(' + c + ', 0.1f, 0.2f, 0.4f);'];
        }
    },
    'act/log': {
        category: 'act', kind: 'exec',
        imports: ['using LightInDark.Core;'],
        lines: function (node, ctx) {
            return ['LightLogger.Log(' + ctx.resolveString(node, '文本', '""') + ');'];
        }
    },

    // ---------- 数值 / 文本（扩展，表达式）----------
    'val/playerName': {
        category: 'val', kind: 'data',
        expr: function (node, ctx) { return '(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + ').Name'; }
    },
    'val/isDead': {
        category: 'val', kind: 'data',
        expr: function (node, ctx) { return '(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + ').IsDead'; }
    },
    'val/isImpostor': {
        category: 'val', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function (node, ctx) { return '(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + ').IsImpostor()'; }
    },
    'val/randomAlive': {
        category: 'val', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function () { return 'RoleUtils.RandomAlivePlayer()'; }
    },
    'val/aliveCount': {
        category: 'val', kind: 'data',
        imports: ['using LightInDark.Roles;', 'using System.Linq;'],
        expr: function () { return 'RoleUtils.AlivePlayers().Count()'; }
    },
    'val/distance': {
        category: 'val', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function (node, ctx) {
            return 'RoleUtils.GetDistance(' + ctx.resolvePlayer(node, 'A', 'MyPlayer') + ', ' +
                ctx.resolvePlayer(node, 'B', 'MyPlayer') + ')';
        }
    },
    'val/inRange': {
        category: 'val', kind: 'data',
        imports: ['using LightInDark.Roles;'],
        expr: function (node, ctx) {
            return 'RoleUtils.IsInRange(' + ctx.resolvePlayer(node, 'A', 'MyPlayer') + ', ' +
                ctx.resolvePlayer(node, 'B', 'MyPlayer') + ', ' +
                csFloat(ctx.resolveNumber(node, '距离', '2')) + ')';
        }
    },
    'val/vector2': {
        category: 'val', kind: 'data',
        imports: ['using UnityEngine;'],
        expr: function (node) {
            return 'new Vector2(' + csNumber(nodeProp(node, 'X', 0)) + 'f, ' +
                csNumber(nodeProp(node, 'Y', 0)) + 'f)';
        }
    },

    // ---------- 游戏状态 / 玩家查询（对齐 GameState / Player 静态 API）----------
    'val/gameState': {
        category: 'val', kind: 'data',
        expr: function (node) {
            return 'LightInDark.Game.GameState.' + sanitizeId(nodeProp(node, 'State', 'IsInGame'));
        }
    },
    'val/isMap': {
        category: 'val', kind: 'data',
        expr: function (node) {
            return 'LightInDark.Game.GameState.IsMap(MapNames.' +
                sanitizeId(nodeProp(node, 'Map', 'Skeld')) + ')';
        }
    },
    'val/hostPlayer': {
        category: 'val', kind: 'data',
        expr: function () { return 'LightInDark.Game.Player.HostPlayer'; }
    },
    'val/isHost': {
        category: 'val', kind: 'data',
        expr: function (node, ctx) {
            return '(' + ctx.resolvePlayer(node, '玩家', 'MyPlayer') + ').IsHost';
        }
    },
    'val/playerById': {
        category: 'val', kind: 'data',
        expr: function (node, ctx) {
            return 'LightInDark.Game.Player.Get((byte)(' + ctx.resolveNumber(node, 'id', '0') + '))';
        }
    },
    'val/allPlayerCount': {
        category: 'val', kind: 'data',
        imports: ['using System.Linq;'],
        expr: function () { return 'LightInDark.Game.Player.AllPlayers.Count()'; }
    },

    // ---------- 逻辑（扩展）----------
    'logic/math': {
        category: 'logic', kind: 'data',
        expr: function (node, ctx) {
            return '(' + ctx.resolveNumber(node, 'A', '0') + ' ' + nodeProp(node, 'Op', '+') +
                ' ' + ctx.resolveNumber(node, 'B', '0') + ')';
        }
    }
};
