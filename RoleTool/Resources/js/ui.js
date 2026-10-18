// 顶部标签切换
function switchTab(tab) {
    document.querySelectorAll('.view-section').forEach(function (s) { s.classList.remove('active'); });
    const view = document.getElementById('view-' + tab);
    if (view) view.classList.add('active');

    document.querySelectorAll('.tab-btn').forEach(function (b) { b.classList.remove('active'); });
    const btn = document.getElementById('tab-' + tab);
    if (btn) btn.classList.add('active');

    if (tab === 'logic' && typeof resizeGraphCanvas === 'function') {
        setTimeout(resizeGraphCanvas, 30);
    }
    if (tab === 'source' && typeof editors !== 'undefined') {
        setTimeout(function () {
            ['cs', 'lang', 'doc'].forEach(function (k) {
                if (editors[k] && editors[k].resize) editors[k].resize();
            });
        }, 30);
    }
}

// 源码预览子标签切换
function switchSrcTab(name) {
    document.querySelectorAll('.src-editor-pane').forEach(function (p) { p.classList.remove('active'); });
    const pane = document.getElementById('pane-' + name);
    if (pane) pane.classList.add('active');

    document.querySelectorAll('.src-tab').forEach(function (b) { b.classList.remove('active'); });
    const btn = document.getElementById('srctab-' + name);
    if (btn) btn.classList.add('active');

    if (typeof editors !== 'undefined' && editors[name] && editors[name].resize) {
        setTimeout(function () { editors[name].resize(); }, 30);
    }
}

let _statusTimer = null;

// 底部状态提示
function showStatus(msg) {
    const el = document.getElementById('status');
    if (!el) return;
    el.textContent = msg;
    el.classList.remove('hidden');
    if (_statusTimer) clearTimeout(_statusTimer);
    _statusTimer = setTimeout(function () { el.classList.add('hidden'); }, 2500);
}

// 通用模态框
function showModalDialog(title, message, type) {
    const modal = document.getElementById('customModal');
    if (!modal) return;
    const t = modal.querySelector('.modal-title');
    const m = document.getElementById('modalMessage');
    if (t) t.textContent = title || '';
    if (m) m.textContent = message || '';
    modal.className = 'modal-overlay' + (type ? ' modal-' + type : '');
}

function closeModal() {
    const modal = document.getElementById('customModal');
    if (modal) modal.classList.add('hidden');
}

// 节点搜索过滤（分类为 <details>，搜索时自动展开命中分组）
function filterNodes(query) {
    const q = (query || '').trim().toLowerCase();
    document.querySelectorAll('#node-list details.node-group').forEach(function (group) {
        let visible = 0;
        group.querySelectorAll('.node-item').forEach(function (item) {
            const match = !q || item.textContent.toLowerCase().indexOf(q) !== -1;
            item.classList.toggle('hidden', !match);
            if (match) visible++;
        });
        group.classList.toggle('hidden', visible === 0);
        group.open = q ? visible > 0 : true;
    });
}

// 路径前缀占位（Task 5 由桥接模块替换）
function getPathPrefix() { return ''; }
