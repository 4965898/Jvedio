// Installed only in the clicked source tab; disconnecting the window stops observation.
(() => {
  if (globalThis.JvedioWatchInstalled) return;
  globalThis.JvedioWatchInstalled = true;
  chrome.runtime.onConnect.addListener(port => {
    if (port.name !== "jvedio-page") return;
    let timer;
    const send = () => {
      clearTimeout(timer);
      try { port.postMessage({ ...globalThis.JvedioExtractor.extract(), documentKey: String(performance.timeOrigin) }); }
      catch { port.disconnect(); }
    };
    const observer = new MutationObserver(() => {
      // A fixed delay also handles pages that continuously update lazy images.
      if (!timer) timer = setTimeout(() => { timer = null; send(); }, 400);
    });
    observer.observe(document.documentElement, { childList: true, subtree: true, characterData: true,
      attributes: true, attributeFilter: ["src", "data-src", "href", "class", "id"] });
    port.onDisconnect.addListener(() => { clearTimeout(timer); observer.disconnect(); });
    send();
  });
})();
