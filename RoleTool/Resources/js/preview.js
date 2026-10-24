// 源码预览：Ace 编辑器初始化与内容刷新

function initEditors() {
    if (typeof ace === 'undefined') return;

    var eds = (typeof editors !== 'undefined' && editors) ? editors : {};
    window.editors = eds;

    function make(id, mode) {
        var el = document.getElementById(id);
        if (!el) return null;
        var ed = ace.edit(id);
        ed.setTheme('ace/theme/monokai');
        ed.session.setMode('ace/mode/' + mode);
        ed.session.setUseWorker(false);
        ed.setReadOnly(false);
        ed.setOptions({ fontSize: '13px', showPrintMargin: false, useSoftTabs: true, tabSize: 4 });
        return ed;
    }

    try {
        eds.cs = make('editor-cs', 'csharp');
        eds.lang = make('editor-lang', 'json');
        eds.doc = make('editor-doc', 'markdown');
    } catch (e) {
        if (typeof console !== 'undefined') console.warn('Ace 初始化失败', e);
    }
}

// 内容变化时才写回，避免光标跳动
function refreshPreview(data) {
    if (!data) return;
    var eds = (typeof editors !== 'undefined' && editors) ? editors : null;
    if (!eds) return;
    ['cs', 'lang', 'doc'].forEach(function (k) {
        var ed = eds[k];
        if (!ed) return;
        var v = data[k] || '';
        if (ed.getValue() !== v) ed.setValue(v, -1);
    });
}

// 职业卡片（名称/颜色/阵营/按钮数）
function updateRoleCard(meta) {
    if (!meta) return;
    var card = document.getElementById('role-card');
    if (!card) return;

    var count = 0;
    if (typeof litegraphGraph !== 'undefined' && litegraphGraph) {
        var arr = litegraphGraph._nodes || litegraphGraph.nodes || [];
        count = arr.filter(function (n) { return n && (n.type === 'btn/ability' || n.type === 'btn/effect'); }).length;
    }

    card.innerHTML = '';
    card.style.borderLeftColor = 'rgb(' +
        Math.max(0, Math.min(255, Number(meta.colorR) || 0)) + ',' +
        Math.max(0, Math.min(255, Number(meta.colorG) || 0)) + ',' +
        Math.max(0, Math.min(255, Number(meta.colorB) || 0)) + ')';

    var name = document.createElement('div');
    name.className = 'role-card-name';
    name.textContent = meta.roleName || meta.className || '';
    card.appendChild(name);

    var info = document.createElement('div');
    info.className = 'role-card-info';
    info.textContent = (meta.category || '') + ' · 技能按钮 ' + count + ' 个';
    card.appendChild(info);
}
