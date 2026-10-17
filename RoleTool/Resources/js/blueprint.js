// 蓝图 .rtp 导出 / 导入与工具栏绑定

// 导出当前表单、配置项与节点图为 .rtp 文件
function exportBlueprint() {
    if (typeof litegraphGraph === 'undefined' || !litegraphGraph) {
        showStatus('节点图尚未初始化，请稍后再试');
        return;
    }
    var form = readMetaForm();
    var cat = CATEGORY_MAP[form.category] || CATEGORY_MAP.Crewmate;
    var obj = {
        _format: 'rtp',
        _version: '1.0',
        _appVersion: APP_VERSION,
        _created: new Date().toISOString(),
        _app: 'RoleTool',
        form: form,
        config: getConfigEntries(),
        graph: litegraphGraph.serialize(),
        output: { targetDir: cat.dir, namespace: cat.ns }
    };

    var json = JSON.stringify(obj, null, 2);
    var blob = new Blob([json], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    var name = (form.className || 'Role').replace(/[^A-Za-z0-9_-]/g, '_');
    a.href = url;
    a.download = name + '.rtp';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    showStatus('蓝图已导出：' + name + '.rtp');
}

// 读取 .rtp 蓝图文件
function importBlueprint(inputEl) {
    var file = inputEl && inputEl.files && inputEl.files[0];
    if (!file) return;
    inputEl.value = '';

    var reader = new FileReader();
    reader.onload = function (e) {
        var bp;
        try {
            bp = JSON.parse(e.target.result);
        } catch (err) {
            showModalDialog('导入失败', '文件解析失败，请确认这是有效的 .rtp 蓝图文件。\n' + (err && err.message ? err.message : err));
            return;
        }
        _applyBlueprint(bp, file);
    };
    reader.onerror = function () { showModalDialog('导入失败', '文件读取失败。'); };
    reader.readAsText(file, 'utf-8');
}

// 应用蓝图数据并刷新界面
function _applyBlueprint(bp, file) {
    if (!bp || typeof bp !== 'object') {
        showModalDialog('导入失败', '蓝图内容为空或格式不受支持。');
        return;
    }
    if (bp._format !== 'rtp' && typeof confirm === 'function') {
        if (!confirm('此文件不是标准 .rtp 蓝图，仍然尝试导入？')) return;
    }
    if (bp._appVersion && Number(bp._appVersion) > Number(APP_VERSION)) {
        showStatus('提示：蓝图由更新版本（' + bp._appVersion + '）生成，可能存在兼容性问题');
    }

    if (bp.form && typeof applyMetaForm === 'function') applyMetaForm(bp.form);
    if (typeof setConfigEntries === 'function') setConfigEntries(bp.config || []);
    if (typeof renderConfigEntries === 'function') renderConfigEntries();

    // 记录当前版本未注册的节点类型，不静默丢弃
    var unknown = [];
    var reg = (typeof LiteGraph !== 'undefined' && LiteGraph.registered_node_types) ? LiteGraph.registered_node_types : null;
    if (reg && bp.graph && bp.graph.nodes) {
        bp.graph.nodes.forEach(function (n) {
            var t = n && n.type;
            if (t && !reg[t] && unknown.indexOf(t) === -1) unknown.push(t);
        });
    }

    var configureErr = null;
    if (bp.graph && typeof litegraphGraph !== 'undefined' && litegraphGraph) {
        try { litegraphGraph.configure(bp.graph); }
        catch (err) { configureErr = err; }
    }

    if (typeof regenerateAll === 'function') regenerateAll();
    if (typeof switchTab === 'function') switchTab('logic');

    setTimeout(function () {
        if (typeof litegraphCanvas === 'undefined' || !litegraphCanvas) return;
        var host = document.getElementById('graphDiv');
        var w = host ? host.clientWidth : window.innerWidth;
        var h = host ? host.clientHeight : (window.innerHeight - 52);
        litegraphCanvas.resize(w, h);
        if (litegraphCanvas.ds) litegraphCanvas.ds.reset();
        litegraphCanvas.setDirty(true, true);
    }, 100);

    if (unknown.length || configureErr) {
        var msg = '';
        if (unknown.length) msg += '以下节点类型在当前版本未注册，将被跳过：\n- ' + unknown.join('\n- ');
        if (configureErr) msg += (msg ? '\n\n' : '') + '节点图恢复时出现问题：' + (configureErr.message || configureErr);
        showModalDialog('蓝图兼容性提示', msg);
    }

    var label = (bp.form && (bp.form.className || bp.form.codeName)) || (file && file.name) || '';
    showStatus('已导入蓝图：' + label);
}

// 工具栏绑定（元素不存在时跳过；已有内联处理时避免重复绑定）
function initBlueprint() {
    var btnExport = document.getElementById('btn-export');
    var btnImport = document.getElementById('btn-import');
    var fileInput = document.getElementById('importFile');

    if (btnExport && !btnExport.getAttribute('onclick')) btnExport.addEventListener('click', exportBlueprint);
    if (btnImport && fileInput && !btnImport.getAttribute('onclick')) {
        btnImport.addEventListener('click', function () { fileInput.click(); });
    }
    if (fileInput && !fileInput.getAttribute('onchange')) {
        fileInput.addEventListener('change', function () { importBlueprint(fileInput); });
    }
}
