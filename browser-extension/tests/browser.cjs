"use strict";
// Loads the real unpacked extension in a fresh browser profile and talks to the isolated WPF receiver.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const http = require("node:http");
const { chromium } = require(process.env.JVEDIO_PLAYWRIGHT || "playwright");
const fixtures = path.resolve(process.argv[2]), samples = path.resolve(process.argv[3]);
const extension = path.resolve(__dirname, "..");
const settings = { ...JSON.parse(fs.readFileSync(path.join(fixtures, "test-connection.json"), "utf8")), autoDetail: false, libraryId: "2" };
const pages = { "/list": "JAVBUS.txt", "/detail": "GVG-107 JAVDB.txt" };
let trailerRequests = 0;
let imageRequests = 0;
const server = http.createServer((request, response) => {
  if (request.url.startsWith("/test-images/")) {
    if (!request.headers.referer?.includes("/detail") && !request.headers.referer?.includes("/list")) { response.writeHead(403); response.end(); return; }
    if (!request.headers.cookie?.includes("clip-test-session=local-only")) { response.writeHead(401); response.end(); return; }
    imageRequests++; response.writeHead(200, { "Content-Type": "image/png" }); response.end(fs.readFileSync(path.join(extension, "icons/128.png"))); return;
  }
  if (request.url === "/actors/WKMq") {
    response.writeHead(200, { "Content-Type": "text/html;charset=utf-8" }); response.end(`<title>仲里紗羽 - JavDB</title><div class="actor-profile"><img src="/test-images/actor.png"></div>`); return;
  }
  if (request.url === "/v/wqmpe/preview_url") {
    if (!request.headers.cookie?.includes("clip-test-session=local-only")) { response.writeHead(401); response.end(); return; }
    response.writeHead(200, { "Content-Type": "application/json" }); response.end(JSON.stringify({ url: `http://127.0.0.1:${server.address().port}/trailer.mp4` })); return;
  }
  if (request.url === "/trailer.mp4") {
    if (!request.headers.referer?.endsWith("/detail") || request.headers.cookie) { response.writeHead(403); response.end(); return; }
    trailerRequests++; response.writeHead(200, { "Content-Type": "video/mp4" }); response.end(Buffer.from("000000206674797069736f6d0000000169736f6d61766331000000086d646174", "hex")); return;
  }
  const filename = pages[request.url];
  if (!filename) { response.writeHead(404); response.end(); return; }
  // Scripts from captured websites are never executed in the test browser.
  const html = fs.readFileSync(path.join(samples, filename), "utf8").replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "")
    .replace(/https:\/\/c0\.jdbstatic\.com\/covers\/wq\/wqmpe\.jpg/g, "/test-images/cover.png")
    .replace(/src="\/pics\/thumb\/[^"\s]+"/g, 'src="/test-images/list-cover.png"');
  response.writeHead(200, { "Content-Type": "text/html;charset=utf-8", "Set-Cookie": "clip-test-session=local-only; Path=/; SameSite=Lax" }); response.end(html);
});
(async () => {
  let context;
  try {
    await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
    const origin = `http://127.0.0.1:${server.address().port}`;
    context = await chromium.launchPersistentContext(path.join(process.env.JVEDIO_TEST_PROFILE_ROOT || fixtures, `browser-profile-${Date.now()}`), {
      ...(process.env.JVEDIO_BROWSER_EXE ? { executablePath: process.env.JVEDIO_BROWSER_EXE } : { channel: "chromium" }),
      headless: true, viewport: { width: 1000, height: 800 },
      args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`]
    });
    await context.route(/^https?:\/\//, route => route.request().url().startsWith(origin + "/") || route.request().url().startsWith(`http://127.0.0.1:${settings.port}/`) ? route.continue() : route.abort());
    let worker = context.serviceWorkers()[0];
    if (!worker) worker = await context.waitForEvent("serviceworker", { timeout: 20000 });
    const id = new URL(worker.url()).hostname;
    await worker.evaluate(async config => chrome.storage.local.set(config), settings);
    const source = await context.newPage(); await source.goto(origin + "/list");
    const tabId = await worker.evaluate(async url => (await chrome.tabs.query({})).find(tab => tab.url === url)?.id, origin + "/list");
    assert.ok(tabId);
    const popup = await context.newPage();
    // Simulate the toolbar's current-tab choice while using genuine scripting, storage, messaging and HTTP APIs.
    await popup.addInitScript(({ tabId, origin }) => {
      const query = chrome.tabs.query.bind(chrome.tabs);
      chrome.tabs.query = async filter => filter.active && filter.currentWindow ? [await chrome.tabs.get(tabId)] : query(filter);
    }, { tabId, origin });
    await popup.goto(`chrome-extension://${id}/popup.html`);
    await popup.locator("#count").filter({ hasText: "本页 30" }).waitFor();
    await popup.locator("#library option").first().waitFor({ state: "attached" });
    await popup.locator("#save:not([disabled])").waitFor();
    assert.equal(await popup.locator("#library").inputValue(), "2");
    await popup.locator("#none").click(); await popup.locator("#items input").first().check();
    await popup.locator("#save").click();
    await popup.locator("#status").filter({ hasText: "跳过已有 1" }).waitFor();
    assert.match(await popup.locator("#status").textContent(), /另一影片库/);
    await popup.screenshot({ path: path.join(fixtures, "chrome-list.png") });
    console.log("PASS: real extension loads, reads 30 list items and saves only one selected identifier to library 2");
    await source.goto(origin + "/detail");
    await worker.evaluate(async () => chrome.storage.local.set({ autoDetail: true }));
    await popup.reload();
    await popup.locator("#status").filter({ hasText: "已保存到" }).waitFor();
    assert.match(await popup.locator("#metadata").textContent(), /160 分钟/);
    assert.match(await popup.locator("#metadata").textContent(), /仲里紗羽/);
    assert.match(await popup.locator("#source").textContent(), /JavDB/);
    await popup.screenshot({ path: path.join(fixtures, "chrome-detail.png") });
    console.log("PASS: opening the detail popup captures GVG-107 metadata and automatically saves to the remembered library");
    const deadline = Date.now() + 15000;
    while ((!trailerRequests || !settings.previewVideoPaths.some(filename => fs.existsSync(filename))) && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 100));
    assert.ok(trailerRequests > 0, "preview video should resolve in the browser and download through WPF");
    assert.ok(settings.previewVideoPaths.some(filename => fs.existsSync(filename) && fs.statSync(filename).size > 0), "preview video should be saved to the movie preview directory");
    console.log("PASS: trailer URL resolves with the browser session and streams to WPF with image saving disabled");
    const errors = [];
    popup.on("pageerror", error => errors.push(error.message));
    await popup.locator("#library").selectOption("1"); await popup.locator("#save").click();
    await popup.locator("#status").filter({ hasText: "剪藏测试库" }).waitFor();
    const filesDeadline = Date.now() + 15000;
    while (!settings.previewVideoPaths.every(filename => fs.existsSync(filename)) && Date.now() < filesDeadline) await new Promise(resolve => setTimeout(resolve, 100));
    assert.ok(settings.previewVideoPaths.every(filename => fs.existsSync(filename)));
    assert.equal(errors.length, 0);
    console.log("PASS: a detail capture can also be saved to a different video library");
    for (const filename of settings.coverImagePaths) {
      assert.ok(filename.includes("jvedio-clipper-check-")); if (fs.existsSync(filename)) fs.unlinkSync(filename);
    }
    fs.writeFileSync(path.join(fixtures, "test-enable-images.flag"), "enable");
    const photoDeadline = Date.now() + 10000;
    let photoStatus;
    do {
      photoStatus = await popup.evaluate(() => chrome.runtime.sendMessage({ kind: "status" }));
      if (!photoStatus.result?.media?.saveCoverImages) await new Promise(resolve => setTimeout(resolve, 100));
    } while (!photoStatus.result?.media?.saveCoverImages && Date.now() < photoDeadline);
    assert.ok(photoStatus.result?.media?.saveActorImages);
    await worker.evaluate(async () => chrome.storage.local.set({ autoDetail: false }));
    await popup.locator("#save").click();
    await popup.locator("#status").filter({ hasText: "图片完成 2" }).waitFor();
    assert.ok(imageRequests >= 2); assert.ok(settings.coverImagePaths.every(filename => fs.existsSync(filename)));
    console.log("PASS: authenticated browser image capture saves cover, thumbnail and actor portrait with Referer while keeping cookies in Chrome");
    await source.goto(origin + "/list"); await popup.reload();
    await popup.locator("#count").filter({ hasText: "本页 30" }).waitFor();
    await popup.locator("#save:not([disabled])").waitFor();
    await popup.locator("#none").click();
    await popup.locator("#items input").nth(0).check(); await popup.locator("#items input").nth(1).check();
    await popup.locator("#save").click(); await popup.locator("#status").filter({ hasText: "图片完成 2" }).waitFor();
    const pictureRoot = path.dirname(path.dirname(settings.coverImagePaths[0]));
    assert.ok(["CLUB-691", "CLUB-685"].every(code => fs.existsSync(path.join(pictureRoot, "BigPic", code + ".jpg")) && fs.existsSync(path.join(pictureRoot, "SmallPic", code + ".jpg"))));
    console.log("PASS: selected list-page covers are saved in bulk without fetching movie detail pages");
  } catch (error) { console.error(error.stack); process.exitCode = 1; }
  finally { if (context) await context.close(); await new Promise(resolve => server.close(resolve)); }
})();
