"use strict";
const $ = id => document.getElementById(id);
let snapshot = { items: [] }, selected = new Set(), connected = false, busy = false, sourceTabId = null;
let libraries = [], marks = [], selectedMarks = new Set(), pagePort = null, pendingSnapshot = null, scanning = false;
let selectionWrite = Promise.resolve();
const viewKey = crypto.randomUUID();
let confirmedSelection = [], selectionRevision = -1, pendingChoices = [];
let markWrite = Promise.resolve(), restoringMarks = false, markImport = false;
const chosen = () => snapshot.items.filter(item => selected.has(item.code));
const visible = () => {
  const query = $("search").value.trim().toUpperCase();
  return snapshot.items.filter(item => `${item.code} ${item.title}`.toUpperCase().includes(query));
};
function status(message, kind = "") { $("status").textContent = message; $("status").dataset.kind = kind; }
function displaySelection() {
  selected = new Set(confirmedSelection);
  for (const choice of pendingChoices) {
    for (const code of choice.remove) selected.delete(code);
    for (const code of choice.add) selected.add(code);
  }
  for (const check of $("items").querySelectorAll("input")) check.checked = selected.has(check.dataset.code);
  update();
}
function receiveSelection(state) {
  if (!state || state.pageUrl !== snapshot.pageUrl || state.documentKey !== snapshot.documentKey || state.revision < selectionRevision) return;
  selectionRevision = state.revision; confirmedSelection = state.codes; displaySelection();
}
function changeSelection(add = [], remove = []) {
  const choice = { add, remove }, page = { pageUrl: snapshot.pageUrl, documentKey: snapshot.documentKey, kindHint: snapshot.kind };
  pendingChoices.push(choice); displaySelection();
  selectionWrite = selectionWrite.catch(() => {}).then(async () => {
    try {
      const response = await chrome.runtime.sendMessage({ kind: "selection-change", sourceTabId, ...page, add, remove });
      if (!response?.ok) throw new Error(response?.error || "勾选失败，请刷新后重试。");
      receiveSelection(response.state);
    } catch (error) { status(error.message, "error"); }
    finally { pendingChoices = pendingChoices.filter(item => item !== choice); displaySelection(); }
  });
  return selectionWrite;
}
function update() {
  const locked = busy || scanning || restoringMarks;
  $("count").textContent = `已加载 ${snapshot.items.length} 个番号 · 已选 ${chosen().length} 个 · 显示 ${visible().length} 个`;
  for (const id of ["copy", "txt", "json"]) $(id).disabled = locked || !chosen().length;
  $("save").disabled = locked || !chosen().length || !connected || !$("library").value;
  $("save").textContent = busy ? "正在保存资料和图片，请稍候…" : `保存所选 ${chosen().length} 个番号`;
  for (const id of ["library", "rescan", "all", "none", "connect", "apply", "port", "token", "search"]) $(id).disabled = locked;
  for (const check of $("items").querySelectorAll("input")) check.disabled = locked;
  for (const check of $("marks").querySelectorAll("input")) check.disabled = locked;
  $("markToggle").disabled = locked || !connected || !markImport;
  $("clearMarks").disabled = locked || !selectedMarks.size;
}
function renderMarks() {
  $("marks").replaceChildren();
  for (const mark of marks) {
    const row = document.createElement("label");
    const check = document.createElement("input"); check.type = "checkbox"; check.checked = selectedMarks.has(mark.id);
    check.setAttribute("aria-label", mark.name);
    check.addEventListener("change", () => {
      if (check.checked && selectedMarks.size >= 100) { check.checked = false; status("一次最多选择 100 个标记。", "error"); return; }
      check.checked ? selectedMarks.add(mark.id) : selectedMarks.delete(mark.id); rememberMarks(); updateMarkCount(); update();
    });
    const text = document.createElement("span"); text.textContent = mark.name; row.append(check, text); $("marks").append(row);
  }
  $("markHint").textContent = !connected ? "连接后自动读取 Jvedio“筛选器 → 标记”。" : !markImport ? "当前桌面端不支持筛选器标记导入，请更新本次配套 EXE 后重新连接。" :
    !marks.length ? "主程序“筛选器 → 标记”暂无标记；新增后点击连接刷新。" : "来自主程序筛选器的全部标记，可多选；选择用于本库后续保存。";
  updateMarkCount(); update();
}
function updateMarkCount() {
  const names = marks.filter(mark => selectedMarks.has(mark.id)).map(mark => mark.name).join("、");
  $("markCount").textContent = names || "不添加标记";
  $("markToggle").title = names || "下拉选择 Jvedio 筛选器已有标记（可多选）";
}
function closeMarks() { $("markMenu").hidden = true; $("markToggle").setAttribute("aria-expanded", "false"); }
function rememberMarks() {
  if (!markImport || !$("library").value) return markWrite;
  const state = { [`markIds:${$("library").value}`]: [...selectedMarks] };
  markWrite = markWrite.catch(() => {}).then(() => chrome.storage.local.set(state));
  return markWrite;
}
async function restoreMarks() {
  restoringMarks = true; selectedMarks.clear(); closeMarks(); renderMarks();
  const libraryId = $("library").value;
  try {
    await markWrite;
    const library = libraries.find(item => String(item.id) === libraryId);
    if (!markImport || !library) return;
    const key = `markIds:${libraryId}`, stored = (await chrome.storage.local.get(key))[key];
    if ($("library").value !== libraryId) return;
    const available = new Set(marks.map(mark => mark.id));
    selectedMarks = new Set((Array.isArray(stored) ? stored : []).filter(id => Number.isSafeInteger(id) && available.has(id)).slice(0, 100));
    await rememberMarks();
  } finally { restoringMarks = false; renderMarks(); }
}
function render() {
  $("items").replaceChildren();
  for (const item of visible()) {
    const row = document.createElement("label"); row.className = "item";
    const check = document.createElement("input"); check.type = "checkbox"; check.checked = selected.has(item.code);
    check.dataset.code = item.code;
    check.setAttribute("aria-label", `选择 ${item.code}`);
    check.addEventListener("change", () => { check.checked ? changeSelection([item.code]) : changeSelection([], [item.code]); });
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
    for (const [mark, value] of [["发行", info.releaseDate], ["时长", info.duration ? `${info.duration} 分钟` : ""], ["导演", info.director], ["片商", info.studio], ["发行商", info.publisher], ["系列", info.series?.join("、")], ["演员", info.actors?.map(actor => actor.name).join("、")], ["类别", info.genres?.join("、")], ["评分", info.rating ? `${info.rating} / 5` : ""], ["图片", info.coverUrl ? `封面 · ${info.previewUrls?.length || 0} 张预览图` : ""]]) {
      if (!value) continue;
      const row = document.createElement("p"); row.textContent = `${mark}：${value}`; $("metadata").append(row);
    }
  }
}
async function scan(reset = false) {
  if (scanning || busy) return;
  scanning = true; update();
  try {
    const requested = Number(new URL(location.href).searchParams.get("tabId"));
    const tab = (sourceTabId || requested > 0) ? await chrome.tabs.get(sourceTabId || requested) : (await chrome.tabs.query({ active: true, currentWindow: true }))[0];
    if (!tab?.id || !/^https?:/.test(tab.url || "")) throw new Error("请在网站页面使用插件。Chrome 内部页面无法剪藏。");
    sourceTabId = tab.id;
    await selectionWrite;
    pagePort?.disconnect(); pagePort = null;
    await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ["extractor.js", "card-buttons.js", "watch.js"] });
    const [response] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: () => ({
      ...globalThis.JvedioExtractor.extract(), documentKey: String(performance.timeOrigin)
    }) });
    snapshot = response.result;
    confirmedSelection = []; pendingChoices = []; selected.clear(); selectionRevision = -1;
    const selection = await chrome.runtime.sendMessage({ kind: "selection-read", sourceTabId, pageUrl: snapshot.pageUrl,
      documentKey: snapshot.documentKey, kindHint: snapshot.kind, viewKey, reset: reset === true });
    if (!selection?.ok) throw new Error(selection?.error || "读取勾选状态失败。");
    receiveSelection(selection.state);
    $("source").textContent = `${snapshot.site || "未支持的页面"} · ${snapshot.pageTitle || tab.title || ""}`;
    $("source").title = snapshot.pageUrl; render();
    if (chrome.tabs.connect) {
      const port = chrome.tabs.connect(tab.id, { name: "jvedio-page" }); pagePort = port;
      port.onMessage.addListener(next => {
        if (pagePort !== port) return;
        if (busy) pendingSnapshot = next;
        else applySnapshot(next);
      });
      port.onDisconnect.addListener(() => {
        if (pagePort === port) { pagePort = null; status("来源页面已关闭或跳转，请点击刷新重新读取。"); }
      });
    }
  } catch (error) { snapshot = { items: [] }; selected.clear(); render(); status(error.message, "error"); }
  finally { scanning = false; update(); }
}
function applySnapshot(next) {
  if (JSON.stringify(next) === JSON.stringify(snapshot)) return;
  const offset = $("items").scrollTop;
  if (next.pageUrl !== snapshot.pageUrl || next.documentKey !== snapshot.documentKey) { scan(); return; }
  snapshot = next;
  $("source").textContent = `${snapshot.site || "未支持的页面"} · ${snapshot.pageTitle || ""}`;
  $("source").title = snapshot.pageUrl;
  render(); $("items").scrollTop = offset;
}
async function connect() {
  connected = false; markImport = false; libraries = []; marks = []; selectedMarks.clear(); closeMarks(); $("library").replaceChildren(); renderMarks();
  try {
    const response = await chrome.runtime.sendMessage({ kind: "status" });
    if (!response?.ok) throw new Error(response?.error || "连接失败。");
    const remembered = (await chrome.storage.local.get("libraryId")).libraryId;
    libraries = response.result.libraries;
    markImport = response.result.markImport === true && response.result.markImportVersion >= 1;
    marks = Array.isArray(response.result.marks) ? response.result.marks : [];
    for (const library of libraries) {
      const option = document.createElement("option"); option.value = library.id; option.textContent = library.name; $("library").append(option);
    }
    const preferred = remembered ?? response.result.currentLibraryId;
    if ([...$("library").options].some(option => option.value === String(preferred))) $("library").value = String(preferred);
    connected = true;
    await restoreMarks();
    status(response.result.libraries.length ? "已连接 Jvedio，请选择要保存的影片。" : "请先在 Jvedio 中创建影片库。", "success");
  } catch (error) { status(error.message, "error"); }
  renderMarks();
}
function exportFile(format) {
  const payload = { version: 1, site: snapshot.site, kind: snapshot.kind, pageUrl: snapshot.pageUrl, pageTitle: snapshot.pageTitle, capturedAt: new Date().toISOString(), markIds: [...selectedMarks], marks: marks.filter(mark => selectedMarks.has(mark.id)), items: chosen() };
  const blob = new Blob([format === "txt" ? chosen().map(item => item.code).join("\r\n") + "\r\n" : JSON.stringify(payload, null, 2)], { type: format === "txt" ? "text/plain;charset=utf-8" : "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob), anchor = document.createElement("a");
  anchor.href = url; anchor.download = `Jvedio-${new Date().toISOString().slice(0, 10)}.${format}`;
  anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 10000); status(`已导出 ${chosen().length} 个番号。`, "success");
}
$("search").addEventListener("input", render);
$("all").addEventListener("click", () => changeSelection(visible().map(item => item.code)));
$("none").addEventListener("click", () => changeSelection([], visible().map(item => item.code)));
$("rescan").addEventListener("click", () => scan(true));
$("connect").addEventListener("click", connect);
$("library").addEventListener("change", () => { chrome.storage.local.set({ libraryId: $("library").value }); restoreMarks(); });
$("markToggle").addEventListener("click", () => {
  $("markMenu").hidden = !$("markMenu").hidden;
  $("markToggle").setAttribute("aria-expanded", String(!$("markMenu").hidden));
});
$("marksDone").addEventListener("click", closeMarks);
$("clearMarks").addEventListener("click", () => { selectedMarks.clear(); rememberMarks(); closeMarks(); renderMarks(); });
document.addEventListener("click", event => { if (!$("markPicker").contains(event.target)) closeMarks(); });
document.addEventListener("keydown", event => { if (event.key === "Escape" && !$("markMenu").hidden) { closeMarks(); $("markToggle").focus(); } });
$("apply").addEventListener("click", async () => {
  const port = Number($("port").value), token = $("token").value.trim();
  if (!Number.isInteger(port) || port < 1024 || port > 65535 || !/^[a-f0-9]{64}$/i.test(token)) { status("请输入有效端口和 Jvedio 提供的 64 位连接密钥。", "error"); return; }
  await chrome.storage.local.set({ port, token }); await connect();
});
$("copy").addEventListener("click", async () => {
  try { await navigator.clipboard.writeText(chosen().map(item => item.code).join("\n")); status(`已复制 ${chosen().length} 个番号。`, "success"); }
  catch { status("复制失败，请使用导出 TXT。", "error"); }
});
for (const format of ["txt", "json"]) $(format).addEventListener("click", () => exportFile(format));
async function save() {
  if (busy || scanning || restoringMarks || !connected || !$("library").value || !chosen().length) return;
  busy = true; closeMarks(); update(); status("正在保存所选影片和标记…");
  try {
    await selectionWrite; await rememberMarks();
    const payload = { libraryId: Number($("library").value), site: snapshot.site, kind: snapshot.kind, pageUrl: snapshot.pageUrl, markIds: [...selectedMarks], items: chosen() };
    const response = await chrome.runtime.sendMessage({ kind: "save", payload, sourceTabId, libraryName: $("library").selectedOptions[0].textContent });
    if (!response?.ok) throw new Error(response?.error || "保存失败。");
    await chrome.storage.local.set({ libraryId: $("library").value });
    const pictures = response.result.pictures;
    const markNote = selectedMarks.size ? ` 已为 ${response.result.markedItems} 部影片添加标记：${response.result.markNames.join("、")}。` : "";
    const pictureNote = pictures ? ` 图片完成 ${pictures.succeeded} 项${pictures.failed ? `，失败 ${pictures.failed} 项（${pictures.error}）` : ""}。${pictures.unavailableActors ? ` ${pictures.unavailableActors} 位演员未找到可用头像链接。` : ""}` : "";
    status(`已保存到「${$("library").selectedOptions[0].textContent}」：新增 ${response.result.added} 个，补全 ${response.result.updated || 0} 个，跳过已有 ${response.result.skipped} 个。${markNote}${pictureNote}${response.result.mediaQueued || response.result.imagesQueued ? " 视频已加入下载任务。" : ""}${response.result.mediaAlreadyQueued ? " 已有媒体任务正在进行，完成后再次剪藏可应用新的保存选项。" : ""}${response.result.videoNote || ""}`, pictures?.failed ? "error" : "success");
  } catch (error) { status(error.message, "error"); }
  finally { busy = false; if (pendingSnapshot) { applySnapshot(pendingSnapshot); pendingSnapshot = null; } update(); }
}
$("save").addEventListener("click", save);
async function showCaptureNotice() {
  if (!sourceTabId) return;
  const key = 'capture:' + sourceTabId;
  const notice = (await chrome.storage.session.get(key))[key];
  if (!notice || (snapshot.pageUrl && notice.pageUrl !== snapshot.pageUrl)) return;
  if (notice.selectDetail && snapshot.kind === "detail") {
    await changeSelection(snapshot.items.map(item => item.code)); render();
    await chrome.storage.session.remove(key);
  }
  status(notice.message, notice.kind);
}
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (!message || sender.id !== chrome.runtime.id || message.sourceTabId !== sourceTabId) return;
  if (message.kind === "selection-changed") { receiveSelection(message.state); return; }
  if (message.kind !== "refresh-capture-window") return;
  if (busy || scanning) { respond({ ok: false }); return; }
  (async () => { await scan(); await showCaptureNotice(); })().then(() => respond({ ok: true }), () => respond({ ok: false }));
  return true;
});
(async () => {
  const settings = await chrome.storage.local.get({ port: 18888, token: "", libraryId: "" });
  $("port").value = settings.port; $("token").value = settings.token;
  if (!settings.token) $("settings").open = true;
  await scan();
  if (settings.token) await connect();
  await showCaptureNotice();
})();
