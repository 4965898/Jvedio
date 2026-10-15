"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
let JSDOM;
try { ({ JSDOM } = require("jsdom")); }
catch { ({ JSDOM } = require("../../build-output/clipper-tests/node_modules/jsdom")); }
const root = path.resolve(__dirname, "..");
const extractor = fs.readFileSync(path.join(root, "extractor.js"), "utf8");
const popup = fs.readFileSync(path.join(root, "popup.js"), "utf8");
const testMarks = [{ id: 21001, name: "AV" }, { id: 21002, name: "套路直播" }, { id: 21003, name: "其他" }];
const cases = [];
function test(name, fn) { cases.push({ name, fn }); }
function extract(html, url = "https://mirror.example/list") {
  const dom = new JSDOM(html, { url, runScripts: "outside-only" });
  dom.window.eval(extractor);
  const result = JSON.parse(JSON.stringify(dom.window.JvedioExtractor.extract()));
  dom.window.close(); return result;
}
function backgroundHarness(context) {
  context.chrome.tabs ??= {};
  context.chrome.tabs.sendMessage ??= async () => {};
  context.chrome.tabs.onUpdated ??= { addListener() {} };
  context.chrome.windows.onRemoved ??= { addListener() {} };
  if (context.chrome.storage.session) context.chrome.storage.session.remove ??= async () => {};
  context.chrome.contextMenus ??= { removeAll: callback => callback(), create() {}, onClicked: { addListener() {} } };
  context.chrome.runtime.onInstalled ??= { addListener() {} };
  context.chrome.runtime.sendMessage ??= async () => ({ ok: true });
  for (const name of ["setBadgeText", "setBadgeBackgroundColor", "setTitle"]) context.chrome.action[name] ??= async () => {};
  context.chrome.scripting ??= { executeScript: async args => args.files ? [] : [{ result: extract(busHtml) }] };
  Object.assign(context, { TextEncoder, setTimeout, clearTimeout, AbortController });
  vm.runInNewContext(fs.readFileSync(path.join(root, "background.js"), "utf8"), context);
}
const busHtml = `<title>列表</title><a class="movie-box" href="/ABP-001"><img title="A &amp; B"><div class="photo-info"><date>abp-001</date><date>2026-10-07</date></div></a><a class="movie-box" href="/ABP-001"><div class="photo-info"><date>ABP-001</date></div></a><a class="movie-box" href="/FC2-PPV-1234567"><div class="photo-info"><date>FC2 PPV 1234567</date></div></a>`;
test("JavBus fields, duplicate removal, leading zeros and FC2", () => {
  const result = extract(busHtml);
  assert.equal(result.site, "JavBus");
  assert.deepEqual(result.items.map(item => item.code), ["ABP-001", "FC2-PPV-1234567"]);
  assert.equal(result.items[0].title, "A & B"); assert.equal(result.items[0].url, "https://mirror.example/ABP-001");
});
test("JavDB mirror, relative link and title fallback", () => {
  const result = extract(`<div class="movie-list"><div class="item"><a class="box" href="/v/abc"><div class="video-title"><strong>300MIUM-123</strong> 示例标题</div></a></div></div>`);
  assert.equal(result.site, "JavDB"); assert.equal(result.items[0].code, "300MIUM-123"); assert.equal(result.items[0].title, "示例标题");
});
test("JAVLibrary uses identifier instead of opaque page ID", () => {
  const result = extract(`<div class="videothumblist"><div class="video" id="vid_opaque"><a href="./javabc.html"><div class="id">SSIS-012</div><div class="title">中文标题</div></a><a class="icn_want" id="opaque"></a></div></div>`, "https://mirror.example/cn/main.php");
  assert.equal(result.items[0].code, "SSIS-012"); assert.equal(result.items[0].url, "https://mirror.example/cn/javabc.html");
});
test("all three detail-page adapters", () => {
  assert.equal(extract(`<div id="video_id"><span class="text">ABP-002</span></div>`).items[0].code, "ABP-002");
  assert.equal(extract(`<div class="video-detail"><div class="video-meta-panel"><div class="panel-block"><strong>番號:</strong><span class="value">ABP-003</span></div></div></div>`).items[0].code, "ABP-003");
  assert.equal(extract(`<div class="movie"><div class="info"><p><span class="header">識別碼:</span> ABP-004</p></div></div>`).items[0].code, "ABP-004");
});
test("JavBus tolerates translated identifiers, copy buttons and spacing; title and movie URL remain usable", () => {
  const html = `<title>GVG-107 标题 - JavBus</title><h3>GVG-107 标题</h3><a class="bigImage" href="/cover.jpg"></a><div class="info"><p><span class="header">识别代码:</span><span> GVG - 107 </span><button>复制</button></p></div>`;
  const result = extract(html, "https://www.javbus.com/GVG-107");
  assert.equal(result.items[0].code, "GVG-107"); assert.equal(result.items[0].title, "标题");
  assert.equal(extract(html.replace("识别代码", "Unknown"), "https://www.javbus.com/GVG-107").items[0].code, "GVG-107");
  assert.equal(extract(`<title>GVG-107 标题 - JavBus</title><h3>GVG-107 标题</h3>`, "https://www.javbus.com/GVG-107").items[0].code, "GVG-107");
});
test("JAVLibrary title works without a bookmark anchor and falls back to the document title", () => {
  const base = `<title>GVG-107 口淫処理 本田莉子 - JAVLibrary</title><div id="video_id"><span class="text">GVG-107</span></div>`;
  assert.equal(extract(base + `<div id="video_title"><h3>GVG-107 口淫処理 本田莉子</h3><button>订正</button></div>`).items[0].title, "口淫処理 本田莉子");
  assert.equal(extract(base).items[0].title, "口淫処理 本田莉子");
});
test("JavDB captures available trailer sources and its lazy preview URL endpoint", () => {
  const html = `<div class="video-detail"><div class="video-meta-panel"><div class="panel-block"><strong>番號:</strong><span class="value">GVG-107</span></div></div></div><video id="preview-video" data-url="/v/wqmpe/preview_url"><source src="https://cdn.example/trailer.mp4"></video>`;
  const info = extract(html, "https://javdb.com/v/wqmpe").items[0].metadata;
  assert.equal(info.previewVideoEndpoint, "https://javdb.com/v/wqmpe/preview_url"); assert.deepEqual(info.previewVideoUrls, ["https://cdn.example/trailer.mp4"]);
});
test("unrelated dates, advertisements and opaque IDs do not become identifiers", () => {
  assert.equal(extract(`<p>ABP-001 2026-10-07</p><script>var id='ABP-002'</script><div class="movie-list"><div class="item"><a class="box"><strong>opaqueID</strong></a></div></div>`).items.length, 0);
});
test("unsafe source links are removed; field values stay plain text", () => {
  const result = extract(`<div class="videothumblist"><div class="video"><a href="javascript:alert(1)"><div class="id">ABP-003</div><div class="title">&lt;img onerror=alert(1)&gt;</div></a></div></div>`);
  assert.equal(result.items[0].url, ""); assert.match(result.items[0].title, /^<img/);
});
test("numeric uncensored IDs are accepted; dates are rejected", () => {
  const dom = new JSDOM("", { runScripts: "outside-only" }); dom.window.eval(extractor);
  const normalize = dom.window.JvedioExtractor.normalizeCode;
  assert.equal(normalize("123456_001"), "123456_001"); assert.equal(normalize("2026-10-07"), ""); assert.equal(normalize("abp—001"), "ABP-001");
  dom.window.close();
});
test("waterfall pages retain all loaded identifiers beyond one receiver batch", () => {
  const cards = Array.from({ length: 510 }, (_, i) => `<div class="video"><a><div class="id">TEST-${1000 + i}</div></a></div>`).join("");
  assert.equal(extract(`<div id="waterfall">${cards}</div>`).items.length, 510);
});

async function popupHarness(store = {}, html = busHtml) {
  const dom = new JSDOM(fs.readFileSync(path.join(root, "popup.html"), "utf8"), { url: "https://extension.test/popup.html", runScripts: "outside-only" });
  const captures = [], session = store.session || (store.session = {}), local = store.local || (store.local = {});
  const area = data => ({ get: async keys => typeof keys === "string" ? { [keys]: data[keys] } : { ...keys, ...data }, set: async entries => Object.assign(data, entries), remove: async key => { delete data[key]; } });
  store.service ||= quickCaptureHarness({ html, live: store, session, broadcast: message => store.onSelection?.(message, { id: "test" }) });
  dom.window.chrome = {
    storage: { local: area(local), session: area(session) },
    tabs: { query: async () => [{ id: 7, url: "https://mirror.example/list" }], get: async id => ({ id, url: "https://mirror.example/list" }),
      connect: () => ({ disconnect() {}, onMessage: { addListener: listener => { store.onPage = listener; } }, onDisconnect: { addListener() {} } }) },
    scripting: { executeScript: async args => {
      if (args.files) return [];
      if (args.args) {
        if (store.previewError) throw new Error("preview endpoint blocked");
        return [{ result: ["https://cdn.example/trailer.mp4"] }];
      }
      return [{ result: { ...extract(store.html || html, store.pageUrl), documentKey: store.documentKey || "source-doc" } }];
    } },
    runtime: { id: "test", onMessage: { addListener: listener => { store.onSelection = listener; } }, sendMessage: async message => {
      if (["selection-read", "selection-change"].includes(message.kind)) return { ok: true, state: await store.service.selection(message, message) };
      if (message.kind === "status") return { ok: true, result: { markImport: !store.oldReceiver, markImportVersion: store.oldReceiver ? 0 : 1, marks: store.catalog || testMarks, libraries: [{ id: 1, name: "片库一" }, { id: 2, name: "片库二" }], currentLibraryId: 2, media: store.media || {} } };
      captures.push(message.payload); return { ok: true, result: { added: message.payload.items.length, skipped: 0, markedItems: message.payload.markIds?.length ? message.payload.items.length : 0,
        markNames: (message.payload.markIds || []).map(id => testMarks.find(mark => mark.id === id)?.name) } };
    } }
  };
  dom.window.eval(popup);
  const flush = () => new Promise(resolve => setImmediate(resolve));
  await flush(); await flush();
  return { dom, captures, flush, get: id => dom.window.document.getElementById(id), select: code => store.service.card(code), clear: store.service.clear };
}
test("popup starts empty and saves only checked identifiers to the chosen library", async () => {
  const h = await popupHarness({ local: { token: "a".repeat(64) } });
  assert.equal(h.get("library").value, "2");
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 0); assert.equal(h.get("save").disabled, true);
  const check = h.get("items").querySelector("input"); check.checked = true; check.dispatchEvent(new h.dom.window.Event("change"));
  h.get("save").click(); await h.flush();
  assert.equal(h.captures.length, 1); assert.equal(h.captures[0].libraryId, 2);
  assert.deepEqual(h.captures[0].items.map(item => item.code), ["ABP-001"]);
  assert.match(h.get("status").textContent, /新增 1/); await h.flush(); h.dom.window.close();
});
test("search and filtered selection preserve hidden choices; reopening starts empty", async () => {
  const store = {}; const h = await popupHarness(store);
  h.get("all").click();
  h.get("search").value = "FC2"; h.get("search").dispatchEvent(new h.dom.window.Event("input")); h.get("none").click();
  assert.match(h.get("count").textContent, /已选 1/); await h.flush(); await h.flush(); h.dom.window.close();
  const reopened = await popupHarness(store);
  assert.match(reopened.get("count").textContent, /已选 0/); assert.equal(reopened.get("items").querySelectorAll("input:checked").length, 0);
  reopened.dom.window.close();
});
test("upgrading discards the old automatic select-all session", async () => {
  const h = await popupHarness({ session: { "selection:7": { pageUrl: "https://mirror.example/list", codes: ["ABP-001", "FC2-PPV-1234567"] } } });
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 0); await h.flush(); h.dom.window.close();
});
test("popup renders untrusted titles without creating HTML elements", async () => {
  const h = await popupHarness({}, busHtml.replace("A &amp; B", "&lt;img id=xss src=x onerror=alert(1)&gt;"));
  assert.equal(h.dom.window.document.getElementById("xss"), null); assert.match(h.get("items").textContent, /<img/); await h.flush(); h.dom.window.close();
});
test("copy/export remain available while the desktop receiver is disconnected", async () => {
  const h = await popupHarness();
  assert.equal(h.get("copy").disabled, true); h.get("all").click();
  assert.equal(h.get("save").disabled, true); assert.equal(h.get("copy").disabled, false); assert.equal(h.get("txt").disabled, false); assert.equal(h.get("json").disabled, false);
  await h.flush(); h.dom.window.close();
});
test("manual detail windows never save on opening, including legacy autoDetail settings", async () => {
  const html = '<div id="video_id"><span class="text">ABP-002</span></div><div id="video_title"><a>ABP-002 标题</a></div>';
  for (const autoDetail of [true, false, undefined]) {
    const h = await popupHarness({ local: { token: "a".repeat(64), libraryId: "1", autoDetail } }, html);
    assert.equal(h.captures.length, 0); assert.equal(h.get("save").disabled, true); await h.flush(); h.dom.window.close();
  }
});
test("detail fallback shows the error and selects the requested movie without saving again", async () => {
  const html = '<div id="video_id"><span class="text">ABP-002</span></div>';
  const h = await popupHarness({ session: { "capture:7": { pageUrl: "https://mirror.example/list", message: "请先选择目标影片库", kind: "error", selectDetail: true } } }, html);
  assert.equal(h.captures.length, 0); assert.equal(h.get("items").querySelectorAll("input:checked").length, 1);
  assert.match(h.get("status").textContent, /选择目标影片库/); await h.flush(); h.dom.window.close();
});
test("legacy card failure notices no longer replace list selection", async () => {
  const h = await popupHarness({ session: { "capture:7": { pageUrl: "https://mirror.example/list", message: "请先连接", kind: "error", selectCodes: ["FC2-PPV-1234567"] } } });
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 0); assert.equal(h.captures.length, 0); await h.flush(); h.dom.window.close();
});
test("detail pages do not capture recommended videos", () => {
  const result = extract(`<div id="video_id"><span class="text">GVG-107</span></div>${busHtml}`);
  assert.equal(result.kind, "detail"); assert.deepEqual(result.items.map(item => item.code), ["GVG-107"]);
});
test("background ignores untrusted senders and uses the authenticated loopback endpoint", async () => {
  let handler, captured;
  const context = { setTimeout, clearTimeout, AbortController, TypeError, Error, chrome: {
    action: { onClicked: { addListener() {} } }, windows: { onBoundsChanged: { addListener() {} } },
    storage: { local: { get: async () => ({ port: 18888, token: "a".repeat(64) }) } },
    runtime: { id: "test", getURL: file => `chrome-extension://test/${file}`, onMessage: { addListener: callback => { handler = callback; } } }
  }, fetch: async (url, options) => { captured = { url, options }; return { ok: true, json: async () => ({ app: "Jvedio", protocol: 1, libraries: [] }) }; } };
  backgroundHarness(context);
  assert.equal(handler({ kind: "status" }, { id: "evil", url: "https://example.com" }, () => assert.fail()), undefined);
  assert.equal(handler({ kind: "save-card", code: "ABP-001", pageUrl: "https://example.com/" }, { id: "test", url: "https://example.com/", tab: { id: 7 }, frameId: 1 }, () => assert.fail()), undefined);
  const response = await new Promise(resolve => handler({ kind: "status" }, { id: "test", url: "chrome-extension://test/popup.html" }, resolve));
  assert.equal(response.ok, true); assert.equal(captured.url, "http://127.0.0.1:18888/v1/status"); assert.equal(captured.options.headers["X-Jvedio-Token"], "a".repeat(64));
});
test("manifest uses the requested name and limits image access to supported sites and image hosts", () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(root, "manifest.json"), "utf8"));
  assert.equal(manifest.manifest_version, 3); assert.equal(manifest.name, "Jvedio Connector");
  assert.ok(manifest.host_permissions.includes("http://127.0.0.1/*")); assert.ok(!manifest.host_permissions.includes("<all_urls>"));
  assert.ok(manifest.host_permissions.includes("https://*.jdbstatic.com/*"));
  assert.deepEqual(manifest.content_scripts[0].matches, ["https://*.javbus.com/*", "https://*.javdb.com/*", "https://*.javlibrary.com/*"]);
  assert.ok(manifest.content_scripts[0].js.includes("card-buttons.js")); assert.ok(manifest.permissions.includes("activeTab"));
  for (const icon of Object.values(manifest.icons)) assert.ok(fs.existsSync(path.join(root, icon)));
});
const sampleDir = process.argv[2];
const fixtureDir = process.argv[3];
const { coverFixture } = require("./cover-fixture.cjs");
const coverBase = '<div class="movie-list h"><div class="item"><a class="box" href="/v/abc" title="测试"><div class="cover"><img src="/cover.jpg"></div><div class="video-title"><strong>ABP-001</strong> 测试</div><div class="meta">2026-10-15</div></a></div></div>';
test("larger-thumbnails hidden first page plus 240 rendered cards yields all 240 distinct movies", () => {
  const fixture = coverFixture(coverBase);
  const result = extract(fixture.html, "https://javdb.com/series/test");
  assert.equal(result.site, "JavDB"); assert.equal(result.items.length, 240);
  assert.equal(result.items[0].title, "瀑布流测试");
  assert.equal(result.items.at(-1).code, "WATER-240"); assert.ok(result.items.every(item => item.metadata.coverUrl));
  const gridOnly = extract(`<div id="grid-b">${fixture.cards.join("")}</div>`, "https://javdb.com/series/test");
  assert.equal(gridOnly.items.length, 240);
});
test("larger-thumbnails cards remain excluded from single-movie detail captures", () => {
  const result = extract('<div id="video_id"><span class="text">ABP-001</span></div>' + coverFixture(coverBase).html);
  assert.equal(result.kind, "detail"); assert.deepEqual(result.items.map(item => item.code), ["ABP-001"]);
});
test("mark dropdown loads names without typing and remembers multiple choices per library", async () => {
  const store = { local: { token: "a".repeat(64) } }, h = await popupHarness(store);
  assert.equal(h.get("markSearch"), null); assert.equal(h.get("markToggle").disabled, false);
  h.get("markToggle").click(); assert.equal(h.get("markMenu").hidden, false);
  for (const check of [...h.get("marks").querySelectorAll("input")].slice(0, 2)) { check.checked = true; check.dispatchEvent(new h.dom.window.Event("change")); }
  await h.flush(); assert.deepEqual(Array.from(store.local["markIds:2"]), [21001, 21002]);
  h.get("library").value = "1"; h.get("library").dispatchEvent(new h.dom.window.Event("change")); await h.flush();
  assert.equal(h.get("marks").querySelectorAll("input:checked").length, 0);
  h.get("library").value = "2"; h.get("library").dispatchEvent(new h.dom.window.Event("change")); await h.flush();
  assert.equal(h.get("marks").querySelectorAll("input:checked").length, 2);
  h.get("clearMarks").click(); await h.flush(); assert.deepEqual(Array.from(store.local["markIds:2"]), []); await h.flush(); h.dom.window.close();
});
test("old desktop receivers visibly disable mark selection", async () => {
  const h = await popupHarness({ oldReceiver: true, local: { token: "a".repeat(64) } });
  assert.equal(h.get("markToggle").disabled, true); assert.match(h.get("markHint").textContent, /更新本次配套/); await h.flush(); h.dom.window.close();
});
test("filter marks are global and include unused choices even when neither library has text labels", async () => {
  const h = await popupHarness({ local: { token: "a".repeat(64) } });
  assert.deepEqual([...h.get("marks").querySelectorAll("input")].map(input => input.getAttribute("aria-label")), ["AV", "套路直播", "其他"]);
  h.get("library").value = "1"; h.get("library").dispatchEvent(new h.dom.window.Event("change")); await h.flush();
  assert.equal(h.get("marks").querySelectorAll("input").length, 3); await h.flush(); h.dom.window.close();
});
test("legacy text label preferences are not guessed into mark IDs; renamed marks retain their selection", async () => {
  const old = await popupHarness({ local: { token: "a".repeat(64), "labels:2": ["AV"] } });
  assert.equal(old.get("marks").querySelectorAll("input:checked").length, 0); old.dom.window.close();
  const renamed = await popupHarness({ local: { token: "a".repeat(64), "markIds:2": [21001] }, catalog: testMarks.map(mark => ({ ...mark, name: mark.id === 21001 ? "AV（改名）" : mark.name })) });
  assert.equal(renamed.get("marks").querySelectorAll("input:checked").length, 1); assert.equal(renamed.get("markCount").textContent, "AV（改名）"); renamed.dom.window.close();
});
test("direct detail capture sends and confirms the remembered multiple marks", async () => {
  const h = quickCaptureHarness({ settings: { "markIds:2": [21001, 21002] } }); await h.run();
  assert.equal(h.windows.length, 0); assert.deepEqual(h.posts[0].markIds, [21001, 21002]);
  assert.match(h.titles.at(-1), /已为 1 部影片添加标记：AV、套路直播/);
});
test("tagged captures reject old receivers and deleted marks before submitting metadata", async () => {
  for (const options of [{ oldReceiver: true }, { settings: { "markIds:2": [999999] } }]) {
    const h = quickCaptureHarness({ settings: { "markIds:2": [21001] }, ...options }); await h.run();
    assert.equal(h.posts.length, 0); assert.equal(h.windows.length, 1); assert.equal(h.badges.at(-1), "!");
  }
});
test("a receiver that fails to acknowledge marks cannot produce a success badge", async () => {
  const h = quickCaptureHarness({ noMarkAck: true, settings: { "markIds:2": [21001] } }); await h.run();
  assert.equal(h.posts.length, 1); assert.equal(h.windows.length, 1); assert.equal(h.badges.at(-1), "!");
  assert.match(h.titles.at(-1), /未确认标记写入/);
});
function quickCaptureHarness(options = {}) {
  const session = options.session || {}, posts = [], windows = [], badges = [], titles = [], live = options.live || {};
  let menu, statusRequests = 0, resolutions = 0;
  const html = options.html || '<div class="video-detail"><div class="video-meta-panel"><div class="panel-block"><strong>番號:</strong><span class="value">ABP-003</span></div></div></div><video id="preview-video" data-url="/v/abc/preview_url"></video>';
  const context = { TypeError, Error, chrome: {
    runtime: { id: "test", getURL: file => `chrome-extension://test/${file}`, onMessage: { addListener() {} }, sendMessage: async message => options.broadcast?.(message) },
    action: { onClicked: { addListener() {} }, setBadgeText: async value => badges.push(value.text), setBadgeBackgroundColor: async () => {}, setTitle: async value => titles.push(value.title) },
    contextMenus: { removeAll: callback => callback(), create() {}, onClicked: { addListener: listener => { menu = listener; } } },
    storage: { local: { get: async defaults => ({ ...defaults, token: "a".repeat(64), libraryId: "2", ...options.settings }) },
      session: { get: async key => ({ [key]: session[key] }), set: async value => Object.assign(session, value) } },
    windows: { create: async value => { windows.push(value); return { id: 10 }; }, get: async () => ({ type: "popup", tabs: [{ id: 11 }] }), onBoundsChanged: { addListener() {} } },
    scripting: { executeScript: async args => {
      if (args.files) return [];
      if (args.args) { resolutions++; if (options.previewError) throw new Error("blocked"); return [{ result: ["https://cdn.example/trailer.mp4"] }]; }
      const pageUrl = live.pageUrl || "https://mirror.example/list", documentKey = live.documentKey || "source-doc";
      if (args.func.toString().includes("performance.timeOrigin")) return [{ result: { pageUrl, documentKey } }];
      return [{ result: extract(live.html || html, pageUrl) }];
    } }
  }, fetch: async (url, args) => {
    if (options.offline) throw new TypeError("offline");
    if (args.body) {
      posts.push(JSON.parse(args.body));
      if (options.saveError) return { ok: false, json: async () => ({ error: "保存失败测试" }) };
      return { ok: true, json: async () => ({ app: "Jvedio", protocol: 1, added: 1, marksApplied: options.noMarkAck ? 0 : posts.at(-1).markIds.length, markedItems: posts.at(-1).markIds.length ? posts.at(-1).items.length : 0, savedItems: [] }) };
    }
    statusRequests++;
    if (options.imageError && statusRequests > 1) throw new Error("图片请求失败测试");
    return { ok: true, json: async () => ({ app: "Jvedio", protocol: 1, markImport: !options.oldReceiver, markImportVersion: options.oldReceiver ? 0 : 1, marks: options.catalog || testMarks, libraries: [{ id: options.deletedLibrary ? 1 : 2, name: "目标影片库" }], media: { savePreviewVideos: !!options.videos } }) };
  } };
  backgroundHarness(context);
  const run = vm.runInNewContext("handleAction", context);
  const card = vm.runInNewContext("selectCard", context), selection = vm.runInNewContext("pageSelection", context), clear = vm.runInNewContext("clearPageSelection", context);
  return { session, posts, windows, badges, titles, run: () => run({ id: 7, url: "https://mirror.example/list" }), resolutions: () => resolutions,
    card: (code, pageUrl = live.pageUrl || "https://mirror.example/list", documentKey = live.documentKey || "source-doc") => card(7, { code, pageUrl, documentKey })
      .then(state => ({ ok: true, state }), error => ({ ok: false, error: error.message })),
    selection: (expected, change) => selection(7, expected, change), clear: () => clear(7),
    manual: () => menu({ menuItemId: "jvedio-choose-destination" }, { id: 7, url: "https://mirror.example/list" }) };
}
test("detail toolbar saves directly to the remembered library with a badge and no window", async () => {
  const h = quickCaptureHarness(); await h.run();
  assert.equal(h.windows.length, 0); assert.equal(h.posts.length, 1); assert.equal(h.posts[0].libraryId, 2);
  assert.deepEqual(h.posts[0].markIds, []); assert.equal(h.badges.at(-1), "✓"); assert.match(h.titles.at(-1), /ABP-003.*目标影片库/);
});
test("repeated clicks while a detail save is in progress share one operation", async () => {
  const h = quickCaptureHarness(); await Promise.all([h.run(), h.run(), h.run()]);
  assert.equal(h.posts.length, 1); assert.equal(h.windows.length, 0);
});
for (const [name, options, error] of [
  ["missing key", { settings: { token: "" } }, /连接密钥/],
  ["unselected library", { settings: { libraryId: "" } }, /选择目标影片库/],
  ["deleted library", { deletedLibrary: true }, /已不存在/],
  ["desktop offline", { offline: true }, /无法连接/],
  ["save rejected", { saveError: true }, /保存失败测试/]
]) test(`detail fallback opens a window for ${name} without silently choosing another library`, async () => {
  const h = quickCaptureHarness(options); await h.run();
  assert.equal(h.windows.length, 1); assert.equal(h.badges.at(-1), "!"); assert.equal(h.session["capture:7"].selectDetail, true);
  assert.match(h.session["capture:7"].message, error); if (!options.saveError) assert.equal(h.posts.length, 0);
});
test("image errors after a successful direct import show a warning without interrupting browsing", async () => {
  const h = quickCaptureHarness({ imageError: true }); await h.run();
  assert.equal(h.posts.length, 1); assert.equal(h.windows.length, 0); assert.equal(h.badges.at(-1), "!");
  assert.match(h.titles.at(-1), /图片请求失败测试/); assert.equal(h.session["capture:7"].kind, "warning");
});
test("a one-item list still opens the selection window; the manual menu never auto-saves", async () => {
  const h = quickCaptureHarness({ html: '<a class="movie-box" href="/ABP-001"><div class="photo-info"><date>ABP-001</date></div></a>' });
  await h.run(); assert.equal(h.windows.length, 1); assert.equal(h.posts.length, 0);
  const manual = quickCaptureHarness(); manual.manual(); await new Promise(resolve => setImmediate(resolve));
  assert.equal(manual.windows.length, 1); assert.equal(manual.posts.length, 0);
});
test("background preview resolution respects media settings and failures still save metadata", async () => {
  const enabled = quickCaptureHarness({ videos: true }); await enabled.run();
  assert.equal(enabled.resolutions(), 1); assert.deepEqual(enabled.posts[0].items[0].metadata.previewVideoUrls, ["https://cdn.example/trailer.mp4"]);
  const disabled = quickCaptureHarness(); await disabled.run(); assert.equal(disabled.resolutions(), 0);
  const failed = quickCaptureHarness({ videos: true, previewError: true }); await failed.run();
  assert.equal(failed.posts.length, 1); assert.equal(failed.windows.length, 0); assert.match(failed.titles.at(-1), /文字资料仍已保存/);
});
test("card buttons accumulate different films and never write to the receiver", async () => {
  const h = quickCaptureHarness({ html: busHtml, settings: { "markIds:2": [21001, 21002] } });
  const results = await Promise.all([h.card("ABP-001"), h.card("FC2-PPV-1234567"), h.card("ABP-001")]);
  assert.ok(results.every(result => result.ok)); assert.equal(h.windows.length, 0); assert.equal(h.posts.length, 0);
  assert.deepEqual(Array.from(h.session["selection:7"].codes), ["ABP-001", "FC2-PPV-1234567"]);
});

test("absent cards and switched pages are rejected without opening a window", async () => {
  for (const [code, url] of [["ABSENT-001", "https://mirror.example/list"], ["ABP-001", "https://mirror.example/other"]]) {
    const h = quickCaptureHarness({ html: coverBase }); const response = await h.card(code, url);
    assert.equal(response.ok, false); assert.equal(h.posts.length, 0); assert.equal(h.windows.length, 0);
  }
});

test("simultaneous card traffic queues loopback requests without exceeding three connections", async () => {
  let active = 0, peak = 0;
  const context = { chrome: { action: { onClicked: { addListener() {} } }, windows: { onBoundsChanged: { addListener() {} } },
    runtime: { id: "test", getURL: value => value, onMessage: { addListener() {} } },
    storage: { local: { get: async () => ({ port: 18888, token: "a".repeat(64) }) } } },
    fetch: async () => {
      active++; peak = Math.max(peak, active); await new Promise(resolve => setTimeout(resolve, 10)); active--;
      return { ok: true, json: async () => ({ app: "Jvedio", protocol: 1 }) };
    } };
  backgroundHarness(context); const request = vm.runInNewContext("request", context);
  await Promise.all(Array.from({ length: 12 }, () => request("status"))); assert.equal(peak, 3); assert.equal(active, 0);
});
test("unconfigured cards can be picked without a window or a desktop connection", async () => {
  const h = quickCaptureHarness({ html: coverBase, settings: { token: "" } }); await h.card("ABP-001");
  assert.equal(h.posts.length, 0); assert.equal(h.windows.length, 0); assert.deepEqual(Array.from(h.session["selection:7"].codes), ["ABP-001"]);
});

test("card picks before opening and during window selection merge with checkbox choices", async () => {
  const store = { html: busHtml }, service = store.service = quickCaptureHarness({ html: busHtml, live: store, broadcast: message => store.onSelection?.(message, { id: "test" }) });
  await service.card("FC2-PPV-1234567");
  const h = await popupHarness(store);
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 1);
  const first = h.get("items").querySelector("input"); first.checked = true; first.dispatchEvent(new h.dom.window.Event("change"));
  await h.select("FC2-PPV-1234567"); await h.flush();
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 2); assert.equal(h.captures.length, 0);
  h.get("none").click(); await h.flush(); assert.equal(service.session["selection:7"].codes.length, 0);
  await h.flush(); h.dom.window.close();
});

test("reload, manual pagination and close clear choices; stale messages cannot restore them", async () => {
  const live = {}, h = quickCaptureHarness({ html: busHtml, live });
  await h.card("ABP-001"); live.documentKey = "reloaded";
  const read = () => h.selection({ pageUrl: live.pageUrl || "https://mirror.example/list", documentKey: live.documentKey });
  assert.equal((await read()).codes.length, 0);
  assert.equal((await h.card("ABP-001", "https://mirror.example/list", "source-doc")).ok, false);
  await h.card("ABP-001"); live.pageUrl = "https://mirror.example/list?page=2";
  assert.equal((await read()).codes.length, 0);
  await h.card("FC2-PPV-1234567"); await h.clear(); assert.equal((await read()).codes.length, 0);
});

test("all three native list layouts and the cover userscript receive buttons without changing extraction or accepting scripted clicks", () => {
  const samples = [busHtml, coverBase, '<div class="videothumblist"><div class="video"><a href="/?v=javabc"><div class="id">ABP-007</div><div class="title">测试</div></a></div></div>', coverFixture(coverBase, 45).html];
  for (const html of samples) {
    const dom = new JSDOM(html, { url: "https://mirror.example/list", runScripts: "outside-only" });
    let sent = 0; dom.window.chrome = { runtime: { id: "test", onMessage: { addListener() {} }, sendMessage: async message => { if (message.kind !== "read-card-selection") sent++; } } };
    dom.window.eval(extractor); const before = JSON.stringify(dom.window.JvedioExtractor.extract());
    const script = fs.readFileSync(path.join(root, "card-buttons.js"), "utf8"); dom.window.eval(script); dom.window.eval(script);
    const hosts = dom.window.document.querySelectorAll("[data-jvedio-card]"); assert.ok(hosts.length > 0);
    assert.equal(dom.window.document.querySelectorAll(".movie-list[style] [data-jvedio-card]").length, 0);
    hosts[0].shadowRoot.querySelector("button").click(); assert.equal(sent, 0);
    assert.equal(JSON.stringify(dom.window.JvedioExtractor.extract()), before); dom.window.close();
  }
});
test("waterfall selectors recognize reparented cards on all three sites", () => {
  const db = `<div id="waterfall"><div class="item"><a class="box" href="/v/abc"><div class="video-title"><strong>ABP-003</strong> 后续页</div><img data-src="/cover.jpg"></a></div></div>`;
  assert.equal(extract(db).items[0].code, "ABP-003");
  assert.equal(extract(`<div id="waterfall_h">${busHtml}</div>`).items.length, 2);
  assert.equal(extract(`<div class="videos" id="waterfall"><div class="video"><a href="/?v=abc"><div class="id">ABP-004</div></a></div></div>`).items[0].code, "ABP-004");
});
test("waterfall additions preserve choices and explicit refresh clears all choices", async () => {
  const store = {}, h = await popupHarness(store);
  const check = h.get("items").querySelector("input"); check.checked = true; check.dispatchEvent(new h.dom.window.Event("change"));
  store.html = busHtml + `<a class="movie-box" href="/ABP-005"><div class="photo-info"><date>ABP-005</date></div></a>`;
  store.onPage({ ...extract(store.html), documentKey: "source-doc" });
  assert.equal(h.get("items").querySelectorAll("input").length, 3);
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 1);
  h.get("rescan").click(); await h.flush(); await h.flush();
  assert.equal(h.get("items").querySelectorAll("input:checked").length, 0); await h.flush(); h.dom.window.close();
});
test("multiple marks are appended to one save and cleared when switching libraries", async () => {
  const h = await popupHarness({ local: { token: "a".repeat(64) } });
  h.get("all").click();
  for (const check of [...h.get("marks").querySelectorAll("input")].slice(0, 2)) { check.checked = true; check.dispatchEvent(new h.dom.window.Event("change")); }
  h.get("save").click(); await h.flush();
  assert.deepEqual(Array.from(h.captures[0].markIds), [21001, 21002]);
  assert.equal(h.captures[0].items.length, 2);
  h.get("library").value = "1"; h.get("library").dispatchEvent(new h.dom.window.Event("change")); await h.flush(); await h.flush();
  assert.equal(h.get("marks").querySelectorAll("input:checked").length, 0);
  assert.match(h.get("marks").textContent, /AV/); assert.match(h.get("marks").textContent, /其他/); await h.flush(); h.dom.window.close();
});
test("page observation follows DOM appends and stops when its window disconnects", async () => {
  const dom = new JSDOM(busHtml, { url: "https://mirror.example/list", runScripts: "outside-only" });
  let listener, disconnect; const messages = [];
  dom.window.chrome = { runtime: { onConnect: { addListener: value => { listener = value; } } } };
  dom.window.eval(extractor); const watch = fs.readFileSync(path.join(root, "watch.js"), "utf8"); dom.window.eval(watch); dom.window.eval(watch);
  listener({ name: "jvedio-page", postMessage: result => messages.push(result.items.length), disconnect() {}, onDisconnect: { addListener: value => { disconnect = value; } } });
  dom.window.document.body.insertAdjacentHTML("beforeend", `<a class="movie-box" href="/ABP-009"><div class="photo-info"><date>ABP-009</date></div></a>`);
  await new Promise(resolve => setTimeout(resolve, 480)); assert.deepEqual(messages, [2, 3]);
  disconnect(); dom.window.document.body.append(dom.window.document.createElement("div"));
  await new Promise(resolve => setTimeout(resolve, 480)); assert.deepEqual(messages, [2, 3]); dom.window.close();
});
test("toolbar opens a reusable, movable native popup bound to its source tab", async () => {
  let clicked; const session = {}, calls = [];
  const context = { chrome: { runtime: { id: "test", getURL: file => `chrome-extension://test/${file}`, onMessage: { addListener() {} } },
    action: { onClicked: { addListener: fn => { clicked = fn; } } },
    storage: { session: { get: async key => ({ [key]: session[key] }), set: async value => Object.assign(session, value) }, local: { get: async () => ({}) } },
    windows: { create: async options => { calls.push(options); return { id: 10 }; }, get: async () => ({ type: "popup", tabs: [{ id: 11 }] }),
      update: async id => calls.push(id), onBoundsChanged: { addListener() {} } } } };
  backgroundHarness(context);
  clicked({ id: 7, url: "https://mirror.example/list" }); await new Promise(resolve => setImmediate(resolve));
  assert.equal(calls[0].url, "chrome-extension://test/popup.html?tabId=7"); assert.equal(calls[0].type, "popup"); assert.equal(calls[0].width, 380);
  clicked({ id: 7, url: "https://mirror.example/list" }); await new Promise(resolve => setImmediate(resolve)); assert.equal(calls[1], 10);
});
test("background splits 501 selected cards into bounded transactions and retains marks", async () => {
  let handler; const batches = [];
  const context = { setTimeout, clearTimeout, AbortController, TextEncoder, TypeError, Error, chrome: {
    action: { onClicked: { addListener() {} } }, windows: { onBoundsChanged: { addListener() {} } },
    storage: { local: { get: async () => ({ port: 18888, token: "a".repeat(64) }) } },
    runtime: { id: "test", getURL: file => `chrome-extension://test/${file}`, onMessage: { addListener: fn => { handler = fn; } } }
  }, fetch: async (url, options) => {
    const body = options.body && JSON.parse(options.body); if (body) batches.push(body);
    return { ok: true, json: async () => ({ app: "Jvedio", protocol: 1, markImport: true, markImportVersion: 1, marks: testMarks, libraries: [{ id: 1 }], added: body?.items.length || 0, marksApplied: body?.markIds?.length || 0, markedItems: body?.markIds?.length ? body.items.length : 0, savedItems: [] }) };
  } };
  backgroundHarness(context);
  const response = await new Promise(resolve => handler({ kind: "save", payload: { libraryId: 1, markIds: [21001], items: Array.from({ length: 501 }, (_, i) => ({ code: `TEST-${i}` })) } },
    { id: "test", url: "chrome-extension://test/popup.html?tabId=7" }, resolve));
  assert.equal(response.ok, true); assert.equal(response.result.added, 501); assert.deepEqual(batches.map(batch => batch.items.length), [500, 1]);
  assert.deepEqual(batches[1].markIds, [21001]);
});
if (sampleDir) {
  for (const [filename, site, url, expected] of [["JAVBUS.txt", "JavBus", "https://www.javbus.com/series/j6r", 30], ["JAVDB.txt", "JavDB", "https://javdb.com/series/Bg74", 40], ["javlib主页.txt", "JAVLibrary", "https://www.javlibrary.com/cn/vl_update.php", 20], ["GVG-107 JAVBUS.txt", "JavBus", "https://www.javbus.com/GVG-107", 1], ["GVG-107 JAVDB.txt", "JavDB", "https://javdb.com/v/wqmpe", 1], ["GVG-107 JAVLIB.txt", "JAVLibrary", "https://www.javlibrary.com/cn/javliiib7m.html", 1]]) {
    test(`supplied complete HTML: ${filename}`, () => {
      const result = extract(fs.readFileSync(path.join(sampleDir, filename), "utf8"), url);
      assert.equal(result.site, site); assert.equal(result.items.length, expected);
      assert.equal(new Set(result.items.map(item => item.code)).size, result.items.length);
      assert.ok(result.items.every(item => item.url.startsWith("https://")));
      if (expected === 1) {
        assert.equal(result.kind, "detail"); assert.equal(result.items[0].code, "GVG-107");
        assert.match(result.items[0].title, /本田莉子/);
        const info = result.items[0].metadata;
        assert.equal(info.releaseDate, "2015-02-19"); assert.equal(info.duration, 160); assert.equal(info.director, "ひょん");
        assert.equal(info.actors[0].name, site === "JavDB" ? "仲里紗羽" : "本田莉子");
        assert.ok(info.genres.length >= 5); assert.ok(info.coverUrl.startsWith("https://"));
        assert.ok(!info.genres.includes("本田莉子"));
        if (site === "JavBus") assert.equal(info.genres.length, 7);
        if (site !== "JavBus") assert.equal(info.rating, 4);
        if (site === "JavDB") assert.equal(info.ratingCount, 27);
        if (site === "JAVLibrary") assert.deepEqual(info.actors[0].aliases, ["仲里紗羽"]);
      }
      if (expected > 1) assert.ok(result.items.every(item => item.metadata?.coverUrl?.startsWith("https://")));
      if (fixtureDir) { fs.mkdirSync(fixtureDir, { recursive: true }); fs.writeFileSync(path.join(fixtureDir, `${site}-${expected === 1 ? "detail" : "list"}.json`), JSON.stringify(result, null, 2)); }
      console.log(`  ${site}: ${result.items.length} identifiers, ${result.items[0].code} … ${result.items.at(-1).code}`);
    });
  }
}
(async () => {
  let failures = 0;
  for (const { name, fn } of cases) {
    try { await fn(); console.log(`PASS: ${name}`); }
    catch (error) { failures++; console.error(`FAIL: ${name}\n${error.stack}`); }
  }
  console.log(`${cases.length - failures}/${cases.length} checks passed`);
  process.exitCode = failures ? 1 : 0;
})();
