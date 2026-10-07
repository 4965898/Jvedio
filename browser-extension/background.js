"use strict";
const DEFAULT_PORT = 18888;
let imageRuleId = 20000;
let imageRulesReady;

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

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  // Only the extension popup may invoke the local receiver.
  if (sender.id !== chrome.runtime.id || sender.url !== chrome.runtime.getURL("popup.html")) return;
  if (!message || !["status", "save"].includes(message.kind)) return;
  (async () => {
    if (message.kind === "save") {
      const payload = message.payload;
      if (!payload || !Array.isArray(payload.items) || !payload.items.length || payload.items.length > 500)
        throw new Error("请选择 1–500 个番号。");
      payload.browserImages = true;
      const result = await request("clips", payload);
      result.pictures = await saveImages(payload, result);
      return result;
    }
    return request("status");
  })().then(result => respond({ ok: true, result }), error => respond({ ok: false, error: error.message }));
  return true;
});
