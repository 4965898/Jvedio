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

- Smart classification

[<img src="https://s1.ax1x.com/2022/10/07/x8QHnU.png" alt="x8QHnU.png" style="zoom:80%;" />](https://imgse.com/i/x8QHnU)

- upgrade

[<img src="https://s1.ax1x.com/2022/10/07/x8liHe.png" alt="x8liHe.png" style="zoom:80%;" />](https://imgse.com/i/x8liHe)


# Thanks

**Thanks to the following netizens for their contributions in the development of Jvedio **, I hope that with your support, `Jvedio` will develop better and better!
