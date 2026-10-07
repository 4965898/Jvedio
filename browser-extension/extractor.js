/* Runs in Chrome's isolated world, only after the user clicks the extension. */
(() => {
  "use strict";
  const text = node => (node?.textContent || "").replace(/\s+/g, " ").trim();

  function normalizeCode(value) {
    const code = String(value || "").normalize("NFKC").replace(/[\u200B-\u200D\uFEFF]/g, "").trim().toUpperCase()
      .replace(/[‐‑–—−]/g, "-").replace(/\s*([-_])\s*/g, "$1");
    const fc2 = code.match(/^FC2[\s_-]*(?:PPV[\s_-]*)?(\d{5,10})$/);
    if (fc2) return `FC2-PPV-${fc2[1]}`;
    // Accept explicit identifier fields, never scan the whole page for numbers.
    if (/^\d{0,6}[A-Z]{2,15}[-_]\d{2,10}(?:[-_][A-Z0-9]{1,8})?$/.test(code)) return code;
    if (/^\d{6}[-_]\d{2,4}$/.test(code)) return code;
    return "";
  }

  function extract(doc = document, pageUrl = location.href) {
    const base = new URL(pageUrl);
    const safeUrl = value => {
      if (!value) return "";
      try {
        const url = new URL(value, base);
        return /^https?:$/.test(url.protocol) ? url.href : "";
      } catch { return ""; }
    };
    const items = new Map();
    const codeInField = value => {
      const exact = normalizeCode(value);
      if (exact) return exact;
      const match = String(value || "").normalize("NFKC").replace(/[\u200B-\u200D\uFEFF]/g, "")
        .match(/(?:^|\s)((?:FC2[\s_-]*(?:PPV[\s_-]*)?\d{5,10})|(?:\d{0,6}[A-Z]{2,15}\s*[-_‐‑–—−]\s*\d{2,10}(?:[-_][A-Z0-9]{1,8})?))(?=$|\s)/i);
      return normalizeCode(match?.[1]);
    };
    const pageCode = normalizeCode(decodeURIComponent(base.pathname.split("/").filter(Boolean).at(-1) || ""));
    const pageTitle = selectors => selectors.map(selector => text(doc.querySelector(selector))).find(Boolean) ||
      doc.querySelector("meta[property='og:title']")?.content || doc.title.replace(/\s*[-|]\s*(?:JAVLibrary|JavBus).*$/i, "");
    let site = "";
    function add(value, title, href, metadata) {
      const code = normalizeCode(value);
      if (!code) return;
      if (!items.has(code)) items.set(code, {
        code, title: String(title || "").trim().replace(new RegExp(`^${code}\\s+`, "i"), "").slice(0, 1000), url: safeUrl(href || pageUrl), ...(metadata ? { metadata } : {})
      });
    }

    const listText = (root, selector) => [...new Set([...root.querySelectorAll(selector)].map(text).filter(Boolean))];
    const number = value => Number(String(value || "").match(/\d+(?:\.\d+)?/)?.[0] || 0);
    const fields = (root, selector, labelSelector, valueSelector) => {
      const result = new Map();
      for (const row of root.querySelectorAll(selector)) {
        const label = text(row.querySelector(labelSelector)).replace(/[:：\s]/g, "");
        if (!label) continue;
        const value = valueSelector ? row.querySelector(valueSelector) : row.cloneNode(true);
        if (!valueSelector) value.querySelector(labelSelector)?.remove();
        if (value) result.set(label, value);
      }
      return pattern => [...result].find(([label]) => pattern.test(label))?.[1];
    };
    function metadata(get, actors, cover, previews) {
      const ratingText = text(get(/^(評分|评分|Rating)$/i));
      return {
        releaseDate: text(get(/^(發行日期|发行日期|日期|Released|Release Date)$/i)).match(/\d{4}-\d{2}-\d{2}/)?.[0] || "",
        duration: number(text(get(/^(長度|长度|時長|时长|Length|Duration)$/i))),
        director: text(get(/^(導演|导演|Director)$/i)),
        studio: text(get(/^(製作商|制作商|片商|Studio|Maker)$/i)),
        publisher: text(get(/^(發行商|发行商|發行|发行|Label|Publisher)$/i)),
        series: get(/^(系列|Series)$/i) ? listText(get(/^(系列|Series)$/i), "a") : [],
        genres: get(/^(類別|类别|Tags|Genre)$/i) ? listText(get(/^(類別|类别|Tags|Genre)$/i), "a") : [],
        actors, coverUrl: safeUrl(cover || ""), previewUrls: [...new Set(previews.map(safeUrl).filter(Boolean))].slice(0, 100),
        previewVideoUrls: [], previewVideoEndpoint: "",
        rating: number(ratingText), ratingCount: Number(ratingText.match(/(?:由|by)\s*(\d+)/i)?.[1] || 0),
        plot: text(get(/^(簡介|简介|剧情|劇情|Plot|Description)$/i)).slice(0, 10000)
      };
    }
    const actor = (anchor, aliases = [], imageUrl = "") => ({ name: text(anchor), aliases, url: safeUrl(anchor?.getAttribute("href") || ""), imageUrl: safeUrl(imageUrl) });
    const listMetadata = image => ({ releaseDate: "", duration: 0, director: "", studio: "", publisher: "", series: [], genres: [], actors: [],
      rating: 0, ratingCount: 0, plot: "", coverUrl: safeUrl(image), previewUrls: [], previewVideoUrls: [], previewVideoEndpoint: "" });
    let kind = "list";
    // Detail pages contain related-video cards: prioritize the primary movie over those lists.
    if (doc.querySelector("#video_id .text")) {
      site = "JAVLibrary"; kind = "detail";
      const value = id => doc.querySelector(`#video_${id} .text`);
      const get = pattern => {
        const names = { date: "发行日期", length: "长度", director: "导演", maker: "制作商", label: "发行商", genres: "类别" };
        return value(Object.keys(names).find(key => pattern.test(names[key])) || "missing");
      };
      const actors = [...doc.querySelectorAll("#video_cast .cast")].map(row => actor(row.querySelector(".star a"),
        text(row.querySelector("[id^=alias]")).replace(/[()（）]/g, "").split(/[,，、]/).map(name => name.trim()).filter(Boolean)));
      const info = metadata(get, actors, doc.querySelector("#video_jacket_img")?.getAttribute("src"),
        [...doc.querySelectorAll("#previewthumbs img, .previewthumbs img")].map(img => img.getAttribute("src")));
      // JAVLibrary's public score is out of 10; Jvedio stores a 5-point score.
      info.rating = number(text(doc.querySelector("#video_review .score"))) / 2;
      add(codeInField(text(value("id"))), pageTitle(["#video_title a[rel='bookmark']", "#video_title h3", "#video_title .post-title", "#video_title .text", "#video_title"]), pageUrl, info);
    } else if (doc.querySelector(".video-detail .video-meta-panel")) {
      site = "JavDB"; kind = "detail";
      const root = doc.querySelector(".video-detail");
      const get = fields(root, ".video-meta-panel .panel-block", "strong", ".value");
      const actorRoot = get(/^(演員|演员|Actor|Actors|Cast)$/i);
      const actors = actorRoot ? [...actorRoot.querySelectorAll("a")].map(anchor => actor(anchor)) : [];
      const info = metadata(get, actors, root.querySelector(".column-video-cover img.video-cover")?.getAttribute("src"),
        [...doc.querySelectorAll(".preview-images a.tile-item")].map(anchor => anchor.getAttribute("href")));
      const previewVideo = doc.querySelector("#preview-video, .preview-images video");
      info.previewVideoEndpoint = safeUrl(previewVideo?.getAttribute("data-url"));
      info.previewVideoUrls = [...new Set([previewVideo?.getAttribute("src"), ...[...doc.querySelectorAll("#preview-video source, .preview-images video source")].map(node => node.getAttribute("src"))].map(safeUrl).filter(Boolean))];
      const original = text(root.querySelector(".origin-title")), current = text(root.querySelector(".current-title"));
      info.titleCN = original && current && original !== current && /^zh/.test(doc.body?.dataset.lang || doc.documentElement.lang) ? current : "";
      add(text(get(/^(番號|番号|ID)$/i)), original || current || text(root.querySelector("h2")), pageUrl, info);
    } else if (doc.querySelector(".movie .info, .screencap a.bigImage, a.bigImage") ||
      /(?:^|\.)javbus\d*\./i.test(base.hostname) && pageCode && codeInField(pageTitle([".container > h3", "h3", "h1"])) === pageCode) {
      site = "JavBus"; kind = "detail";
      const root = doc.querySelector(".movie") || doc;
      const get = fields(root, "p", ".header, strong, b");
      const actors = [...root.querySelectorAll(".star-name a")].map(anchor => actor(anchor, [], anchor.closest("li")?.querySelector("img")?.getAttribute("src")));
      const info = metadata(get, actors, root.querySelector("a.bigImage")?.getAttribute("href"),
        [...doc.querySelectorAll("#sample-waterfall a.sample-box")].map(anchor => anchor.getAttribute("href")));
      info.genres = listText(root, ".info a[href*='/genre/']");
      const title = pageTitle([".container > h3", "h3", "h1"]);
      const identifier = codeInField(text(get(/^(識別碼|识别码|識別番号|识别番号|識別代碼|识别代码|Identifier|ID)$/i))) ||
        (pageCode && codeInField(title) === pageCode ? pageCode : "") || codeInField(title);
      const preview = doc.querySelector("#preview-video, #sample-video, .preview-video video, .trailer video");
      info.previewVideoUrls = [...new Set([preview?.getAttribute("src"), ...[...(preview?.querySelectorAll("source") || [])].map(node => node.getAttribute("src"))].map(safeUrl).filter(Boolean))];
      add(identifier, title, pageUrl, info);
    }

    const bus = doc.querySelectorAll("a.movie-box");
    const db = doc.querySelectorAll(".movie-list .item a.box");
    const library = doc.querySelectorAll(".videothumblist .video, #videos .video");
    if (kind === "detail") {
      // Only the primary movie belongs in a single-click detail capture.
    } else if (bus.length) {
      site = "JavBus";
      for (const card of bus) {
        const code = text(card.querySelector(".photo-info date"));
        let fallback = "";
        if (!code) { try { fallback = new URL(card.getAttribute("href") || "", base).pathname.split("/").pop(); } catch { } }
        const image = card.querySelector("img");
        const info = listMetadata(image?.getAttribute("data-src") || image?.getAttribute("src"));
        info.releaseDate = text(card.querySelectorAll(".photo-info date")[1]);
        add(code || fallback, image?.getAttribute("title"), card.getAttribute("href"), info);
      }
    } else if (db.length) {
      site = "JavDB";
      for (const card of db) {
        const field = card.querySelector(".video-title strong");
        const code = text(field);
        const image = card.querySelector(".cover img, img");
        const info = listMetadata(image?.getAttribute("data-src") || image?.getAttribute("src"));
        info.releaseDate = text(card.querySelector(".meta")).match(/\d{4}-\d{2}-\d{2}/)?.[0] || "";
        const score = text(card.querySelector(".score .value"));
        info.rating = number(score); info.ratingCount = Number(score.match(/(?:由|by)\s*(\d+)/i)?.[1] || 0);
        add(code, card.getAttribute("title") || text(card.querySelector(".video-title")).slice(code.length).trim(), card.getAttribute("href"), info);
      }
    } else if (library.length) {
      site = "JAVLibrary";
      for (const card of library) {
        const anchor = card.querySelector("a[href]");
        const image = card.querySelector("img");
        add(text(card.querySelector(".id")), text(card.querySelector(".title")), anchor?.getAttribute("href"), listMetadata(image?.getAttribute("data-src") || image?.getAttribute("src")));
      }
    }
    return { site, kind, pageUrl, pageTitle: doc.title, items: Array.from(items.values()).slice(0, 500) };
  }
  globalThis.JvedioExtractor = Object.freeze({ extract, normalizeCode });
})();
