"use strict";
// Loads the real unpacked extension in a fresh browser profile and talks to the isolated WPF receiver.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const http = require("node:http");
const { coverFixture } = require("./cover-fixture.cjs");
const { DatabaseSync } = require("node:sqlite");
const { chromium } = require(process.env.JVEDIO_PLAYWRIGHT || "playwright");
const fixtures = path.resolve(process.argv[2]), samples = path.resolve(process.argv[3]);
const extension = path.resolve(__dirname, "..");
const settings = { ...JSON.parse(fs.readFileSync(path.join(fixtures, "test-connection.json"), "utf8")), libraryId: "2" };
assert.ok(settings.databasePath.includes("jvedio-clipper-check-"), "read only the isolated receiver database");
const database = new DatabaseSync(settings.databasePath, { readOnly: true });
const marksFor = (libraryId, code) => database.prepare("select distinct s.TagName from metadata_to_tagstamp t join common_tagstamp s on s.TagID=t.TagID join metadata m on m.DataID=t.DataID join metadata_video v on v.DataID=m.DataID where m.DBId=? and v.VID=? and t.TagID<>10000 order by s.TagName").all(libraryId, code).map(row => row.TagName);
const movieCount = (libraryId, code) => database.prepare("select count(*) as count from metadata m join metadata_video v on v.DataID=m.DataID where m.DBId=? and v.VID=?").get(libraryId, code).count;
const textLabelsFor = (libraryId, code) => database.prepare("select count(*) as count from metadata_to_label l join metadata m on m.DataID=l.DataID join metadata_video v on v.DataID=m.DataID where m.DBId=? and v.VID=?").get(libraryId, code).count;
const cover = coverFixture(fs.readFileSync(path.join(samples, "JAVDB.txt"), "utf8"));
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
    if ((!request.headers.referer?.endsWith("/detail") && !request.headers.referer?.endsWith("/tag-detail")) || request.headers.cookie) { response.writeHead(403); response.end(); return; }
    trailerRequests++; response.writeHead(200, { "Content-Type": "video/mp4" }); response.end(Buffer.from("000000206674797069736f6d0000000169736f6d61766331000000086d646174", "hex")); return;
  }
  if (request.url === "/cover") {
    response.writeHead(200, { "Content-Type": "text/html;charset=utf-8" });
    response.end('<title>JavDB</title>' + cover.hidden + '<div id="grid-b">' + cover.cards.slice(0, 40).join("") + '</div>'); return;
  }
  const filename = pages[request.url === "/tag-detail" ? "/detail" : request.url];
  if (!filename) { response.writeHead(404); response.end(); return; }
  // Scripts from captured websites are never executed in the test browser.
  const html = fs.readFileSync(path.join(samples, filename), "utf8").replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "")
    .replace(/https:\/\/c0\.jdbstatic\.com\/covers\/wq\/wqmpe\.jpg/g, "/test-images/cover.png")
    .replace(/src="\/pics\/thumb\/[^"\s]+"/g, 'src="/test-images/list-cover.png"');
  const rendered = request.url === "/tag-detail" ? html.replaceAll("GVG-107", "SINGLE-900").replace(/>GVG<\/a>\s*-107/g, ">SINGLE</a>-900") : html;
  response.writeHead(200, { "Content-Type": "text/html;charset=utf-8", "Set-Cookie": "clip-test-session=local-only; Path=/; SameSite=Lax" }); response.end(rendered);
});
(async () => {
  let context;
  try {
    await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
    const origin = `http://127.0.0.1:${server.address().port}`;
    context = await chromium.launchPersistentContext(path.join(process.env.JVEDIO_TEST_PROFILE_ROOT || fixtures, `browser-profile-${Date.now()}`), {
      ...(process.env.JVEDIO_BROWSER_EXE ? { executablePath: process.env.JVEDIO_BROWSER_EXE } : { channel: "chromium" }),
      headless: true, viewport: null,
      args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`]
    });
    await context.route(/^https?:\/\//, route => route.request().url().startsWith(origin + "/") || route.request().url().startsWith(`http://127.0.0.1:${settings.port}/`) ? route.continue() : route.abort());
    let worker = context.serviceWorkers()[0];
    if (!worker) worker = await context.waitForEvent("serviceworker", { timeout: 20000 });
    const id = new URL(worker.url()).hostname;
    await worker.evaluate(async config => chrome.storage.local.set(config), settings);
    assert.equal(await worker.evaluate(() => new Promise(resolve => {
      chrome.contextMenus.update("jvedio-choose-destination", { title: "选择影片库、标记与连接设置…" }, () => resolve(!chrome.runtime.lastError));
    })), true);
    console.log("PASS: the toolbar context menu exposes the manual library, marks and connection entry");
    const source = await context.newPage(); await source.goto(origin + "/list");
    const tabId = await worker.evaluate(async url => (await chrome.tabs.query({})).find(tab => tab.url === url)?.id, origin + "/list");
    assert.ok(tabId);
    // Invoke the toolbar handler with its source tab; all window and tab APIs are genuine.
    await worker.evaluate(async tabId => handleAction(await chrome.tabs.get(tabId)), tabId);
    let popup = context.pages().find(page => page.url().startsWith(`chrome-extension://${id}/popup.html`)) || await context.waitForEvent("page");
    await popup.waitForLoadState();
    await popup.locator("#count").filter({ hasText: "已加载 30" }).waitFor();
    await popup.locator("#library option").first().waitFor({ state: "attached" });
    await popup.locator("#connect:not([disabled])").waitFor();
    assert.equal(await popup.locator("#library").inputValue(), "2");
    assert.equal(await popup.locator("#items input:checked").count(), 0);
    assert.equal(await popup.locator("#save").isEnabled(), false);
    const windowId = await worker.evaluate(async tabId => (await chrome.storage.session.get(`window:${tabId}`))[`window:${tabId}`].windowId, tabId);
    const bounds = await worker.evaluate(async windowId => chrome.windows.update(windowId, { width: 420, height: 600, left: 100, top: 90 }), windowId);
    assert.equal(bounds.type, "popup"); assert.equal(bounds.alwaysOnTop, false);
    assert.equal(bounds.width, 420); assert.equal(bounds.height, 600);
    await popup.waitForFunction(async () => {
      const { clipperBounds } = await chrome.storage.local.get("clipperBounds");
      return clipperBounds?.width === 420 && clipperBounds?.height === 600;
    });
    await worker.evaluate(async tabId => openClipper(await chrome.tabs.get(tabId)), tabId);
    assert.equal(await worker.evaluate(async tabId => (await chrome.storage.session.get(`window:${tabId}`))[`window:${tabId}`].windowId, tabId), windowId);
    assert.equal(context.pages().filter(page => page.url().startsWith(`chrome-extension://${id}/popup.html`)).length, 1);
    console.log("PASS: a native resizable popup opens with no selection, is not always-on-top and is reused for its source tab");
    await popup.locator("#items input").first().check();
    await popup.locator("#save").click();
    await popup.locator("#status").filter({ hasText: "跳过已有 1" }).waitFor();
    assert.match(await popup.locator("#status").textContent(), /另一影片库/);
    await popup.screenshot({ path: path.join(fixtures, "chrome-list.png") });
    console.log("PASS: real extension loads, reads 30 list items and saves only one selected identifier to library 2");
    await popup.locator("#markCount").click();
    assert.equal(await popup.getByRole("checkbox", { name: "其他", exact: true }).count(), 1);
    await popup.getByRole("checkbox", { name: "AV", exact: true }).check();
    await popup.getByRole("checkbox", { name: "套路直播", exact: true }).check(); await popup.locator("#marksDone").click();
    await popup.locator("#save").click(); await popup.locator("#status").filter({ hasText: "补全 1" }).waitFor();
    console.log("PASS: multiple library marks reach the WPF receiver on an existing movie");
    await source.evaluate(() => {
      for (let i = 1; i <= 2; i++) {
        const card = document.querySelector("a.movie-box").cloneNode(true);
        card.href = `/FLOW-00${i}`; card.querySelector("date").textContent = `FLOW-00${i}`;
        document.querySelector("a.movie-box").parentElement.append(card);
      }
    });
    await popup.locator("#count").filter({ hasText: "已加载 32" }).waitFor();
    assert.equal(await popup.locator("#items input:checked").count(), 1);
    await popup.locator("#rescan").click();
    await popup.locator("#rescan:not([disabled])").waitFor();
    assert.equal(await popup.locator("#items input:checked").count(), 0);
    await popup.locator("#items input").first().check();
    await popup.locator("#items").getByText("FLOW-001", { exact: true }).click();
    await popup.locator("#items").getByText("FLOW-002", { exact: true }).click();
    assert.equal(await popup.locator("#items input:checked").count(), 3);
    await popup.locator("#save").click(); await popup.locator("#status").filter({ hasText: "新增 2" }).waitFor();
    await popup.evaluate(() => window.scrollTo(0, 0));
    await popup.screenshot({ path: path.join(fixtures, "chrome-window-marks-waterfall.png"), fullPage: true });
    await popup.close();
    await worker.evaluate(async tabId => openClipper(await chrome.tabs.get(tabId)), tabId);
    popup = context.pages().find(page => page.url().startsWith(`chrome-extension://${id}/popup.html`)) || await context.waitForEvent("page");
    await popup.locator("#count").filter({ hasText: "已加载 32" }).waitFor();
    assert.equal(await popup.locator("#items input:checked").count(), 0);
    await popup.locator("#markToggle:not([disabled])").waitFor();
    assert.deepEqual((await popup.locator("#markCount").textContent()).split("、").sort(), ["AV", "套路直播"].sort());
    const restoredBounds = await popup.evaluate(() => chrome.windows.getCurrent());
    assert.equal(restoredBounds.width, 420); assert.equal(restoredBounds.height, 600);
    console.log("PASS: newly appended waterfall cards appear live, remain unselected and can be saved with both selected marks");
    await source.goto(origin + "/cover"); await popup.reload();
    await popup.locator("#count").filter({ hasText: "已加载 40" }).waitFor();
    await popup.locator("#rescan:not([disabled])").waitFor();
    await popup.locator("#items input").first().check();
    await source.evaluate(html => document.getElementById("grid-b").insertAdjacentHTML("beforeend", html), cover.cards.slice(40).join(""));
    await popup.locator("#count").filter({ hasText: "已加载 240" }).waitFor();
    assert.equal(await popup.locator("#items input:checked").count(), 1);
    await popup.locator("#all").click(); await popup.locator("#save").click();
    await popup.locator("#status").filter({ hasText: "新增 240" }).waitFor();
    await popup.locator("#status").filter({ hasText: "已为 240 部影片添加标记" }).waitFor();
    for (let i = 1; i <= 240; i++) assert.deepEqual(marksFor(2, 'WATER-' + String(i).padStart(3, "0")), ["AV", "套路直播"].sort());
    await popup.locator("#markToggle").click();
    await popup.screenshot({ path: path.join(fixtures, "chrome-240-mark-dropdown.png"), fullPage: true });
    console.log("PASS: actual larger-thumbnails renderer grows 40 to 240 cards, preserves selection and saves both marks on all 240 movies in SQLite");
    for (const [site, domain, filename, oldCode, code] of [
      ["JavBus", "www.javbus.com", "JAVBUS.txt", "CLUB-691", "INLINEBUS-001"],
      ["JavDB", "javdb.com", "JAVDB.txt", "STOL-134", "INLINEDB-001"],
      ["JAVLibrary", "www.javlibrary.com", "javlib主页.txt", "HND-655", "INLINELIB-001"]
    ]) {
      const url = 'https://' + domain + '/__jvedio_connector_test__/list';
      const body = fs.readFileSync(path.join(samples, filename), "utf8").replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "").replaceAll(oldCode, code);
      await context.route(url + "**", route => route.fulfill({ contentType: "text/html;charset=utf-8", body }));
      const cardPage = await context.newPage(); await cardPage.goto(url);
      const button = cardPage.getByRole("button", { name: '勾选 ' + code + ' 到 Jvedio', exact: true });
      await button.waitFor();
      const codes = [code, code.replace("001", "002"), code.replace("001", "003")];
      await cardPage.evaluate(({ site, codes }) => {
        const selector = site === "JavBus" ? "a.movie-box" : site === "JavDB" ? ".movie-list .item" : ".videothumblist .video";
        const card = document.querySelector(selector);
        for (const code of codes.slice(1)) card.parentElement.insertAdjacentHTML("beforeend", card.outerHTML.replaceAll(codes[0], code));
      }, { site, codes });
      const pick = code => cardPage.getByRole("button", { name: '勾选 ' + code + ' 到 Jvedio', exact: true });
      const windowsBefore = context.pages().filter(page => page.url().startsWith('chrome-extension://' + id + '/popup.html')).length;
      await pick(codes[0]).click(); await pick(codes[2]).click(); await pick(codes[0]).click();
      await pick(codes[2]).filter({ hasText: "已勾选" }).waitFor();
      assert.equal(cardPage.url(), url);
      assert.ok(codes.every(code => movieCount(2, code) === 0), "picking must not write any movie");
      assert.equal(context.pages().filter(page => page.url().startsWith('chrome-extension://' + id + '/popup.html')).length, windowsBefore);
      const cardTabId = await worker.evaluate(async url => (await chrome.tabs.query({})).find(tab => tab.url === url)?.id, url);
      const opening = context.waitForEvent("page"); await worker.evaluate(async tabId => handleAction(await chrome.tabs.get(tabId)), cardTabId);
      const cardPopup = await opening;
      await cardPopup.locator("#count").filter({ hasText: "已选 2 个" }).waitFor();
      await pick(codes[1]).click();
      await cardPopup.locator("#count").filter({ hasText: "已选 3 个" }).waitFor();
      const check = cardPopup.getByRole("checkbox", { name: "选择 " + codes[1], exact: true });
      await check.uncheck(); await cardPage.waitForFunction(code => document.querySelector('[data-jvedio-card="' + code + '"]').shadowRoot.querySelector('button').getAttribute('aria-pressed') === 'false', codes[1]);
      await pick(codes[1]).click(); await cardPopup.locator("#count").filter({ hasText: "已选 3 个" }).waitFor();
      await cardPopup.locator("#save").click(); await cardPopup.locator("#status").filter({ hasText: "新增 3" }).waitFor();
      for (const code of codes) assert.deepEqual(marksFor(2, code), ["AV", "套路直播"].sort());
      if (site === "JavDB") {
        await cardPopup.screenshot({ path: path.join(fixtures, "chrome-scattered-selection.png"), fullPage: true });
        await cardPopup.reload(); await cardPopup.locator("#count").filter({ hasText: "已选 0 个" }).waitFor();
        await pick(codes[0]).click(); await cardPopup.locator("#count").filter({ hasText: "已选 1 个" }).waitFor();
        await cardPage.goto(url + "?page=2");
        await cardPopup.locator("#count").filter({ hasText: "已选 0 个" }).waitFor();
        await pick(codes[0]).click(); await cardPopup.locator("#count").filter({ hasText: "已选 1 个" }).waitFor();
        await cardPage.reload(); await cardPopup.locator("#count").filter({ hasText: "已选 0 个" }).waitFor();
        console.log("PASS: window reload, manual pagination and source reload clear selections on the actual Chrome page");
      }
      await cardPopup.close();
      await cardPage.waitForFunction(code => document.querySelector('[data-jvedio-card="' + code + '"]').shadowRoot.querySelector('button').getAttribute('aria-pressed') === 'false', codes[0]);
      console.log('PASS: ' + site + ' card clicks accumulate scattered choices without saving or navigation; explicit bulk save writes all three films and marks, and closing clears choices');
      await cardPage.close();
    }
    await popup.locator("#none").click();
    const enhancedButton = source.getByRole("button", { name: "勾选 WATER-240 到 Jvedio", exact: true });
    await enhancedButton.click(); await enhancedButton.filter({ hasText: "已勾选" }).waitFor();
    await popup.locator("#count").filter({ hasText: "已选 1 个" }).waitFor();
    assert.equal(source.url(), origin + "/cover");
    console.log("PASS: dynamically appended larger-thumbnails cards add to the same live selection without changing the 240-film batch behavior");
    await source.addStyleTag({ content: "#grid-b{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;padding:18px;font:14px/1.5 system-ui}#grid-b>.item-b:nth-child(n+5){display:none}.box-b{border:1px solid #ddd;border-radius:6px;overflow:hidden;background:white;box-shadow:0 2px 4px #0002}.cover-b{height:130px;background:linear-gradient(130deg,#e7eaf7,#d7deed)}.cover-b img{display:none}.detail-b{padding:12px}a{color:#334a73;text-decoration:none}.info-bottom-one{margin-top:8px}.score{display:none}" });
    await source.screenshot({ path: path.join(fixtures, "chrome-card-buttons-preview.png") });
    await popup.close(); await source.goto(origin + "/tag-detail");
    await worker.evaluate(async tabId => handleAction(await chrome.tabs.get(tabId)), tabId);
    assert.equal(context.pages().filter(page => page.url().startsWith('chrome-extension://' + id + '/popup.html')).length, 0);
    assert.deepEqual(marksFor(2, "SINGLE-900"), ["AV", "套路直播"].sort()); assert.equal(textLabelsFor(2, "SINGLE-900"), 0);
    console.log("PASS: direct single-movie capture retains both selected library marks in SQLite without opening a window");
    const manualSingle = context.waitForEvent("page");
    await worker.evaluate(async tabId => openClipper(await chrome.tabs.get(tabId)), tabId); popup = await manualSingle;
    await popup.locator("#metadata").waitFor(); await popup.locator("#library").selectOption("1");
    await popup.locator("#markToggle:not([disabled])").waitFor(); await popup.locator("#markToggle").click();
    await popup.getByRole("checkbox", { name: "AV", exact: true }).check(); await popup.getByRole("checkbox", { name: "其他", exact: true }).check(); await popup.locator("#marksDone").click();
    await popup.locator("#items input").first().check(); await popup.locator("#save").click();
    await popup.locator("#status").filter({ hasText: "已为 1 部影片添加标记" }).waitFor();
    assert.deepEqual(marksFor(1, "SINGLE-900"), ["AV", "其他"].sort()); assert.equal(textLabelsFor(1, "SINGLE-900"), 0);
    console.log("PASS: manual single-movie dropdown selection writes both marks to the chosen library in SQLite");
    await worker.evaluate(async () => chrome.storage.local.set({ libraryId: "2" }));
    await popup.close();
    await source.goto(origin + "/detail");
    await worker.evaluate(async tabId => handleAction(await chrome.tabs.get(tabId)), tabId);
    assert.equal(context.pages().filter(page => page.url().startsWith('chrome-extension://' + id + '/popup.html')).length, 0);
    assert.equal(await worker.evaluate(async tabId => chrome.action.getBadgeText({ tabId }), tabId), "✓");
    assert.match(await worker.evaluate(async tabId => chrome.action.getTitle({ tabId }), tabId), /GVG-107.*另一影片库/);
    console.log("PASS: clicking a detail page saves directly with a success badge and creates no popup window");
    const detailWindow = context.waitForEvent("page");
    await worker.evaluate(async tabId => openClipper(await chrome.tabs.get(tabId)), tabId);
    popup = await detailWindow;
    await popup.locator("#metadata").filter({ hasText: "160 分钟" }).waitFor();
    await popup.locator("#library option").first().waitFor({ state: "attached" });
    assert.match(await popup.locator("#metadata").textContent(), /仲里紗羽/);
    assert.equal(await popup.locator("#items input:checked").count(), 0);
    await popup.locator("#items input").first().check();
    await popup.screenshot({ path: path.join(fixtures, "chrome-detail.png") });
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
    await popup.locator("#count").filter({ hasText: "已加载 30" }).waitFor();
    await popup.locator("#none").click();
    await popup.locator("#items input").nth(0).check(); await popup.locator("#items input").nth(1).check();
    await popup.locator("#save").click(); await popup.locator("#status").filter({ hasText: "图片完成 2" }).waitFor();
    const pictureRoot = path.dirname(path.dirname(settings.coverImagePaths[0]));
    assert.ok(["CLUB-691", "CLUB-685"].every(code => fs.existsSync(path.join(pictureRoot, "BigPic", code + ".jpg")) && fs.existsSync(path.join(pictureRoot, "SmallPic", code + ".jpg"))));
    console.log("PASS: selected list-page covers are saved in bulk without fetching movie detail pages");
    await popup.close(); await source.goto(origin + "/detail");
    for (const [name, change, expected] of [
      ["missing key", { token: "" }, "连接密钥"],
      ["unselected library", { token: settings.token, libraryId: "" }, "选择目标影片库"],
      ["deleted library", { libraryId: "999999" }, "已不存在"],
      ["desktop offline", { libraryId: "2", port: 65534 }, "无法连接"]
    ]) {
      await worker.evaluate(async value => chrome.storage.local.set(value), change);
      const pending = context.waitForEvent("page");
      await worker.evaluate(async tabId => handleAction(await chrome.tabs.get(tabId)), tabId);
      popup = await pending;
      await popup.locator("#status").filter({ hasText: expected }).waitFor();
      assert.equal(await popup.locator("#items input:checked").count(), 1);
      assert.equal(await worker.evaluate(async tabId => chrome.action.getBadgeText({ tabId }), tabId), "!");
      console.log('PASS: detail fallback opens the selection window for ' + name + ' and displays the reason without auto-saving');
      if (name === "missing key") await popup.screenshot({ path: path.join(fixtures, "chrome-detail-setup.png"), fullPage: true });
      await popup.close();
    }
    await worker.evaluate(async config => chrome.storage.local.set(config), { port: settings.port, token: settings.token, libraryId: "2" });
    await worker.evaluate(async tabId => handleAction(await chrome.tabs.get(tabId)), tabId);
    assert.equal(context.pages().filter(page => page.url().startsWith('chrome-extension://' + id + '/popup.html')).length, 0);
    assert.equal(await worker.evaluate(async tabId => chrome.action.getBadgeText({ tabId }), tabId), "✓");
    console.log("PASS: fixing connection settings restores detail saving without a window, including cover and actor images");
  } catch (error) { console.error(error.stack); process.exitCode = 1; }
  finally { if (context) await context.close(); await new Promise(resolve => server.close(resolve)); database.close(); }
})();
