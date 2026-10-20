"use strict";
const DEFAULT_PORT = 18888;
let imageRuleId = 20000;
let imageRulesReady;
let openingWindow = Promise.resolve();
const capturesInProgress = new Map();
const MANUAL_MENU = "jvedio-choose-destination";
let receiverActive = 0;
const receiverWaiters = [];
const selectionQueues = new Map();

// One queue per source page merges card clicks and window checkboxes without lost updates.
function queueSelection(tabId, operation) {
  const previous = selectionQueues.get(tabId) || Promise.resolve();
  const next = previous.catch(() => {}).then(operation);
  selectionQueues.set(tabId, next);
  next.finally(() => { if (selectionQueues.get(tabId) === next) selectionQueues.delete(tabId); }).catch(() => {});
  return next;
}
async function publishSelection(tabId, state) {
  await Promise.allSettled([
    chrome.runtime.sendMessage({ kind: "selection-changed", sourceTabId: tabId, state }),
    chrome.tabs.sendMessage(tabId, { kind: "selection-changed", state }, { frameId: 0 }),
    ...(state.kind === "list" ? [chrome.action.setBadgeText({ tabId, text: state.codes.length ? String(state.codes.length) : "" }),
      chrome.action.setBadgeBackgroundColor({ tabId, color: "#5367da" }),
      chrome.action.setTitle({ tabId, title: `已勾选 ${state.codes.length} 部影片，点击图标后统一保存。` })] : [])
  ]);
}
function pageSelection(tabId, expected, change = {}) {
  return queueSelection(tabId, async () => {
    const [response] = await chrome.scripting.executeScript({ target: { tabId }, func: () => ({
      pageUrl: location.href, documentKey: String(performance.timeOrigin)
    }) });
    const page = response.result;
    if (page.pageUrl !== expected.pageUrl || page.documentKey !== expected.documentKey)
      throw new Error("页面已切换，请在当前页面重新勾选。");
    const key = `selection:${tabId}`, previous = (await chrome.storage.session.get(key))[key];
    const samePage = previous?.version === 3 && previous.pageUrl === page.pageUrl && previous.documentKey === page.documentKey;
    const state = { version: 3, ...page, kind: expected.kindHint || previous?.kind || "list", codes: samePage ? previous.codes : [],
      viewKey: samePage ? previous.viewKey : "", revision: (previous?.revision || 0) + 1 };
    // Reloading the selection window starts a new picking session. Its first opening keeps card picks.
    if (change.reset || (change.viewKey && state.viewKey && change.viewKey !== state.viewKey)) state.codes = [];
    if (change.viewKey) state.viewKey = change.viewKey;
    const codes = new Set(state.codes);
    for (const code of change.remove || []) codes.delete(code);
    for (const code of change.add || []) codes.add(code);
    // A repeated card click unchecks the same film; decided here so queued clicks cannot race.
    for (const code of change.toggle || []) codes.has(code) ? codes.delete(code) : codes.add(code);
    state.codes = [...codes];
    await chrome.storage.session.set({ [key]: state });
    await publishSelection(tabId, state);
    return state;
  });
}
function clearPageSelection(tabId) {
  return queueSelection(tabId, async () => {
    const key = `selection:${tabId}`, previous = (await chrome.storage.session.get(key))[key];
    if (!previous) return;
    const state = { ...previous, codes: [], viewKey: "", revision: (previous.revision || 0) + 1 };
    await chrome.storage.session.set({ [key]: state });
    await publishSelection(tabId, state);
  });
}

async function openClipper(tab) {
  if (!tab?.id || !/^https?:/.test(tab.url || "")) return;
  const key = `window:${tab.id}`, url = chrome.runtime.getURL(`popup.html?tabId=${tab.id}`);
  const existing = (await chrome.storage.session.get(key))[key];
  if (existing?.windowId && existing?.tabId) {
    try {
      const window = await chrome.windows.get(existing.windowId, { populate: true });
      // URLs can be omitted without the broad tabs permission; use the IDs we created.
      if (window.type === "popup" && window.tabs?.some(item => item.id === existing.tabId)) {
        await chrome.storage.session.set({ [`clipper-window:${window.id}`]: { tabId: existing.tabId, sourceTabId: tab.id } });
        await chrome.windows.update(existing.windowId, { focused: true });
        await chrome.runtime.sendMessage({ kind: "refresh-capture-window", sourceTabId: tab.id }).catch(() => {});
        return;
      }
    } catch { /* Closed windows are recreated on demand. */ }
  }
  const { clipperBounds } = await chrome.storage.local.get("clipperBounds");
  const bounds = clipperBounds && ["width", "height", "left", "top"].every(key => Number.isInteger(clipperBounds[key]))
    ? { width: Math.max(320, clipperBounds.width), height: Math.max(360, clipperBounds.height), left: clipperBounds.left, top: clipperBounds.top }
    : { width: 380, height: 560 };
  const window = await chrome.windows.create({ url, type: "popup", focused: true, ...bounds });
  const full = await chrome.windows.get(window.id, { populate: true });
  const tabId = full.tabs[0].id;
  await chrome.storage.session.set({ [key]: { windowId: window.id, tabId }, [`clipper-window:${window.id}`]: { tabId, sourceTabId: tab.id } });
}
chrome.action.onClicked.addListener(tab => {
  handleAction(tab).catch(() => {});
});
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.removeAll(() => chrome.contextMenus.create({ id: MANUAL_MENU, title: "选择影片库、标记、标签与连接设置…", contexts: ["action"] }));
});
chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === MANUAL_MENU) showClipper(tab).catch(() => {});
});
function showClipper(tab) {
  openingWindow = openingWindow.catch(() => {}).then(() => openClipper(tab));
  return openingWindow;
}
async function reportCapture(tabId, pageUrl, message, kind, selectDetail = false) {
  const badge = kind === "success" ? "✓" : kind === "pending" ? "…" : "!";
  const color = kind === "success" ? "#237950" : kind === "pending" ? "#5367da" : "#b36b12";
  // Closing the source tab must not turn a completed import into a reported failure.
  await Promise.allSettled([
    chrome.action.setBadgeText({ tabId, text: badge }), chrome.action.setBadgeBackgroundColor({ tabId, color }),
    chrome.action.setTitle({ tabId, title: `${message}\n右键图标可选择影片库、标记、标签与连接设置。` }),
    chrome.storage.session.set({ [`capture:${tabId}`]: { pageUrl, message, kind, selectDetail } })
  ]);
}
function captureMessage(payload, result, libraryName) {
  const pictures = result.pictures;
  return `${payload.items.length === 1 ? payload.items[0].code + "：" : ""}已保存到「${libraryName}」：新增 ${result.added} 个，补全 ${result.updated} 个，跳过已有 ${result.skipped} 个。` +
    ` 图片完成 ${pictures.succeeded} 项${pictures.failed ? `，失败 ${pictures.failed} 项（${pictures.error}）` : ""}。` +
    (pictures.unavailableActors ? ` ${pictures.unavailableActors} 位演员未找到可用头像链接。` : "") +
    (payload.markIds?.length ? ` 已为 ${result.markedItems} 部影片添加标记：${result.markNames.join("、")}。` : "") +
    (result.labelNames?.length ? ` 已为 ${result.labeledItems} 部影片添加标签：${result.labelNames.join("、")}。` : "") +
    (result.mediaQueued ? " 视频已加入下载任务。" : "") +
    (result.mediaAlreadyQueued ? " 已有媒体任务正在进行。" : "") + (result.videoNote || "");
}
function captureKind(result) {
  return result.pictures.failed || result.pictures.unavailableActors || result.videoNote ? "warning" : "success";
}
function handleAction(tab) {
  if (!tab?.id) return Promise.resolve();
  if (capturesInProgress.has(tab.id)) return capturesInProgress.get(tab.id);
  const capture = captureFromToolbar(tab).finally(() => capturesInProgress.delete(tab.id));
  capturesInProgress.set(tab.id, capture);
  return capture;
}
async function clipDestination() {
  const settings = await chrome.storage.local.get({ token: "", libraryId: "" });
  if (!settings.token) throw new Error("请先填写连接密钥，选择影片库并保存一次，之后点击详情页图标即可直接保存。");
  if (!settings.libraryId) throw new Error("请先选择目标影片库并保存一次，之后点击详情页图标即可直接保存。");
  const connection = await request("status");
  const library = connection.libraries.find(item => String(item.id) === String(settings.libraryId));
  if (!library) throw new Error("上次使用的影片库已不存在，请重新选择目标影片库。");
  const markKey = `markIds:${library.id}`, storedMarks = (await chrome.storage.local.get(markKey))[markKey];
  const labelKey = `labelNames:${library.id}`, storedLabels = (await chrome.storage.local.get(labelKey))[labelKey];
  return { library, connection, markIds: Array.isArray(storedMarks) ? storedMarks : [], labels: Array.isArray(storedLabels) ? storedLabels : [] };
}
async function selectCard(tabId, message) {
  await chrome.scripting.executeScript({ target: { tabId }, files: ["extractor.js"] });
  const [response] = await chrome.scripting.executeScript({ target: { tabId }, func: () => globalThis.JvedioExtractor.extract() });
  const snapshot = response.result;
  if (snapshot?.pageUrl !== message.pageUrl || snapshot.kind !== "list" || !snapshot.items.some(item => item.code === message.code))
    throw new Error("当前列表中未找到这个番号，请刷新页面后重试。");
  return pageSelection(tabId, { ...message, kindHint: "list" }, { toggle: [message.code] });
}
async function captureFromToolbar(tab) {
  let snapshot;
  try {
    if (!/^https?:/.test(tab.url || "")) return;
    await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ["extractor.js", "card-buttons.js"] });
    const [response] = await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: () => globalThis.JvedioExtractor.extract() });
    snapshot = response.result;
    if (snapshot?.kind !== "detail" || snapshot.items.length !== 1) {
      await chrome.action.setBadgeText({ tabId: tab.id, text: "" });
      await chrome.action.setTitle({ tabId: tab.id, title: "剪藏到 Jvedio：选择影片、影片库、标记与标签" });
      await showClipper(tab); return;
    }
    await reportCapture(tab.id, snapshot.pageUrl, `正在保存 ${snapshot.items[0].code}…`, "pending");
    const { library, connection, markIds, labels } = await clipDestination();
    const payload = { libraryId: Number(library.id), site: snapshot.site, kind: snapshot.kind, pageUrl: snapshot.pageUrl,
      markIds, labels, items: snapshot.items };
    const result = await saveClips(payload, tab.id, connection);
    await reportCapture(tab.id, snapshot.pageUrl, captureMessage(payload, result, library.name), captureKind(result));
  } catch (error) {
    await reportCapture(tab.id, snapshot?.pageUrl || tab.url, error.message, "error", snapshot?.kind === "detail");
    await showClipper(tab);
  }
}
chrome.windows.onBoundsChanged.addListener(async window => {
  try {
    const full = await chrome.windows.get(window.id, { populate: true });
    const known = (await chrome.storage.session.get(`clipper-window:${window.id}`))[`clipper-window:${window.id}`];
    if (full.type !== "popup" || !known || !full.tabs?.some(tab => tab.id === known.tabId)) return;
    if (full.state === "normal") await chrome.storage.local.set({ clipperBounds: {
      width: full.width, height: full.height, left: full.left, top: full.top
    } });
  } catch { /* The user may close the window during a bounds change. */ }
});
chrome.windows.onRemoved.addListener(windowId => {
  // Serialize closure and reopening so an old window cannot clear a new window's picks.
  openingWindow = openingWindow.catch(() => {}).then(async () => {
    const key = `clipper-window:${windowId}`, known = (await chrome.storage.session.get(key))[key];
    if (!Number.isInteger(known?.sourceTabId)) return;
    const sourceKey = `window:${known.sourceTabId}`, current = (await chrome.storage.session.get(sourceKey))[sourceKey];
    if (current?.windowId === windowId) {
      await clearPageSelection(known.sourceTabId);
      await chrome.storage.session.remove(sourceKey);
    }
    await chrome.storage.session.remove(key);
  });
});
chrome.tabs.onUpdated.addListener((tabId, change) => {
  if (change.status === "loading" || change.url) clearPageSelection(tabId).catch(() => {});
  if (change.status === "complete") chrome.runtime.sendMessage({ kind: "refresh-capture-window", sourceTabId: tabId }).catch(() => {});
});

async function actorPortrait(actor, pageUrl) {
  if (actor.imageUrl || !actor.url) return actor.imageUrl || "";
  const url = new URL(actor.url, pageUrl);
  if (url.origin !== new URL(pageUrl).origin) return "";
  const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 8000);
  try {
    const response = await fetch(url.href, { credentials: "include", signal: controller.signal });
    if (!response.ok) return "";
    const html = (await response.text()).slice(0, 1000000);
    const title = html.match(/<title[^>]*>([\s\S]*?)<\/title>/i)?.[1] || "";
    if (![actor.name, ...(actor.aliases || [])].some(name => title.includes(name))) return "";
    const section = html.match(/<(?:div|section)[^>]+(?:class|id)=["'][^"']*(?:actor-profile|actor-cover|actor-avatar|star_photo|star-photo|star-info|profile-photo)[^"']*["'][^>]*>([\s\S]*?)<\/(?:div|section)>/i)?.[1];
    const tag = section?.match(/<img\b[^>]*>/i)?.[0] || html.match(/<img\b[^>]*(?:class|id)=["'][^"']*(?:actor-avatar|actor-image|star_photo|profile-photo)[^"']*["'][^>]*>/i)?.[0];
    const src = tag?.match(/(?:data-src|src)=["']([^"']+)["']/i)?.[1];
    if (!src || /logo|placeholder|no[-_]image|\.svg(?:\?|$)/i.test(src)) return "";
    return new URL(src.replace(/&amp;/g, "&"), url).href;
  } catch { return ""; }
  finally { clearTimeout(timer); }
}

async function captureImage(task, pageUrl, libraryId) {
  const url = new URL(task.url);
  if (!/^https?:$/.test(url.protocol)) throw new Error("图片地址无效。");
  const referer = new URL(pageUrl);
  if (!imageRulesReady) imageRulesReady = chrome.declarativeNetRequest.getSessionRules().then(rules =>
    chrome.declarativeNetRequest.updateSessionRules({ addRules: [], removeRuleIds: rules.filter(rule => rule.id >= 20000 && rule.id < 100000).map(rule => rule.id) }));
  await imageRulesReady;
  const id = ++imageRuleId;
  const controller = new AbortController(), timeout = setTimeout(() => controller.abort(), 18000);
  try {
    await chrome.declarativeNetRequest.updateSessionRules({ addRules: [{ id, priority: 1,
      action: { type: "modifyHeaders", requestHeaders: [{ header: "Referer", operation: "set", value: referer.href }] },
      condition: { regexFilter: "^" + url.href.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$", initiatorDomains: [chrome.runtime.id], resourceTypes: ["xmlhttprequest"] }
    }], removeRuleIds: [] });
    const response = await fetch(url.href, { credentials: "include", signal: controller.signal });
    if (!response.ok) throw new Error(`图片请求被网站拒绝（${response.status}）。`);
    const blob = await response.blob();
    if (!blob.type.startsWith("image/") || blob.size > 8000000) throw new Error("图片格式或大小不受支持。");
    const bitmap = await createImageBitmap(blob);
    const scale = Math.min(1, 4096 / Math.max(bitmap.width, bitmap.height));
    const canvas = new OffscreenCanvas(Math.max(1, Math.round(bitmap.width * scale)), Math.max(1, Math.round(bitmap.height * scale)));
    const context = canvas.getContext("2d"); context.fillStyle = "white"; context.fillRect(0, 0, canvas.width, canvas.height); context.drawImage(bitmap, 0, 0, canvas.width, canvas.height); bitmap.close();
    const normalized = await canvas.convertToBlob({ type: "image/jpeg", quality: .93 });
    const bytes = new Uint8Array(await normalized.arrayBuffer()); let binary = "";
    for (let offset = 0; offset < bytes.length; offset += 32768) binary += String.fromCharCode(...bytes.subarray(offset, offset + 32768));
    return request("images", { libraryId, dataId: task.dataId, actorId: task.actorId || 0, kind: task.kind, index: task.index || 0, data: btoa(binary) });
  } finally {
    clearTimeout(timeout);
    await chrome.declarativeNetRequest.updateSessionRules({ addRules: [], removeRuleIds: [id] }).catch(() => {});
  }
}

async function saveImages(payload, result) {
  const status = await request("status"), media = status.media || {};
  const tasks = []; let unavailableActors = 0;
  for (const saved of result.savedItems || []) {
    const info = payload.items.find(item => item.code === saved.code)?.metadata;
    if (!info) continue;
    if (media.saveCoverImages && info.coverUrl) tasks.push({ kind: "cover", dataId: saved.id, url: info.coverUrl });
    if (media.savePreviewImages) (info.previewUrls || []).forEach((url, index) => tasks.push({ kind: "preview", dataId: saved.id, url, index }));
    if (media.saveActorImages) for (const savedActor of saved.actors || []) {
      const actor = info.actors?.find(actor => actor.name === savedActor.sourceName);
      const url = actor ? await actorPortrait(actor, payload.pageUrl) : "";
      if (url) tasks.push({ kind: "actor", dataId: saved.id, actorId: savedActor.id, url });
      else unavailableActors++;
    }
    if (media.saveActorImages) unavailableActors += Math.max(0, (info.actors?.length || 0) - (saved.actors?.length || 0));
  }
  let cursor = 0, succeeded = 0; const failures = [];
  await Promise.all(Array.from({ length: Math.min(3, tasks.length) }, async () => {
    while (cursor < tasks.length) {
      const task = tasks[cursor++];
      try { await captureImage(task, payload.pageUrl, payload.libraryId); succeeded++; }
      catch (error) { failures.push(`${task.kind === "actor" ? "演员图" : task.kind === "cover" ? "封面" : "预览图"}：${error.message}`); }
    }
  }));
  return { succeeded, failed: failures.length, unavailableActors, error: failures[0] || "" };
}

async function request(path, payload) {
  // Scattered card clicks share the receiver's bounded connection capacity.
  if (receiverActive < 3) receiverActive++;
  else await new Promise(resolve => receiverWaiters.push(resolve));
  try { return await receiverRequest(path, payload); }
  finally {
    const next = receiverWaiters.shift();
    if (next) next(); else receiverActive--;
  }
}
async function receiverRequest(path, payload) {
  const settings = await chrome.storage.local.get({ port: DEFAULT_PORT, token: "" });
  const port = Number(settings.port);
  if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error("连接端口应为 1024–65535。");
  if (!settings.token) throw new Error("请在连接设置中填入 Jvedio 的连接密钥。");
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 20000);
  try {
    const response = await fetch(`http://127.0.0.1:${port}/v1/${path}`, {
      method: payload ? "POST" : "GET",
      headers: { "Content-Type": "application/json", "X-Jvedio-Token": settings.token },
      body: payload ? JSON.stringify(payload) : undefined,
      signal: controller.signal,
      credentials: "omit",
      redirect: "error",
      cache: "no-store"
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || `Jvedio 返回错误（${response.status}）。`);
    if (result.app !== "Jvedio" || result.protocol !== 1) throw new Error("接收端不是兼容的 Jvedio 剪藏服务。");
    return result;
  } catch (error) {
    if (error.name === "AbortError") throw new Error("连接超时，请查看 Jvedio；重试时已有番号会自动跳过。");
    if (error instanceof TypeError) throw new Error("无法连接 Jvedio。请打开软件中的「浏览器剪藏」并开启接收。");
    throw error;
  } finally { clearTimeout(timeout); }
}

async function resolvePreviewVideos(payload, tabId, media) {
  if (payload.kind !== "detail" || !Number.isInteger(tabId)) return "";
  media ??= (await request("status")).media || {};
  if (!media.savePreviewVideos) return "";
  let note = "";
  for (const item of payload.items) {
    const info = item.metadata;
    if (info?.previewVideoEndpoint && !info.previewVideoUrls?.length) {
      try {
        const [resolution] = await chrome.scripting.executeScript({ target: { tabId }, args: [info.previewVideoEndpoint, payload.pageUrl], func: async (endpoint, expectedPage) => {
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
      } catch { note = " 预览视频地址暂时无法取得，文字资料仍已保存。"; }
    }
    if (!info?.previewVideoUrls?.length && !note) note = " 当前页面没有可保存的预览视频直链。";
  }
  return note;
}
async function saveClips(payload, tabId, connection) {
  if (!payload || !Array.isArray(payload.items) || !payload.items.length)
    throw new Error("请先选择番号。");
  if (payload.markIds != null && (!Array.isArray(payload.markIds) || payload.markIds.length > 100 || payload.markIds.some(id => !Number.isSafeInteger(id) || id <= 0)))
    throw new Error("无效的标记选择，请重新选择标记。");
  payload.markIds = [...new Set(payload.markIds || [])];
  if (payload.labels != null && (!Array.isArray(payload.labels) || payload.labels.length > 100 ||
    payload.labels.some(name => { const trimmed = typeof name === "string" ? name.trim() : "";
      return !trimmed.length || trimmed.length > 200 || /[\u0000-\u001f\u007f-\u009f]/.test(trimmed); })))
    throw new Error("无效的标签选择，请重新选择标签。");
  payload.labels = [...new Set((payload.labels || []).map(name => name.trim()))];
  payload.newLabels = [];
  let markNames = [], labelNames = [...payload.labels];
  if (payload.markIds.length || payload.labels.length) {
    connection ??= await request("status");
    if (payload.markIds.length && (connection.markImport !== true || !(connection.markImportVersion >= 1))) throw new Error("当前桌面接收端不支持筛选器标记导入，请更新本次配套桌面 EXE 后重新连接；本次尚未提交影片。");
    if (payload.labels.length && (connection.labelImport !== true || !(connection.labelImportVersion >= 3))) throw new Error("当前桌面接收端不支持清单标签导入，请更新本次配套桌面 EXE 后重新连接；本次尚未提交影片。");
    const library = connection.libraries.find(item => String(item.id) === String(payload.libraryId));
    if (!library) throw new Error("目标影片库已不存在，请重新连接并选择；本次尚未提交影片。");
    if (payload.markIds.length) {
      const catalog = new Map((connection.marks || []).map(mark => [mark.id, mark.name]));
      if (payload.markIds.some(id => !catalog.has(id))) throw new Error("所选标记已删除，请重新连接并选择标记；本次尚未提交影片。");
      markNames = payload.markIds.map(id => catalog.get(id));
    }
    if (payload.labels.length) {
      // Names already in the receiver's catalog validate as labels; the rest are declared new and created on save.
      const available = new Set(library.labels || []);
      payload.newLabels = payload.labels.filter(name => !available.has(name));
      payload.labels = payload.labels.filter(name => available.has(name));
    }
  }
  const videoNote = await resolvePreviewVideos(payload, tabId, connection?.media);
  payload.browserImages = true;
  // Keep each receiver transaction below its 500-item / 1 MB limits.
  const batches = []; let batch = [], bytes = 0;
  const envelopeBytes = new TextEncoder().encode(JSON.stringify({ ...payload, items: [] })).length;
  for (const item of payload.items) {
    const size = new TextEncoder().encode(JSON.stringify(item)).length + 1;
    if (size + envelopeBytes > 1000000) throw new Error("单部影片资料超过接收大小上限。");
    if (batch.length && (batch.length === 500 || bytes + size + envelopeBytes > 1000000)) { batches.push(batch); batch = []; bytes = 0; }
    batch.push(item); bytes += size;
  }
  if (batch.length) batches.push(batch);
  const total = { added: 0, updated: 0, skipped: 0, markedItems: 0, labeledItems: 0, imagesQueued: 0, mediaQueued: 0, mediaAlreadyQueued: 0,
    pictures: { succeeded: 0, failed: 0, unavailableActors: 0, error: "" } };
  let completed = 0;
  for (const items of batches) {
    try {
      const part = { ...payload, items }, result = await request("clips", part);
      if (payload.markIds.length && (result.marksApplied !== payload.markIds.length || result.markedItems !== items.length))
        throw new Error("影片资料可能已保存，但接收端未确认标记写入，请更新配套桌面 EXE 后重新连接再重试。");
      if (payload.labels.length + payload.newLabels.length && (result.labelsApplied !== payload.labels.length + payload.newLabels.length || result.labeledItems !== items.length))
        throw new Error("影片资料可能已保存，但接收端未确认标签写入，请更新配套桌面 EXE 后重新连接再重试。");
      completed += items.length;
      for (const key of ["added", "updated", "skipped", "markedItems", "labeledItems", "imagesQueued", "mediaQueued", "mediaAlreadyQueued"]) total[key] += result[key] || 0;
      try { result.pictures = await saveImages(part, result); }
      catch (error) { result.pictures = { failed: 1, error: error.message }; }
      for (const key of ["succeeded", "failed", "unavailableActors"]) total.pictures[key] += result.pictures[key] || 0;
      total.pictures.error ||= result.pictures.error;
    } catch (error) { throw new Error(`${completed ? `已完成 ${completed} 个番号；后续批次失败。` : ""}${error.message}`); }
  }
  return { ...total, videoNote, markNames, labelNames };
}
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (["select-card", "read-card-selection"].includes(message?.kind)) {
    if (sender.id !== chrome.runtime.id || !Number.isInteger(sender.tab?.id) || sender.frameId !== 0 ||
      !/^https?:\/\//.test(sender.url || "") || message.pageUrl !== sender.url || typeof message.documentKey !== "string") return;
    if (message.kind === "select-card" && (typeof message.code !== "string" || message.code.length > 40)) return;
    (message.kind === "select-card" ? selectCard(sender.tab.id, message) : pageSelection(sender.tab.id, message))
      .then(state => respond({ ok: true, state }), error => respond({ ok: false, error: error.message }));
    return true;
  }
  // Only the extension's selection window may invoke the local receiver.
  if (sender.id !== chrome.runtime.id || sender.url?.split("?")[0] !== chrome.runtime.getURL("popup.html")) return;
  if (["selection-read", "selection-change"].includes(message?.kind)) {
    if (!Number.isInteger(message.sourceTabId) || typeof message.pageUrl !== "string" || typeof message.documentKey !== "string" ||
      ![message.add || [], message.remove || []].every(codes => Array.isArray(codes) && codes.every(code => typeof code === "string" && code.length <= 40))) return;
    pageSelection(message.sourceTabId, message, message).then(state => respond({ ok: true, state }), error => respond({ ok: false, error: error.message }));
    return true;
  }
  if (!message || !["status", "save"].includes(message.kind)) return;
  (async () => {
    if (message.kind === "save") {
      const result = await saveClips(message.payload, message.sourceTabId);
      if (Number.isInteger(message.sourceTabId)) await reportCapture(message.sourceTabId, message.payload.pageUrl,
        captureMessage(message.payload, result, message.libraryName || `影片库 ${message.payload.libraryId}`), captureKind(result));
      return result;
    }
    return request("status");
  })().then(result => respond({ ok: true, result }), error => respond({ ok: false, error: error.message }));
  return true;
});
