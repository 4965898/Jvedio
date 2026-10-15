"use strict";
// Exercise kygo233/tkjs's actual pure card renderer when JVEDIO_COVER_SCRIPT is
// supplied. The userscript startup, network calls and GM APIs are never run.
const fs = require("node:fs");
const { JSDOM } = require("jsdom");
const jquery = require("jquery");

function coverFixture(html, count = 240) {
  const dom = new JSDOM(html, { url: "https://javdb.com/series/test", runScripts: "outside-only" });
  const { window } = dom;
  const $ = jquery(window);
  const original = window.document.querySelector(".movie-list .item");
  if (!original) throw new Error("A JavDB list fixture is required");
  const raw = Array.from({ length: count }, (_, index) => {
    const card = original.cloneNode(true), code = `WATER-${String(index + 1).padStart(3, "0")}`;
    card.querySelector("a").setAttribute("href", `/v/water${index + 1}`);
    card.querySelector("a").setAttribute("title", `${code} 瀑布流测试`);
    card.querySelector(".video-title strong").textContent = code;
    return card;
  });
  let cards;
  if (process.env.JVEDIO_COVER_SCRIPT) {
    const script = fs.readFileSync(process.env.JVEDIO_COVER_SCRIPT, "utf8");
    const rules = script.slice(script.indexOf("        javdb: {"), script.indexOf("        avmoo: {"));
    const getStart = rules.indexOf("getAvItem: function"), getEnd = rules.indexOf("\n            //init", getStart);
    const fn = rules.slice(getStart + "getAvItem: ".length, getEnd).trim();
    const grid = script.slice(script.indexOf("    class GridPanel{"), script.indexOf("    class ScrollerPlugin{"));
    if (!fn.startsWith("function") || !grid.startsWith("    class GridPanel{")) throw new Error("Userscript renderer changed; review its source before updating this test");
    Object.assign(window, { $, currentWeb: "javdb", Status: { isHalfImg: () => false, get: () => false }, lang: {},
      halfImgCSS: "", fullImgCSS: "", copy_Svg: "", magnet_Svg: "", download_Svg: "", picture_Svg: "" });
    window.currentObj = { getAvItem: window.eval(`(${fn})`) };
    window.eval(grid + "\nglobalThis.CoverGrid = GridPanel;");
    cards = window.CoverGrid.parseItems($(raw)).toArray().map(node => node.outerHTML);
  } else {
    cards = raw.map(node => {
      const anchor = node.querySelector("a"), code = node.querySelector(".video-title strong").textContent;
      return `<div class="item-b"><div class="box-b"><div class="cover-b"><a href="${anchor.href}"><img data-src="/cover.jpg"></a></div><div class="detail-b"><a name="av-title" href="${anchor.href}" title="${code} 瀑布流测试"><span>瀑布流测试</span></a><div class="info-bottom"><div class="info-bottom-one"><a href="${anchor.href}"><date name="avid">${code}</date> / 2026-10-15</a></div></div></div></div></div>`;
    });
  }
  const hidden = `<div class="movie-list h" style="display:none">${raw.slice(0, 40).map(node => node.outerHTML).join("")}</div>`;
  dom.window.close();
  return { hidden, cards, html: `<title>JavDB</title>${hidden}<div id="grid-b">${cards.join("")}</div>` };
}
module.exports = { coverFixture };
