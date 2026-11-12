// Card buttons only choose films. The selection window performs the explicit library save.
(() => {
  "use strict";
  if (globalThis.JvedioCardButtons) { globalThis.JvedioCardButtons.refresh(); return; }
  const mounted = new WeakMap(), entries = new Set();
  let timer, selected = new Set(), revision = -1, pageUrl = "";
  // 「已拥有」角标 + 直达 JavDB 跳转：owned 缓存本文档已查询的番号，ownedMiss 记录
  // 桌面端未返回（无效番号/旧桌面端）的番号避免反复查询；均为本文档生命周期内有效。
  let owned = new Map(), ownedMiss = new Set(), ownedTimer = null, site = "";
  const documentKey = String(performance.timeOrigin);
  function paint(entry) {
    const checked = selected.has(entry.code);
    const isOwned = owned.get(entry.code)?.owned === true;
    entry.button.textContent = checked ? "已勾选" : isOwned ? "已入库" : "＋Jvedio";
    entry.button.dataset.owned = String(isOwned);
    entry.button.setAttribute("aria-pressed", String(checked));
    entry.button.title = checked ? "已加入本页选择；再次点击取消勾选，挑选完成后点击扩展图标统一保存"
      : isOwned ? "已在 Jvedio 影片库中；点击仍可加入本页勾选（保存时会自动补全）"
      : "加入本页选择；挑选完成后点击扩展图标，再保存所选影片";
  }
  function renderJump(entry) {
    // 直达 JavDB 详情页：有已缓存的 DataCode 用 /v/{code}，否则退回搜索页；
    // JavDB 页面自身无需跳转。挂载点随 host 走 .meta/.score 旁，不受单行省略号裁剪影响。
    if (site === "JavDB") { entry.jump?.remove(); entry.jump = null; return; }
    if (!entry.jump) {
      const jump = document.createElement("a");
      jump.target = "_blank"; jump.rel = "noopener noreferrer";
      jump.textContent = "DB";
      jump.setAttribute("aria-label", `在 JavDB 打开 ${entry.code}`);
      jump.title = "在 JavDB 打开（已入库影片直达详情页，其余为搜索页）";
      for (const name of ["pointerdown", "mousedown"]) jump.addEventListener(name, event => event.stopPropagation());
      entry.jump = jump; entry.shadow.append(jump);
    }
    const info = owned.get(entry.code);
    entry.jump.href = info?.javdbCode ? `https://javdb.com/v/${encodeURIComponent(info.javdbCode)}`
      : `https://javdb.com/search?q=${encodeURIComponent(entry.code)}&f=all`;
  }
  function receive(state) {
    // Waterfall scripts rewrite the address bar inside one document; picks belong to the
    // document, so only the document key (never the drifting URL) may reject a state.
    if (!state || state.documentKey !== documentKey || state.revision < revision) return;
    revision = state.revision; selected = new Set(state.codes);
    for (const entry of entries) {
      if (entry.host.isConnected) { paint(entry); renderJump(entry); } else entries.delete(entry);
    }
  }
  function applyOwned(items) {
    for (const item of items || []) {
      if (!item?.code) continue;
      owned.set(item.code, { owned: item.owned === true, javdbCode: item.javdbCode || "" });
    }
    for (const entry of entries) {
      if (!entry.host.isConnected) { entries.delete(entry); continue; }
      paint(entry); renderJump(entry);
    }
  }
  function scheduleOwnedQuery() {
    // 只在出现未查询过的番号时（重）排队；无新增不得重置定时器，
    // 否则瀑布流的连续 refresh 会把查询无限后移
    const codes = [];
    for (const entry of entries) {
      const code = entry.code;
      if (owned.has(code) || ownedMiss.has(code)) continue;
      ownedMiss.add(code); codes.push(code);
    }
    if (!codes.length) return;
    clearTimeout(ownedTimer);
    ownedTimer = setTimeout(async () => {
      ownedTimer = null;
      try {
        const response = await chrome.runtime.sendMessage({ kind: "query-owned", codes, documentKey });
        if (!response?.ok || !response.enabled) return;
        const items = response.items || [];
        applyOwned(items);
        const known = new Set(items.map(item => item.code));
        for (const code of codes) if (!known.has(code)) ownedMiss.add(code);
      } catch { /* 桌面端未开/网络失败：保持无角标，不打扰 */ }
    }, 300);
  }
  function visible(element) {
    for (let node = element; node && node.nodeType === 1; node = node.parentElement)
      if (node.hidden || getComputedStyle(node).display === "none") return false;
    return true;
  }
  function refresh() {
    clearTimeout(timer); timer = null;
    if (!chrome.runtime?.id || !globalThis.JvedioExtractor) { observer.disconnect(); return; }
    const snapshot = globalThis.JvedioExtractor.extract();
    if (snapshot.kind !== "list") return;
    site = snapshot.site || "";
    if (pageUrl !== location.href) {
      // Same-document URL rewrite: re-read the background's authoritative picks
      // without wiping the local selection, so waterfall scrolling keeps them.
      pageUrl = location.href;
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
      style.textContent = ":host{color-scheme:light}button{font:11px/1.5 system-ui,sans-serif;white-space:nowrap;border:1px solid #df9866;border-radius:4px;background:#fff5eb;color:#88431b;padding:1px 6px;cursor:pointer}button:hover{background:#ffe5ce}button[aria-pressed=true]{background:#e9f4ed;border-color:#71a982;color:#245e35}button[data-owned=true]{background:#7f1d1d;border-color:#a33;color:#fff}a{font:11px/1.5 system-ui,sans-serif;white-space:nowrap;margin-inline-start:4px;color:#5367da;text-decoration:none;border:1px solid #8fa6d9;border-radius:4px;padding:1px 6px;background:#fff}a:hover{background:#eef2fb}button:disabled{opacity:.65;cursor:wait}button:focus-visible{outline:2px solid #5367da;outline-offset:2px}";
      const button = document.createElement("button"); button.type = "button";
      button.setAttribute("aria-label", `选择或取消 ${card.code} 到 Jvedio`);
      const entry = { host, shadow, button, code: card.code, jump: null }; paint(entry);
      for (const name of ["pointerdown", "mousedown"]) button.addEventListener(name, event => event.stopPropagation());
      button.addEventListener("click", async event => {
        event.preventDefault(); event.stopImmediatePropagation();
        if (!event.isTrusted || button.disabled) return;
        button.disabled = true; button.textContent = selected.has(card.code) ? "取消中…" : "勾选中…";
        try {
          const response = await chrome.runtime.sendMessage({ kind: "select-card", code: card.code, pageUrl: location.href, documentKey });
          if (!response?.ok) throw new Error(response?.error || "勾选失败，请重试。");
          receive(response.state);
        } catch (error) { button.textContent = "重试勾选"; button.title = error.message; }
        finally { button.disabled = false; paint(entry); renderJump(entry); }
      });
      shadow.append(style, button); card.position.append(host);
      renderJump(entry);
      mounted.set(card.element, entry); entries.add(entry);
    }
    scheduleOwnedQuery();
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
