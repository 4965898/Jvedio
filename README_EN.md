[中文](README.md) [English](README_EN.md) [日本語](README_JP.md)



<h1 align="center">Jvedio</h1>




<div align="center" >
<img src="https://s1.ax1x.com/2022/06/11/XcePQf.png"><h3 >Local Video Management</h3>
</div>




---



[![.NET CORE](https://img.shields.io/badge/.NET%20Framework-4.7.2-d.svg)](#)
[![Platform](https://img.shields.io/badge/Platform-Win-brightgreen.svg)](#)
[![LICENSE](https://img.shields.io/badge/license-GPL%203.0-blue)](#)
[![Star](https://img.shields.io/github/stars/4965898/Jvedio?label=Star%20this%20repo)](https://github.com/4965898/Jvedio)
[![Fork](https://img.shields.io/github/forks/4965898/Jvedio?label=Fork%20this%20repo)](https://github.com/4965898/Jvedio/fork)



&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`Jvedio` is a local video management software that supports scanning local videos and importing the software to establish a video library,
Extract the unique identification code of the video, automatically classify the video,
Add tags to manage videos, use artificial intelligence to identify actors, support translation information,
Capture video pictures based on `FFmpeg`, smooth and beautiful application software on Window desktop


WebSite：[Jvedio](https://hitchao.github.io/JvedioWebPage/) | Download：[Latest Version](https://github.com/4965898/Jvedio/releases)

---

[<img src="https://s1.ax1x.com/2022/10/07/x8KbvT.png" alt="x8KbvT.png" style="zoom:80%;" />](https://imgse.com/i/x8KbvT)


---
[<img src="https://s1.ax1x.com/2022/10/07/x8KOrF.png" alt="x8KOrF.png" style="zoom:80%;" />](https://imgse.com/i/x8KOrF)

---

[<img src="https://s1.ax1x.com/2022/10/07/x8MVVH.png" alt="x8MVVH.png" style="zoom:80%;" />](https://imgse.com/i/x8MVVH)

---

[<img src="https://s1.ax1x.com/2022/10/07/x8MZad.png" alt="x8MZad.png" style="zoom:80%;" />](https://imgse.com/i/x8MZad)



# Document

Developers : [Developer Document](https://github.com/hitchao/Jvedio/wiki/20_Developer)

Users : [User Guide](https://github.com/hitchao/Jvedio/wiki/02_Beginning)


# Related items


|||
|--|--|
|Jvedio official webpage|[JvedioWebPage](https://github.com/hitchao/JvedioWebPage)|
|Chrome (360 speed browser) plug-in|[Jvedio-Chrome-Extensions](https://github.com/hitchao/Jvedio-Chrome-Extensions)|
|Jvedio upgraded server source|[jvedioupdate](https://github.com/hitchao/jvedioupdate)|
|Gif control modified in|[WpfAnimatedGif](https://github.com/hitchao/WpfAnimatedGif)|


# Version History (Fork)

> The original repository (hitchao/Jvedio) has been archived. Since 5.4.0.1 it is maintained by [4965898/Jvedio](https://github.com/4965898/Jvedio):

| Version | Date | Highlights |
|---|---|---|
| 5.4.0.1 | 2025-12-04 | Batch delete images; filter by images |
| 5.4.0.2 | 2025-12-10 | Optimize poster / thumbnail filtering |
| 5.4.0.3 | 2025-12-30 | Maintenance update |
| 5.4.0.4 | 2026-02-21 | One-click restart of failed sync tasks; extra filters |
| 5.4.0.5 | 2026-05-02 | Filter by actor-info presence; other optimizations |
| 5.4.0.6 | 2026-08-10 | Pagination rendering optimization (fix crash on fast page-flip and slowdown); auto rebuild of image-existence index; background startup scan; JavBus scraping fixes; SQLite lock fixes (no more "database is locked" freeze); scraping task persistence (resume manually after restart); numeric sort for video codes; watch-online quick links (27 sites, customizable URLs); title translation (AI/ChatGPT-compatible + Baidu/Google/DeepL etc., per-platform config) |
| 5.4.1.8 (Jvedio29.16) | 2026-08-16 | Fixed 12 issues from the original repo: avatar stretching, context-menu direction, name sorting, URL double-prefix, NFO uniqueid & Kodi compatibility, NFO fallback, -U/-UC correction marking, page-number memory, actor avatars in separate directory, CSV export, ISO/.strm support |
| 5.4.1.9 (Jvedio29.17) | 2026-08-17 | Export enhancements (CSV/Excel/JSON + one-click full-library export); actor shoe size field, cup dropdown, height/weight units |
| 5.4.1.10 (Jvedio29.18) | 2026-08-17 | Batch NFO export; birthday date-picker + real-time age calculation |
| 5.4.1.11 (Jvedio29.19) | 2026-08-17 | Right-click export now applies to selected videos only; detailed translation errors (HTTP status + response body) |
| 5.4.1.12 (Jvedio29.20) | 2026-08-17 | Chinese-title field on edit page; per-platform translation config persisted independently; Bing Region header fix |
| 5.4.1.13 (Jvedio29.21) | 2026-08-17 | Translation config moved to data dir (legacy config auto-migrated); Bing auto source-language fix |
| 5.4.1.14 (Jvedio29.22) | 2026-08-17 | Fix single-video right-click title translation not responding |
| 5.4.1.15 (Jvedio29.23) | 2026-08-17 | Settings > Display: follow system DPI + UI font-scale slider |
| 5.4.1.16 (Jvedio29.24) | 2026-08-17 | Fix Settings page crash (DpiConfig static binding namespace prefix) |
| 5.4.1.17 (Jvedio29.25) | 2026-08-17 | Font slider now covers all hardcoded font sizes (12/13/14/15, 89 spots) - sidebar and all UI text scale with it |
| 5.4.1.18 (Jvedio29.26) | 2026-08-17 | Fix db (JavDB) scraper not scraping director/rating (director label fix + ParseDictInfo float support + 5-point rating normalization); add Publisher field (details/edit pages + trilingual; JavBus parses 發行商); fix "two Publishers" display bug (SuperControls mistranslated the Studio key as "Publisher", now overridden to Studio); online-watch button ordering (JavDB/JavBus/JAVLib first, MISSAV 4th) |
| 5.4.1.19 (Jvedio29.27) | 2026-08-19 | Fix stale results in the "Playable/Not Playable" filter: added automatic maintenance of the resource-existence (playable) index (DataIndexManager) - silently rebuilds metadata.PathExist in the background on entering the library and after each scan (controlled by the "rebuild resource-existence index after scan" option), and incrementally syncs it on file deletion / local-path edits / move-rename, so no manual "Build Resource-Existence Index" is needed after upgrading; fixed the startup-rebuild giant transaction that caused "database is locked" lag/crash (lightweight query + chunked transactions); DB (JavDB) scraper now scrapes the Publisher (发行商) field (binary plugin rebuilt from decompiled source, the 發行 field was previously missing) |
| 5.4.1.20 (Jvedio29.28) | 2026-08-21 | Fixed batch title translation stopping after only 1-2 videos (root cause: the background loop enumerated the live selected-video list, which is mutated by UI actions, throwing "collection was modified"; now a snapshot is taken first and each video is enqueued as a translation task); translation is now task-based with a dedicated task page: a new translate button (文+T icon) in the bottom-right status bar next to download/scan/screenshot opens the translation task page, supporting Cancel All / Restart Failed / Clear List, per-task cancel/restart, per-row status and an overall progress bar - consistent with the download task module |
| 5.4.1.21 (Jvedio29.29) | 2026-08-21 | Removed the automatic jump to the translation task page after starting a translation (no longer interrupts the current view); the translation task page is now opened manually via the 文+T button in the bottom-right |
| 5.4.1.22 (Jvedio29.30) | 2026-08-21 | Redrew the translate icon in the common translator style: "文/T" with equal-size glyphs separated by a slash, colors/background matching the other three status-bar icons |
| 5.4.1.23 (Jvedio29.31) | 2026-08-21 | Fixed inability to add a new translation batch while one is running: translation tasks can now be added mid-run (new tasks continue automatically after old ones complete, same as the sync module), deduplicated by ID to avoid re-translating |
| 5.4.1.24 (Jvedio29.32) | 2026-08-21 | Fixed the details-window left/right navigation arrows becoming unresponsive (stuck async state now reset in try/finally, dispatcher priority starvation fixed, stale page snapshot self-heals with a full-library fallback, tag-removal DB write moved off the UI thread); added a one-click copy-name button next to the actor name in the actor detail panel; shrank the translate icon font |
| 5.4.1.25~28 (Jvedio29.33~36) | 2026-08-21 | Translate icon iterations: font size 9→10; Google-Translate-style corner layout tried then reverted (glyphs overlapped at 12px); final horizontal "文/T" at 10px |
| 5.4.1.29 (Jvedio29.37) | 2026-08-24 | Actor detail panel: new "Online Search" jump buttons (JavDB/JavBus/JAVLib top three, MISSAV fourth; placed below hobbies, styled like the video details online-watch buttons; searches by actor name) |
| 5.4.1.30 (Jvedio29.38) | 2026-08-24 | Online watch/search refactored to "root-address linkage": each site split into root address (domain) + video path + search path; video details and actor details share the root address (fill in the domain once in Options-Network, changing the mirror site takes effect everywhere); all 27 sites ported to the actor page in the same order as the video page |
| 5.4.1.31 (Jvedio29.39) | 2026-08-24 | Corrected 4 sites' search paths (123AV `cn/search`, JAVMENU `zh/search?wd`, Jav.Guru `jav-actress-list/?taxonomy_search`, JAVLib `searchstar.php`); fixed actor-page buttons not wrapping and stretching the panel over video info (WrapPanel MaxWidth, 3-4 per row) |
| 5.4.1.32 (Jvedio29.40) | 2026-08-24 | New actor English-name (romaji) feature: actor_info adds ActorNameEN column; romaji auto-extracted from the JavDB actor slug while scraping; kana→romaji conversion algorithm (RomajiConverter) as fallback; right-click "Convert to English name" batch conversion in the actor list; English name row in the detail panel (with copy button); editable on the edit page; included in actor export |
| 5.4.1.33 (Jvedio29.41) | 2026-08-24 | English-name row always visible (no longer hidden when empty); "EN" button added to the actor detail action-button row (converts the current actor's English name - auto-fills if kana is convertible, prompts manual entry if kanji) |
| 5.4.1.34 (Jvedio29.42) | 2026-09-07 | Fixed the "Playable/Not Playable" filter returning inverted results: the index is only a snapshot of the last rebuild (files externally added/removed/moved during the session, or the removable/network drive not ready when the index was built, make it stale). Clicking "Not Playable/Playable" now rebuilds the index on the spot against the current disk state (with a "verifying files" overlay) before filtering; a successful on-the-spot rebuild automatically marks the index as created, so upgrading users no longer need to manually build the playability index |
| 5.4.1.35 (Jvedio29.43) | 2026-09-07 | Video info adds "Subtitles" and "Subtitle Path" fields (after file size): detected from external SRT files, supporting both same-name xxx.srt and language-suffixed xxx.chs.srt / xxx.zh.srt naming; the "Copy video info" button automatically includes the new fields |
| 5.4.1.36 (Jvedio29.44) | 2026-09-07 | New "Subtitles" filter (below actor info, above duration; With/Without, click again to cancel): metadata adds a SubtitleExist column, rebuilt in the background together with the resource-existence index on startup/after scans (directory-level caching for speed), rebuilt on the spot when clicked to guarantee results match the disk; incrementally synced on file deletion/move; no manual action needed after upgrading |
| 5.4.1.37 (Jvedio29.45) | 2026-09-08 | Search box now supports space-separated keywords (Everything-style AND matching, applies to all search fields): typing "ESM 016" or "ESM016" both match hyphenated IDs like "ESM-016" (compact IDs auto-split); fixed the search popup "Tag" tab actually searching the Series field (renamed to "Series" with i18n for all three languages); blank-area right-click "All Data" menu now adds: tag all videos and advanced functions (translate title / screenshot / GIF / rename / strip leading zeros / three image-deletion options, each with a count confirmation) |
| 5.4.1.38 (Jvedio29.46) | 2026-09-08 | Tag list in the filter panel now supports drag-and-drop reordering: each tag has a six-dot grip handle on the left, hold and drag up/down to reorder with instant persistence (new tags are appended at the end); "All Data" batch operations semantics corrected to "the currently displayed result set" (including filter/search conditions, matching the pagination total, not the physical whole library) |
| 5.4.1.39 (Jvedio29.47) | 2026-09-08 | Video info "Subtitle Path" supports drag-to-pan: when the long path doesn't fit on one line, hold and drag left/right to reveal the hidden part (tooltip shows the full path on hover; short paths keep normal text selection) |
| 5.4.1.40 (Jvedio29.48) | 2026-09-08 | Fixed subtitle path drag not working (layout cause: unconstrained TextBox width meant the content never overflowed; now fills the remaining width); drag direction adjusted: drag right to reveal the hidden part, drag left to return to the beginning |
| 5.4.1.41 (Jvedio29.49) | 2026-09-08 | Subtitle path now uses standard text-box interaction: click shows the I-beam cursor, drag to select with a light-blue highlight, auto-scrolls at the edge to reveal everything, right-click to copy; fixed the "Duration" sort being wrong (legacy string values caused mixed-type misordering; now integer-sorted with unknown durations always last) |
| 5.4.1.42 (Jvedio29.50) | 2026-09-08 | Dual-track duration sorting: fixed the root cause of duration always sorting as strings (the "video" in the column name contains the substring "vid", hijacking the sort branch); the old "Duration" renamed to "Movie Duration" (scraped metadata); new "Video Duration" sort — by the real length of local video files (multi-part videos sum all parts), with a "Build Video Duration Index" button under Options-Index for background rebuild; opening the details page also records it lazily; unreadable files sort last as unknown |
| 5.4.1.43 (Jvedio29.51) | 2026-09-09 | Fixed the misleading screenshot error "screenshot count exceeds total frames": the real cause is usually an unreadable duration from corrupted video files (downloader pre-allocated empty shells); failures now diagnose on the spot and report the accurate reason with the full file path |
| 5.4.1.44 (Jvedio29.52) | 2026-09-13 | Fixed the non-working select-all buttons in the Genre/Series/Director/Studio filter panels (clicks gave no feedback): now uses direct panel references and applies the filter immediately on click; the Genre panel adds a tag search box that filters tags live as you type |
| 5.4.1.46 (Jvedio29.54) | 2026-09-13 | Fixed tag-filter false hits: selecting "高" also matched videos tagged "高画质" and any tag containing "高" (substring LIKE); switched to exact-tag matching (measured: 5881 → 205 videos) |
| 5.4.1.47 (Jvedio29.55) | 2026-09-13 | Unlocked the Series filter panel: a leftover hard-coded zero height from the original author kept the 5309 series tags loaded but permanently invisible and unselectable; now displayed and filterable normally |
| 5.4.1.48 (Jvedio29.56) | 2026-09-13 | Fixed two long-standing filter bugs: ① combining filter groups silently turned into a UNION instead of an intersection when a single tag/year was selected (e.g. Series+Genre showed 206 union results instead of the 5-video intersection); ② the Year filter never worked (the ReleaseYear column was never populated — all zeros), now matches by release date year |
| 5.4.1.49 (Jvedio29.57) | 2026-09-21 | Fixed two task-scheduler bugs on the sync-info / translate task pages: ① "Cancel All" stopped working after clicking "Restart All" (the restart loop kept pulling canceled tasks back in batches — now cancel/clear-list aborts the restart chain immediately, with a double-click guard); ② after completing tasks, clearing the list and adding new ones left them stuck in "Waiting" forever (a dispatcher exit race / dead work loop orphaned the queue — added a 4s liveness fallback that re-kicks the dispatcher) |


# Software Characteristics

## plugin

Including the following plug -in

- Ter skin plug -in
- Setal information plug -in


[<img src="https://s1.ax1x.com/2022/10/07/x8MJaj.png" alt="x8MJaj.png" style="zoom:80%;" />](https://imgse.com/i/x8MJaj)

**Skin plug -in supports a variety of skin switching**

[<img src="https://s1.ax1x.com/2022/10/07/x8MUGq.png" alt="x8MUGq.png" style="zoom:80%;" />](https://imgse.com/i/x8MUGq)


## Language

**Support Chinese, English, Japanese**


[<img src="https://s1.ax1x.com/2022/10/07/x8MydJ.png" alt="x8MydJ.png" style="zoom:80%;" />](https://imgse.com/i/x8MydJ)


## Multi-Video Library Management


[<img src="https://s1.ax1x.com/2022/10/07/x8KbvT.png" alt="x8KbvT.png" style="zoom:80%;" />](https://imgse.com/i/x8KbvT)


## Support NFO recognition import

[<img src="https://s1.ax1x.com/2022/10/07/x8M5LD.png" alt="x8M5LD.png" style="zoom:80%;" />](https://imgse.com/i/x8M5LD)


## Support information editing and modification

[<img src="https://s1.ax1x.com/2022/10/07/x8MTdH.png" alt="x8MTdH.png" style="zoom:80%;" />](https://imgse.com/i/x8MTdH)

## tag management/screening

- Profile batch addition/modification/delete marks
- Sef according to the mark

[<img src="https://s1.ax1x.com/2022/10/07/x8MLWt.png" alt="x8MLWt.png" style="zoom:80%;" />](https://imgse.com/i/x8MLWt)

## Rich search function

- Multi-field search: ID, title, path, actor, label, genre, series, studio, director
- Space-separated keywords (AND matching, Everything-style): "ESM 016" or "ESM016" both find "ESM-016"

[<img src="https://s1.ax1x.com/2022/10/07/x8MxOS.png" alt="x8MxOS.png" style="zoom:80%;" />](https://imgse.com/i/x8MxOS)

## New actor information

[<img src="https://s1.ax1x.com/2022/10/07/x8QAS0.png" alt="x8QAS0.png" style="zoom:80%;" />](https://imgse.com/i/x8QAS0)

## video processing function

- screenshot
- Cut GIF

[<img src="https://s1.ax1x.com/2022/10/07/x8QVyT.png" alt="x8QVyT.png" style="zoom:80%;" />](https://imgse.com/i/x8QVyT)

## Renamed Video Function

[<img src="https://s1.ax1x.com/2022/10/07/x8Qnw4.png" alt="x8Qnw4.png" style="zoom:80%;" />](https://imgse.com/i/x8Qnw4)

## Other functions

- Agenic display mode: shrinkage diagram, poster diagram

- The rich screening function: Whether the resources have been screening, whether there are screening in the picture, only displayed video, video type selection

[<img src="https://s1.ax1x.com/2022/10/07/x8Qr1P.png" alt="x8Qr1P.png" style="zoom:80%;" />](https://imgse.com/i/x8Qr1P)


- The rich right -click function

[<img src="https://s1.ax1x.com/2022/10/07/x8Qhhn.png" alt="x8Qhhn.png" style="zoom:80%;" />](https://imgse.com/i/x8Qhhn)

- Blank-area right-click "All Data" batch operations: add tag, translate title, screenshot/GIF, rename files, strip leading zeros from IDs, delete images, sync info, export video data (each with a count confirmation)

- Smart classification

[<img src="https://s1.ax1x.com/2022/10/07/x8QHnU.png" alt="x8QHnU.png" style="zoom:80%;" />](https://imgse.com/i/x8QHnU)

- upgrade

[<img src="https://s1.ax1x.com/2022/10/07/x8liHe.png" alt="x8liHe.png" style="zoom:80%;" />](https://imgse.com/i/x8liHe)


# Thanks

**Thanks to the following netizens for their contributions in the development of Jvedio **, I hope that with your support, `Jvedio` will develop better and better!
