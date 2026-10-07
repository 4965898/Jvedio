"use strict";
const $ = id => document.getElementById(id);
let snapshot = { items: [] }, selected = new Set(), sessionKey = "", connected = false, busy = false, sourceTabId = null, mediaSettings = {};
const chosen = () => snapshot.items.filter(item => selected.has(item.code));
const visible = () => {
  const query = $("search").value.trim().toUpperCase();
  return snapshot.items.filter(item => `${item.code} ${item.title}`.toUpperCase().includes(query));
};
function status(message, kind = "") { $("status").textContent = message; $("status").dataset.kind = kind; }
function remember() {
  if (sessionKey) chrome.storage.session.set({ [sessionKey]: { pageUrl: snapshot.pageUrl, codes: [...selected] } });
}
function update() {
  $("count").textContent = `本页 ${snapshot.items.length} 个番号 · 已选 ${chosen().length} 个 · 显示 ${visible().length} 个`;
  for (const id of ["copy", "txt", "json"]) $(id).disabled = busy || !chosen().length;
  $("save").disabled = busy || !chosen().length || !connected || !$("library").value;
  $("save").textContent = busy ? "正在保存资料和图片，请稍候…" : `保存所选 ${chosen().length} 个番号`;
  for (const id of ["library", "rescan", "all", "none", "connect"]) $(id).disabled = busy;
  for (const check of $("items").querySelectorAll("input")) check.disabled = busy;
}
function render() {
  $("items").replaceChildren();
  for (const item of visible()) {
    const row = document.createElement("label"); row.className = "item";
    const check = document.createElement("input"); check.type = "checkbox"; check.checked = selected.has(item.code);
    check.setAttribute("aria-label", `选择 ${item.code}`);
    check.addEventListener("change", () => { check.checked ? selected.add(item.code) : selected.delete(item.code); remember(); update(); });
    const content = document.createElement("div");
    const code = document.createElement("strong"); code.textContent = item.code;
    const title = document.createElement("span"); title.textContent = item.title || "暂无标题";
    content.append(code, title); row.append(check, content); $("items").append(row);
  }
  $("empty").hidden = snapshot.items.length > 0; update();
  $("metadata").replaceChildren();
  $("metadata").hidden = snapshot.kind !== "detail";
  if (snapshot.kind === "detail" && snapshot.items[0]?.metadata) {
    const info = snapshot.items[0].metadata;
    for (const [label, value] of [["发行", info.releaseDate], ["时长", info.duration ? `${info.duration} 分钟` : ""], ["导演", info.director], ["片商", info.studio], ["发行商", info.publisher], ["系列", info.series?.join("、")], ["演员", info.actors?.map(actor => actor.name).join("、")], ["类别", info.genres?.join("、")], ["评分", info.rating ? `${info.rating} / 5` : ""], ["图片", info.coverUrl ? `封面 · ${info.previewUrls?.length || 0} 张预览图` : ""]]) {
      if (!value) continue;
      const row = document.createElement("p"); row.textContent = `${label}：${value}`; $("metadata").append(row);
    }
  }
}
async function scan() {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.id || !/^https?:/.test(tab.url || "")) throw new Error("请在网站页面使用插件。Chrome 内部页面无法剪藏。");
    await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ["extractor.js"] });
    const [response] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: () => globalThis.JvedioExtractor.extract() });
    snapshot = response.result;
    sourceTabId = tab.id;
    const nextKey = `selection:${tab.id}`;
    const stored = (await chrome.storage.session.get(nextKey))[nextKey];
    // Keep per-page selections when reopening; a different page starts selected.
    if (stored?.pageUrl === snapshot.pageUrl) selected = new Set(stored.codes);
    else if (sessionKey !== nextKey || snapshot.pageUrl !== sessionStorage.getItem("pageUrl")) selected = new Set(snapshot.items.map(item => item.code));
    sessionKey = nextKey; sessionStorage.setItem("pageUrl", snapshot.pageUrl);
    selected = new Set([...selected].filter(code => snapshot.items.some(item => item.code === code)));
    $("source").textContent = `${snapshot.site || "未支持的页面"} · ${snapshot.pageTitle || tab.title || ""}`;
    $("source").title = snapshot.pageUrl; remember(); render();
  } catch (error) { snapshot = { items: [] }; selected.clear(); render(); status(error.message, "error"); }
}
async function connect() {
  connected = false; $("library").replaceChildren(); update();
  try {
    const response = await chrome.runtime.sendMessage({ kind: "status" });
    if (!response?.ok) throw new Error(response?.error || "连接失败。");
    mediaSettings = response.result.media || {};
    const remembered = (await chrome.storage.local.get("libraryId")).libraryId;
    for (const library of response.result.libraries) {
      const option = document.createElement("option"); option.value = library.id; option.textContent = library.name; $("library").append(option);
    }
    const preferred = remembered ?? response.result.currentLibraryId;
    if ([...$("library").options].some(option => option.value === String(preferred))) $("library").value = String(preferred);
    connected = true;
    status(response.result.libraries.length ? "已连接 Jvedio，请选择要保存的影片。" : "请先在 Jvedio 中创建影片库。", "success");
  } catch (error) { status(error.message, "error"); }
  update();
}
function exportFile(format) {
  const payload = { version: 1, site: snapshot.site, kind: snapshot.kind, pageUrl: snapshot.pageUrl, pageTitle: snapshot.pageTitle, capturedAt: new Date().toISOString(), items: chosen() };
  const blob = new Blob([format === "txt" ? chosen().map(item => item.code).join("\r\n") + "\r\n" : JSON.stringify(payload, null, 2)], { type: format === "txt" ? "text/plain;charset=utf-8" : "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob), anchor = document.createElement("a");
  anchor.href = url; anchor.download = `Jvedio-${new Date().toISOString().slice(0, 10)}.${format}`;
  anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 10000); status(`已导出 ${chosen().length} 个番号。`, "success");
}
$("search").addEventListener("input", render);
$("all").addEventListener("click", () => { visible().forEach(item => selected.add(item.code)); remember(); render(); });
$("none").addEventListener("click", () => { visible().forEach(item => selected.delete(item.code)); remember(); render(); });
$("rescan").addEventListener("click", scan);
$("connect").addEventListener("click", connect);
$("library").addEventListener("change", () => { chrome.storage.local.set({ libraryId: $("library").value }); update(); });
$("apply").addEventListener("click", async () => {
  const port = Number($("port").value), token = $("token").value.trim();
  if (!Number.isInteger(port) || port < 1024 || port > 65535 || !/^[a-f0-9]{64}$/i.test(token)) { status("请输入有效端口和 Jvedio 提供的 64 位连接密钥。", "error"); return; }
  await chrome.storage.local.set({ port, token, autoDetail: $("autoDetail").checked }); await connect();
});
$("copy").addEventListener("click", async () => {
  try { await navigator.clipboard.writeText(chosen().map(item => item.code).join("\n")); status(`已复制 ${chosen().length} 个番号。`, "success"); }
  catch { status("复制失败，请使用导出 TXT。", "error"); }
});
for (const format of ["txt", "json"]) $(format).addEventListener("click", () => exportFile(format));
async function save() {
  if (busy || !connected || !$("library").value || !chosen().length) return;
  busy = true; update();
  try {
    let videoNote = "";
    if (mediaSettings.savePreviewVideos && snapshot.kind === "detail") {
      for (const item of chosen()) {
        const info = item.metadata;
        if (info?.previewVideoEndpoint && !info.previewVideoUrls?.length) {
          status("正在读取预览视频地址…");
          try {
            const [resolution] = await chrome.scripting.executeScript({ target: { tabId: sourceTabId }, args: [info.previewVideoEndpoint, snapshot.pageUrl], func: async (endpoint, expectedPage) => {
              if (location.href !== expectedPage) throw new Error("页面已切换，请刷新后重试。");
              const url = new URL(endpoint, location.href);
              if (url.origin !== location.origin || !/\/preview_url$/.test(url.pathname)) throw new Error("无效的预览视频接口。");
              const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 8000);
              try {
                const response = await fetch(url.href, { credentials: "include", signal: controller.signal, headers: { Accept: "application/json" } });
                if (!response.ok) throw new Error("无法取得预览视频地址。");
                const data = await response.json();
                const candidates = [typeof data === "string" ? data : "", data.url, data.preview_url, data.video_url, data.data?.url, data.data?.preview_url];
                return candidates.filter(value => typeof value === "string" && /^https?:\/\//i.test(value) && !/\.m3u8(?:\?|$)/i.test(value)).slice(0, 1);
              } finally { clearTimeout(timer); }
            } });
            info.previewVideoUrls = resolution.result || [];
          } catch { videoNote = " 预览视频地址暂时无法取得，文字资料仍已保存。"; }
        }
        if (!info?.previewVideoUrls?.length && !videoNote) videoNote = " 当前页面没有可保存的预览视频直链。";
      }
    }
    const payload = { libraryId: Number($("library").value), site: snapshot.site, kind: snapshot.kind, pageUrl: snapshot.pageUrl, items: chosen() };
    const response = await chrome.runtime.sendMessage({ kind: "save", payload });
    if (!response?.ok) throw new Error(response?.error || "保存失败。");
    await chrome.storage.local.set({ libraryId: $("library").value });
    const pictures = response.result.pictures;
    const pictureNote = pictures ? ` 图片完成 ${pictures.succeeded} 项${pictures.failed ? `，失败 ${pictures.failed} 项（${pictures.error}）` : ""}。${pictures.unavailableActors ? ` ${pictures.unavailableActors} 位演员未找到可用头像链接。` : ""}` : "";
    status(`已保存到「${$("library").selectedOptions[0].textContent}」：新增 ${response.result.added} 个，补全 ${response.result.updated || 0} 个，跳过已有 ${response.result.skipped} 个。${pictureNote}${response.result.mediaQueued || response.result.imagesQueued ? " 视频已加入下载任务。" : ""}${response.result.mediaAlreadyQueued ? " 已有媒体任务正在进行，完成后再次剪藏可应用新的保存选项。" : ""}${videoNote}`, pictures?.failed ? "error" : "success");
  } catch (error) { status(error.message, "error"); }
  finally { busy = false; update(); }
}
$("save").addEventListener("click", save);
(async () => {
  const settings = await chrome.storage.local.get({ port: 18888, token: "", autoDetail: true, libraryId: "" });
  $("port").value = settings.port; $("token").value = settings.token;
  $("autoDetail").checked = settings.autoDetail;
  if (!settings.token) $("settings").open = true;
  await scan();
  if (settings.token) {
    await connect();
    if (settings.autoDetail && settings.libraryId && snapshot.kind === "detail" && $("library").value === String(settings.libraryId)) await save();
  }
})();
