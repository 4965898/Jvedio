

[中文](README.md) [English](README_EN.md) [日本語](README_JP.md)


<h1 align="center">Jvedio</h1>




<div align="center" >
<img src="https://s1.ax1x.com/2022/06/11/XcePQf.png"><h3 >本地视频管理</h3>
</div>





---

[![.NET CORE](https://img.shields.io/badge/.NET%20Framework-4.7.2-d.svg)](#)
[![Platform](https://img.shields.io/badge/Platform-Win-brightgreen.svg)](#)
[![LICENSE](https://img.shields.io/badge/license-GPL%203.0-blue)](#)
[![Star](https://img.shields.io/github/stars/4965898/Jvedio?label=Star%20this%20repo)](https://github.com/4965898/Jvedio)
[![Fork](https://img.shields.io/github/forks/4965898/Jvedio?label=Fork%20this%20repo)](https://github.com/4965898/Jvedio/fork)

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`Jvedio` 是本地视频管理软件，支持扫描本地视频并导入软件，建立视频库，
提取出视频的 **唯一识别码**，自动分类视频，
添加标签管理视频，使用人工智能识别演员，支持翻译信息，
基于 `FFmpeg` 截取视频图片，Window 桌面端流畅美观的应用软件


官方网址：[Jvedio](https://hitchao.github.io/JvedioWebPage/) | 下载地址：[最新版本](https://github.com/4965898/Jvedio/releases)

---

[<img src="https://s1.ax1x.com/2022/10/07/x8KbvT.png" alt="x8KbvT.png" style="zoom:80%;" />](https://imgse.com/i/x8KbvT)


---
[<img src="https://s1.ax1x.com/2022/10/07/x8KOrF.png" alt="x8KOrF.png" style="zoom:80%;" />](https://imgse.com/i/x8KOrF)

---

[<img src="https://s1.ax1x.com/2022/10/07/x8MVVH.png" alt="x8MVVH.png" style="zoom:80%;" />](https://imgse.com/i/x8MVVH)

---

[<img src="https://s1.ax1x.com/2022/10/07/x8MZad.png" alt="x8MZad.png" style="zoom:80%;" />](https://imgse.com/i/x8MZad)

# 使用说明

开发者请看：[开发者文档](https://github.com/hitchao/Jvedio/wiki/20_Developer)

用户请看：[用户文档](https://github.com/hitchao/Jvedio/wiki/02_Beginning)


# 相关项目


|项目|网址|
|--|--|
|Jvedio 官方网页|[JvedioWebPage](https://github.com/hitchao/JvedioWebPage)|
|Chrome（360极速浏览器） 插件|[Jvedio-Chrome-Extensions](https://github.com/hitchao/Jvedio-Chrome-Extensions)|
|Jvedio 升级的服务器源|[jvedioupdate](https://github.com/hitchao/jvedioupdate)|
|Gif 控件修改于|[WpfAnimatedGif](https://github.com/hitchao/WpfAnimatedGif)|

# 自改版本记录

> 原仓库（hitchao/Jvedio）已归档，自 5.4.0.1 起由 [4965898/Jvedio](https://github.com/4965898/Jvedio) 接手维护，以下为自改版本变更记录：

| 版本 | 发布日期 | 主要内容 |
|---|---|---|
| 5.4.0.1 | 2025-12-04 | 增加批量删图功能；增加按图片筛选功能 |
| 5.4.0.2 | 2025-12-10 | 优化海报图、缩略图筛选功能 |
| 5.4.0.3 | 2025-12-30 | 维护性更新 |
| 5.4.0.4 | 2026-02-21 | 增加失败同步信息一键重启功能；增加额外筛选功能 |
| 5.4.0.5 | 2026-05-02 | 增加按演员信息有无筛选；其他优化 |
| 5.4.0.6 | 2026-08-10 | 翻页渲染优化（修复快速翻页闪退、越翻越慢）；图片存在性索引自动重建；启动扫描后台化；JavBus 刮削修复；SQLite 锁冲突修复（database is locked 卡顿假死）；刮削任务持久化（保存任务、启动后由用户手动继续）；识别码（番号）数字序排序修复；在线观看跳转（27 个站点、网址可自定义）；标题翻译功能（AI/ChatGPT 兼容 + 百度/Google/DeepL 等机器翻译平台，每个平台独立配置） |
| 5.4.1.8（Jvedio29.16） | 2026-08-16 | 基于原仓库 issues 批量修复 12 项：头像拉伸、右键菜单方向、名称排序、URL 双前缀、NFO uniqueid 与 Kodi 兼容、NFO 兜底识别、识别码 -U/-UC 修正标记、浏览页码记忆、演员头像独立目录、CSV 导出、ISO/.strm 支持 |
| 5.4.1.9（Jvedio29.17） | 2026-08-17 | 导出增强（CSV/Excel/JSON 三格式 + 一键导出全库）；演员新增鞋码字段、罩杯下拉选择、身高/体重单位 |
| 5.4.1.10（Jvedio29.18） | 2026-08-17 | 批量导出 NFO；演员生日日历选择 + 年龄实时计算 |
| 5.4.1.11（Jvedio29.19） | 2026-08-17 | 右键导出改为「选中影片」语义；翻译失败错误详情（状态码 + 响应体） |
| 5.4.1.12（Jvedio29.20） | 2026-08-17 | 编辑页新增中文标题栏；翻译配置独立持久化（每平台独立）；必应翻译 Region 头修复 |
| 5.4.1.13（Jvedio29.21） | 2026-08-17 | 翻译配置迁移至 data 目录（旧配置自动迁移）；必应源语言 auto 修复 |
| 5.4.1.14（Jvedio29.22） | 2026-08-17 | 修复单选影片右键翻译标题无反应 |
| 5.4.1.15（Jvedio29.23） | 2026-08-17 | 选项-界面新增显示设置：跟随系统缩放 + 界面字号滑条 |
| 5.4.1.16（Jvedio29.24） | 2026-08-17 | 修复设置页打开报错（DpiConfig 静态绑定命名空间前缀） |
| 5.4.1.17（Jvedio29.25） | 2026-08-17 | 字号滑条覆盖全部硬编码字号（12/13/14/15 共 89 处），侧边栏等全部界面文字跟随缩放 |
| 5.4.1.18（Jvedio29.26） | 2026-08-17 | 修复 db（JavDB）刮削器导演/评分字段无法刮削（导演标签修正 + ParseDictInfo 支持 float + 评分五分制归一）；补全发行商（Publisher）字段（详情页/编辑页 + 三语，JavBus 解析發行商）；修复「两个发行商」显示问题（SuperControls 将 Studio 键误译为发行商，已覆盖为制作商）；在线观看按钮排序（JavDB/JavBus/JAVLib 置顶、MISSAV 第四） |
| 5.4.1.19（Jvedio29.27） | 2026-08-19 | 修复「可播放/不可播放」筛选残留过期结果：新增资源存在性（可播放）索引自动维护（DataIndexManager）——启动进入库、扫描完成后均后台静默重建 metadata.PathExist（受「扫描后重建资源存在索引」开关控制），删除文件/修改本地路径/移动重命名时增量同步，升级后无需再手动点「建立资源存在索引」；修复启动重建巨型事务导致的 database is locked 卡顿闪退（改轻量查询 + 分块小事务）；DB（JavDB）爬虫补齐「发行商（Publisher）」自动刮削（二进制插件反编译还原为源码重建，原缺失發行字段） |
| 5.4.1.20（Jvedio29.28） | 2026-08-21 | 修复批量翻译标题只翻译前一两部就停止的问题（根因：后台线程枚举活动选中列表被 UI 操作修改抛「集合已修改」，改为先快照再逐部入队翻译任务）；翻译任务化并新增翻译任务页：右下角状态栏（下载/扫描/截图旁）新增翻译按钮（文+T 图标），点击打开翻译任务页，支持取消所有/重启失败/清除列表、单任务取消/重启、逐条状态与总体进度条，与下载任务模块一致 |
| 5.4.1.21（Jvedio29.29） | 2026-08-21 | 移除翻译开始后自动跳转翻译任务页的行为（不打断当前浏览），翻译任务页改由右下角「文+T」按钮手动打开 |
| 5.4.1.22（Jvedio29.30） | 2026-08-21 | 翻译图标重绘为翻译软件通用样式：「文/T」等大字号、中间斜杠分隔，颜色/背景与其他三个状态栏图标一致 |
| 5.4.1.23（Jvedio29.31） | 2026-08-21 | 修复翻译中无法追加新批量：翻译任务支持运行中添加（旧任务完成后自动接续，与同步信息模块一致），按识别码去重防重复翻译 |
| 5.4.1.24（Jvedio29.32） | 2026-08-21 | 修复影片详情页左右翻页箭头失效（异步复位卡死、调度优先级饿死、翻页列表快照过期自愈、翻页写库移后台）；演员详情页名字旁新增一键复制按钮；翻译图标字号调小 |
| 5.4.1.25（Jvedio29.33） | 2026-08-21 | 翻译图标字号 9 → 10（微调） |
| 5.4.1.26（Jvedio29.34） | 2026-08-21 | 翻译图标布局仿 Google Translate：「文」左上、「/」居中、「T」右下，字号均 12 |
| 5.4.1.27（Jvedio29.35） | 2026-08-21 | 翻译图标字号 12 → 10（12 时文/T 重叠） |
| 5.4.1.28（Jvedio29.36） | 2026-08-21 | 撤销翻译图标角落布局（仍重叠）：恢复横排「文/T」10 号字 |

# 版本计划

---

<img src="https://s1.ax1x.com/2023/03/26/ppseG9K.png" alt="x8MJaj.png" style="zoom:80%;" />

---

<img src="https://s1.ax1x.com/2023/03/26/ppseM7R.png" alt="x8MJaj.png" style="zoom:80%;" />








# 软件特性

## 插件

包含以下插件

- 皮肤插件
- 同步信息插件

[<img src="https://s1.ax1x.com/2022/10/07/x8MJaj.png" alt="x8MJaj.png" style="zoom:80%;" />](https://imgse.com/i/x8MJaj)

**皮肤插件支持多种皮肤切换**

[<img src="https://s1.ax1x.com/2022/10/07/x8MUGq.png" alt="x8MUGq.png" style="zoom:80%;" />](https://imgse.com/i/x8MUGq)

## 语言

**支持中文、英语、日语**

[<img src="https://s1.ax1x.com/2022/10/07/x8MydJ.png" alt="x8MydJ.png" style="zoom:80%;" />](https://imgse.com/i/x8MydJ)


## 多影视库管理

[<img src="https://s1.ax1x.com/2022/10/07/x8KbvT.png" alt="x8KbvT.png" style="zoom:80%;" />](https://imgse.com/i/x8KbvT)

## 支持 NFO 识别导入

[<img src="https://s1.ax1x.com/2022/10/07/x8M5LD.png" alt="x8M5LD.png" style="zoom:80%;" />](https://imgse.com/i/x8M5LD)

## 支持信息编辑与修改

[<img src="https://s1.ax1x.com/2022/10/07/x8MTdH.png" alt="x8MTdH.png" style="zoom:80%;" />](https://imgse.com/i/x8MTdH)

## 标记管理/筛选

- 支持批量添加/修改/删除标记
- 根据标记进行筛选

[<img src="https://s1.ax1x.com/2022/10/07/x8MLWt.png" alt="x8MLWt.png" style="zoom:80%;" />](https://imgse.com/i/x8MLWt)

## 丰富的搜索功能

[<img src="https://s1.ax1x.com/2022/10/07/x8MxOS.png" alt="x8MxOS.png" style="zoom:80%;" />](https://imgse.com/i/x8MxOS)

## 新增演员信息

[<img src="https://s1.ax1x.com/2022/10/07/x8QAS0.png" alt="x8QAS0.png" style="zoom:80%;" />](https://imgse.com/i/x8QAS0)

## 视频处理功能

- 截图
- 截取 GIF

[<img src="https://s1.ax1x.com/2022/10/07/x8QVyT.png" alt="x8QVyT.png" style="zoom:80%;" />](https://imgse.com/i/x8QVyT)

## 重命名影片功能

[<img src="https://s1.ax1x.com/2022/10/07/x8Qnw4.png" alt="x8Qnw4.png" style="zoom:80%;" />](https://imgse.com/i/x8Qnw4)



## 其他功能

- 图片展示模式：缩略图、海报图

- 丰富的筛选功能：资源自否存在筛选、图片是否存在筛选、仅显示分段视频、视频类型选择

[<img src="https://s1.ax1x.com/2022/10/07/x8Qr1P.png" alt="x8Qr1P.png" style="zoom:80%;" />](https://imgse.com/i/x8Qr1P)


- 丰富的右键功能

[<img src="https://s1.ax1x.com/2022/10/07/x8Qhhn.png" alt="x8Qhhn.png" style="zoom:80%;" />](https://imgse.com/i/x8Qhhn)

- 智能分类

[<img src="https://s1.ax1x.com/2022/10/07/x8QHnU.png" alt="x8QHnU.png" style="zoom:80%;" />](https://imgse.com/i/x8QHnU)

- 升级

[<img src="https://s1.ax1x.com/2022/10/07/x8liHe.png" alt="x8liHe.png" style="zoom:80%;" />](https://imgse.com/i/x8liHe)

# 鸣谢

**感谢以下网友在 Jvedio 开发中的贡献**，希望在大家的支持下， `Jvedio` 发展的越来越好！


板块|网友
:--:|:--:
UI|青萍之末, Engine, Erdon, Erik
调试|Sheldon, SHAWN, dddsG, EEE, Jion 等人
赞助支持|小猪培根 等众多网友