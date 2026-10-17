// 静态校验：元数据、分配、配色与蓝图连接

var existingRoles = null; // 主插件已有职业（Task 5 提供 scanExistingRoles）
var _VALID_NAME_RE = /^[A-Za-z][A-Za-z0-9_]*$/;

// 各阵营默认调色板色（对应 RoleTemplate.ResolveBlockHighlight）
var CATEGORY_PALETTE = {
    Crewmate: [140, 255, 255],
    Impostor: [255, 25, 25],
    Neutral:  [140, 255, 255],
    Ghost:    [140, 255, 255]
};

// ---------- 数据读取工具 ----------
function _vRegTypes() {
    return (typeof LiteGraph !== 'undefined' && LiteGraph.registered_node_types) ? LiteGraph.registered_node_types : {};
}

function _vNodes() {
    var g = (typeof litegraphGraph !== 'undefined') ? litegraphGraph : null;
    if (!g) return [];
    return g._nodes || g.nodes || [];
}

function _vLinks() {
    var g = (typeof litegraphGraph !== 'undefined') ? litegraphGraph : null;
    if (!g || !g.links) return [];
    var l = g.links;
    if (Array.isArray(l)) return l.filter(Boolean);
    return Object.keys(l).map(function (k) { return l[k]; });
}

function _vType(n) { return (n && (n.type || n.typeStr)) || ''; }

function _vOrigin(list) {
    var r = {};
    (list || []).forEach(function (i) { r[i.id] = i; });
    return r;
}

// 归一化已有职业列表为 CodeName 字符串数组
function _vRoleCodeNames(arr) {
    var out = [];
    var list = Array.isArray(arr) ? arr : (arr && Array.isArray(arr.roles) ? arr.roles : []);
    (list || []).forEach(function (r) {
        if (r == null) return;
        if (typeof r === 'string') { out.push(r); return; }
        var v = r.codeName || r.CodeName || r.code || r.Code || r.name || r.Name;
        if (v) out.push(String(v));
    });
    return out;
}

// 沿执行输出求可达集合
function _vReachable(ids, adj) {
    var reach = {}, queue = (ids || []).slice();
    queue.forEach(function (id) { reach[id] = true; });
    while (queue.length) {
        var cur = queue.shift();
        (adj[cur] || []).forEach(function (nx) {
            if (!reach[nx]) { reach[nx] = true; queue.push(nx); }
        });
    }
    return reach;
}

// 反向查找上游最近的生命周期根节点
function _vAncestorLife(nodeId, idMap, links) {
    var rev = {};
    links.forEach(function (l) {
        if (l) (rev[l.target_id] = rev[l.target_id] || []).push(l.origin_id);
    });
    var seen = {}, queue = (rev[nodeId] || []).slice(), hit = null;
    queue.forEach(function (id) { seen[id] = true; });
    while (queue.length) {
        var cur = queue.shift();
        var n = idMap[cur];
        if (n && _vType(n).indexOf('life/') === 0) { hit = n; break; }
        (rev[cur] || []).forEach(function (id) { if (!seen[id]) { seen[id] = true; queue.push(id); } });
    }
    return hit;
}

// ---------- 校验主体 ----------
function _vCollect() {
    var errors = [], warnings = [];
    var meta = (typeof readMetaForm === 'function') ? readMetaForm() : {};
    if (typeof getConfigEntries === 'function') getConfigEntries();

    // 1. 名称合法性
    var code = String(meta.codeName || '').trim();
    var cls = String(meta.className || '').trim();
    if (!code) errors.push('内部名(CodeName)不能为空');
    else if (!_VALID_NAME_RE.test(code)) errors.push('内部名(CodeName)含非法字符：' + code);
    if (cls && !_VALID_NAME_RE.test(cls)) errors.push('类名含非法字符：' + cls);
    if (code && cls && cls !== code) {
        var pascal = code.charAt(0).toUpperCase() + code.slice(1);
        if (cls !== pascal) warnings.push('类名与内部名不一致：' + cls + ' / ' + code);
    }

    // 2. 与主插件已有职业重名
    if (existingRoles) {
        var used = _vRoleCodeNames(existingRoles);
        if (code && used.indexOf(code) !== -1) errors.push('内部名与已有职业重复：' + code);
    }

    // 3. 分配范围
    var max = Number(meta.maxCount);
    var guar = Number(meta.guaranteedCount);
    var chance = Number(meta.chance);
    if (!(guar >= 0 && max >= 0 && guar <= max && max <= 15)) {
        errors.push('分配数量不合法：必出 ' + guar + ' / 最大 ' + max + '（需满足 0 ≤ 必出 ≤ 最大 ≤ 15）');
    }
    if (!(chance >= 0 && chance <= 100)) errors.push('出现概率超出范围（0-100）：' + chance);
    if (max > 0 && meta.category === 'Ghost') warnings.push('幽灵阵营设置了大于 0 的最大数量：' + max);

    // 4. 颜色分量与默认色
    ['colorR', 'colorG', 'colorB', 'colorA'].forEach(function (k) {
        var v = Number(meta[k]);
        if (!(v >= 0 && v <= 255)) errors.push('颜色分量超出范围（0-255）：' + k + ' = ' + meta[k]);
    });
    var pal = CATEGORY_PALETTE[meta.category];
    if (pal && Number(meta.colorR) === pal[0] && Number(meta.colorG) === pal[1] && Number(meta.colorB) === pal[2]) {
        warnings.push('颜色与阵营默认色相同，建议自定义：' + pal.join(','));
    }

    // ---------- 蓝图部分 ----------
    var nodes = _vNodes();
    var links = _vLinks();
    var reg = _vRegTypes();
    var idMap = _vOrigin(nodes);

    var connOut = {}, connIn = {};
    links.forEach(function (l) {
        if (!l) return;
        connOut[l.origin_id + ':' + l.origin_slot] = true;
        connIn[l.target_id + ':' + l.target_slot] = true;
    });

    // 5. 生命周期唯一 + 空生命周期
    var lifeCount = {};
    nodes.forEach(function (n) {
        var t = _vType(n);
        if (t.indexOf('life/') === 0) lifeCount[t] = (lifeCount[t] || 0) + 1;
    });
    Object.keys(lifeCount).forEach(function (t) {
        if (lifeCount[t] > 1) errors.push('生命周期节点重复：' + t + '（' + lifeCount[t] + ' 个）');
    });
    nodes.forEach(function (n) {
        var t = _vType(n);
        if (t.indexOf('life/') !== 0) return;
        var hasExecOut = false, hasDataUse = false;
        (n.outputs || []).forEach(function (o, i) { if (connOut[n.id + ':' + i]) hasExecOut = true; });
        (n.inputs || []).forEach(function (inp, i) { if (connIn[n.id + ':' + i]) hasDataUse = true; });
        if (!hasExecOut && !hasDataUse) warnings.push('空生命周期：' + t + ' 未连接任何节点');
    });

    // 6. 悬空执行输出 + 孤立节点
    var adj = {};
    links.forEach(function (l) {
        if (!l) return;
        (adj[l.origin_id] = adj[l.origin_id] || []).push(l.target_id);
    });
    links.forEach(function (l) {
        if (!l) return;
        var src = idMap[l.origin_id];
        var out = src && src.outputs && src.outputs[l.origin_slot];
        if (!out || out.type !== 'exec') return;
        var dst = idMap[l.target_id];
        if (!dst) { warnings.push('悬空执行输出：指向已不存在的节点'); return; }
        if (!dst.inputs || !dst.inputs[l.target_slot]) {
            warnings.push('悬空执行输出：目标节点缺少对应输入槽（' + _vType(dst) + '）');
        }
    });

    var undirected = {};
    links.forEach(function (l) {
        if (!l) return;
        (undirected[l.origin_id] = undirected[l.origin_id] || []).push(l.target_id);
        (undirected[l.target_id] = undirected[l.target_id] || []).push(l.origin_id);
    });
    // 入口根：生命周期 + 事件监听（二者都是合法的起点）
    var roots = nodes.filter(function (n) {
        var t = _vType(n);
        return t.indexOf('life/') === 0 || t.indexOf('ev/') === 0;
    });
    var reach = _vReachable(roots.map(function (n) { return n.id; }), undirected);
    nodes.forEach(function (n) {
        var t = _vType(n);
        if (t.indexOf('life/') === 0 || t.indexOf('ev/') === 0) return;
        if (!reach[n.id]) warnings.push('孤立节点：' + t + ' 未连接到任何生命周期');
    });

    // 7. 端口类型不匹配（连线时已拦截，此处兜底扫描）
    links.forEach(function (l) {
        if (!l) return;
        var src = idMap[l.origin_id];
        var dst = idMap[l.target_id];
        if (!src || !dst) return;
        var out = src.outputs && src.outputs[l.origin_slot];
        var inp = dst.inputs && dst.inputs[l.target_slot];
        if (out && inp && out.type && inp.type && out.type !== inp.type) {
            errors.push('端口类型不匹配：' + _vType(src) + ' → ' + _vType(dst) + '（' + out.type + ' / ' + inp.type + '）');
        }
    });

    // 8. 事件根约束
    if (reg['logic/cancelEvent']) {
        nodes.forEach(function (n) {
            if (_vType(n) !== 'logic/cancelEvent') return;
            var root = _vAncestorLife(n.id, idMap, links);
            if (root) errors.push('可取消事件节点不能挂在不可取消的生命周期根下：' + _vType(root));
        });
    }
    var checkWin = nodes.filter(function (n) { return _vType(n) === 'life/checkWin'; });
    if (checkWin.length) {
        var btnReach = _vReachable(checkWin.map(function (n) { return n.id; }), adj);
        nodes.forEach(function (n) {
            if (_vType(n).indexOf('btn/') === 0 && btnReach[n.id]) errors.push('技能按钮不能挂在胜负判定(life/checkWin)下');
        });
    }

    // 9. data/* 引用未声明的 id
    var hasData = Object.keys(reg).some(function (t) { return t.indexOf('data/') === 0; });
    if (hasData) {
        var declared = {};
        nodes.forEach(function (n) {
            if (_vType(n) !== 'data/registerId') return;
            var d = n.properties && (n.properties.id != null ? n.properties.id : n.properties.Id);
            if (d != null && d !== '') declared[d] = true;
        });
        nodes.forEach(function (n) {
            var t = _vType(n);
            if (t.indexOf('data/') !== 0 || t === 'data/registerId') return;
            var r = n.properties && (n.properties.id != null ? n.properties.id : n.properties.Id);
            if (r != null && r !== '' && !declared[r]) errors.push('数据节点引用了未声明的 id：' + r + '（' + t + '）');
        });
    }

    // 11. 击杀按钮「目标」端口只能在「点击」分支内使用
    //     （生成代码里 target 是击杀回调的形参，用在回调外会引用到作用域外的变量）
    nodes.forEach(function (n) {
        if (_vType(n) !== 'btn/kill') return;
        var outs = n.outputs || [];
        var clickSlot = -1, targetSlot = -1;
        outs.forEach(function (o, i) {
            if (o && o.name === '点击') clickSlot = i;
            if (o && o.name === '目标') targetSlot = i;
        });
        if (clickSlot < 0 || targetSlot < 0) return;

        var clickStarts = [];
        links.forEach(function (l) {
            if (l && l.origin_id === n.id && l.origin_slot === clickSlot) clickStarts.push(l.target_id);
        });
        var clickSet = clickStarts.length ? _vReachable(clickStarts, adj) : {};

        links.forEach(function (l) {
            if (!l || l.origin_id !== n.id || l.origin_slot !== targetSlot) return;
            var fwd = _vReachable([l.target_id], adj);
            var inClick = Object.keys(fwd).some(function (k) { return clickSet[k]; });
            if (!inClick) {
                var dst = idMap[l.target_id];
                errors.push('击杀按钮的「目标」端口只能用在「点击」分支内（当前接给了：' + _vType(dst) + '）');
            }
        });
    });

    // 12. 「玩家」节点的上下文相关选项（Killer/Victim/Reporter/Target）
    //     生成代码里它们分别取自事件形参 e 的成员 / 击杀回调的 target，
    //     而哪些成员存在取决于所处的事件类型（详见契约文档）。
    var EVENT_PLAYER_MEMBERS = {
        PlayerDeathEvent: ['Player', 'Killer'],
        PlayerReviveEvent: ['Player', 'Healer'],
        PlayerExileEvent: ['Exiled'],
        MeetingStartEvent: ['Reporter'],
        MeetingEndEvent: [],
        PlayerMurderEvent: ['Player', 'Killer', 'Victim'],
        PlayerTryMurderEvent: ['Player', 'Killer', 'Victim'],
        PlayerVoteEvent: ['Player', 'Voter', 'VoteFor'],
        VentUsedEvent: ['Player']
    };
    var LIFE_EVENT = {
        'life/onDied': 'PlayerDeathEvent',
        'life/onRevived': 'PlayerReviveEvent',
        'life/onExiled': 'PlayerExileEvent',
        'life/onMeetingStarted': 'MeetingStartEvent',
        'life/onMeetingEnded': 'MeetingEndEvent'
    };
    var ACCESSOR_EVENTS = {
        Killer: ['PlayerDeathEvent', 'PlayerMurderEvent', 'PlayerTryMurderEvent'],
        Victim: ['PlayerMurderEvent', 'PlayerTryMurderEvent'],
        Reporter: ['MeetingStartEvent']
    };

    var revAdj = {};
    links.forEach(function (l) {
        if (!l) return;
        (revAdj[l.target_id] = revAdj[l.target_id] || []).push(l.origin_id);
    });
    // 从某节点沿上游（反向连线）找最近的处理方法入口
    function rootOf(startId) {
        var seen = {}, queue = (revAdj[startId] || []).slice();
        queue.forEach(function (id) { seen[id] = true; });
        while (queue.length) {
            var cur = queue.shift();
            var n = idMap[cur];
            var t = n ? _vType(n) : '';
            if (t.indexOf('life/') === 0 || t.indexOf('ev/') === 0 || t === 'btn/kill') return t;
            (revAdj[cur] || []).forEach(function (id) { if (!seen[id]) { seen[id] = true; queue.push(id); } });
        }
        return '';
    }

    nodes.forEach(function (n) {
        if (_vType(n) !== 'val/player') return;
        var opt = String((n.properties && n.properties['MyPlayer']) || 'MyPlayer');
        if (opt === 'MyPlayer') return;

        var consumers = [];
        links.forEach(function (l) { if (l && l.origin_id === n.id) consumers.push(l.target_id); });
        if (!consumers.length) return;   // 未使用：由「孤立节点」检查覆盖

        var root = '';
        consumers.forEach(function (cid) { if (!root) root = rootOf(cid); });
        if (!root) { warnings.push('「玩家」节点的「' + opt + '」无法确定所处的事件上下文'); return; }

        if (opt === 'Target') {
            if (root !== 'btn/kill') {
                errors.push('「玩家」的「目标」只能在击杀按钮的「点击」分支里使用（当前在 ' + root + '）');
            }
            return;
        }

        var evType = (root.indexOf('ev/') === 0 && typeof EMIT !== 'undefined' && EMIT[root])
            ? EMIT[root].event : (LIFE_EVENT[root] || '');
        if (!evType) {
            errors.push('「玩家」的「' + opt + '」需要事件上下文，当前在 ' + root + ' 里不可用');
            return;
        }
        if ((ACCESSOR_EVENTS[opt] || []).indexOf(evType) < 0) {
            var mem = EVENT_PLAYER_MEMBERS[evType] || [];
            errors.push('「玩家」的「' + opt + '」在 ' + evType + ' 里不存在（可用：' +
                (mem.length ? mem.join(' / ') : '无玩家成员') + '）');
        }
    });

    // 10. 翻译完整性
    if (!String(meta.roleName || '').trim() && !String(meta.roleNameEn || '').trim()) {
        errors.push('职业名(中文)与职业名(EN)至少填写一项');
    }

    return { errors: errors, warnings: warnings };
}

// ---------- 对外接口 ----------
async function runValidation() {
    if (typeof invoke === 'function' && existingRoles === null) {
        try {
            var res = await invoke('scanExistingRoles');
            existingRoles = Array.isArray(res) ? res : (res && Array.isArray(res.roles) ? res.roles : []);
        } catch (e) { existingRoles = []; }
    }
    var result = _vCollect();
    renderValidation(result);
    return result;
}

function renderValidation(result) {
    var list = document.getElementById('validation-list');
    var panel = document.getElementById('validation-panel');
    if (!list) return;
    var errors = (result && result.errors) || [];
    var warnings = (result && result.warnings) || [];
    list.innerHTML = '';

    var head = document.createElement('li');
    head.className = errors.length ? 'err' : (warnings.length ? 'warn' : 'ok');
    head.textContent = errors.length
        ? ('错误 ' + errors.length + ' · 警告 ' + warnings.length)
        : (warnings.length ? ('通过 · 警告 ' + warnings.length) : '校验通过');
    list.appendChild(head);

    errors.forEach(function (m) {
        var li = document.createElement('li');
        li.className = 'err';
        li.textContent = '✖ ' + m;
        list.appendChild(li);
    });
    warnings.forEach(function (m) {
        var li = document.createElement('li');
        li.className = 'warn';
        li.textContent = '⚠ ' + m;
        list.appendChild(li);
    });

    if (panel) panel.classList.toggle('has-errors', errors.length > 0);
}

function validateBeforeWrite() {
    var result = _vCollect();
    renderValidation(result);
    return result.errors.length === 0;
}

function initValidation() {
    renderValidation(_vCollect());
}
