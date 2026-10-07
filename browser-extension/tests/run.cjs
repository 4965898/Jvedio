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
const cases = [];
function test(name, fn) { cases.push({ name, fn }); }
function extract(html, url = "https://mirror.example/list") {
  const dom = new JSDOM(html, { url, runScripts: "outside-only" });
  dom.window.eval(extractor);
  const result = JSON.parse(JSON.stringify(dom.window.JvedioExtractor.extract()));
  dom.window.close(); return result;
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
test("large pages are bounded at 500 identifiers", () => {
  const cards = Array.from({ length: 510 }, (_, i) => `<div class="video"><a><div class="id">TEST-${1000 + i}</div></a></div>`).join("");
  assert.equal(extract(`<div class="videothumblist">${cards}</div>`).items.length, 500);
});

async function popupHarness(store = {}, html = busHtml) {
  const dom = new JSDOM(fs.readFileSync(path.join(root, "popup.html"), "utf8"), { url: "https://extension.test/popup.html", runScripts: "outside-only" });
  const captures = [], session = store.session || (store.session = {}), local = store.local || (store.local = {});
  const area = data => ({ get: async keys => typeof keys === "string" ? { [keys]: data[keys] } : { ...keys, ...data }, set: async entries => Object.assign(data, entries) });
  dom.window.chrome = {
    storage: { local: area(local), session: area(session) },
    tabs: { query: async () => [{ id: 7, url: "https://mirror.example/list" }] },
    scripting: { executeScript: async args => {
      if (args.files) return [];
      if (args.args) {
        if (store.previewError) throw new Error("preview endpoint blocked");
        return [{ result: ["https://cdn.example/trailer.mp4"] }];
      }
      return [{ result: extract(html) }];
    } },
    runtime: { sendMessage: async message => {
      if (message.kind === "status") return { ok: true, result: { libraries: [{ id: 1, name: "片库一" }, { id: 2, name: "片库二" }], currentLibraryId: 2, media: store.media || {} } };
      captures.push(message.payload); return { ok: true, result: { added: message.payload.items.length, skipped: 0 } };
    } }
  };
  dom.window.eval(popup);
  const flush = () => new Promise(resolve => setImmediate(resolve));
  await flush(); await flush();
  return { dom, captures, flush, get: id => dom.window.document.getElementById(id) };
}
test("popup selects all by default and saves only checked identifiers to the chosen library", async () => {
  const h = await popupHarness({ local: { token: "a".repeat(64) } });
  assert.equal(h.get("library").value, "2");
  h.get("none").click(); assert.equal(h.get("save").disabled, true);
  const check = h.get("items").querySelector("input"); check.checked = true; check.dispatchEvent(new h.dom.window.Event("change"));
  h.get("save").click(); await h.flush();
  assert.equal(h.captures.length, 1); assert.equal(h.captures[0].libraryId, 2);
  assert.deepEqual(h.captures[0].items.map(item => item.code), ["ABP-001"]);
  assert.match(h.get("status").textContent, /新增 1/); h.dom.window.close();
});
test("search and filtered selection preserve hidden choices; selections survive reopening", async () => {
  const store = {}; const h = await popupHarness(store);
  h.get("search").value = "FC2"; h.get("search").dispatchEvent(new h.dom.window.Event("input")); h.get("none").click();
  assert.match(h.get("count").textContent, /已选 1/); await h.flush(); h.dom.window.close();
  const reopened = await popupHarness(store);
  assert.match(reopened.get("count").textContent, /已选 1/); assert.equal(reopened.get("items").querySelectorAll("input:checked").length, 1);
  reopened.dom.window.close();
});
test("popup renders untrusted titles without creating HTML elements", async () => {
  const h = await popupHarness({}, busHtml.replace("A &amp; B", "&lt;img id=xss src=x onerror=alert(1)&gt;"));
  assert.equal(h.dom.window.document.getElementById("xss"), null); assert.match(h.get("items").textContent, /<img/); h.dom.window.close();
});
test("copy/export remain available while the desktop receiver is disconnected", async () => {
  const h = await popupHarness();
  assert.equal(h.get("save").disabled, true); assert.equal(h.get("copy").disabled, false); assert.equal(h.get("txt").disabled, false); assert.equal(h.get("json").disabled, false);
  h.dom.window.close();
});
test("single-click detail capture uses the remembered library and can be disabled", async () => {
  const html = `<div id="video_id"><span class="text">ABP-002</span></div><div id="video_title"><a>ABP-002 标题</a></div>`;
  const h = await popupHarness({ local: { token: "a".repeat(64), libraryId: "1", autoDetail: true } }, html);
  await h.flush(); assert.equal(h.captures.length, 1); assert.equal(h.captures[0].libraryId, 1); assert.equal(h.captures[0].kind, "detail");
  assert.equal(h.captures[0].items[0].title, "标题"); h.dom.window.close();
  const manual = await popupHarness({ local: { token: "a".repeat(64), libraryId: "1", autoDetail: false } }, html);
  assert.equal(manual.captures.length, 0); assert.equal(manual.get("save").disabled, false); manual.dom.window.close();
});
test("detail pages do not capture recommended videos", () => {
  const result = extract(`<div id="video_id"><span class="text">GVG-107</span></div>${busHtml}`);
  assert.equal(result.kind, "detail"); assert.deepEqual(result.items.map(item => item.code), ["GVG-107"]);
});
test("preview video resolution respects desktop choices; a blocked endpoint still saves metadata", async () => {
  const html = `<div class="video-detail"><div class="video-meta-panel"><div class="panel-block"><strong>番號:</strong><span class="value">GVG-107</span></div></div></div><video id="preview-video" data-url="/v/wqmpe/preview_url"></video>`;
  const h = await popupHarness({ local: { token: "a".repeat(64), autoDetail: false }, media: { savePreviewVideos: true } }, html);
  h.get("save").click(); await h.flush(); await h.flush();
  assert.deepEqual(h.captures[0].items[0].metadata.previewVideoUrls, ["https://cdn.example/trailer.mp4"]); h.dom.window.close();
  const failed = await popupHarness({ local: { token: "a".repeat(64), autoDetail: false }, media: { savePreviewVideos: true }, previewError: true }, html);
  failed.get("save").click(); await failed.flush(); await failed.flush();
  assert.equal(failed.captures.length, 1); assert.match(failed.get("status").textContent, /文字资料仍已保存/); failed.dom.window.close();
});
test("background ignores untrusted senders and uses the authenticated loopback endpoint", async () => {
  let handler, captured;
  const context = { setTimeout, clearTimeout, AbortController, TypeError, Error, chrome: {
    storage: { local: { get: async () => ({ port: 18888, token: "a".repeat(64) }) } },
    runtime: { id: "test", getURL: file => `chrome-extension://test/${file}`, onMessage: { addListener: callback => { handler = callback; } } }
  }, fetch: async (url, options) => { captured = { url, options }; return { ok: true, json: async () => ({ app: "Jvedio", protocol: 1, libraries: [] }) }; } };
  vm.runInNewContext(fs.readFileSync(path.join(root, "background.js"), "utf8"), context);
  assert.equal(handler({ kind: "status" }, { id: "evil", url: "https://example.com" }, () => assert.fail()), undefined);
  const response = await new Promise(resolve => handler({ kind: "status" }, { id: "test", url: "chrome-extension://test/popup.html" }, resolve));
  assert.equal(response.ok, true); assert.equal(captured.url, "http://127.0.0.1:18888/v1/status"); assert.equal(captured.options.headers["X-Jvedio-Token"], "a".repeat(64));
});
test("manifest uses the requested name and limits image access to supported sites and image hosts", () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(root, "manifest.json"), "utf8"));
  assert.equal(manifest.manifest_version, 3); assert.equal(manifest.name, "Jvedio Connector");
  assert.ok(manifest.host_permissions.includes("http://127.0.0.1/*")); assert.ok(!manifest.host_permissions.includes("<all_urls>"));
  assert.ok(manifest.host_permissions.includes("https://*.jdbstatic.com/*"));
  assert.equal(manifest.content_scripts, undefined); assert.ok(manifest.permissions.includes("activeTab"));
  for (const icon of Object.values(manifest.icons)) assert.ok(fs.existsSync(path.join(root, icon)));
});
const sampleDir = process.argv[2];
const fixtureDir = process.argv[3];
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
