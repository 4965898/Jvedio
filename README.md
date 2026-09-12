

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
| 5.4.1.29（Jvedio29.37） | 2026-08-24 | 演员详情页新增「在线搜索」跳转按钮（JavDB/JavBus/JAVLib 前三、MISSAV 第四，位于爱好下方，样式同影片详情页在线观看按钮，点击按演员名搜索） |
| 5.4.1.30（Jvedio29.38） | 2026-08-24 | 在线观看/在线搜索跳转重构为「根地址联动」：站点拆分为根地址（域名）+ 影片路径 + 搜索路径，影片详情页与演员详情页共用根地址（选项-网络只填域名，换镜像站一处生效），演员页移植全部 27 个站点且按影片页顺序排列 |
| 5.4.1.31（Jvedio29.39） | 2026-08-24 | 修正 4 个站点搜索路径（123AV `cn/search`、JAVMENU `zh/search?wd`、Jav.Guru `jav-actress-list/?taxonomy_search`、JAVLib `searchstar.php`）；修复演员页按钮不换行导致面板被拉宽覆盖影片信息（WrapPanel 加 MaxWidth，3-4 个一行换行） |
| 5.4.1.32（Jvedio29.40） | 2026-08-24 | 新增演员英文名（罗马字）功能：actor_info 加 ActorNameEN 列；刮削时从 JavDB 演员 slug 自动提取罗马字；新增假名→罗马字转换算法（RomajiConverter）兜底；演员列表右键「转换英文名」批量转换；详情页名字下方显示英文名（带复制按钮、样式与名字一致）；编辑页可改英文名；演员导出含英文名列 |
| 5.4.1.33（Jvedio29.41） | 2026-08-24 | 英文名行改为始终显示（不再为空时隐藏）；演员详情页操作按钮行新增「EN」按钮（转换当前演员英文名，假名可转则自动填、含汉字则提示手动填写） |
| 5.4.1.34（Jvedio29.42） | 2026-09-07 | 修复「可播放/不可播放」筛选结果与实际相反：索引只是上次重建时的快照（会话期间文件被外部增删/移动、或建索引时移动硬盘未就绪都会过时），现点击「不可播放/可播放」时先按当前磁盘状态现场重建索引（带「正在校验文件」遮罩）再筛选；现场重建成功后自动标记索引已建立，升级用户无需再手动建立播放索引 |
| 5.4.1.35（Jvedio29.43） | 2026-09-07 | 视频信息新增「有无字幕」「字幕地址」两个字段（位于文件大小之后）：根据外挂 SRT 文件判断，支持同名 xxx.srt 与带语言后缀的 xxx.chs.srt / xxx.zh.srt 等命名；右上角「复制视频信息」自动包含新字段 |
| 5.4.1.36（Jvedio29.44） | 2026-09-07 | 筛选器新增「字幕」筛选项（演员信息之下、时长之上，有字幕/无字幕、可再次点击取消）：metadata 新增 SubtitleExist 列，启动/扫描后随资源存在索引一并后台重建（目录级缓存加速），点击时现场重建保证结果与磁盘一致；删除/移动文件时增量同步；升级后无需任何手动操作 |
| 5.4.1.37（Jvedio29.45） | 2026-09-08 | 搜索框支持空格分词（模仿 Everything 的多词 AND 匹配，全部搜索字段生效）：搜「ESM 016」或「ESM016」均可命中「ESM-016」这类带连字符番号（紧凑番号自动拆分）；修复搜索弹层「Tag」页签实际搜索的是系列字段的问题（更正为「系列」并补齐三语）；空白处右键「全部资源」新增：添加标记（全库一键打标）与扩展功能（翻译标题/生成截图/生成GIF/重命名文件/清除识别码前导零/三种删图，均带数量确认防误触） |
| 5.4.1.38（Jvedio29.46） | 2026-09-08 | 筛选面板标记列表支持拖拽排序：每个标记左侧新增六点拖拽手柄，按住手柄上下拖动即可重排，顺序即时保存（新建标记自动排在末尾）；「全部资源」批量操作语义修正为「当前展示的结果集」（含筛选/搜索条件，与页面分页总数一致，而非物理全库） |
| 5.4.1.39（Jvedio29.47） | 2026-09-08 | 视频信息「字幕地址」支持拖拽平移查看：长路径单行显示不全时，按住左右拖动即可查看被遮挡部分（悬停提示显示完整路径；路径较短时保持正常选中文本） |
| 5.4.1.40（Jvedio29.48） | 2026-09-08 | 修复字幕地址拖拽无效的问题（布局原因：输入框宽度不受限导致内容永不溢出，改为填充剩余宽度）；拖拽方向调整为：向右拖查看被遮挡部分、向左拖回到开头 |
| 5.4.1.41（Jvedio29.49） | 2026-09-08 | 字幕地址改为标准文本框交互：单击出现 I 型光标、拖动框选出淡蓝色选区、拖到边缘自动滚动查看全部、右键可复制；修复「时长」排序错误（历史字符串值导致乱序，改整数排序且未知时长恒排末尾） |
| 5.4.1.42（Jvedio29.50） | 2026-09-08 | 时长排序双轨制：修复排序分支误匹配导致时长始终按字符串排序的根因（"video" 含 "vid" 子串）；原「时长」更名「影片时长」（刮削元数据排序）；新增「视频时长」排序——按本地视频文件真实长度（分段视频取各段之和），选项-索引新增「建立视频时长索引」后台重建，打开详情页也会自动补录，读不到的按未知排末尾 |
| 5.4.1.43（Jvedio29.51） | 2026-09-09 | 修复截图报「需要获取的截图数量超出了视频的总帧数」的误导性提示：真实根因多为视频文件损坏（下载器预分配的空壳文件）导致时长读取失败，现在失败时现场诊断并给出准确原因 + 完整文件路径 |
| 5.4.1.44（Jvedio29.52） | 2026-09-13 | 修复筛选器「类别/系列/导演/制作商」四个标签面板全选按钮失效（点击后无任何反馈）：改为直达面板引用 + 点击后立即应用筛选；「类别」筛选器新增标签搜索框，输入关键词实时过滤标签 |
| 5.4.1.46（Jvedio29.54） | 2026-09-13 | 修复标签筛选误命中：勾选「高」会把「高画质」等同前缀标签的影片全部筛出（子串 LIKE 匹配），改为整标签匹配后只命中精确标签的影片（实测 5881 部 → 205 部） |
| 5.4.1.47（Jvedio29.55） | 2026-09-13 | 解锁「系列」筛选面板：原作者遗留的高度锁死导致系列标签（5309 个）已加载但永不可见、无法勾选，现恢复正常显示与筛选 |
| 5.4.1.48（Jvedio29.56） | 2026-09-13 | 修复两个原版遗留筛选 bug：① 跨筛选组叠加变并集——单选一个标签/年份时组间 AND 变 OR（如 系列+类别 应显示交集 5 部却显示并集 206 部）；② 年份筛选完全失效（ReleaseYear 列从未填充，全库为 0），改按发行日期年份匹配 |

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

- 支持识别码、标题、路径、演员、标签、类别、系列、制作商、导演多字段搜索
- 空格分词搜索（多词 AND 匹配，模仿 Everything）：搜「ESM 016」或「ESM016」都能找到「ESM-016」

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

- 空白处右键「全部资源」批量操作：添加标记、翻译标题、生成截图/GIF、重命名文件、清除识别码前导零、删除图片、同步信息、导出影片数据（均带数量确认）

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