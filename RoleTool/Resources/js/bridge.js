// C# ↔ JS 桥接：与 RoleTool 主窗口(WebView2)的消息管道对接

// 待响应请求表（按自增 id 关联）
var _bridgePending = {};
var _bridgeSeq = 0;
var _bridgeTimeoutMs = 20000;
var _bridgeListenerReady = false;

// 注册全局响应监听（幂等）
function _bridgeListen() {
    if (_bridgeListenerReady) return;
    if (!(window.chrome && window.chrome.webview && window.chrome.webview.addEventListener)) return;
    _bridgeListenerReady = true;
    window.chrome.webview.addEventListener('message', _onBridgeMessage);
}

function _onBridgeMessage(e) {
    var msg = e && e.data;
    if (typeof msg === 'string') {
        try { msg = JSON.parse(msg); } catch (err) { return; }
    }
    if (!msg || msg.id == null) return;

    var rec = _bridgePending[msg.id];
    if (!rec) return;
    delete _bridgePending[msg.id];
    if (rec.timer) clearTimeout(rec.timer);

    if (msg.ok === false || msg.error) {
        rec.reject(new Error(msg.error || '桥接调用失败'));
    } else {
        rec.resolve(msg.data);
    }
}

// 调用 C# 命令，返回 Promise
function invoke(cmd, payload) {
    return new Promise(function (resolve, reject) {
        if (!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function')) {
            reject(new Error('当前不在 WebView2 环境中，无法调用桥接命令：' + cmd));
            return;
        }

        var id = ++_bridgeSeq;
        var timer = setTimeout(function () {
            if (_bridgePending[id]) {
                delete _bridgePending[id];
                reject(new Error('桥接调用超时：' + cmd));
            }
        }, _bridgeTimeoutMs);

        _bridgePending[id] = { resolve: resolve, reject: reject, timer: timer };

        try {
            window.chrome.webview.postMessage(JSON.stringify({
                id: id, cmd: cmd, payload: payload == null ? {} : payload
            }));
        } catch (err) {
            delete _bridgePending[id];
            clearTimeout(timer);
            reject(err);
        }
    });
}

// 解析 generateLangJson() 结果 → { zh, en }（兼容单一对象约定）
function _splitLang(langJson) {
    var out = { zh: {}, en: {} };
    try {
        var o = JSON.parse(langJson);
        if (o && (o.zh || o.en)) {
            out.zh = o.zh || {};
            out.en = o.en || {};
        } else if (o && typeof o === 'object') {
            out.zh = o;
            out.en = o;
        }
    } catch (e) { /* 解析失败则留空 */ }
    return out;
}

// 校验通过后写入主插件源码树
async function onWriteToPlugin() {
    if (typeof runValidation === 'function') {
        var results = await runValidation();
        if (typeof renderValidation === 'function') renderValidation(results);
        if (results && results.errors && results.errors.length) {
            showModalDialog('校验未通过', '存在错误，已阻止写入');
            return;
        }
    }

    try {
        var paths = await invoke('getPaths');
        var repoRoot = paths && paths.repoRoot;

        var meta = readMetaForm();
        var cat = CATEGORY_MAP[meta.category] || CATEGORY_MAP.Crewmate;
        var cls = meta.className || 'Role';
        var cs = generateCSharp();

        var lang = _splitLang(generateLangJson());
        var language = {};
        language['Light/Resources/Language/SChinese.json'] = lang.zh;
        language['Light/Resources/Language/English.json'] = lang.en;

        var payload = {
            repoRoot: repoRoot,
            files: [{ relPath: 'Light/Roles/' + cat.dir + '/' + cls + '.cs', content: cs }],
            language: language
        };

        var res = await invoke('writeFiles', payload);
        var written = res && res.written ? res.written : [];
        var first = written.length ? written[0] : ('Light/Roles/' + cat.dir + '/' + cls + '.cs');
        showStatus('已写入主插件源码树：' + first);

        try { await invoke('openInExplorer', { path: repoRoot + '\\Light\\Roles\\' + cat.dir }); }
        catch (e) { /* 打开资源管理器失败不阻断 */ }
    } catch (err) {
        showModalDialog('写入失败', err && err.message ? err.message : String(err));
    }
}

// 构建输出模态框：切换按钮禁用态
function _setBuildButtonDisabled(disabled) {
    var btn = document.getElementById('btn-build');
    if (!btn) return;
    if (disabled) btn.classList.add('disabled'); else btn.classList.remove('disabled');
}

// 显示构建输出模态框并填充文本
function _showBuildModal(text) {
    var out = document.getElementById('build-output');
    if (out) out.textContent = text || '';
    var modal = document.getElementById('build-modal');
    if (modal) modal.classList.remove('hidden');
}

function closeBuildModal() {
    var modal = document.getElementById('build-modal');
    if (modal) modal.classList.add('hidden');
}

// 一键编译主插件：展示构建输出
async function onBuildPlugin() {
    _showBuildModal('构建中…');
    _setBuildButtonDisabled(true);
    try {
        var data = (await invoke('buildPlugin', { configuration: 'Release' })) || {};
        _showBuildModal(data.output || '(无输出)');
        showStatus('构建完成 (exit ' + data.exitCode + ')');
    } catch (err) {
        _showBuildModal('构建失败：' + (err && err.message ? err.message : String(err)));
    } finally {
        _setBuildButtonDisabled(false);
    }
}

// 页面加载时调用：注册响应监听
function initBridge() {
    _bridgeListen();
}

_bridgeListen();
