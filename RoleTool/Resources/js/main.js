// Ace 编辑器实例集合（由 preview.js 的 initEditors 填充）
var editors = {};

// 等待 LiteGraph 加载完成
function waitLG() {
    if (typeof LiteGraph === 'undefined') { setTimeout(waitLG, 50); return; }
    if (typeof initLiteGraph === 'function') initLiteGraph();
}

document.addEventListener('DOMContentLoaded', function () {
    // 以下模块由后续 Task 提供，缺失时跳过
    if (typeof initForm === 'function') initForm();
    if (typeof initEmitMap === 'function') initEmitMap();
    if (typeof initCodegen === 'function') initCodegen();
    if (typeof initPreview === 'function') initPreview();
    if (typeof initBlueprint === 'function') initBlueprint();
    if (typeof initValidation === 'function') initValidation();
    if (typeof initBridge === 'function') initBridge();

    if (typeof initEditors === 'function') initEditors();
    if (typeof initDocs === 'function') initDocs();
    waitLG();
});
