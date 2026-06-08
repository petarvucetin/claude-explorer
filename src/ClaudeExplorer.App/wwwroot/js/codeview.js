// Minimal highlight.js bridge. cx.highlight(el) colorizes a <code> block and adds a line-number
// gutter. Read-only views only; safe to call repeatedly (guarded by a data flag).
window.cx = window.cx || {};
window.cx.highlight = function (el) {
    if (!el || !window.hljs) return;
    if (el.dataset.cxHighlighted === el.textContent.length.toString()) return;
    delete el.dataset.highlighted;          // allow re-highlight after content change
    window.hljs.highlightElement(el);
    if (window.hljs.lineNumbersBlock) window.hljs.lineNumbersBlock(el, { singleLine: true });
    el.dataset.cxHighlighted = el.textContent.length.toString();
};
