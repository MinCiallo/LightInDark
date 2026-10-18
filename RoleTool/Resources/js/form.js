// 职业配置表单：构建 DOM、读写元数据、管理轻量配置项

var _configEntries = [];

// ---------- DOM 工具 ----------
function _el(tag, cls, parent) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (parent) parent.appendChild(e);
    return e;
}

function _section(parent, title) {
    var s = _el('div', 'meta-section', parent);
    var t = _el('div', 'meta-title', s);
    t.textContent = title;
    return s;
}

function _row(parent, labelText) {
    var r = _el('div', 'meta-row', parent);
    var l = _el('label', 'meta-label', r);
    l.textContent = labelText;
    return r;
}

function _input(parent, labelText, id, type, value) {
    var r = _row(parent, labelText);
    var i = _el('input', 'meta-input', r);
    i.id = id; i.type = type || 'text';
    if (value != null) i.value = value;
    return i;
}

function _textarea(parent, labelText, id, value) {
    var r = _row(parent, labelText);
    var i = _el('textarea', 'meta-input meta-textarea', r);
    i.id = id;
    if (value != null) i.value = value;
    return i;
}

function _checkbox(parent, labelText, id, checked) {
    var r = _row(parent, labelText);
    var i = _el('input', 'meta-check', r);
    i.id = id; i.type = 'checkbox'; i.checked = !!checked;
    return i;
}

function _select(parent, labelText, id, options, value) {
    var r = _row(parent, labelText);
    var s = _el('select', 'meta-input', r);
    s.id = id;
    options.forEach(function (o) {
        var op = document.createElement('option');
        op.value = o; op.textContent = o;
        s.appendChild(op);
    });
    if (value != null) s.value = value;
    return s;
}

// ---------- 构建表单 ----------
function initForm() {
    var host = document.getElementById('meta-form');
    if (!host) return;
    host.innerHTML = '';
    _buildMetaForm(host);
    renderConfigEntries();
    syncFieldAvailability();

    // 任意输入变化 → 同步字段可用性 + 触发重新生成
    host.addEventListener('input', function () { syncFieldAvailability(); scheduleRegen(); });
    host.addEventListener('change', function () { syncFieldAvailability(); scheduleRegen(); });
}

// 当前设置下无效的字段置灰禁用（避免"阵营是船员却能设中立细分"这种矛盾项）
function _setEnabled(id, enabled, hint) {
    var el = document.getElementById(id);
    if (!el) return;
    el.disabled = !enabled;
    var row = (el.closest ? el.closest('.meta-row') : null);
    if (row) row.classList.toggle('meta-row-disabled', !enabled);
    if (enabled) el.removeAttribute('title');
    else el.title = hint || '当前设置下无效';
}

function syncFieldAvailability() {
    var cat = _v('category') || 'Crewmate';
    var canKill = _c('canKill');
    var maxCount = _n('maxCount', 0);

    _setEnabled('neutralType', cat === 'Neutral', '只有中立阵营需要选择中立细分');
    _setEnabled('teamCode', cat === 'Neutral', '只有中立阵营会生成队伍标识');
    _setEnabled('killCooldown', canKill, '未勾选「可击杀」时不生效');
    _setEnabled('killDeathCauseId', canKill, '未勾选「可击杀」时不生效');
    _setEnabled('guaranteedCount', maxCount > 0, '最大数量为 0 时不参与分配');
    _setEnabled('chance', maxCount > 0, '最大数量为 0 时不参与分配');
}

function _buildMetaForm(host) {
    var head = _el('div', 'meta-header', host);
    var card = _el('div', 'role-card', head);
    card.id = 'role-card';

    var s1 = _section(host, '基础信息');
    _input(s1, '类名', 'className', 'text', 'Caller');
    _input(s1, '内部名(CodeName)', 'codeName', 'text', 'Caller');
    _input(s1, '职业名(中文)', 'roleName', 'text', '召集者');
    _input(s1, '一句话简介', 'shortDes', 'text', '可随时强制召开紧急会议的船员职业');
    _textarea(s1, '开场白', 'intro', '你是召集者！');
    _textarea(s1, '描述', 'desc', '');

    var s2 = _section(host, '英文文案');
    _input(s2, '职业名(EN)', 'roleNameEn', 'text', 'Caller');
    _input(s2, '一句话简介(EN)', 'shortDesEn', 'text', '');
    _textarea(s2, '开场白(EN)', 'introEn', '');
    _textarea(s2, '描述(EN)', 'descEn', '');

    var s3 = _section(host, '阵营与颜色');
    _select(s3, '阵营', 'category', ['Crewmate', 'Impostor', 'Neutral', 'Ghost'], 'Crewmate');
    _select(s3, '中立细分', 'neutralType', ['Benign', 'Evil'], 'Benign');
    _input(s3, '颜色 R', 'colorR', 'number', 255);
    _input(s3, '颜色 G', 'colorG', 'number', 235);
    _input(s3, '颜色 B', 'colorB', 'number', 4);
    _input(s3, '颜色 A', 'colorA', 'number', 255);

    var s4 = _section(host, '分配与能力');
    _input(s4, '最大数量', 'maxCount', 'number', 1);
    _input(s4, '必出数量', 'guaranteedCount', 'number', 0);
    _input(s4, '出现概率(%)', 'chance', 'number', 100);
    _checkbox(s4, '可击杀', 'canKill', false);
    _input(s4, '击杀冷却(秒)', 'killCooldown', 'number', 20);
    _checkbox(s4, '可用管道', 'canUseVents', false);
    _checkbox(s4, '可报告尸体', 'canReport', true);
    _checkbox(s4, '可召开紧急会议', 'canCallMeeting', true);
    _input(s4, '任务数', 'taskCount', 'number', 1);
    _input(s4, '队伍标识', 'teamCode', 'text', '');
    _input(s4, '自定义死因 id', 'killDeathCauseId', 'text', '');

    var s5 = _section(host, '资源与文档');
    _input(s5, '立绘路径', 'roleImagePath', 'text', '');
    _input(s5, '开场音效路径', 'introSFX', 'text', '');
    _select(s5, '文档类型', 'documentType', ['Normal', 'MarkDown', 'Html'], 'Normal');
}

// ---------- 读取 / 应用 ----------
function _v(id) { var e = document.getElementById(id); return e ? e.value : ''; }
function _n(id, def) { var v = parseFloat(_v(id)); return isFinite(v) ? v : def; }
function _c(id) { var e = document.getElementById(id); return e ? !!e.checked : false; }

function readMetaForm() {
    // 先算"当前设置下是否有效"，无效的字段统一回落默认值，避免把矛盾设置带进生成结果
    var cat = _v('category') || 'Crewmate';
    var canKill = _c('canKill');
    var maxCount = _n('maxCount', 1);

    return {
        className: _v('className').trim() || 'Caller',
        codeName: _v('codeName').trim() || 'Caller',
        roleName: _v('roleName'),
        shortDes: _v('shortDes'),
        intro: _v('intro'),
        desc: _v('desc'),
        roleNameEn: _v('roleNameEn'),
        shortDesEn: _v('shortDesEn'),
        introEn: _v('introEn'),
        descEn: _v('descEn'),
        category: cat,
        neutralType: cat === 'Neutral' ? (_v('neutralType') || 'Benign') : 'Benign',
        colorR: _n('colorR', 255),
        colorG: _n('colorG', 255),
        colorB: _n('colorB', 255),
        colorA: _n('colorA', 255),
        maxCount: maxCount,
        guaranteedCount: maxCount > 0 ? _n('guaranteedCount', 0) : 0,
        chance: maxCount > 0 ? _n('chance', 100) : 0,
        canKill: canKill,
        killCooldown: canKill ? _n('killCooldown', 20) : 20,
        canUseVents: _c('canUseVents'),
        canReport: _c('canReport'),
        canCallMeeting: _c('canCallMeeting'),
        taskCount: _n('taskCount', 1),
        teamCode: cat === 'Neutral' ? _v('teamCode') : '',
        killDeathCauseId: canKill ? _v('killDeathCauseId') : '',
        roleImagePath: _v('roleImagePath'),
        introSFX: _v('introSFX'),
        documentType: _v('documentType') || 'Normal'
    };
}

function applyMetaForm(data) {
    if (!data) return;
    var map = {
        className: 'text', codeName: 'text', roleName: 'text', shortDes: 'text',
        intro: 'text', desc: 'text', roleNameEn: 'text', shortDesEn: 'text',
        introEn: 'text', descEn: 'text', category: 'text', neutralType: 'text',
        colorR: 'text', colorG: 'text', colorB: 'text', colorA: 'text',
        maxCount: 'text', guaranteedCount: 'text', chance: 'text',
        killCooldown: 'text', taskCount: 'text', teamCode: 'text',
        killDeathCauseId: 'text', roleImagePath: 'text', introSFX: 'text',
        documentType: 'text'
    };
    Object.keys(map).forEach(function (k) {
        var el = document.getElementById(k);
        if (el && data[k] != null) el.value = data[k];
    });
    ['canKill', 'canUseVents', 'canReport', 'canCallMeeting'].forEach(function (k) {
        var el = document.getElementById(k);
        if (el && data[k] != null) el.checked = !!data[k];
    });
    if (typeof syncFieldAvailability === 'function') syncFieldAvailability();
    if (typeof scheduleRegen === 'function') scheduleRegen();
}

// ---------- 轻量配置项 ----------
function getConfigEntries() { return _configEntries; }

function setConfigEntries(arr) {
    _configEntries = (arr || []).slice();
    renderConfigEntries();
}

function addConfigEntry() {
    _configEntries.push({ key: 'cooldown', type: 'Float', default: 20, min: 5, max: 60, step: 2.5, suffix: 'Second' });
    renderConfigEntries();
    if (typeof scheduleRegen === 'function') scheduleRegen();
}

function onAddConfig() { addConfigEntry(); }

function removeConfigEntry(i) {
    _configEntries.splice(i, 1);
    renderConfigEntries();
    if (typeof scheduleRegen === 'function') scheduleRegen();
}

function renderConfigEntries() {
    var host = document.getElementById('config-entries');
    if (!host) return;
    host.innerHTML = '';

    var title = _el('div', 'meta-title', host);
    title.textContent = '专属配置项';

    _configEntries.forEach(function (entry, i) {
        var row = _el('div', 'config-row', host);

        var key = _el('input', 'meta-input', row);
        key.type = 'text'; key.value = entry.key || '';
        key.placeholder = '键名';
        key.addEventListener('input', function () { entry.key = key.value; scheduleRegen(); });

        var type = _el('select', 'meta-input', row);
        ['Bool', 'Int', 'Float', 'String'].forEach(function (o) {
            var op = document.createElement('option');
            op.value = o; op.textContent = o;
            type.appendChild(op);
        });
        type.value = entry.type || 'Int';
        type.addEventListener('change', function () { entry.type = type.value; scheduleRegen(); });

        var def = _el('input', 'meta-input', row);
        def.type = 'text'; def.value = entry.default != null ? entry.default : '';
        def.placeholder = '默认值';
        def.addEventListener('input', function () { entry.default = def.value; scheduleRegen(); });

        var min = _el('input', 'meta-input', row);
        min.type = 'number'; min.value = entry.min != null ? entry.min : 0;
        min.placeholder = '最小';
        min.addEventListener('input', function () { entry.min = parseFloat(min.value) || 0; scheduleRegen(); });

        var max = _el('input', 'meta-input', row);
        max.type = 'number'; max.value = entry.max != null ? entry.max : 100;
        max.placeholder = '最大';
        max.addEventListener('input', function () { entry.max = parseFloat(max.value) || 0; scheduleRegen(); });

        var step = _el('input', 'meta-input', row);
        step.type = 'number'; step.value = entry.step != null ? entry.step : 0;
        step.placeholder = '步进';
        step.addEventListener('input', function () { entry.step = parseFloat(step.value) || 0; scheduleRegen(); });

        var suffix = _el('select', 'meta-input', row);
        ['None', 'Second', 'Percent', 'Times', 'Multiplier'].forEach(function (o) {
            var op = document.createElement('option');
            op.value = o; op.textContent = o;
            suffix.appendChild(op);
        });
        suffix.value = entry.suffix || 'None';
        suffix.addEventListener('change', function () { entry.suffix = suffix.value; scheduleRegen(); });

        var del = _el('button', 'tool-btn', row);
        del.type = 'button'; del.textContent = '删除';
        del.addEventListener('click', function () { removeConfigEntry(i); });
    });
}
