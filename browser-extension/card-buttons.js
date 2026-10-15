// Card buttons only choose films. The selection window performs the explicit library save.
(() => {
  "use strict";
  if (globalThis.JvedioCardButtons) { globalThis.JvedioCardButtons.refresh(); return; }
  const mounted = new WeakMap(), entries = new Set();
  let timer, selected = new Set(), revision = -1, pageUrl = "";
  const documentKey = String(performance.timeOrigin);
  function paint(entry) {
    const checked = selected.has(entry.code);
    entry.button.textContent = checked ? "已勾选" : "＋Jvedio";
    entry.button.setAttribute("aria-pressed", String(checked));
    entry.button.title = checked ? "已加入本页选择；点击扩展图标后统一保存，可在窗口中取消勾选" : "加入本页选择；挑选完成后点击扩展图标，再保存所选影片";
  }
  function receive(state) {
    if (!state || state.pageUrl !== location.href || state.documentKey !== documentKey || state.revision < revision) return;
    revision = state.revision; selected = new Set(state.codes);
    for (const entry of entries) { if (entry.host.isConnected) paint(entry); else entries.delete(entry); }
  }
  function visible(element) {
    for (let node = element; node && node.nodeType === 1; node = node.parentElement)
      if (node.hidden || getComputedStyle(node).display === "none") return false;
    return true;
  }
  function refresh() {
    clearTimeout(timer); timer = null;
    if (!chrome.runtime?.id || !globalThis.JvedioExtractor) { observer.disconnect(); return; }
    if (globalThis.JvedioExtractor.extract().kind !== "list") return;
    if (pageUrl !== location.href) {
      pageUrl = location.href; revision = -1; selected.clear();
      chrome.runtime.sendMessage({ kind: "read-card-selection", pageUrl, documentKey }).then(response => {
        if (response?.ok) receive(response.state);
      }).catch(() => {});
    }
    for (const card of globalThis.JvedioExtractor.listCards()) {
      const old = mounted.get(card.element);
      if (old?.host.isConnected && old.code === card.code) continue;
      old?.host.remove(); if (old) entries.delete(old);
      if (!visible(card.element)) continue;
      for (const stale of card.element.querySelectorAll("[data-jvedio-card]")) stale.remove();
      const host = document.createElement("span"); host.dataset.jvedioCard = card.code;
      host.style.cssText = "display:inline-block;margin-inline-start:6px;vertical-align:middle";
      const shadow = host.attachShadow({ mode: "open" }), style = document.createElement("style");
      style.textContent = ":host{color-scheme:light}button{font:11px/1.5 system-ui,sans-serif;white-space:nowrap;border:1px solid #df9866;border-radius:4px;background:#fff5eb;color:#88431b;padding:1px 6px;cursor:pointer}button:hover{background:#ffe5ce}button[aria-pressed=true]{background:#e9f4ed;border-color:#71a982;color:#245e35}button:disabled{opacity:.65;cursor:wait}button:focus-visible{outline:2px solid #5367da;outline-offset:2px}";
      const button = document.createElement("button"); button.type = "button";
      button.setAttribute("aria-label", `勾选 ${card.code} 到 Jvedio`);
      const entry = { host, button, code: card.code }; paint(entry);
      for (const name of ["pointerdown", "mousedown"]) button.addEventListener(name, event => event.stopPropagation());
      button.addEventListener("click", async event => {
        event.preventDefault(); event.stopImmediatePropagation();
        if (!event.isTrusted || button.disabled) return;
        button.disabled = true; button.textContent = "勾选中…";
        try {
          const response = await chrome.runtime.sendMessage({ kind: "select-card", code: card.code, pageUrl: location.href, documentKey });
          if (!response?.ok) throw new Error(response?.error || "勾选失败，请重试。");
          receive(response.state);
        } catch (error) { button.textContent = "重试勾选"; button.title = error.message; }
        finally { button.disabled = false; }
      });
      shadow.append(style, button); card.position.append(host);
      mounted.set(card.element, entry); entries.add(entry);
    }
  }
  const observer = new MutationObserver(() => { if (!timer) timer = setTimeout(refresh, 250); });
  observer.observe(document.documentElement, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ["class", "style"] });
  chrome.runtime.onMessage.addListener((message, sender) => {
    if (sender.id === chrome.runtime.id && message?.kind === "selection-changed") receive(message.state);
  });
  addEventListener("pageshow", () => { pageUrl = ""; refresh(); });
  globalThis.JvedioCardButtons = Object.freeze({ refresh });
  refresh();
})();
