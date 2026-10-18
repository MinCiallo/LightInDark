let litegraphGraph = null;
let litegraphCanvas = null;
let _regenTimer = null;

// 防抖：节点变化后延迟重新生成代码
function scheduleRegen() {
    if (_regenTimer) clearTimeout(_regenTimer);
    _regenTimer = setTimeout(function () {
        _regenTimer = null;
        if (typeof regenerateAll === 'function') regenerateAll();
    }, 80);
}

// 自适应画布尺寸
function resizeGraphCanvas() {
    if (!litegraphCanvas) return;
    const host = document.getElementById('graphDiv');
    if (!host) return;
    const w = host.clientWidth || window.innerWidth;
    const h = host.clientHeight || (window.innerHeight - 52);
    const canvas = litegraphCanvas.canvas;
    if (canvas) { canvas.width = w; canvas.height = h; }
    litegraphCanvas.resize(w, h);
}

// 空白区右键 → 添加节点子菜单
function openAddNodeMenu(lgc, graph, event, prevMenu) {
    const cats = {};
    Object.keys(LiteGraph.registered_node_types).forEach(function (type) {
        const cat = type.split('/')[0];
        (cats[cat] = cats[cat] || []).push(type);
    });
    const catItems = Object.keys(cats).map(function (cat) {
        return {
            content: CN_CATEGORY[cat] || cat,
            has_submenu: true,
            callback: function (_v, _o, ev, prev) {
                const nodeItems = cats[cat].map(function (type) {
                    return {
                        content: LiteGraph.registered_node_types[type].title || type,
                        callback: function () {
                            const node = LiteGraph.createNode(type);
                            if (!node) return;
                            const canvas = lgc.canvas;
                            const rect = canvas.getBoundingClientRect();
                            const x = (event.clientX - rect.left) / lgc.ds.scale - lgc.ds.offset[0];
                            const y = (event.clientY - rect.top) / lgc.ds.scale - lgc.ds.offset[1];
                            node.pos = [x, y];
                            graph.add(node);
                        }
                    };
                });
                new LiteGraph.ContextMenu(nodeItems, { event: ev, parentMenu: prev });
            }
        };
    });
    new LiteGraph.ContextMenu(catItems, { event: event, parentMenu: prevMenu });
}

// 左侧节点列表按分类折叠填充（<details> 收纳，与 RC Studio 一致）
function fillNodeList() {
    const list = document.getElementById('node-list');
    if (!list) return;
    list.innerHTML = '';
    const cats = {};
    Object.keys(LiteGraph.registered_node_types).forEach(function (type) {
        const cat = type.split('/')[0];
        (cats[cat] = cats[cat] || []).push(type);
    });
    Object.keys(cats).forEach(function (cat) {
        const group = document.createElement('details');
        group.className = 'node-group';
        group.open = true;

        const title = document.createElement('summary');
        title.className = 'node-group-title';
        title.textContent = CN_CATEGORY[cat] || cat;
        const count = document.createElement('span');
        count.className = 'node-group-count';
        count.textContent = cats[cat].length;
        title.appendChild(count);
        group.appendChild(title);

        const body = document.createElement('div');
        body.className = 'node-group-body';
        cats[cat].forEach(function (type) {
            const item = document.createElement('div');
            item.className = 'node-item';
            item.draggable = true;
            item.dataset.node = type;
            item.textContent = LiteGraph.registered_node_types[type].title || type;
            body.appendChild(item);
        });
        group.appendChild(body);
        list.appendChild(group);
    });
}

// 连接端口类型校验（仅此版本安全方式：包装原型 connect）
function installTypeCheck() {
    if (LiteGraph.LGraphNode.prototype.__typeChecked) return;
    const origConnect = LiteGraph.LGraphNode.prototype.connect;
    LiteGraph.LGraphNode.prototype.connect = function (slot, target_node, target_slot) {
        const out = this.outputs && this.outputs[slot];
        const inp = target_node && target_node.inputs && target_node.inputs[target_slot];
        if (out && inp && out.type && inp.type && out.type !== inp.type) {
            showStatus('端口类型不匹配');
            return;
        }
        return origConnect.call(this, slot, target_node, target_slot);
    };
    LiteGraph.LGraphNode.prototype.__typeChecked = true;
}

// 变化钩子 → 触发防抖重新生成
function installChangeHooks(graph) {
    graph.onNodeAdded = scheduleRegen;
    graph.onNodeRemoved = scheduleRegen;

    if (LiteGraph.LGraphNode.prototype.__changeHooked) return;
    const origConn = LiteGraph.LGraphNode.prototype.onConnectionsChange;
    LiteGraph.LGraphNode.prototype.onConnectionsChange = function () {
        const r = origConn ? origConn.apply(this, arguments) : undefined;
        scheduleRegen();
        return r;
    };
    const origProp = LiteGraph.LGraphNode.prototype.onPropertyChanged;
    LiteGraph.LGraphNode.prototype.onPropertyChanged = function () {
        const r = origProp ? origProp.apply(this, arguments) : undefined;
        scheduleRegen();
        return r;
    };
    LiteGraph.LGraphNode.prototype.__changeHooked = true;
}

function initLiteGraph() {
    if (typeof LiteGraph === 'undefined') { setTimeout(initLiteGraph, 100); return; }

    registerAllNodes();

    const graph = new LiteGraph.LGraph();
    litegraphGraph = graph;

    const container = document.getElementById('graphDiv');
    if (!container) return;

    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'position:absolute;top:0;left:0;';
    container.appendChild(canvas);

    const lgc = new LiteGraph.LGraphCanvas(canvas, graph);
    litegraphCanvas = lgc;
    lgc.background_image = null;
    lgc.clear_background_color = '#181b27';

    resizeGraphCanvas();
    window.addEventListener('resize', resizeGraphCanvas);

    // 空白区右键菜单
    lgc.getMenuOptions = function () {
        return [
            {
                content: '添加节点', has_submenu: true,
                callback: function (_v, _o, ev, prev) { openAddNodeMenu(lgc, graph, ev, prev); }
            },
            null,
            { content: '适应全部节点', callback: function () { lgc.ds.reset(); lgc.setDirty(true, true); } },
            {
                content: '清空画布', callback: function () {
                    if (confirm('确定清空画布上的所有节点？此操作不可撤销。')) {
                        graph.clear();
                        scheduleRegen();
                    }
                }
            }
        ];
    };

    // 节点右键菜单
    lgc.getNodeMenuOptions = function (node) {
        return [
            {
                content: '克隆节点', callback: function () {
                    const c = node.clone();
                    c.pos = [node.pos[0] + 30, node.pos[1] + 30];
                    graph.add(c);
                }
            },
            null,
            { content: '删除节点', callback: function () { graph.remove(node); } }
        ];
    };

    // 拖拽放置节点
    container.addEventListener('dragover', function (e) {
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
        e.preventDefault();
    });
    container.addEventListener('drop', function (e) {
        e.preventDefault();
        if (!e.dataTransfer) return;
        const nodeType = e.dataTransfer.getData('nodeType');
        if (!nodeType) return;
        const rect = canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / lgc.ds.scale - lgc.ds.offset[0];
        const y = (e.clientY - rect.top) / lgc.ds.scale - lgc.ds.offset[1];
        const node = LiteGraph.createNode(nodeType);
        if (node) { node.pos = [x, y]; graph.add(node); }
    });

    installTypeCheck();
    installChangeHooks(graph);

    // 左侧列表拖拽：列表是动态填充的，用事件委托（逐个绑定会因填充晚于绑定而失效）
    const nodeList = document.getElementById('node-list');
    if (nodeList && !nodeList.__dragBound) {
        nodeList.__dragBound = true;
        nodeList.addEventListener('dragstart', function (e) {
            const item = e.target && e.target.closest ? e.target.closest('.node-item') : null;
            if (!item || !e.dataTransfer) return;
            e.dataTransfer.setData('nodeType', item.dataset.node);
            e.dataTransfer.effectAllowed = 'copy';
        });
    }

    fillNodeList();

    // 节点注册完成后再渲染文档（顺序必须在 registerAllNodes 之后）
    if (typeof renderDocs === 'function') renderDocs();

    // 默认起始节点
    const first = LiteGraph.createNode('life/onActivated');
    if (first) { first.pos = [120, 140]; graph.add(first); }

    graph.start();
    scheduleRegen();
}
