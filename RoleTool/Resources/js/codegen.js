// C# 代码生成器：读取表单元数据 + 节点蓝图 → 输出职业源码 / 翻译 / 文档
// 数据表见 emit-map.js（EMIT），统一元素先行。

// ---------- 基础工具 ----------
function indentStr(n) { return '    '.repeat(Math.max(0, n)); }
function addImport(set, line) { if (line) set[line] = true; }
function indentLines(lines, n) {
    return (lines || []).map(function (l) { return l ? indentStr(n) + l : ''; });
}
function isoDate() { return new Date().toISOString().slice(0, 10); }

// 颜色分量 0-255 → 归一化 C# float 字面量
function csColorComponent(v) {
    var n = Math.max(0, Math.min(255, Number(v) || 0)) / 255;
    if (n === 1) return '1f';
    if (n === 0) return '0f';
    return String(Number(n.toFixed(3))) + 'f';
}

// 取节点类型字符串（EMIT 键）
function nodeKey(node) {
    if (!node) return '';
    return node.type || node.typeStr || '';
}

// 沿执行输出找下一个执行节点
function findNextExec(node, slot) {
    if (!node) return null;
    if (typeof node.getOutputNode === 'function') {
        var n = node.getOutputNode(slot);
        if (n) return n;
    }
    if (typeof node.getOutputNodes === 'function') {
        var arr = node.getOutputNodes(slot);
        if (arr && arr.length) return arr[0];
    }
    return null;
}

// 取连到指定命名输入上的来源节点
function getLinkedSourceNode(node, inputName) {
    var info = getLinkedSourceInfo(node, inputName);
    return info ? info.node : null;
}

// 取来源节点 + 其连到此输入端口上的输出名
function getLinkedSourceInfo(node, inputName) {
    if (!node || !node.inputs || typeof node.getInputNode !== 'function') return null;
    for (var i = 0; i < node.inputs.length; i++) {
        var inp = node.inputs[i];
        if (!inp || inp.name !== inputName) continue;
        var src = node.getInputNode(i);
        if (!src) return null;

        var outName = null;
        try {
            var linkId = inp.link;
            if (linkId != null && node.graph && node.graph.links && node.graph.links[linkId]) {
                var slot = node.graph.links[linkId].origin_slot;
                if (src.outputs && src.outputs[slot]) outName = src.outputs[slot].name;
            }
        } catch (e) { }
        return { node: src, outName: outName };
    }
    return null;
}

// 来源节点若是数据节点，求其表达式
function linkedExpr(node, inputName) {
    var info = getLinkedSourceInfo(node, inputName);
    if (!info) return null;
    return sourceExpr(info.node, info.outName);
}

// ---------- 输入解析（无连线时回退）----------
function resolveString(node, inputName, fallback) {
    var e = linkedExpr(node, inputName);
    if (e != null) return e;
    return (fallback != null) ? fallback : '""';
}

function resolveNumber(node, inputName, fallback) {
    var src = getLinkedSourceNode(node, inputName);
    if (src && nodeKey(src) === 'val/number') return csNumber(nodeProp(src, 'number', 0));
    var e = linkedExpr(node, inputName);
    if (e != null) return e;
    return (fallback != null) ? fallback : '0';
}

function resolveBoolean(node, inputName, fallback) {
    var src = getLinkedSourceNode(node, inputName);
    if (src && nodeKey(src) === 'val/bool') return nodeProp(src, 'true', true) ? 'true' : 'false';
    var e = linkedExpr(node, inputName);
    if (e != null) return e;
    return (fallback != null) ? fallback : 'false';
}

function resolvePlayer(node, inputName, fallback) {
    var src = getLinkedSourceNode(node, inputName);
    if (src && nodeKey(src) === 'val/player') return String(nodeProp(src, 'MyPlayer', 'MyPlayer'));
    var e = linkedExpr(node, inputName);
    if (e != null) return e;
    return (fallback != null) ? fallback : 'MyPlayer';
}

// 事件的表达式（事件输入端口）
function resolveEvent(node, inputName, fallback) {
    var e = linkedExpr(node, inputName);
    if (e != null) return e;
    return (fallback != null) ? fallback : 'e';
}

// 通用表达式解析（颜色 / 坐标等无专用解析器的端口）
function resolveExpr(node, inputName, fallback) {
    var e = linkedExpr(node, inputName);
    if (e != null) return e;
    return (fallback != null) ? fallback : 'null';
}

// 传给 EMIT 函数的上下文
var ctx = {
    className: 'Role',
    resolveString: resolveString,
    resolveNumber: resolveNumber,
    resolveBoolean: resolveBoolean,
    resolvePlayer: resolvePlayer,
    resolveEvent: resolveEvent,
    resolveExpr: resolveExpr
};

// 求来源节点在指定输出端口上的表达式（dataOut 优先，其次 data 节点的 expr）
function sourceExpr(src, outName) {
    var def = EMIT[nodeKey(src)];
    if (!def) return null;
    if (typeof def.dataOut === 'function') {
        var v = def.dataOut(outName, src, ctx);
        if (v != null) return v;
    }
    if (def.kind === 'data' && typeof def.expr === 'function') return def.expr(src, ctx, outName);
    return null;
}

// ---------- 执行链遍历 ----------
function traverseExec(startNode, indent, imports) {
    var out = [];
    var node = startNode;
    var guard = 0;

    while (node && guard++ < 500) {
        var key = nodeKey(node);
        var def = EMIT[key];
        if (!def || def.kind === 'data') break;
        if (def.imports) def.imports.forEach(function (l) { addImport(imports, l); });

        // 技能按钮：双输出（0=点击 事件体 / 1=下一 继续链）
        if (key === 'btn/ability') {
            out = out.concat(emitAbilityButton(node, indent, imports));
            node = findNextExec(node, 1);
            continue;
        }

        // 击杀按钮：CreateKill(this, target => { <点击> })，随后走 下一
        if (key === 'btn/kill') {
            out = out.concat(emitKillButton(node, indent, imports));
            node = findNextExec(node, 1);
            continue;
        }

        // 条件分支：两个子执行槽各自递归
        if (key === 'logic/if') {
            out.push(indentStr(indent) + 'if (' + resolveBoolean(node, '条件', 'false') + ')');
            out.push(indentStr(indent) + '{');
            out = out.concat(traverseExec(findNextExec(node, 0), indent + 1, imports));
            out.push(indentStr(indent) + '}');
            var elseNode = findNextExec(node, 1);
            if (elseNode) {
                out.push(indentStr(indent) + 'else');
                out.push(indentStr(indent) + '{');
                out = out.concat(traverseExec(elseNode, indent + 1, imports));
                out.push(indentStr(indent) + '}');
            }
            break;
        }

        // 普通执行节点
        if (typeof def.lines === 'function') {
            def.lines(node, ctx).forEach(function (l) { out.push(indentStr(indent) + l); });
        }
        node = findNextExec(node, 0);
    }
    return out;
}

// 技能按钮 → AbilityButtonFactory.Create(...) 语句块
function emitAbilityButton(node, indent, imports) {
    addImport(imports, 'using LightInDark.UI.Ability;');

    var labelKey = nodeProp(node, 'Button.<Role>.label', '') ||
        ('Button.' + (ctx.className || 'Role') + '.label');
    var hotkey = nodeProp(node, 'Hotkey', 'E') || 'E';
    var cooldown = Number(nodeProp(node, 'Cooldown', 20));
    if (!isFinite(cooldown)) cooldown = 20;
    var leftSide = !!nodeProp(node, 'LeftSide', false);

    var setters = [];
    setters.push('.SetLabelKey(' + csString(labelKey) + ')');
    setters.push('.SetHotkey(KeyCode.' + hotkey + ')');
    setters.push('.SetCooldown(' + csNumber(cooldown) + 'f)');
    if (leftSide) setters.push('.SetLeftSide(true)');

    var lines = [];
    lines.push(indentStr(indent) + 'AbilityButtonFactory.Create(this, new RoleButtonConfig()');
    for (var i = 0; i < setters.length; i++) {
        lines.push(indentStr(indent + 1) + setters[i] + (i === setters.length - 1 ? ',' : ''));
    }
    lines.push(indentStr(indent + 1) + '() =>');
    lines.push(indentStr(indent + 1) + '{');
    lines = lines.concat(traverseExec(findNextExec(node, 0), indent + 2, imports));
    lines.push(indentStr(indent + 1) + '});');
    return lines;
}

// 击杀按钮 → AbilityButtonFactory.CreateKill(this, target => { ... })
function emitKillButton(node, indent, imports) {
    addImport(imports, 'using LightInDark.UI.Ability;');

    var lines = [];
    lines.push(indentStr(indent) + 'AbilityButtonFactory.CreateKill(this, target =>');
    lines.push(indentStr(indent) + '{');
    lines = lines.concat(traverseExec(findNextExec(node, 0), indent + 1, imports));
    lines.push(indentStr(indent) + '});');
    return lines;
}

// ---------- 元数据读写 ----------
function defaultMeta() {
    return {
        className: 'Caller', codeName: 'Caller',
        roleName: '', shortDes: '', intro: '', desc: '',
        roleNameEn: '', shortDesEn: '', introEn: '', descEn: '',
        category: 'Crewmate', neutralType: 'Benign',
        colorR: 255, colorG: 235, colorB: 4, colorA: 255,
        maxCount: 1, guaranteedCount: 0, chance: 100,
        canKill: false, killCooldown: 20, canUseVents: false,
        canReport: true, canCallMeeting: true, taskCount: 1,
        teamCode: '', killDeathCauseId: '', roleImagePath: '', introSFX: '',
        documentType: 'Normal'
    };
}

function readMeta() {
    var m = (typeof readMetaForm === 'function') ? readMetaForm() : defaultMeta();
    var d = defaultMeta();
    if (m) for (var k in d) { if (m[k] !== undefined && m[k] !== null) d[k] = m[k]; }
    return d;
}

function readEntries() {
    var e = (typeof getConfigEntries === 'function') ? getConfigEntries() : [];
    return (e && e.length) ? e : [];
}

function findNodes(graph, type) {
    if (!graph) return [];
    if (typeof graph.findNodesByType === 'function') {
        var r = graph.findNodesByType(type);
        if (r && r.length) return r;
    }
    var arr = graph._nodes || graph.nodes || [];
    return arr.filter(function (n) { return nodeKey(n) === type; });
}

// 可在 RuntimeInstance 中作为顶层方法输出的生命周期节点
var LIFE_TYPES = [
    'life/onActivated', 'life/onInactivated', 'life/checkWin',
    'life/onDied', 'life/onRevived', 'life/onExiled',
    'life/onMeetingStarted', 'life/onMeetingEnded', 'life/onRoleData'
];

function collectLifeNodes(graph) {
    var found = {};
    LIFE_TYPES.forEach(function (t) {
        var r = findNodes(graph, t);
        if (r && r.length) found[t] = r[0];
    });
    return found;
}

// 收集声明类节点：数据位字段 / 追踪器字段 / 事件监听方法 / 自定义 RPC 方法
function collectDeclarations(graph, imports) {
    var fields = [];
    var evMethods = [];
    var rpcMethods = [];

    // data/registerId → RuntimeInstance 静态字段
    var seenIds = {};
    findNodes(graph, 'data/registerId').forEach(function (n) {
        var key = nodeProp(n, 'Key', 'Counter');
        var name = '_' + sanitizeId(key) + 'Id';
        if (seenIds[name]) return;
        seenIds[name] = true;
        addImport(imports, 'using LightInDark.Roles;');
        fields.push(indentStr(2) + 'private static readonly int ' + name +
            ' = RoleData.RegisterId(' + csString(key) + ');');
    });

    // target/createTracker → 追踪器字段（只声明一次）
    if (findNodes(graph, 'target/createTracker').length) {
        addImport(imports, 'using LightInDark.Roles;');
        fields.push(indentStr(2) + 'private PlayerTracker _tracker;');
    }

    // ev/* → 私有监听方法
    var evIndex = 0;
    var nodes = (graph && (graph._nodes || graph.nodes)) || [];
    nodes.forEach(function (n) {
        var key = nodeKey(n);
        var def = EMIT[key];
        if (!def || def.kind !== 'ev') return;
        evIndex++;
        if (def.imports) def.imports.forEach(function (l) { addImport(imports, l); });

        var priority = Number(nodeProp(n, 'Priority', 0));
        if (!isFinite(priority)) priority = 0;
        // 用 Attribute 后缀全名：OnlyHost/OnlyMyPlayer/Local 有同名别名类，[OnlyHost] 会 CS1614 歧义
        evMethods.push(indentStr(2) + '[EventPriority(' + priority + ')]');
        if (nodeProp(n, 'OnlyHost', false)) evMethods.push(indentStr(2) + '[OnlyHostAttribute]');
        if (nodeProp(n, 'OnlyMyPlayer', false)) evMethods.push(indentStr(2) + '[OnlyMyPlayerAttribute]');
        if (nodeProp(n, 'Local', false)) evMethods.push(indentStr(2) + '[LocalAttribute]');
        evMethods.push(indentStr(2) + 'private void On_' + def.name + '_' + evIndex +
            '(' + def.event + ' e)');
        evMethods.push(indentStr(2) + '{');
        evMethods = evMethods.concat(traverseExec(findNextExec(n, 0), 3, imports));
        evMethods.push(indentStr(2) + '}');
    });

    // rpc/customDefinition → 静态 RPC 方法
    findNodes(graph, 'rpc/customDefinition').forEach(function (n) {
        addImport(imports, 'using LightInDark.RPCs;');
        var name = sanitizeId(nodeProp(n, 'MethodName', 'SyncValue'));
        var params = String(nodeProp(n, 'Params', ''));
        var reliable = nodeProp(n, 'Reliable', true);
        rpcMethods.push(indentStr(2) + (reliable ? '[LidRPC]' : '[LidRPC(Reliable = false)]'));
        rpcMethods.push(indentStr(2) + 'public static void ' + name + '(' + params + ')');
        rpcMethods.push(indentStr(2) + '{');
        rpcMethods = rpcMethods.concat(traverseExec(findNextExec(n, 0), 3, imports));
        rpcMethods.push(indentStr(2) + '}');
    });

    return { fields: fields, evMethods: evMethods, rpcMethods: rpcMethods };
}

// ---------- 类成员 ----------
function buildMetaMembers(meta, imports) {
    var cls = meta.className;
    var lines = [];

    lines.push(indentStr(1) + 'public const string Code = ' + csString(meta.codeName || cls) + ';');
    lines.push('');
    lines.push(indentStr(1) + 'public static readonly ' + cls + ' MyRole = new();');
    lines.push('');
    lines.push(indentStr(1) + 'public override string CodeName => Code;');
    lines.push(indentStr(1) + 'public override LightInDark.Color Color => new(' +
        csColorComponent(meta.colorR) + ', ' + csColorComponent(meta.colorG) + ', ' +
        csColorComponent(meta.colorB) + ', ' + csColorComponent(meta.colorA) + ');');
    lines.push(indentStr(1) + 'public override RoleCategory RoleCategory => RoleCategory.' + meta.category + ';');

    if (meta.category === 'Neutral') {
        lines.push(indentStr(1) + 'public override NeutralType NeutralType => NeutralType.' + meta.neutralType + ';');
        if (meta.teamCode) lines.push(indentStr(1) + 'public override string TeamCode => ' + csString(meta.teamCode) + ';');
    }

    if (meta.canKill) {
        lines.push(indentStr(1) + 'public override bool CanKill => true;');
        lines.push(indentStr(1) + 'public override float KillCooldown => ' + csNumber(meta.killCooldown) + 'f;');
    }
    if (meta.canUseVents) lines.push(indentStr(1) + 'public override bool CanUseVents => true;');
    if (!meta.canReport) lines.push(indentStr(1) + 'public override bool CanReport => false;');
    if (!meta.canCallMeeting) lines.push(indentStr(1) + 'public override bool CanCallEmergencyMeeting => false;');
    if (Number(meta.taskCount) !== 1) lines.push(indentStr(1) + 'public override int TaskCount => ' + csNumber(meta.taskCount) + ';');

    var max = Number(meta.maxCount) || 0;
    if (max > 0) {
        var alloc = ['MaxCount = ' + csNumber(max)];
        if (Number(meta.guaranteedCount) > 0) alloc.push('GuaranteedCount = ' + csNumber(meta.guaranteedCount));
        if (Number(meta.chance) > 0) alloc.push('Chance = ' + csNumber(meta.chance));
        lines.push(indentStr(1) + 'public override AllocationParameters Allocation => new() { ' + alloc.join(', ') + ' };');
    }

    if (meta.introSFX) lines.push(indentStr(1) + 'public override string IntroSFX => ' + csString(meta.introSFX) + ';');
    if (meta.roleImagePath) lines.push(indentStr(1) + 'public override string? RoleImagePath => ' + csString(meta.roleImagePath) + ';');

    if (meta.documentType === 'MarkDown' || meta.documentType === 'Html') {
        addImport(imports, 'using LightInDark.Documents;');
        var ext = (meta.documentType === 'Html') ? 'html' : 'md';
        lines.push(indentStr(1) + 'public override RoleDocumentType DocumentType => RoleDocumentType.' + meta.documentType + ';');
        lines.push(indentStr(1) + 'public override string Describe => ' + csString('./Resources/Documents/' + cls + '.' + ext) + ';');
    }

    if (meta.killDeathCauseId) {
        var id = meta.killDeathCauseId;
        lines.push(indentStr(1) + 'public const string KillCauseId = ' + csString(id) + ';');
        lines.push(indentStr(1) + 'static ' + cls + '() { LightInDark.Game.DeathCause.Register(KillCauseId, ' +
            csString(id) + ', ' + csString((meta.roleName || cls) + '击杀') + '); }');
        lines.push(indentStr(1) + 'public override string KillDeathCauseId => KillCauseId;');
    }

    return lines;
}

function configItemLine(e) {
    var t = e.type || 'Int';
    var parts = ['Key = ' + csString(e.key || '')];

    if (t === 'Bool') {
        parts.push('Type = ConfigType.Bool');
        parts.push('Default = ' + ((e.default === true || e.default === 'true') ? 'true' : 'false'));
    } else if (t === 'String') {
        // 无 ConfigType.String → 映射为枚举/候选值类型 Value
        parts.push('Type = ConfigType.Value');
        parts.push('Default = new[] { ' + csString(e.default || '') + ' }');
    } else if (t === 'Float') {
        parts.push('Type = ConfigType.Float');
        parts.push('Default = ' + csNumber(e.default) + 'f');
        parts.push('Min = ' + csNumber(e.min) + 'f');
        parts.push('Max = ' + csNumber(e.max != null ? e.max : 100) + 'f');
        if (e.step) parts.push('Step = ' + csNumber(e.step) + 'f');
        if (e.suffix && e.suffix !== 'None') parts.push('Suffix = ConfigSuffix.' + e.suffix);
    } else {
        parts.push('Type = ConfigType.Int');
        parts.push('Default = ' + csNumber(e.default));
        parts.push('Min = ' + csNumber(e.min));
        parts.push('Max = ' + csNumber(e.max != null ? e.max : 100));
        if (e.step) parts.push('Step = ' + csNumber(e.step));
        if (e.suffix && e.suffix !== 'None') parts.push('Suffix = ConfigSuffix.' + e.suffix);
    }
    return 'new RoleConfigItem { ' + parts.join(', ') + ' },';
}

function buildConfigEntries(entries) {
    var lines = [];
    lines.push(indentStr(1) + 'public override RoleConfigItem[] RoleConfiguration => new[]');
    lines.push(indentStr(1) + '{');
    entries.forEach(function (e) { lines.push(indentStr(2) + configItemLine(e)); });
    lines.push(indentStr(1) + '};');
    return lines;
}

function buildRuntimeMembers(meta, lifeNodes, decls, imports) {
    var cls = meta.className;
    var m = [];

    // 字段（注册数据位 / 追踪器）—— 在方法之前
    if (decls.fields.length) {
        m = m.concat(decls.fields);
        m.push('');
    }

    m.push(indentStr(2) + 'public RuntimeInstance(PlayerControl owner, RoleTemplate template) : base(owner, template) { }');

    LIFE_TYPES.forEach(function (t) {
        var host = lifeNodes[t];
        if (!host) return;
        var def = EMIT[t];
        if (def.imports) def.imports.forEach(function (l) { addImport(imports, l); });

        m.push('');
        m.push(indentStr(2) + def.method);
        m.push(indentStr(2) + '{');

        if (t === 'life/onActivated') {
            m.push(indentStr(3) + 'try');
            m.push(indentStr(3) + '{');
            m.push(indentStr(4) + 'if (!AmOwner) return;');
            m.push(indentStr(4) + 'NormalizeVanillaRole();');
            m = m.concat(traverseExec(findNextExec(host, 0), 4, imports));
            m.push(indentStr(3) + '}');
            m.push(indentStr(3) + 'catch (Exception ex)');
            m.push(indentStr(3) + '{');
            m.push(indentStr(4) + 'LightLogger.LogError(' + csString('[' + cls + '.OnActivated]') + ', ex);');
            m.push(indentStr(3) + '}');
        } else if (typeof def.body === 'function') {
            m = m.concat(indentLines(def.body(host, ctx), 3));
        } else {
            m = m.concat(traverseExec(findNextExec(host, 0), 3, imports));
        }

        m.push(indentStr(2) + '}');
    });

    // 事件监听方法
    if (decls.evMethods.length) {
        m.push('');
        m = m.concat(decls.evMethods);
    }

    return m;
}

// ---------- 生成主流程 ----------
var BASE_IMPORTS = [
    'using LightInDark.Configuration;',
    'using LightInDark.Core;',
    'using LightInDark.Roles;',
    'using LightInDark.RPCs;',
    'using LightInDark.UI.Ability;',
    'using System;',
    'using UnityEngine;'
];

function importCmp(a, b) {
    var x = a.toLowerCase(), y = b.toLowerCase();
    return x < y ? -1 : (x > y ? 1 : 0);
}

function generateCSharp() {
    var meta = readMeta();
    var entries = readEntries();

    var imports = {};
    BASE_IMPORTS.forEach(function (l) { addImport(imports, l); });
    ctx.className = meta.className;

    var graph = (typeof litegraphGraph !== 'undefined' && litegraphGraph) ? litegraphGraph : null;
    var lifeNodes = collectLifeNodes(graph);
    var decls = collectDeclarations(graph, imports);
    var metaMembers = buildMetaMembers(meta, imports);
    var runtimeMembers = buildRuntimeMembers(meta, lifeNodes, decls, imports);

    var cls = meta.className;
    var ns = (CATEGORY_MAP[meta.category] || CATEGORY_MAP.Crewmate).ns;
    var summary = meta.shortDes ? ((meta.roleName || cls) + '：' + meta.shortDes) : cls;

    var out = [];
    out.push('// 由 RoleTool 生成 · 蓝图 ' + cls + '.rtp · ' + isoDate());
    out.push('');
    Object.keys(imports).sort(importCmp).forEach(function (l) { out.push(l); });
    out.push('');
    out.push('namespace ' + ns + ';');
    out.push('');
    out.push('/// <summary>' + summary + '</summary>');
    out.push('public class ' + cls + ' : RoleTemplate');
    out.push('{');
    out = out.concat(metaMembers);

    if (entries.length) {
        out.push('');
        out = out.concat(buildConfigEntries(entries));
    }

    out.push('');
    out.push(indentStr(1) + 'public override RuntimeRoleTemplate CreateRuntime(PlayerControl owner) => new RuntimeInstance(owner, this);');
    out.push('');
    out.push(indentStr(1) + 'public class RuntimeInstance : RuntimeRoleTemplate');
    out.push(indentStr(1) + '{');
    out = out.concat(runtimeMembers);
    out.push(indentStr(1) + '}');

    // 自定义 RPC 静态类（RuntimeInstance 之后、职业类内部）
    if (decls.rpcMethods.length) {
        out.push('');
        out.push(indentStr(1) + 'public static class ' + cls + 'Rpcs');
        out.push(indentStr(1) + '{');
        out = out.concat(decls.rpcMethods);
        out.push(indentStr(1) + '}');
    }

    out.push('}');

    return out.join('\n');
}

// ---------- 翻译 JSON ----------
function generateLangJson() {
    var meta = readMeta();
    var lower = String(meta.codeName || meta.className || 'role').toLowerCase();
    var cls = meta.className;

    function pack(zh) {
        var o = {};
        o['role.' + lower + '.name'] = zh ? (meta.roleName || '') : (meta.roleNameEn || '');
        o['role.' + lower + '.shortdes'] = zh ? (meta.shortDes || '') : (meta.shortDesEn || '');
        o['role.' + lower + '.des'] = zh ? (meta.desc || '') : (meta.descEn || '');
        o['role.' + lower + '.intro'] = zh ? (meta.intro || '') : (meta.introEn || '');
        o['Button.' + cls + '.label'] = zh ? (meta.roleName || '技能') : (meta.roleNameEn || 'Ability');
        return o;
    }

    return JSON.stringify({ zh: pack(true), en: pack(false) }, null, 2);
}

// ---------- 说明文档 ----------
function generateDocMarkdown() {
    var meta = readMeta();
    if (meta.documentType !== 'MarkDown' && meta.documentType !== 'Html') return '';

    var cls = meta.className;
    var lines = [];
    lines.push('# ' + (meta.roleName || cls));
    lines.push('');
    lines.push('- 内部名：`' + (meta.codeName || cls) + '`');
    lines.push('- 阵营：' + meta.category);
    lines.push('- 一句话：' + (meta.shortDes || ''));
    lines.push('');
    lines.push('## 简介');
    lines.push('');
    lines.push(meta.intro || '');
    lines.push('');
    lines.push('## 描述');
    lines.push('');
    lines.push(meta.desc || '');
    return lines.join('\n');
}

// ---------- 统一重新生成入口 ----------
var _regenBusy = false;

function regenerateAll() {
    if (_regenBusy) return;
    _regenBusy = true;
    try {
        var cs = generateCSharp();
        var lang = generateLangJson();
        var doc = generateDocMarkdown();

        if (typeof refreshPreview === 'function') refreshPreview({ cs: cs, lang: lang, doc: doc });
        if (typeof runValidation === 'function') runValidation({ cs: cs, lang: lang, doc: doc });
        if (typeof updateRoleCard === 'function' && typeof readMetaForm === 'function') updateRoleCard(readMetaForm());
    } catch (e) {
        if (typeof showStatus === 'function') showStatus('生成失败：' + (e && e.message ? e.message : e));
        if (typeof console !== 'undefined') console.error(e);
    } finally {
        _regenBusy = false;
    }
}
