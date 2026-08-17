# Jvedio 维护日志 V5.4.0.6

> 本文档沉淀自 2025-12 起对 Jvedio（WPF 本地视频管理软件）的接手维护与二次开发实践，供后续开发参考。
> 最后更新：2026-08-16

---

## 一、项目背景与接手现状

### 1.1 仓库沿革

| 仓库 | 角色 | 状态 | 时间范围 |
|---|---|---|---|
| `hitchao/Jvedio` | 原作者仓库 | **已归档（archived）**，最后提交 2023-12-18 | 2022-06 ~ 2023-12 |
| `4965898/Jvedio` | 接手 fork（`fork: true`，`parent: hitchao/Jvedio`） | 活跃，默认分支 `master` | 2025-12-04 起 |
| 本地 `a:\Trae\repository\Jvedio-1` | 开发工作树 | origin 已切到 `4965898/Jvedio`，源码已 commit（`e5a8e36`）并 push | — |

### 1.2 接手后的关键现状（务必知晓）

1. **源码已入库**：2026-08-09 起 origin 已切到 `4965898/Jvedio`，25 个改动文件 + Bus2 爬虫新目录已 commit（`e5a8e36`「接手维护：修复图片存在性索引、新增Bus2爬虫、重构筛选与下载层」）并 push 到 fork master，不再走「只发 exe 不提交源码」的旧流程。
2. **本地工作树状态**：源码改动已全部提交；`build-output/`（编译产物 + 打包脚本）、`nuget.exe`（8MB 工具）、`LULU-430.txt`（JavBus 抓取测试样本）、本文档按约定**未入库**（刻意保留为本地文件）。
3. **本地 master**：`0d29c1d`（hitchao 最后 commit）→ `e5a8e36`（接手维护汇总 commit），远程 fork master 已同步。
4. **原仓库已归档**，无法向上游提 PR，所有维护只能在自己 fork 内进行。

### 1.3 技术栈

- .NET Framework 4.7.2 / WPF
- SQLite（`app_datas.sql` 定义 schema）
- HtmlAgilityPack（HTML 解析）
- SuperUtils / SuperControls.Style（原作者自有库）
- 插件式爬虫：DLL 动态加载，契约见 `Document/爬虫插件示例/bus/main.json`

---

## 二、版本发布时间线

接手后版本号从 `5.4.0.1` 递增到 `5.4.0.5`，命名统一为「自改X.Y.Z」，发布说明即变更日志：

| 版本 | 发布日期 | 主要内容（来自 Release Body） |
|---|---|---|
| v5.4.0.1 | 2025-12-04 | 增加批量删图功能；增加按图片筛选功能 |
| 5.4.0.2 | 2025-12-10 | 优化海报图、缩略图筛选功能 |
| 5.4.0.3 | 2025-12-30 | （无说明） |
| 5.4.0.4 | 2026-02-21 | 增加失败同步信息一键重启功能；增加额外筛选功能 |
| 5.4.0.5 | 2026-05-02 | 增加按演员信息有无筛选；其他优化 |
| 5.4.0.6（开发中） | 2026-08-10 | 翻页渲染优化：修复快速翻页闪退（渲染单飞串行化）、修复越翻越慢（渲染移后台线程、图片缓存加内存上限、关联查询邻接表缓存等，见 3.9）；图片存在性索引后台静默重建（ImageIndexManager，见 3.10）；启动扫描后台化 + 右下角扫描状态指示（见 3.11）。**2026-08-16 追加**：SQLite 锁冲突修复（WAL+busy_timeout+统计/评分异步化+Mapper 锁，见 3.12）；刮削任务持久化断点恢复（见 3.13）；CF 验证识别提示（3.14）；演员头像下载修复（3.15）；在线观看跳转（3.16）+ 站点网址自定义（3.17）+ 设置页闪退修复/绿底按钮/恢复任务不自动开始（3.18）+ 网址填写提示（3.19~3.21）+ 冒号错位（3.22）+ VID 排序（3.23）+ 翻译标题（3.24~3.26）。**2026-08-16 发布 `5.4.1.7`（Jvedio29.15）**：完整 zip 已发布至 GitHub Release（下载指引 + 改进总结 + 历史记录三段式 body），源码 commit `ea5eb32` 已推送 |
| 5.4.1.8（Jvedio29.16） | 2026-08-16 | 基于原仓库 issues（hitchao/Jvedio）批量修复 12 项：头像拉伸（#436）、右键菜单方向（#398）、名称排序（#437/#362）、URL 双前缀（#421/#371）、NFO uniqueid（#425）、NFO 兜底识别（#415/#381）、-U/-UC 未修正标记（#424）、浏览页码记忆（#430/#241）、NFO 导出 Kodi 兼容（#429/#388）、演员头像独立目录（#445/#338/#270）、CSV 导出（#346/#212）、ISO/.strm（#401/#200），见 3.27 |
| 5.4.1.9（Jvedio29.17） | 2026-08-17 | 导出功能增强 + 演员字段增强（见 3.28）：导出三格式（CSV/Excel/JSON）+ 选项-库「导出本库所有影片」按钮 + 空白处右键「全部功能」加导出；演员新增鞋码字段（详情/编辑页爱好上方）、罩杯改下拉菜单（A-Z）、身高/体重加单位（CM/KG） |
| 5.4.1.10（Jvedio29.18） | 2026-08-17 | 导出 NFO + 生日日历/年龄实时（见 3.29）：新增批量导出 NFO（参考 sqlite2nfo.py，三处入口）；演员生日改日历选择（DatePicker，支持手填）+ 年龄按生日实时计算；修复鞋码输入框与标签间距 |
| 5.4.1.11（Jvedio29.19） | 2026-08-17 | 右键导出选中语义 + 设置页提示语位置 + 翻译报错诊断（见 3.30） |
| 5.4.1.12（Jvedio29.20） | 2026-08-17 | 编辑页翻译标题栏 + 翻译配置独立持久化 + 必应 Region 修复（见 3.31） |
| 5.4.1.13（Jvedio29.21） | 2026-08-17 | 翻译配置改存 data 目录 + 必应源语言 auto 修复（见 3.32） |
| 5.4.1.14（Jvedio29.22） | 2026-08-17 | 修复单选右键翻译标题无反应（见 3.33） |
| 5.4.1.15（Jvedio29.23） | 2026-08-17 | 选项-界面新增缩放设置：跟随系统缩放(PerMonitorV2) + 界面字号滑条（见 3.34） |
| 5.4.1.16（Jvedio29.24） | 2026-08-17 | 修复设置页打开报错：DpiConfig 静态绑定命名空间前缀错误（见 3.35） |
| 5.4.1.17（Jvedio29.25） | 2026-08-17 | 字号滑条覆盖全部硬编码字号（12/13/14/15，89 处）（见 3.36） |

> 这些发布说明与本地 diff 吻合，可互相印证。5.4.0.5 的 Release Body 已于 2026-08-09 更新为「下载指引 + 相对原版 5.4 的改进总结 + 原记录」三段式，源码也已同步 commit（见 1.2、第五章）。

---

## 三、接手开发的主要改动

### 3.1 数据库 schema 修复与安全迁移（最核心）

**问题**：`common_picture_exist` 表原唯一约束为 `unique(DataID, PathType, ImageType, Exist)`——把 `Exist`（0/1）纳入唯一键是设计缺陷，导致同一影片/路径类型/图片类型无法从「不存在」翻成「存在」，`INSERT OR REPLACE` 失效。

**修复**（三处一致改动）：
- [app_datas.sql](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Data/Sql/app_datas.sql)：约束改为 `unique(DataID, PathType, ImageType)`，`Exist` 退回普通列。
- [Sqlite.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/DataBase/Tables/Sqlite.cs)：新增 `SQL.PictureExistMigration` 迁移脚本，用 temp-table swap 模式重建表 + 索引。
- [MapperManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Mapper/MapperManager.cs)：`Init` 启动时通过 `sqlite_master.sql` 反射检测旧约束定义，命中即触发迁移；整段 `try/catch + Logger.Error`，**不阻断启动**。

**经验**：接手历史 SQLite 库时，schema 迁移必须做到「检测旧结构 → temp-table swap → 重建索引 → 失败可降级」，绝不能假设用户是新装。

### 3.2 新增 Bus2 爬虫（JavBus 刮削）

**位置**：[Core/Crawler/Bus2/BusCrawler/Class1.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/Bus2/BusCrawler/Class1.cs)（21KB，约 450 行），编译产出 `BusCrawler.dll`。

**契约**：遵循原版 DLL 插件接口（见 [bus/main.json](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Document/爬虫插件示例/bus/main.json)），暴露静态方法 `GetWebType / IsPluginAvailable / GetInfo`，由主程序通过 `CrawlerType=DLL` 反射加载。

**实现要点**：
- 目标站 `https://www.busjav.bond/{VID}`，支持 `dataInfo["Url"]` 覆盖 baseUrl（便于切换镜像域名）。
- 反爬：请求头设 `Referer = 页面URL`，默认 Cookie `existmag=all`（JavBus 看磁力链的前置 cookie）。
- 解析用 HtmlAgilityPack + XPath（`//span[@class='header']` 定位信息行），繁体字段名识别：`發行日期 / 長度 / 製作商 / 系列 / 導演`。
- 演员名多策略获取：img `title` → a `title` → 纯文本，再叠加正则备选（[Class1.cs:336-359](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/Bus2/BusCrawler/Class1.cs#L336-L359)）。
- **大量 `logs.Add(...)` 调试输出**（html-length、host、节点数量），便于线上排查解析失败。

**经验**：接手他人插件契约时，先严格复刻原接口签名（`GetInfo` 返回 `Dictionary<string,object>` + `Error/StatusCode/Logs` 约定），再在内部重写实现；日志要打到「字段级」，否则刮削失败无从诊断。

### 3.3 下载层 HttpClient 重写

[VideoDownLoader.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/VideoDownLoader.cs) 用 `HttpClient + HttpClientHandler` 替换原 `HttpHelper.AsyncDownLoadFile`：
- `AllowAutoRedirect = true`（自动跟随重定向）
- `AutomaticDecompression = GZip | Deflate`（自动解压）
- 显式挂载 `header.WebProxy` + `UseProxy = true`
- 默认超时 8000ms，默认 UA 伪装 Chrome 120
- `catch (Exception)` 兜底（原仅捕 `WebException`）

[DownLoadTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs) 配套增强：
- 海报/缩略图下载失败 → 延迟 1 秒重试一次
- 缩略图失败 → 备用 URL（`BigImageUrl` 的 `_b.jpg` → `_s.jpg`）回退
- 演员信息导入去掉「必须先有头像」硬限制，`Count` 不一致时 fallback 而非丢弃
- **修复 `header.TimeOut` 设置位置 bug**：原写在 `if (header == null)` 分支内，复用 header 时超时无效

**经验**：原 `HttpHelper` 不支持代理/重定向/gzip，是刮削在墙内/CDN 后失效的根因；重写时保留旧方法签名，避免牵连调用方。

### 3.4 筛选系统重构

[Filter.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/Filter.xaml) + [Filter.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewModels/VieModel_VideoList.cs) 把单一「图片类型」筛选拆成三个独立「存在性」维度：
- 海报：有海报 / 无海报
- 缩略图：有缩略图 / 无缩略图
- 演员信息：有演员 / 无演员

交互上实现「再次点击同一 RadioButton 取消选择」（用 `_lastCheckedXxxRadio` 字段记录上次选择）；`Refresh` 统一调 `ResetToDefault() + ApplyFilter()`，刷新即重置。

SQL 全部改为 `LEFT JOIN` + `IsNull` 判定，不再混用 `INNER JOIN` + 取反按钮：
- 海报/缩略图：`LEFT JOIN common_picture_exist` 两次（别名 `cpe_p` / `cpe_t`），按 `Exist=1` 是否 null 判断
- 演员：`LEFT JOIN metadata_to_actor`，按 `mta.ActorID >= 1` 判断

排序新增「演员」（`SortFields` 加 `ACTOR_FIRST_NAME`），用子查询取每个视频的第一个演员名，空值排末尾。

### 3.5 视频列表与图片管理

[VideoList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml.cs)：
- 右键菜单新增三项：仅删海报 / 仅删缩略图 / 同时删两者（删文件 + 清 ImageCache + 重置默认图 + 更新索引）
- `AutoGenScreenShot` 改为 `async`，截图循环放后台线程，UI 更新切回 `Dispatcher.BeginInvoke`，避免阻塞和跨线程异常
- `UpdateImageIndex` SQL 从 `insert or replace` 改为 `delete + insert`（按 `DataID + PathType` 先删再插，事务包裹），避免 `PathType` 不一致残留旧记录
- **修复参数误传 bug**：`UpdateImageIndex(currentVideo.DataID, false, true)` → `(currentVideo.DataID, true, true)`，原把 small 设 false 会丢小图状态
- 图片「存在」判定纳入截图：`small || hasScreen` / `big || hasScreen`

[Window_Details.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Details.xaml.cs)：详情页打开时回写图片存在性索引（`try/catch` 静默吞异常，避免索引写失败拖崩 UI）。

[Window_Main.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Main.xaml.cs)：新增 `Ctrl+A` 全选快捷键，复用既有 `VideoList.SelectAll`。

### 3.6 设置与并发配置

[Settings.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/WindowConfig/Settings.cs) 新增 `SyncConcurrency`（默认 2），[Window_Settings.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml) 在「同步选项」块加 UI（`SearchBox` 双向绑定 + 显示默认值参考），`SaveSettings()` 落盘。

[ProxyConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/ProxyConfig.cs)：`DEFAULT_TIMEOUT` 从 10s 改 8s，与 `VideoDownLoader` 默认 8000ms 对齐。

**经验**：刮削并发要可调（默认保守值 2），避免高并发被目标站封 IP；超时基线全局统一，不要各模块各定各的。

### 3.7 任务批量重试

[DownloadManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Tasks/DownloadManager.cs) 新增 `RestartAllFailed()`：筛 `TaskStatus.Canceled` 任务，按 `TASK_COUNT` 分批 `Restart()`，每批轮询 `Running/WaitingToRun` + `Task.Delay(TASK_DELAY)`，最后 `Start()`。

[TaskList.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/Tasks/TaskList.xaml) 新增「重启所有」按钮，可见性绑定 `onRestartAll` 是否非空（沿用 `NullToVisibilityConverter` 模式），[TabItemManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/ViewModels/TabItemManager.cs) 绑定到 `App.DownloadManager.RestartAllFailed`。

### 3.8 构建环境清理（接手第一件事）

[Jvedio.csproj](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Jvedio.csproj) PreBuildEvent：
- 删除 4 行依赖 `D:\SuperStudio\...` 绝对路径的 XCOPY（`Newtonsoft.Json.dll` / `SuperUtils.dll` / `SuperControls.Style.dll`）——这些原本从原作者本地源码目录抓 DLL，换机器就构建失败
- 剩余本地依赖拷贝（`JvedioLib.dll` / `Jvedio.ico` / `MediaInfo.dll`）全部加 `if exist` 守卫

**经验**：接手他人仓库第一件事是「脱离原作者环境」——把硬编码绝对路径、本地-only 依赖全部改成相对路径 + 存在性守卫，否则后续一切开发都无从谈起。

### 3.9 翻页渲染优化：闪退修复 + 越翻越慢（2026-08-10）

**现象**：
1. 翻页时前几页很顺滑、进度条一冲到底；随翻页数增多，进度条越来越慢；
2. 进度条没走完时快速点翻页，经常闪退。

**根因**（渲染链路 `VieModel_VideoList.Select/Render` 四个叠加问题）：
1. **并发渲染竞态（闪退主因）**：`Select()` 用 `while (Rendering) { RenderVideoCTS?.Cancel(); await Task.Delay(100); }` 轮询等待，快速连点时多个 `Select()` 会同时从等待中醒来，各自启动 `Render()`；而 `Render()` 开头没有重入保护，两个渲染循环并发操作同一个 `CurrentVideoList`——一个循环在结尾 `RemoveAt` 清多余项，另一个循环按自己捕获的旧 `idx` 访问 `CurrentVideoList[idx].DataID` → `IndexOutOfRangeException` / 集合被修改 → 未捕获异常直接闪退。
2. **token 生命周期错误**：`RefreshVideoRenderToken()` 直接把共享的 `RenderVideoCTS` 换掉，旧 token 无人取消；`catch (OperationCanceledException)` 里 `RenderVideoCTS?.Dispose()` 可能 dispose 掉新循环的 token。
3. **每页在 UI 线程做重活**：`SetAsso` → `GetAssociationDatas()` 每次调用都 `InitAdjacencyList()` **全表查询 `common_association` + O(N²) 递归 BFS**，一页 60 部影片 = 60 次全表查询；`SetImage` 磁盘 IO + JPEG 解码也在 UI 线程；`SetTagStamps` 对全局标签列表做线性扫描。
4. **图片缓存无上限**：`ImageCache` 用 `MemoryCache.Default`，默认滑动过期 10 分钟，翻页越多驻留 BitmapImage 越多 → LOH 大对象堆压力 + GC 变慢 → 「越翻越慢」。

**修复**（7 个文件，全部已编译通过）：
- [VieModel_VideoList.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewModels/VieModel_VideoList.cs)：**渲染串行化（单飞）**——新增版本号 `_RenderVersion` + 渲染任务 `_RenderTask`，翻页时版本号自增，旧渲染循环每条目检测版本不一致立即退出；新 `Select()` 通过 `await prevTask` 真正等待旧渲染结束才开始，彻底杜绝并发；DB 查询（count + select + 实体映射）移入 `Task.Run`；渲染循环整段移入后台线程，UI 集合更新从逐条 `BeginInvoke` 改为**每 12 条批量一次**；`RefreshVideoRenderToken()` 改为先取消+释放旧 token 再建新的。
- [AssociationMapper.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Mapper/Common/AssociationMapper.cs)：全表查询改**正向+反向邻接表缓存**（首次构建一次），`GetAssociationDatas` 改 BFS 按连通分量 O(E) 遍历；新增 `InvalidateCache()` 失效入口。
- [VieModel_SearchAsso.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Dialog/VieModels/VieModel_SearchAsso.cs)：`SaveAssociation()` 保存关联后调用 `InvalidateCache()`。
- [TagStamp.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/CommonSQL/TagStamp.cs)：新增 `TagStampDict`（TagID→TagStamp 字典），由 `TagStamps` 属性 setter 自动重建（覆盖 `Filter.InitTagStamp` 直接赋值的路径）。
- [Video.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/Video.cs)：`SetTagStamps` 从 `TagStamps.Where(Contains)` 线性扫描改为字典 O(1) 查找。
- [ImageCache.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Media/ImageCache.cs)：`MemoryCache.Default` 改**专属实例 + 512MB / 物理内存 25% 上限**；后台线程解码的图片自动 `Freeze()`；`Clear()` 的 `Dispose`（会导致缓存实例不可用）改 `Trim(100)`。
- [MetaData.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/MetaData.cs)：默认三张图改为 `CacheOption.OnLoad` + `Freeze()`，后台线程可安全引用（渲染移到后台后原本是跨线程隐患）。

**验证**：Release 编译通过；实测快速连点翻页不再闪退，连续翻页进度条保持匀速。本次构建产物为 `Jvedio.exe` / `Jvedio27.exe`（bin\Release）。**注意：本次改动尚未 commit/push**（截至 2026-08-10），发版前须按 5.4 流程提交。

**经验**：
- WPF 任何「翻页/加载 + 可打断」的需求，一律「**版本号 + 单飞**」：新请求先自增版本号让旧循环尽快退出，再 await 旧渲染任务真正结束，绝不 `while + Rendering` 轮询假等待——那是并发渲染闪退的温床。
- `ObservableCollection` 严禁并发写；UI 更新宁可攒批（每 N 条一次 `BeginInvoke`），也不要逐条往返 Dispatcher。
- 「每个视频 / 每次调用全表查询」是隐形性能黑洞（此次 `SetAsso` 一页 60 次全表查询）；全局缓存必须带**失效入口**，数据变更处显式调用。
- `MemoryCache` 必须设内存上限，否则滑动过期救不了「越用越慢」。
- 图片从后台线程产出自用的话，`BitmapImage` 必须 `Freeze()`（含静态默认图）。

### 3.10 图片存在性索引后台静默重建（ImageIndexManager，2026-08-10）

**背景**：3.1 已修复 `common_picture_exist` 唯一约束的错误设计，索引**正确性**解决，但**时效性**仍靠用户在「选项-库」手动「建立图片索引」——刮削新拿到海报/缩略图、截图生成后，筛选「无海报图 / 无缩略图」会残留过期结果。

**实现**（新增 [ImageIndexManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Tasks/ImageIndexManager.cs)，146 行静态类）：
- **事件订阅**（静态构造函数）：`DownLoadTask.onDownloadSuccess`（刮削成功；注意**同一任务会触发两次回调**，用 `_LastCountedTask` 判等去重，只计一次）+ `ScreenShotTask.onScreenShotCompleted`（截图完成也影响「以截图当海报/缩略图」的存在性判定），每成功一条 `Count(1)`。`App.xaml.cs` 启动时 `ImageIndexManager.Init()` 触发静态构造函数完成订阅。
- **阈值防抖**：累计 `_PendingCount` 达 `Settings.AutoRebuildImageIndexCount`（默认 10，0=关闭）才触发一次后台重建，避免每刮一部就全库重建一次。
- **单飞防并发**：`_Rebuilding` 标志位；重建结束后 `finally` 里 `Volatile.Read` 复查 `_PendingCount` 是否又攒够阈值，够了连下一次，全程只有一条重建链路。
- **重建逻辑与手动索引一致**：全量 `Select(DataID, Path, VID, Hash)` → 逐条 `File.Exists` 判定小图/大图 + 截图目录任意文件即视为海报/缩略图存在 → `begin; delete where PathType; insert ...; commit;` 事务重建，`Logger.Info` 静默记录条数；异常仅 `Logger.Error`，**绝不打断刮削主流程**。
- **设置 UI**：「选项-库」新增 SearchBox「刮削获得图片后自动重建索引的累计条数(0为关闭)」（i18n key `AutoRebuildImageIndex`，zh-CN / en-US / ja-JP 三语同步；`SaveSettings()` 时 `<0` 归一为 0）。

**经验**：全局索引的时效性要靠「变更计数 + 阈值防抖 + 后台单飞重建」自动闭环，不要指望用户手动点按钮；事件驱动的计数必须留意「同一事件多次触发」的去重（`onDownloadSuccess` 每任务触发两次）；后台任务失败只能记日志，绝不能向上抛打断主流程。

### 3.11 启动扫描后台化与扫描状态指示（2026-08-10）

**现象**：进入库时 `WindowStartUp.LoadDataBase` 用 `while (ScanTask.Running) { await Task.Delay(100); }` 死等启动扫描完成，扫描目录大时要卡几秒~十几秒才能进主界面。

**修复**（[WindowStartUp.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/WindowStartUp.xaml.cs) + [Window_Main.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Main.xaml) + [Window_Main.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Main.xaml.cs)）：
- `LoadDataBase()` 去掉 `while` 等待：`ScanTask` 注册进 `App.ScanManager` 后立即 `Start()`，主窗口马上 `Show()`；扫描改为 `onCompleted` 回调 `ScanCompleteInBackground` 驱动补充动作。
- **主窗口就绪竞态收敛**：回调里 `Dispatcher.Invoke` 检查主窗口是否已 `IsLoaded`——未就绪置 `BackgroundScanPending` 标志，等 `main.Show()` 之后补刷一次，再调 `main.OnBackgroundScanComplete`；已就绪直接调。
- `OnBackgroundScanComplete(insertVideos)`：静默刷新统计 `Statistic()` + 按 `LoadDataAfterScan` 加载数据 + 按 `ScreenShotAfterImport` 补截图，**不弹窗打扰**。
- **右下角扫描状态圈**（Window_Main.xaml）：绑定 `App.ScanManager.Running`；运行中 = 旋转高亮圆圈（`RotateTransform`，DataTrigger 驱动），空闲 = 绿圈白勾，扫描/索引进行中一眼可见。

**经验**：启动路径上的「等后台任务完成」一律改「先开主界面 + 完成回调补齐」；回调与主窗口 Initialized 存在竞态时用「pending 标志 + `Show()` 后补刷」收敛，比 `while 轮询` 假等待可靠得多（同 4.7 的教训一脉相承）。

### 3.12 SQLite 锁冲突修复：database is locked 卡顿假死（2026-08-16）

**现象**：刮削多条信息（后台写库密集）时在详情页点评分，UI 突然卡顿假死数十秒，随后抛 `code = Busy (5), database is locked`（堆栈：`Rate_ValueChanged → RefreshGrade → Statistic → SelectCount`）。

**根因**（三层叠加）：
1. **连接串零配置**：所有 mapper 连接串只有 `data source=`（SuperUtils.dll 内部拼串，无法改 DLL），无 `busy_timeout` / WAL。System.Data.SQLite 默认 busy 等待结束后直接抛 SQLITE_BUSY；刮削并发 2 + 图片索引重建 + UI 评分写库共用同一数据库文件，写锁互相顶。
2. **统计在 UI 线程全同步**：评分 → `onStatistic` → `VideoSideMenu.Statistic` 在 UI 线程执行 10 个查询 + 1 次写（`appDatabaseMapper.UpdateFieldById("Count", ...)`），与后台刮削写库撞锁时整个 UI 等锁 → 假死 → 超时抛异常。
3. **SqliteMapper 复用单条 SQLiteCommand 无锁**：DLL 内每个 mapper 一把 command，跨线程并发调用会互相覆盖 `CommandText`（潜在数据错乱/诡异异常，只是概率低未暴露）。

**修复**（7 处）：
- [MapperManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Mapper/MapperManager.cs)：`Init()` 末尾新增 `ApplySqlitePragmas()`——反射遍历全部 15 条连接统一执行 `PRAGMA journal_mode=WAL;`（读写不互斥，大幅消除锁冲突）、`PRAGMA busy_timeout=30000;`（撞锁等待 30s 而非立刻报错）、`PRAGMA synchronous=NORMAL;`（WAL 下安全、减少 fsync）。**注意 busy_timeout/synchronous 是连接级**，必须逐连接执行；journal_mode 是文件级，执行一次持久生效。失败仅 `Logger.Error`，绝不阻断启动。
- [VideoSideMenu.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoSideMenu.xaml.cs)：`Statistic` 异步化——查询全部移 `Task.Run`，9 个 UI 属性经 `Dispatcher.BeginInvoke` 批量回填；`_StatisticBusy/_StatisticPending` 防抖 + 完成后 pending 重跑（评分拖动连点不漏统计）；写库计数包 try/catch 后台执行。
- [BaseMapper.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Mapper/BaseMapper/BaseMapper.cs)：新增 per-T `MapperLock`（泛型静态字段，每个 T 一把），12 个转发到 `SqliteMapper` 的方法全部 `lock` 串行化——从根上消除共享 SQLiteCommand 竞态（含刮削并发 2 对同一 mapper 的并发读）。
- 评分写库异步化 4 处：`VieModel_Details.SaveLove`、[VideoList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml.cs) `Rate_ValueChanged`、[ViewVideo.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewVideo.xaml.cs) `Rate_ValueChanged`、[Window_Details.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Details.xaml.cs) `AssocDataRate_ValueChanged`——UI 线程不再做任何等锁的写库。

**验证**：临时库实测 `PRAGMA journal_mode=WAL` 返回 `wal`、`busy_timeout` 设置生效；Release 编译通过（2026-08-16，产物 `Jvedio.exe` / `build-output/Jvedio29.exe`）。注意：**运行时实测被单实例互斥挡住**（用户旧版 Jvedio28.exe 正在运行），需用户关旧版后实测。

**经验**：
- 接手 DLL 黑盒（连接串不可改）时，锁冲突用「PRAGMA 逐连接修复」绕开，无需动 DLL。
- 「UI 假死 + 超时异常」的组合拳是：**WAL（读不阻塞写）+ busy_timeout（撞锁等待）+ 异步化（UI 线程永不碰锁）**，三件套缺一不可。
- 一切「每 mapper 复用单 command」的 ORM 黑盒，跨线程调用必须加 per-instance 锁，否则并发刮削迟早踩 CommandText 竞态。

### 3.13 刮削任务持久化：闪退/退出后断点恢复（2026-08-16）

**背景**：刮削任务全部在内存（`TaskDispatcher` 队列 + `CurrentTasks`），闪退/主动退出后任务列表清空，用户必须从头重新勾选影片——刮削几十部时非常痛苦。

**实现**（[DownloadManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Tasks/DownloadManager.cs) 为主，约 +160 行）：
- **快照模型**：`DownLoadTaskRecord`（DataID/DataType/Title/DownloadPreview/OverrideInfo/Status/CreateTime）+ `_PendingRecords` 字典（DataID 去重）+ `download_tasks.json`（`PathManager.CurrentUserFolder`，**临时文件 + Delete + Move 原子替换**写入，写失败仅记日志）。
- **保存时机**（`SaveTasksToFile` 全量重写）：`AddTask` 后、任务 `onCompleted` 成功时移除、用户 `CancelTask/CancelAll` 时移除、`RemoveTask` 时移除、退出时（`Window_Main.Dispose` 先 `Exiting=true` 再保存最后快照再 `CancelAll`，`App.OnExit` 兜底再存一次）。`Exiting` 标志区分「退出取消」与「用户手动取消」——手动取消的**不恢复**，退出/崩溃中断的**恢复**。
- **恢复**（`RestoreTasksFromFile`，返回恢复数）：在 `WindowStartUp.Window_Loaded` 的 `InitMapper()` + `ConfigManager.Init()` **之后**调用（任务 DoWork 会读 `ConfigManager.DownloadConfig`）；上次 `Canceled`（失败/中断）→ 只恢复到任务列表（保持失败态，`TaskInterrupted` 新 i18n key 三语言，用户点「重启全部失败」继续）；`WaitingToRun/Running` → `new DownLoadTask(MetaData)` 重建后 `AddTask`（自动入队继续刮削）。
- [DownLoadTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs) `DoWork` 加防护：`SelectVideoByID(DataID)` 返回 null（影片已被删）时记日志并 `FinalizeWithCancel`，不再 NRE。

**经验**：内存态任务要跨崩溃存活，靠「变更即写快照（原子替换）+ 启动读回重建」，而非依赖退出钩子（闪退没有退出钩子）；「自动继续 vs 用户取消」的边界用 `Exiting` 标志收敛；任务重建只依赖 DataID/DataType（其余运行时信息 DoWork 里按 DataID 重查），天然抗脏数据。

### 3.14 Cloudflare 人机验证识别与提示（2026-08-16）

**背景**：javlibrary（`library` 插件，目标 javlibrary.com）自去年起启用 Cloudflare JS Challenge，纯 HTTP 客户端（Jvedio 的 HttpClient）拿不到真实页面，测试返回 `"Just a moment..."`。用户误以为是软件问题。

**定性**：不是 Jvedio 验证机制，是站点侧人机验证。CF 挑战通过后发放 `cf_clearance` cookie，**绑定 IP + User-Agent + 短时效**；代理 IP 变更即失效。唯一出路：浏览器过验证 → 复制最新 Cookie/UA 填入刮削器请求头（设置页已支持原始 `key: value` 头多行粘贴自动转 JSON）。

**代码增强**（3 处）：
- [CrawlerHeader.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/CrawlerHeader.cs)：新增 `IsCloudflareChallengeTitle`（识别 `Just a moment...` / `Attention Required` / `Verify you are human` 等挑战页标题）。
- [Window_Settings.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml.cs) `CheckUrl`：命中挑战页 → `Available=-1` + 明确提示（新 i18n key `CloudflareChallenge` 三语言），不再把挑战页标题误判为「测试成功」。
- [DownLoadTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs) 403 文案改为「IP 被限制或站点启用 Cloudflare 人机验证，请更新 Cookie/UA 或更换代理」。

**经验**：测试按钮的「可用性」判断不能只看「拿到响应/标题非空」——要识别站点反爬特征页（CF 挑战页、验证码页、封禁页），否则会把「必然刮削失败」误报为「测试成功」，误导排查方向；这类提示要落到 i18n（三语言同步），因为它是用户高频可见的诊断信息。

### 3.15 演员头像下载失败修复（2026-08-16）

**现象**：刮削时 DB（javdb）与 bus（javbus）的影片信息/海报/缩略图都正常，唯独**演员头像下载 403**。日志特征：`covers/thumbs` 200，`avatars/...` 403。

**实测定位**（真实 URL 验证）：`c0.jdbstatic.com` 的 `/covers/`、`/thumbs/` 裸请求 200（无 cookie 无 referer）；`/avatars/` 带完整 cookie（含 cf_clearance）+ referer 仍然 403——**头像目录是独立的 CDN/CF 路径级风控**，与 Referer 无关；旧 `cf_clearance`（日志中为 5 月签发，早已过期）不足以放行头像路径。javbus 图床对头像目录的防盗链同理。

**修复**（[DownLoadTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs) `DownloadActors`）：
- 下载头像前自动补 `Referer = dict["Url"]`（刮削源根域名）——对齐部分图床的防盗链要求（对 bus 类有 hotlink 检查的图床有效）。
- 头像下载失败（403/空）→ **延迟 1s 重试一次**，与海报/缩略图行为对齐（原头像下载无任何重试）。
- 仍失败仅记日志，不影响任务状态（演员信息本身已入库）。

**给用户的现实结论**：javdb avatars 需要**浏览器新鲜过 CF 验证后的 cf_clearance + 同 UA** 才可能放行（同 3.14 的 Cookie 更新流程）；若更新 Cookie 后仍 403，则是头像目录对数据中心/代理 IP 的风控，只能接受缺失或从浏览器手动另存头像。

**经验**：图片类资源失败要先区分「路径级风控」——同一 CDN 不同目录（covers/thumbs vs avatars）策略可以完全不同；修复前必须用真实 URL 实测（带/不带 cookie、referer 对照），不能凭「加 Referer 应该有用」的直觉写补丁。

### 3.16 在线观看跳转（参考浏览器脚本「JAV 添加跳转在线观看」）（2026-08-16）

**需求**：用户在使用 Tampermonkey 脚本「JAV 添加跳转在线观看」（greasyfork 429173）时觉得体验好，希望在 Jvedio 内同款：按番号一键跳转到 27 个 JAV 站的影片页/搜索页。

**实现**：
- 新增 [OnlineSites.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/OnlineSites.cs)：27 个站点（FANZA/Jable/MISSAV/123av/Supjav/NETFLAV/Avgle/JAVHHH/BestJP/JAVMENU/Jav.Guru/JAVMOST/HAYAV/AvJoy/JAVFC2/baihuse/GGJAV/AV01/18sex/highporn/evojav/18av/javgo/javhub/JavBus/JavDB/JAVLib），URL 模板 `{{code}}` 替换 + 站点特化格式化（FANZA：数字补足 5 位、START 前缀→`1startxxxxx`；JavBus：MIUM→`300MIUM-XXX`）。与脚本区别：不做页面可达性验证（WPF 直开 URL，parser 型站点直跳搜索结果页，与脚本「多结果」行为一致）。
- **右键菜单**（[VideoList.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml)）：按用户要求重排：同步信息 → **添加标记** → **在线观看**（新）→ 修改信息 → 拓展功能 → **添加到播放列表**（从原第 3 位移到拓展功能下、访问网址前）→ 访问所在网址 → …；`ContextMenuOpening` 动态填充站点子项（无 VID 时禁用）。
- **详情页**（[Window_Details.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Details.xaml)）：标签 Grid 下方新增「在线观看」行（WrapPanel 按钮组），按钮样式仿脚本（圆角 4px、细边框 #DCDFE6、hover 蓝 #ECF5FF/#409EFF）；`QueryCompleted` 后 `LoadOnlineJumpButtons()` 填充，无 VID 整行隐藏。
- 新 i18n key `OnlineJump`（三语言）。
- **未做**（脚本有但用户没要求）：按钮可达性预检（绿/红/加载态）、「无码/字幕」标签、站点启用开关——需要时再加。

**经验**：把网页脚本功能移植到 WPF 时，照搬「站点列表 + URL 模板 + code 格式化器」数据层即可，脚本的 DOM 验证/预检逻辑要按桌面端使用习惯取舍（桌面端直开浏览器是最顺手的交互）。

### 3.17 在线观看站点网址可自定义（2026-08-16）

**需求**：站点域名常变（弃站/镜像/免翻墙站），内置死的 URL 模板不够用；用户要求在「选项-网络」中直接编辑这 27 个跳转按钮的网址。

**实现**：
- 新增 [OnlineConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/OnlineConfig.cs)：`AbstractConfig` 标准配置（ConfigName="OnlineSites"，存 `app_configs.sqlite` 的 app_config 表），`Dictionary<string,string> UrlOverrides`（站点名 → 覆盖模板）；已接入 `ConfigManager`（CreateInstance/Init 反射 Read/SaveAll）——**零散改动，只有 3 行注册**。
- [OnlineSites.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/OnlineSites.cs)：`OnlineSite.UrlOverride` 属性（get 从配置读、set 直接写配置内存，LostFocus 即生效），`GetUrl` 优先用覆盖模板（空则回退内置默认）。
- [Window_Settings.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml)「网络」tab 底部新增「在线观看」区：27 行「站点名 + 网址输入框」（绑定 `UrlOverride` 双向）+「恢复默认」按钮；提示文案说明 `{{code}}` 是番号占位符。
- 持久化链路：输入即写内存 → 退出/保存时 `ConfigManager.SaveAll()` 落盘（与既有配置一致）；无需在设置页额外做保存逻辑。
- 新 i18n key `OnlineJumpTip` / `ResetDefault`（三语言）。

**经验**：给「内置常量列表」加用户可覆盖能力，最省事的模式是「配置字典按 name 覆盖 + getter 回退默认」，数据与 UI 双向绑定到该 getter/setter，天然实现「改了立即生效、留空恢复默认」；配置类走既有 AbstractConfig 模板可以只改 3 行就接进 SaveAll 闭环。

### 3.18 设置页闪退修复 + 在线按钮配色 + 恢复任务不自动开始（2026-08-16）

**三个反馈与修复**：
1. **点击「选项」闪退**：`Window_Settings.xaml` 新增在线观看配置区里用了 `Style="{StaticResource ViewTextBox}"`——该样式在设置页不存在（ViewTextBox 是详情页的），StaticResource 解析失败 → `InitializeComponent` 抛 XamlParseException → 窗口一开就闪退。**修复**：改用默认 TextBox 样式。教训：跨窗口复制 XAML 时 StaticResource 引用的是「当前窗口 Resources + 全局资源」，不存在即运行时崩，编译期不报。
2. **详情页在线按钮配色**：由「浅灰底深灰字（hover 蓝）」改为脚本 `jop-button_green` 同款**绿底白字**（#67C23A，hover #95D475，按下 #5DAF34，圆角 4）。
3. **恢复任务不自动开始**：`RestoreTasksFromFile` 取消「WaitingToRun/Running 自动 AddTask 入队」分支——所有恢复任务统一只进任务列表（状态「上次未完成，可重启」），由用户点「重启全部失败」或单任务「重启」后才继续刮削；手动取消/完成后照旧从快照移除。

### 3.19 在线观看网址填写区提示增强（2026-08-16）

**反馈**：设置页在线观看网址输入框无任何提示——用户不知道「只填地址还是带占位符、占位符怎么放、中间要不要加内容」。

**改进**（[Window_Settings.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml)）：
- 每行输入框下方**灰字显示该站内置默认网址**（如 `默认网址: https://javdb.com/search?q={{code}}`），用户照着改域名即可；输入框 ToolTip 同步显示默认模板。
- 每行新增「**填默认**」按钮：一键把内置模板填入输入框，用户在此基础上改（换镜像域名）；刷新列表前先触发 LostFocus 提交，不会丢其他行的编辑。
- 顶部说明重写：明确「填完整网址；`{{code}}` 是番号占位符必须保留；留空=默认」+ 镜像站示例（`https://镜像域名/search?q={{code}}`）。
- 新 i18n key `FillDefault`，`OnlineJumpTip` 三语言重写。
- 附带：`deploy.ps1` 中文注释在 PowerShell 5.1（GBK 解码无 BOM UTF-8）下解析报错，重写为纯 ASCII 注释。

**经验**：给「模板替换式」配置做 UI 时，占位符语义对用户不透明——必须「每行显示默认值 + 一键填默认 + 说明示例」三件套，否则用户只能瞎猜。

### 3.20 默认网址示例行 UI 细节（2026-08-16）

**反馈**：示例行字太小、与输入框未左对齐、鼠标无法选中复制。

**改进**（[Window_Settings.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml)）：示例行从「TextBlock 硬偏移 Margin」改为**与输入框同列（Grid 两列布局，示例行放 Column=1）**实现真正左对齐；字号 11 → 13；TextBlock 改**只读 TextBox**（无边框透明背景）→ 鼠标可选中复制。
附带：`deploy.ps1` 相对路径改为基于 `$PSScriptRoot` 计算，不再依赖调用时工作目录。

**经验**：WPF 里「可选中复制的文本」必须用只读 TextBox（TextBlock 不支持文本选择）；左右对齐用同一 Grid 列而非手动 Margin 对齐。

### 3.21 示例行可选中复制（RichTextBox 方案）（2026-08-16）

**反馈**：只读 TextBox 字号小、鼠标无法选中复制、没有选中高亮框。

**根因**：WPF 的 `TextBox IsReadOnly=true` **硬编码禁用鼠标拖选**（键盘可选），TextBlock 也不支持文本选择。

**修复**（[Window_Settings.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml)）：示例行改用**只读 RichTextBox**（`IsReadOnly=true` 时鼠标可选中 + Ctrl+C 复制，选中高亮为系统默认半透明蓝），字号 15，无边框透明背景。文本填充用 `Loaded` 事件 + `Tag` 绑定（绕开 XAML 附加属性——试验过 `RegisterAttached` 附加属性方案，MarkupCompile 阶段 MC3072「属性不存在」解析失败，原因未明，放弃改为事件填充，更简单可靠）。

**经验**：WPF 只读可复制文本 = 只读 RichTextBox；XAML 附加属性在同一程序集内有时会被 MarkupCompile 解析失败，先用「Tag + Loaded 事件」的平替方案，不要死磕。

### 3.22 详情页在线观看按钮左侧"多出的冒号"（2026-08-16）

**现象**：按钮换行后，第二行按钮（Jav.Guru / HAYAV / JAVMOST / AvJoy 等）左侧出现一个孤立的冒号。

**根因**：在线观看 Grid 只有**单行**，WrapPanel 内容高两行 → Grid 行高被撑到两行按钮高度；标题冒号 TextBlock 默认垂直 Stretch **居中于 52px 的行高中间** → 视觉上冒号落在第一行与第二行按钮之间，看起来像第二行按钮"左边多了个冒号"。

**修复**（[Window_Details.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Details.xaml)）：Grid 拆为两行——Row0 = 标题 + 冒号（VerticalAlignment=Center），Row1 = WrapPanel **跨三列**（与标签行的 TagPanel 同构），按钮无论换几行都从最左开始，冒号永远停在标题旁。

**经验**：Grid 单行里同时放「标题 + 多行换行的内容区」时，内容高度会把标题垂直拉伸——凡是「标题 + 冒号 + 会换行的内容」的布局，一律拆成「标题行 + 内容行跨列」两行结构（项目内标签行/TagPanel 已是此范式）。

### 3.23 识别码（VID）排序修复：LUXU-119 后应接 LUXU-120 而非 LUXU-1190（2026-08-16）

**现象**：按识别码排序时出现 LUXU-119 → LUXU-1190 → LUXU-120 的字符串序（字典序），数字部分没有按大小排。

**根因**：`SetSortOrder`（[VieModel_VideoList.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewModels/VieModel_VideoList.cs)）对 `metadata_video.VID` 直接 `ORDER BY VID`，纯字符串排序。

**修复**：VID 特判为**两段排序**：
- 第一段：字母前缀（第一个 `-` 之前，`COLLATE NOCASE` 忽略大小写）；
- 第二段：数字后缀（最后一个 `-` 之后 `CAST AS INTEGER`——用嵌套 `INSTR` 实现，兼容 `FC2-PPV-123456` 双连字符；SQLite 3.44 无 `REVERSE`，且用了双参 `INSTR` 避免依赖 3.42+ 的三参形式）。
- 实测：`AAA-1 → ABC-5 → ABC-9 → ABC-50 → FC2-PPV-999 → FC2-PPV-123456 → LUXU-119 → LUXU-120 → LUXU-1190` ✓
- 升/降序均生效。

**经验**：「字母+数字」混排字段的排序必须先拆分再排；SQL 表达式先在临时库用真实数据验证（含边界：单连字符、双连字符、无连字符、大小写），再进代码。

> **3.23 追加修复（同日晚些时候）**：第一版修复把「前缀 + 数字」写成**两个 `wrapper.Asc/Desc` 调用**，实测排序完全混乱（FC2PPV1339973、OKAZUTIMES、032715_004 排最前）。**根因：SuperUtils 的 `SelectWrapper.Asc/Desc` 是覆盖语义**（反编译+运行时实测：`Asc("a"); Asc("b")` 后 `ToOrder()` 只输出 `ORDER BY b ASC`）——第二个表达式覆盖了第一个，实际只按数字列排序，无连字符番号（数字=0）全按原始顺序堆在最前。
> **修复**：两段排序**合并为单个表达式** `(前缀 || printf('%015d', 数字)) COLLATE NOCASE`（数字零填充 15 位后字符串比较等价于数值比较；COLLATE 需用括号包住整个拼接表达式）。在用户真实库（2.9 万部）实测：`LUXU-119 → LUXU-120 → LUXU-1131 → LUXU-1190` ✓、数字开头番号排最前 ✓、FC2-PPV 双连字符取最后一段 ✓。**教训：调用黑盒 ORM 的链式方法前先验证它是追加还是覆盖语义，两字段排序必须合成一个表达式。**

### 3.24 翻译标题功能（2026-08-16）

**需求**：参考 PotPlayer 字幕翻译插件（A:\PotPlayer_20181126\PotPlayer\Extension\Subtitle\Translate，**只读参考，未改动**），为 Jvedio 增加「翻译标题」：支持插件覆盖的各平台 + ChatGPT 兼容 Completion 格式；选项新增「翻译」模块；右键菜单批量翻译；详情页外文名下方显示中文标题 + 「翻译标题」按钮。

**实现**：
- 新 [TranslateManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Translation/TranslateManager.cs)：12 平台（ChatGPT 兼容/百度/阿里/腾讯/小牛/火山/Google/DeepL/微软/Libre/Yandex/Papago），接口与语言映射（方言：百度 zh→zh、ja→jp；cutil 代理 zh-CN→zh 等）逐字对照 .as 插件实现；HttpClient 挂代理 + 30s 超时；失败返回 null 仅记日志。
- 新 [TranslationConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/TranslationConfig.cs)：Platform/Model/ApiUrl/Field1~3（各平台字段标签不同：密钥/App ID/Client Secret…）/SourceLang/TargetLang，AbstractConfig 模板接入 SaveAll。
- **选项-翻译 tab**：平台下拉（字段标签按平台动态变化）、模型、API 地址（留空默认）、源/目标语言、「测试翻译」按钮（内置测试文本，结果实时显示）。
- **存储**：`metadata` 加 `TitleCN` 列（SqlCommands 迁移）+ `MetaData.TitleCN` + `VideoMapper.SelectFields`。
- **右键菜单**：拓展功能第一位「翻译标题」（批量）：逐部翻译 Title → 写 TitleCN，间隔 500ms 防限流，完成后提示成功/失败数并刷新列表标题；防重入。
- **详情页**：标题栏（外文名）下方新增同字号同字体（16px Bold）中文标题栏（空则隐藏）；右上角「同步信息」左侧新增「翻译标题」按钮；翻译结果写库并即时显示。
- 新增 i18n key ×11（三语言同步）。

**经验**：翻译插件迁移的关键是「接口契约层」——每个平台的 URL 模板/鉴权头/表单字段/响应路径各不同（百度 MD5 签名、papago 双 header、cutil 代理统一 {code:200,data:{text}}），逐平台对照原插件实现并标注来源目录；AI 类平台（OpenAI/Ollama/LM Studio）全是 ChatGPT 兼容格式，统一一个实现 + 可配 URL 即可覆盖全部。

> **3.24 追加修复（同日晚些时候）**：打开选项闪退（NullReferenceException @ `UpdateTranslateLabels`）。**根因：XAML 中 ComboBox 的 `SelectedIndex` 绑定赋值发生在 `InitializeComponent` 期间（`SelectJustThisItem` 触发 `SelectionChanged`），而事件处理器访问的后续控件（`translateField1Label` 等）此时尚未创建**——事件处理函数不能假设「触发时所有命名控件都已就绪」。**修复**：handler 判空 + 窗口 `ContentRendered` 后再统一刷新标签。教训：XAML 事件在 InitializeComponent 中途就可能触发（绑定先于控件创建），事件处理必须对命名控件判空，初始化类工作放 ContentRendered/Loaded。

### 3.25 翻译平台分类：AI（ChatGPT 兼容）vs 机器翻译（2026-08-16）

**需求**：AI 类平台（OpenAI/DeepSeek/Ollama/LM Studio/自定义中转，含 opencode 可用的兼容端点）与机器翻译平台（百度/Google/DeepL 等）分开，用户先选类别再选平台。

**实现**（[TranslateManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Translation/TranslateManager.cs) 重构）：
- 新增 `TranslatePlatformClass`（AI=0 / Machine=1）；`TranslatePlatformDef` 加 `Platform/Class/DefaultUrl/DefaultModel`。
- 枚举重构：AI 类 0-4（OpenAI/DeepSeek/Ollama/LMStudio/CustomChat——全部 ChatGPT 兼容格式，统一一个 `ChatGPTCompat` 实现，仅默认 URL/模型不同）；机器翻译类 100+（百度~Papago）。旧值兼容：`ChatGPTCompat=0 → OpenAI=0` ✓；**注意**：旧版若把 Platform 存成 1-11（旧枚举机器翻译），新枚举下会读成 DeepSeek(1)/Ollama(2)…——测试期用户需重新选择一次。
- **设置页**：翻译 tab 顶部新增「平台类别」下拉（AI / 机器翻译），联动「平台」下拉（按类别过滤），选择即写入配置；字段标签仍按平台动态。
- 默认模型建议：平台带 `DefaultModel`（如 gpt-4o-mini / deepseek-chat / qwen2.5:7b），模型留空时自动使用。
- **PotPlayer 密钥迁移结论**：PotPlayer 的 ini 加密（`ExtensionInfoList` 节 `0U/0P` 两段 base64）尝试了循环 XOR / 加减 / DES / 3DES / RC4 / 头偏移+对称解密，均无法还原；网上无公开解密实现。**结论：不做逆向，由用户直接在选项-翻译中重新填写**（baidu/calf/ollama/bing/OpenAI API 五组已确认填过配置，可直接对应平台重建）。

**经验**：配置枚举值带语义迁移时（如平台枚举重排），要保留旧值兼容映射并在日志/说明中提示用户重选；二进制加密的逆向若 1 小时内无头绪就果断放弃，转人工重新配置——时间是用户最贵的资源。

### 3.26 翻译配置 per-platform 独立保存 + 错误可见（2026-08-16）

**反馈**：①切换平台时输入框不更新（各平台共用一份字段、无保存/加载逻辑）；②两类平台测试均失败但看不到原因。

**根因**：`TranslationConfig` 只有一组 `Model/ApiUrl/Field1-3` 全局字段，所有平台共用；切换平台时输入框绑定值不变 → 看起来"没清空/没保存"；`TranslateManager` 失败一律返回 null → UI 只显示"翻译失败"无具体原因。

**修复**：
- [TranslationConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/TranslationConfig.cs) 重构：`Dictionary<int, TranslationPlatformSetting> PlatformSettings`（**每平台独立配置**：Model/ApiUrl/Field1-3），`GetSetting(platform)` 懒创建；保留全局 `Platform/SourceLang/TargetLang`。
- [TranslateManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Translation/TranslateManager.cs)：改用 `cfg.GetSetting(cfg.Platform)`；新增 `LastError` 静态属性（请求异常/响应缺 content/接口返回 error.message 都会记录），测试按钮直接显示。
- [Window_Settings.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml.cs)：输入框改代码管理——`_LastTranslatePlatform` 记住上一个平台，切换时**先保存旧平台输入框内容 → 写 Platform → 加载新平台配置**；测试前也先保存当前输入框；URL 输入框 PlaceHolder = 当前平台默认地址。

**经验**：多平台配置的 UI 必须 per-platform 存储 + 切换保存/加载闭环；"失败"必须带原因（LastError）否则用户无法排查；切换类事件的保存要用"旧值记忆"而非"当前值"。

### 3.27 原仓库 issues 批量修复：12 项（2026-08-16，发布 5.4.1.8 / Jvedio29.16）

> 基于已归档原仓库 `hitchao/Jvedio` issues 区（119 条 open）逐条研判，筛选出可修复 bug 与紧迫功能，一次迭代落地 12 项。源码 commit 见 5.4 流程。

1. **演员头像拉伸（#436）**：[ActorList.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ActorList.xaml) 两处 + [ActorInfoView.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ActorInfoView.xaml) 的头像 `ImageBrush Stretch="Fill"` → `UniformToFill`（保持比例居中裁剪，非 1:1 头像不再被拉宽）。
2. **右键菜单次级项反向弹出（#398，双屏副屏在左）**：[App.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/App.xaml.cs) 启动时反射强制 `SystemParameters._menuDropAlignment = false`（WPF 系统级菜单对齐 bug，修复所有右键菜单子项方向，非单菜单 hack）。
3. **按名称排序错乱（#437/#362）**：[VieModel_VideoList.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewModels/VieModel_VideoList.cs) `SetSortOrder` 的 `metadata.Title` 分支：空/未同步标题（NULL）强制排末尾——之前 SQLite NULL 排最前，导致"新加入未同步影片"混排到最前、"最近播放（已同步）"被挤到末尾。沿用 3.23 教训：Asc/Desc 是覆盖语义，多键必须合并为**单个表达式**（逗号分隔），已用反射实测 `ORDER BY CASE...END, Title COLLATE NOCASE ASC` 正确生成。**同日追加**：`ACTOR_FIRST_NAME` 分支存在同样的覆盖 bug（先 `Asc(CASE 空值)` 再 `Asc/Desc(sub)`，后者覆盖前者，空值排末尾失效）——同样合并为单表达式修复。**方向语义实测结论**：SQLite 的 `ORDER BY a, b ASC/DESC` 方向只作用于最后一个键，CASE 键恒 ASC → 空值恒排末尾、升降序均正确（曾考虑降序时 CASE 返回 -1，实测 -1 DESC 空值反而排最前，弃用）。
4. **图片 URL `http:https://` 双前缀（#421/#371）**：[VideoDownLoader.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/VideoDownLoader.cs) 新增 `NormalizeUrl` 静态方法（取首个 `http(s)://` 位置截断清洗），`DownloadImage` 入口统一调用——所有图片下载（海报/缩略图/头像/预览图）都经过此入口。
5. **NFO uniqueid 标签（#425）**：[Movie.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Common/Movie.cs) `GetInfoFromNfo` 特判：id 为空时按 `default="true"` → `type="num"` → 第一个 顺序取 `<uniqueid>`。**注意不能把 uniqueid 加进 NFO 解析列表**——多个 uniqueid 节点会按文档顺序互相覆盖（cid 值盖掉 num 值），特判 + 优先级才可控。
6. **同文件夹 NFO 兜底识别（#415/#381）**：同上，id 仍为空时从同目录第一个视频文件名提取识别码（`JvedioLib.Security.Identify.GetVID`），解决"只有 title/plot 没有 id 的 nfo 无法导入"。
7. **文件名 -U/-UC 识别未修正（#424）**：新增默认标签 `Uncensored`（TagID=3，[app_datas.sql](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Data/Sql/app_datas.sql) 新库 + [Sqlite.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/DataBase/Tables/Sqlite.cs) SqlCommands 老库迁移）；[Video.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/Video.cs) 新增 `IsUncensored()`（正则 `(?:^|[-_\s]|[0-9])(u|uc)(?:$|[-_\s.])` 段边界匹配，实测不误伤 UHD/番号中间含 U/中文 C 后缀）；[ScanTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Scan/ScanTask.cs) `AddTags` 扫描入库自动打标。
8. **浏览页码记忆（#430/#241）**：[VideoConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Data/VideoConfig.cs) 新增 `LastPage`（AbstractConfig 自动持久化）；[VideoList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml.cs) 翻页时写、`UserControl_Loaded` 时恢复（>1 才恢复，进库接续上次浏览位置）。
9. **NFO 导出 Kodi 兼容（#429/#388）**：[NFO.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Common/NFO.cs) 增加多属性 `AppendNewNode(node, text, Dictionary<string,string>)` 重载；[Video.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/Video.cs) `SaveToNFO` 追加 `<uniqueid type="num" default="true">`（Kodi/JavSP 标准）+ actor 节点补 `<thumb>`（ImageUrl）。
10. **演员头像独立目录（#445/#338/#270）**：[ActorInfo.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Common/ActorInfo.cs) `GetImagePath` 重构：图片路径为「相对影片」时也回退到软件数据目录的 `Actresses`（Gfriends 等平铺导出目录直接可用，列表页无需影片上下文即可显示头像）；传入 dataPath 时再查影片目录的 `.actor`/`.actors`（MDCx 兼容）。
11. **CSV 导出（#346/#212）**：[VideoList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml.cs) 右键菜单「导出影片数据」+ [ActorList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ActorList.xaml.cs) 右键菜单「导出演员列表」：按当前筛选/搜索条件导出**全部**（非当前页），UTF-8 BOM（Excel 兼容）、字段转义、后台线程执行、完成提示。影片导出依赖 [VieModel_VideoList.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewModels/VieModel_VideoList.cs) 新增的 `LastQuerySql/LastWrapper`（Select 时保存本次查询条件）。
12. **ISO/.strm 支持（#401/#200）**：[ScanTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Scan/ScanTask.cs) `DEFAULT_VIDEO_EXT` 加 `iso,strm`；[ScanHelper.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Scan/ScanHelper.cs) 大小过滤豁免 `.strm`（文本流链接只有几百字节，否则永远 SizeTooSmall）。

**验证**：Release 编译通过（BuildTools + ReferenceAssemblies.net472 + `/p:LangVersion=9.0`）；逗号排序串与 U/UC 正则、NormalizeUrl 均经独立用例实测。部署 `E:\Jvedio-5.3.1\Jvedio29.16.exe`（v5.4.1.8）。

**经验**：
- 批量处理 issue 时先按「根因是否在主程序可定位」过滤，插件/站点侧问题（CF、站点改版）单独归档，不混入代码迭代。
- 黑盒 wrapper（SuperUtils.dll）链式方法行为必须反射实测（Asc 覆盖语义确认 + 逗号合并方案验证），不能凭维护日志推断。
- 新标签/新列一律「app_datas.sql（新库）+ SqlCommands（老库迁移）」双处补，TagID 显式指定 + INSERT OR IGNORE 防冲突。
- 多键排序/多属性 XML 节点这类"接口能力不足"问题，优先在实体层加小重载/合并表达式，不改黑盒。

### 3.28 导出功能增强 + 演员字段增强（2026-08-17，发布 5.4.1.9 / Jvedio29.17）

**导出增强**：
- 新增 [ExportHelper.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Export/ExportHelper.cs) 公共导出类（新目录 Core/Export，csproj 显式登记）：影片/演员统一入口，**三种格式**——CSV（UTF-8 BOM）/ Excel（SpreadsheetML 2003 纯 XML，`<?mso-application progid="Excel.Sheet"?>` 声明，Excel 原生打开无警告，**零第三方依赖**，符合项目风格）/ JSON（Newtonsoft 缩进，中文表头作 key）。所有导出共用 `WriteRows` + 字段转义（CSV 引号、XML `&<>"'`）。
- **选项-库**新增「导出」区块：「导出本库所有影片」按钮 + 灰字提示（支持三种格式），点击按当前库（`ConfigManager.Main.CurrentDBId` + `DataType=0`）全量导出，不依赖当前筛选。
- **空白处右键「全部功能」**新增第三项「导出数据」（同步信息/生成截图之后）：与设置页按钮同逻辑（全库导出）。影片海报右键的「导出影片数据」保留**当前筛选**语义不变。
- SaveFileDialog 三种格式联动：`FilterIndex` → 格式，默认 CSV；不预置扩展名由对话框按所选 filter 追加。

**演员字段增强**：
- **鞋码**：`actor_info` 加 `ShoeSize VARCHAR(100)`（SqlCommands 老库迁移 + [single_db_actor.sql](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Data/Sql/single_db_actor.sql) 新库 + Sqlite.cs TABLES 建表）；[ActorInfo.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Common/ActorInfo.cs) 加 `ShoeSize` 属性；详情页（[ActorInfoView.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ActorInfoView.xaml)）与编辑页（[Window_EditActor.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_EditActor.xaml)）均显示在**爱好上方**；`ActorList.SelectedField` 加列（列表/导出）；演员导出 CSV/Excel/JSON 含鞋码列。
- **罩杯下拉**：[Window_EditActor.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_EditActor.xaml) 的罩杯 SearchBox 改 `ComboBox`，构造时 `Enumerable.Range('A',26)` 填充 A-Z，`SelectedItem` 双向绑定 char（char 与 ComboBoxItem SelectedValue 的 string 转换不兼容，故用 ItemsSource 字符列表 + SelectedItem）。
- **身高/体重单位**：编辑页 Height 后加 "CM"、Weight 后加 "KG"；详情页补上缺失的体重行（KG），身高加 CM。
- 新 i18n key：`Actress_ShoeSize` / `Export` / `ExportLibraryAll` / `ExportLibraryTip`（三语言同步）。

**验证**：Release 编译通过（BuildTools + net472 引用包 + LangVersion=9.0）；部署 `E:\Jvedio-5.3.1\Jvedio29.17.exe`（v5.4.1.9）。

**经验**：
- 「保存对话框选格式」用 `FilterIndex` 驱动格式枚举最省事，注意 `FileName` 不要带扩展名（AddExtension 自动追加），否则用户切格式扩展名不变。
- Excel 导出不引第三方库的可行方案是 SpreadsheetML 2003 XML（Excel 官方兼容格式），比 HTML 伪装 .xls 更干净。
- 数据库加字段三处同步（SqlCommands 迁移 + 新库建表 SQL + 实体属性），ActorList.SelectedField 也要加列否则列表/导出查不到该字段。
- 详情页与编辑页的字段布局要成对维护（这次顺带发现详情页缺体重行）。

### 3.29 导出 NFO + 生日日历/年龄实时（2026-08-17，发布 5.4.1.10 / Jvedio29.18）

**批量导出 NFO（参考用户脚本 E:\Jvedio-5.3.1\sqlite2nfo.py）**：
- [ExportHelper.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Export/ExportHelper.cs) 新增 `ExportVideosToNfo(outputDir, sql, dbId)`：每部影片一个 .nfo 输出到指定目录。
  - 字段与空标签策略对齐脚本：`source/plot/director/rating/criticrating/year/mpaa/customrating/countrycode` 强制输出（空标签展开），`premiered/release/runtime/country/studio/id/num`、genre（分隔符→逗号拆分）、tag（系列）、thumb（本地图片优先→在线 URL 回退）、fanart（ExtraImageUrl，thumb 带 preview 属性）、actor（库映射 DataID→ActorName/ImageUrl，空则回退 ImageUrls JSON 的 ActorNames/ActressImageUrl，去重）。
  - 日期归一 `FormatToYmd`（年月日→年月→年逐级兜底）、文件名安全清洗 `SafeNfoFileName`（字母数字 + `-_.()[] {}`），均照搬脚本逻辑。
  - 注意脚本是 2026-04-29 写的，字段名未变但**当前库已有 TitleCN 列**——导出用 `VideoMapper.SelectFields`（含新字段）而非脚本的 `SELECT m.*, mv.*`，实体映射更稳。
  - 本地图片用 `Video.GetBigImage/GetSmallImage(searchExt:false)` + `File.Exists` 判定，不存在回退在线 URL（脚本的 LOCAL_PIC_INDEX 索引逻辑等价替代）。
- **三处入口**：①选项-库新增「导出本库所有影片为 NFO」按钮（FolderBrowserDialog 选目录，全库）②海报右键「导出影片数据」改**子菜单**（常规导出 / 导出为 NFO，当前筛选）③空白右键「全部功能」导出项改**子菜单**（常规导出 / 导出为 NFO，全库）。i18n：`ExportNormal/ExportNfo/ExportLibraryAllNfo/SelectOutputFolder`（三语言）。
- XML 用 `XmlWriter`（Indent=true, IndentChars="  ", UTF-8 无 BOM），`WriteStartElement+WriteEndElement` 强制空标签展开成 `<tag></tag>`（对齐脚本 pretty_xml 的展开逻辑）。

**生日日历 + 年龄实时（详情页/编辑页）**：
- 生日输入改标准 WPF `DatePicker`（弹出日历可选年月日，文本框支持手动填写），绑定走事件（Birthday 是 string，不引入转换器）：`SelectedDateChanged` → `CurrentActorInfo.Birthday = "yyyy-MM-dd"` 并**同步重算 Age**。
- [ActorInfo.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Common/ActorInfo.cs) 新增 `CalculateAge(birthday)` 静态方法（不足周岁向下取整）。
- 详情页（[ActorInfoView.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ActorInfoView.xaml)）：新增 `DisplayAge` 属性——生日有效时**实时按当前日期计算**，生日无效回退库内 Age；Age 行改只读展示（实时语义）。编辑页（[Window_EditActor.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_EditActor.xaml)）：Age 仍可手动编辑（双向绑定），生日变更时自动重算覆盖。
- 年龄语义说明：详情页 = 实时计算（显示为主）；编辑页 = 生日变更时重算、也可手动改。

**鞋码间距修复**：编辑页鞋码行是 DockPanel **最后一个子元素**，`LastChildFill=True` 强制拉伸（设 Width 无效）→ 输入框被拉满整行、与标签视觉距离过远。改为 `StackPanel Orientation=Horizontal`（不拉伸），与其他字段间距一致。**教训：DockPanel 最后一个子元素会被拉伸填充，固定宽度无效；「标签+单输入框」的一行用 StackPanel 而非 DockPanel。**

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.18.exe`（v5.4.1.10）。

### 3.30 右键导出选中语义 + 设置页提示语位置 + 翻译报错诊断（2026-08-17，发布 5.4.1.11 / Jvedio29.19）

**右键导出改为「选中影片」语义**（用户反馈：对单影片右键导出 NFO 却导出了全库）：
- 根因：海报右键的两个导出（常规 CSV/Excel/JSON 与 NFO）用的是 `LastQuerySql + LastWrapper.ToWhere(false)`（= 当前筛选条件全量），与"选中影片"无关。
- 修复：两个方法开头调用 `HandleMenuSelected(sender, 1)`（depth=1，因为导出项已是**二级子菜单**——子菜单项的 Parent 是父 MenuItem，其 Parent 才是 ContextMenu）把右键点击的影片加入 `SelectedVideo`（非编辑模式先 Clear 再 Add = 单选语义；编辑/多选模式保留多选），再用 `WHERE metadata.DataID IN (ids)` 只导选中的影片。选中为空则提示。
- 语义划分（符合用户预期）：**海报右键 = 只对选中影片生效**（单选=当前影片，多选=选中的）；**空白处右键「全部功能」= 服务整个影片库**（全库导出，两处全库方法未动）。
- 教训：`HandleMenuSelected/GetIDFromMenuItem/GetVideosByMenu` 的 depth 参数与菜单层级强绑定——**把一级菜单项改成二级子菜单时，调 depth=0 的方法会拿不到 ContextMenu（NRE 或返回 null）**，必须 depth=1。

**设置页提示语位置**：上一轮把「支持 CSV/Excel/JSON…」提示放到了第二行 NFO 按钮旁（错位）。已移回第一行「导出本库所有影片」按钮旁，NFO 按钮行只留按钮。

**翻译报错诊断**（用户反馈：404 / 402 unauthorized / 响应缺少 choices[0].message.content）：
- `PostJson` 不再 `EnsureSuccessStatusCode()`（异常消息只有状态码、没有响应体，用户看不出原因），改为非 2xx 时**读取响应体**：优先取 `error.message`，取不到则截断回显原始 body（200 字符），拼成 `HTTP 404 (Not Found)：xxx` 抛出。404=URL/路径不对，402=余额或鉴权，一眼可辨。
- `ChatGPTCompat` 新增 `NormalizeChatUrl`：用户填 base 地址（如 `https://api.xxx.com/v1`）自动补全 `/chat/completions`（填完整地址则不动），消除一半 404 来源。
- 响应解析失败/缺 content 时，`LastError` 附上**原始响应截断**（300 字符）——平台返回 error 对象、空 choices 或非 JSON 都能直接看到，不再是无信息的"检查模型名/密钥"。
- 注意：这次修复后 404/402/缺 content 的**真实原因会在错误提示里直接显示**；用户之前"配齐了密钥 URL 还失败"很可能就是 URL 是 base 地址缺 `/chat/completions` 后缀（现在会自动补）或该平台模型名不对（现在能看到原始响应）。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.19.exe`（v5.4.1.11）。

### 3.31 编辑页翻译标题栏 + 翻译配置独立持久化 + 必应 Region 修复（2026-08-17，发布 5.4.1.12 / Jvedio29.20）

**编辑信息页新增「翻译后的标题」栏**（用户反馈：编辑页找不到翻译后的标题）：
- [Window_Edit.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Edit.xaml) 名称（Title）行下、导入时间行上方新增 `TitleCN` 行：Label + `SearchBox` 双向绑定 `CurrentVideo.TitleCN`，用户可直接修改保存。i18n 新增 `TitleCN` key（翻译后的标题 / Translated Title / 翻訳タイトル）。

**翻译配置独立持久化**（用户反馈：每次换新版本 exe，填好的密钥/URL 全部消失）：
- 根因：`AbstractConfig.Read/Save` 把配置存 `app_configs.sqlite`（MapperManager.appConfigMapper），而该库路径是 `PathManager.CurrentUserFolder = exe目录\data\用户名`——**跟随 exe 所在目录**。每次新版本 exe 换目录运行（或从 build-output 直接跑）→ 全新库 → 配置全丢。
- 修复：[TranslationConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/TranslationConfig.cs) 覆写 `Read()/Save()`，改存独立 JSON：`%APPDATA%\Jvedio\translation.config.json`（固定用户目录，跨 exe 目录/版本永久保留）。`Read()` 用 `JsonConvert.PopulateObject`（避免私有构造函数反序列化问题）；文件不存在时回退 `base.Read()` 读数据库旧值并 Save() 迁移一次。`Save()` 双写（JSON 文件 + 数据库，双保险）。
- **注意**：首次升级运行后需在设置页重新填一次密钥（旧库在旧 exe 目录下读不到），之后任何新版本 exe 都能直接读到。

**必应翻译 401 修复**（用户反馈：本软件 401 Unauthorized，但 PotPlayer 同密钥正常）：
- 对比 PotPlayer 插件 `SubtitleTranslate - bing.as`：插件请求带 `Ocp-Apim-Subscription-Region: Koreacentral` 头（密钥绑定区域），且用亚太端点 `api-apc.cognitive.microsofttranslator.com`；Jvedio 之前只发 `Ocp-Apim-Subscription-Key` **缺 Region 头** → 区域不匹配 → 401 "credentials are missing or invalid"。
- 修复：[TranslateManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Translation/TranslateManager.cs) ①`PostJson` 新增可选 `region` 参数发送 `Ocp-Apim-Subscription-Region` 头；②`Bing()` 默认传 `Koreacentral`（与 PotPlayer 一致），并在设置页 Bing 平台新增「服务地区」输入框（Field2，可空，空则默认 Koreacentral，其他区域用户自行填）。③Bing def 的 `Field2Label` 说明。URL 本就是 `api-apc` 端点（无需改）。
- 教训：Azure Translator 的 key 与区域强绑定，**请求必须带 Region 头**，否则 401；PotPlayer 插件脚本是现成参考（用户环境该密钥就是 Koreacentral 区域）。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.20.exe`（v5.4.1.12）。

### 3.32 翻译配置改存 data 目录 + 必应源语言 auto 修复（2026-08-17，发布 5.4.1.13 / Jvedio29.21）

**翻译配置位置调整**（用户要求放 exe目录\data\用户名）：
- 主存位置改为 `PathManager.CurrentUserFolder\translation.config.json`（= exe目录\data\用户名，与库数据同目录）。
- 安全性确认：deploy.ps1 只 `Copy-Item` exe 文件、**不删不覆盖 data 目录** → 固定部署目录（E:\Jvedio-5.3.1）下迭代新 exe 配置不会丢。
- 双保险：`Save()` 同时写 %APPDATA%\Jvedio\translation.config.json（LegacyPersistPath）；`Read()` 找不到新位置时回退读旧位置并自动迁移。**注意：若从 build-output 或其他目录直接运行新 exe，data 目录是新的、读不到配置**（这是用户此前"配置消失"的真正场景，E:\Jvedio-5.3.1 固定目录运行则无此问题）。

**必应 400 "The source language is not valid" 修复**：
- 根因：Azure Translator 的 `from` 参数**必须传 BCP-47 代码**（ja/en/zh-Hans 等），**不支持 "auto" 值**——传 "auto"/"JPN"/"Japan" 全部 400。源语言留空（不传 from 参数）才是自动检测；原代码 `src` 为空时默认填 "auto" → 必中 400。
- 修复：`Bing()` 中 `src` 为空或 "auto" 时**不传 from 参数**（Azure 自动检测），其余原样传。
- 用户操作指引：必应平台源语言**留空即可**（自动检测），或填 `ja`/`en`/`zh-Hans` 这类 BCP-47 代码；目标语言 `zh-CN` 有效。

**批量翻译确认**：列表页海报右键「高级-翻译标题」（VideoContextMenu → Menu_Advance → TranslateMovie）本就支持选中语义——单选=当前影片，多选模式=选中的多部（`vieModel.SelectedVideo`），与导出语义一致。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.21.exe`（v5.4.1.13）。

### 3.33 修复单选右键翻译标题无反应（2026-08-17，发布 5.4.1.14 / Jvedio29.22）

**现象**（用户报告）：海报右键「高级-翻译标题」单影片无提示不翻译；多选模式却正常；详情页按钮也正常。

**根因**：[VideoList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml.cs) 的 `TranslateMovie` 直接读 `vieModel.SelectedVideo`，**漏调 `HandleMenuSelected(sender)`**（对比 `DownLoadSelectMovie/OpenWeb/EditInfo` 等右键方法都先调它把右键点击的影片加入选中列表）：
- 单选（非编辑模式）：`SelectedVideo` 为空 → 静默 `return`，无任何提示 ✓ 完全吻合现象；
- 多选模式：`SelectedVideo` 里已有选中的影片 → 正常；
- 详情页：用 `CurrentVideo` 不依赖 SelectedVideo → 正常。
- 隐藏风险：单选时若 SelectedVideo 有残留（曾多选过），会翻译**残留**影片而非当前右键的影片。

**修复**：`TranslateMovie` 开头加 `HandleMenuSelected(sender, 1)`（「高级」是二级子菜单，depth=1，与同菜单下 GenerateScreenShot 等一致），单选=当前影片、多选=选中的影片，语义与导出/同步一致。

**教训**：所有「对影片右键生效」的菜单处理必须走 `HandleMenuSelected` 统一入口获取选中语义，否则单选必踩「SelectedVideo 为空直接 return」的坑（且无提示、难排查）。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.22.exe`（v5.4.1.14）。

### 3.34 选项-界面新增缩放设置：跟随系统缩放 + 界面字号滑条（2026-08-17，发布 5.4.1.15 / Jvedio29.23）

**需求**：高分屏下 UI 适配 + 用户自定义字体/UI 大小。建议方案（先调研后实施）：①跟随系统缩放 = 进程级 PerMonitorV2；②字号滑条 = 全局 FontSize 注入（比整体 ScaleTransform 改动小、立竿见影；整体缩放留作远期）。用户确认做 1+2。

**配置存储**：[DpiConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/DpiConfig.cs)（新文件，已登记 csproj）——独立 JSON `data\用户名\dpi.config.json`（同翻译配置策略，不随 exe 版本丢）：`UseSystemDpiScale`（默认 true）+ `UiFontScale`（0.5~2.0 校验）。

**① 跟随系统缩放（勾选框，需重启生效）**：
- [App.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/App.xaml.cs) 静态构造函数（任何窗口创建前）`DpiConfig.Load()` + `ApplyDpiAwareness()`：
  - 勾选 → `SetProcessDpiAwarenessContext(-4)`（DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2）：跨屏实时缩放、系统改缩放比例即时生效；
  - 不勾选 → `AppContext.SetSwitch("Switch.System.Windows.DoNotUsePerMonitorDpiAwareness", true)` 阻止 WPF 自动启用 PerMonitor（固定 96 DPI 渲染）。
- 注意：DPI 感知是**进程级、只能在窗口创建前设置一次**，勾选/取消后**必须重启程序**才生效（界面上已注明）。
- 背景知识：WPF 4.6.2+ 默认会自动尝试 PerMonitorV2，所以此前程序其实已跟随系统 DPI；该勾选框的意义是给用户**显式开关**（不勾选=固定渲染）。

**② 界面字号滑条（0.8~1.5，立即生效）**：
- App.xaml 定义全局资源 `<sys:Double x:Key="GlobalFontSize">14</sys:Double>`（需加 xmlns:sys）；OnStartup 里按 `14 × UiFontScale` 覆盖初值。
- **17 个窗口**（Window*.xaml + Dialog*.xaml 全部）根标签统一加 `FontSize="{DynamicResource GlobalFontSize}"`（PowerShell 批量注入，保留 BOM；DynamicResource 变更时全窗口自动生效，无需遍历窗口）。
- 设置页「界面」区块新增：勾选框 + 滑条（仿 ScrollSpeed 行样式）+ 百分比 Label；滑条 `ValueChanged` → `App.Current.Resources["GlobalFontSize"] = 14 × scale` 即时更新；关闭窗口时 `DpiConfig.Save()`（Window_Closing）。
- 局限（已知）：XAML 硬编码 FontSize（13/14/15，58 处）与部分主题控件固定高度不跟随缩放，大字号下个别控件需实测微调。
- **绑定踩坑**：静态类成员绑定不能用 `Source={x:Static type:DpiConfig}`（x:Static 不能引用类型本身，MC3029 编译错）——必须用 WPF 4.5+ 的**静态属性绑定语法** `{Binding Path=(config:DpiConfig.UiFontScale), Mode=TwoWay}`（括号包裹命名空间限定属性路径）。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.23.exe`（v5.4.1.15）。待用户实测：勾选框重启后系统缩放是否实时生效；滑条各档位字号与布局。

### 3.35 修复设置页打开报错：DpiConfig 静态绑定命名空间前缀错误（2026-08-17，发布 5.4.1.16 / Jvedio29.24）

**现象**（用户报告）：点击选项（打开 Window_Settings）直接抛 `XamlParseException: 类型引用无法找到名为"{clr-namespace:Jvedio;assembly=Jvedio}DpiConfig"的类型`。

**根因**：Window_Settings.xaml 里 `xmlns:config="clr-namespace:Jvedio;assembly=Jvedio"`（ConfigManager 在 Jvedio 根命名空间，故此前 config:ConfigManager 一直可用）；而 DpiConfig 位于 `Jvedio.Core.Config`——用 `config:DpiConfig` 解析到的是 Jvedio 根命名空间 → 类型找不到。静态属性绑定 `Path=(前缀:类型.属性)` 的前缀在**运行时**由 PropertyPath 解析器按 XAML 命名空间映射解析，编译期不报错（所以 Release 编译通过、运行打开设置页才炸）。

**修复**：新增 `xmlns:cfg="clr-namespace:Jvedio.Core.Config;assembly=Jvedio"` 前缀，三处绑定（勾选框、滑条 Value、Label 显示）改用 `cfg:DpiConfig`。

**教训**：①静态属性绑定 `Path=(前缀:类型.属性)` 的**前缀必须指向类型所在的命名空间**，与普通 `Source={x:Static config:ConfigManager}` 的映射（Jvedio 根命名空间）不同，混用会运行时 XamlParseException；②此类 XAML 运行时错误编译期检测不到，改动后必须实际打开对应窗口验证。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.24.exe`（v5.4.1.16）。待用户实测：设置页可正常打开；缩放设置生效。

### 3.36 字号滑条覆盖全部硬编码字号（2026-08-17，发布 5.4.1.17 / Jvedio29.25）

**现象**（用户反馈）：滑条对侧边栏（所有视频/我的收藏/演员/类别/系列/导演等）无效——这些控件的字号是**显式硬编码**（如 VideoSideMenu.xaml FontSize="14"），不继承窗口级 FontSize，DynamicResource 全局资源对其无效。

**修复**：
- 统计全项目硬编码字号分布：12(31)/13(4)/14(30)/15(24) 为主（另有 7/8/10/16/18/20/24/25 少量特殊控件保留不动）。
- App.xaml 新增三个缩放资源 `GlobalFontSize12/13/15`（GlobalFontSize=14 已有）；App.OnStartup 与设置页滑条 ValueChanged 同步按 `基数 × UiFontScale` 更新全部 4 个资源。
- 批量替换全部 XAML：`FontSize="12"→GlobalFontSize12`、`"13"→GlobalFontSize13`、`"14"→GlobalFontSize`、`"15"→GlobalFontSize15`，共 **89 处、21 个文件**（PowerShell 逐文件替换，保留 BOM）。
- 效果：侧边栏、筛选栏、列表、详情、设置等所有 12~15 号字控件全部跟随滑条缩放（DynamicResource 自动更新，拖动即时生效）。
- 剩余不跟随：7/8/10/16/18/20/24/25 号字（共 18 处，图片查看器缩放控件/进度条等特殊控件）与部分主题模板内部字号——按需再处理。
- 风险提示：个别固定尺寸控件（按钮/输入框）在大字号下可能文字截断或换行，用户实测发现具体位置可再微调。

**验证**：Release 编译通过；部署 `E:\Jvedio-5.3.1\Jvedio29.25.exe`（v5.4.1.17）。

### 3.37 db（JavDB）刮削器：导演/评分字段无法刮削（2026-08-17）

**现象**（用户反馈）：使用 db（JavDB，非豆瓣）刮削器同步影片信息，标题/演员/类别/图片等都正常，唯独**导演（Director）与评分（Rating）字段为空**。

**根因**（两个独立 bug 叠加，分别定位）：

1. **导演——`DBCrawler.dll` 解析标签写错**：用 IL 反编译 `plugins/crawlers/db/DBCrawler.dll`（`Jvedio.Crawler.DBCrawler.<Parse>d__14.MoveNext`）发现，导演信息行分支匹配的标签字符串是 **`賣家`**，而 JavDB 当前详情页该行标签是 **`導演`**（实测样本 `snos-401.txt`：`<strong>導演:</strong>`）→ `IndexOf("賣家")` 永远 -1 → 导演分支永不触发 → `Info["Director"]` 从未被爬虫写入。
2. **评分——`Video.ParseDictInfo` 不支持 float**：DBCrawler 其实**有**返回 `Rating`，但 [Video.cs:717](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/Video.cs#L717) 的 `ParseDictInfo` 反射赋值只处理 `string`/`int` 两种属性类型，而 `MetaData.Rating` 是 **`float`** → 命中不了任何分支 → 评分被静默丢弃。

**修复**：

- **DBCrawler.dll 二进制修补**（导演）：把 #US 堆中 `賣家`（UTF-16 `E3 8C B6 5B`）字节原位替换为 `導演`（`0E 5C 14 6F`，等长 4 字节、不改文件结构）。部署目录 `E:\Jvedio-5.3.1\plugins\crawlers\db\DBCrawler.dll` 与根目录副本已修补，原文件备份为 `.orig`。用保存的详情页 HTML 直接调 `Parse()` 实测：Director 由缺失 → 正常返回 `ヒモパン・オブ・ジョイトイ`。
- **[Video.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/Video.cs) `ParseDictInfo` 增加数值类型分支 + 评分五分制归一**：在原有 `string`/`int` 之后补 `long`/`float`/`double` 三个 `TryParse` 分支；并对 `Rating` 属性做 `>10 → /20` 归一（JavDB 详情页是 5 分制，DBCrawler 却按 `Math.Ceiling(5分制×20)` 存成百分制整数，如 4.27→86；Jvedio 库表注释为满分 5 分，故 86 入库前 /20 还原为 4.3）。

**注意**：二进制补丁只对当前这份 `DBCrawler.dll` 生效，但原仓库（hitchao/Jvedio）已归档、插件不会再更新，此补丁可长期使用；若重新下载插件副本需重打。

**验证**：修补后 DLL 用 `snos-401.txt` 实测返回 12 个字段，Director/Rating 均在；评分归一逻辑（"86"→4.3、五分制"4.3"不受影响）经独立用例验证。Jvedio 侧改动待重建 exe 后实测。

### 3.38 补全「发行商」字段 + 在线观看按钮排序（2026-08-17，发布 5.4.1.18 / Jvedio29.26）

**需求**：JavDB/JavBus 详情页都把影片分为「片商/製作商」与「发行商/發行商」，本软件此前只有单一「制作（Studio）」字段，缺少发行商；另要求详情页在线观看按钮把 JavDB、JavBus、JAVLib 置顶、MISSAV 第四。

**实现**：
- **发行商（Publisher）字段补全**：
  - 数据层早已支持（`metadata_video.Publisher` 列 + `VideoMapper.SelectFields` + `Video.Publisher` 属性），缺的只是 UI 与爬虫填充。
  - UI 三处：详情页 [Window_Details.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Details.xaml) 在「制作」行下新增「发行商」只读行（`{DynamicResource Publisher}`）；编辑页 [Window_Edit.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Edit.xaml) 在制作商行下新增可编辑 SearchBox（绑定 `CurrentVideo.Publisher`）；三语 i18n 新增 `Publisher` 键（发行商 / Publisher / 発売元）。
  - 爬虫：Bus2（JavBus）[Class1.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/Bus2/BusCrawler/Class1.cs) 新增 `發行商 → result["Publisher"]` 解析分支（重新编译 `BusCrawler.dll`）。
  - **遗留**：db（JavDB）的 `DBCrawler.dll` 是二进制插件，无法用补丁增加「發行商」解析逻辑，需照 Bus2 模式重写为源码后才能自动填充；当前 db 的发行商只能手工编辑（已与用户确认先测试当前版本，重写列为后续项）。
- **在线观看按钮排序**：[OnlineSites.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/OnlineSites.cs) 的 `Sites` 列表重排：JavDB → JavBus → JAVLib → MISSAV → FANZA 動画 → …（其余 23 个保持原序）；右键菜单与详情页共用该列表，自动生效。

**验证**：Release 编译通过（BuildTools + ReferenceAssemblies.net472 + LangVersion=9.0），版本号 5.4.1.17→5.4.1.18；部署 `E:\Jvedio-5.3.1\Jvedio29.26.exe`；`BusCrawler.dll` 重新编译待 Jvedio 关闭后替换 `plugins/crawlers/bus/`。

---

## 四、踩坑经验（重点）

### 4.1 唯一约束把状态列纳入唯一键

**现象**：`common_picture_exist` 的 `unique(DataID, PathType, ImageType, Exist)` 让 `Exist` 从 0→1 的更新无法用 `INSERT OR REPLACE` 完成，图片存在性索引长期不准。

**根因**：设计时把「业务状态列」误放进「唯一标识列」。

**修复**：见 3.1，约束改为不含 `Exist` + 迁移 + 落库改 `delete+insert`。

**教训**：唯一约束只能由「实体标识」列组成（这里是 DataID+PathType+ImageType），任何会变化的业务状态列（Exist、Status、Count）都不可入唯一键。

### 4.2 配置赋值写在 if 分支内

**现象**：`header.TimeOut` 只在新建 header 时生效，复用 header 时超时丢失。

**根因**：`header.TimeOut = ...` 写在 `if (header == null) { header = new RequestHeader(); ... }` 分支内。

**教训**：对「无论新建还是复用都应生效」的赋值，必须放在分支外。接手代码时优先排查这类「在条件分支里做无条件配置」的代码味。

### 4.3 布尔参数误传

**现象**：`UpdateImageIndex(dataID, false, true)` 把 small 设成 false，导致小图存在状态丢失。

**根因**：方法签名 `(long dataID, bool small, bool big)`，调用方把 small/big 传反。

**教训**：相邻的多个 bool 参数是高危签名，接手时建议改为枚举或具名参数；调用处逐一核对。

### 4.4 异步与 UI 线程

**现象**：自动截图循环阻塞 UI 线程，或跨线程访问 WPF 控件抛异常。

**修复**：循环体放 `Task.Run`，UI 更新用 `App.Current.Dispatcher.BeginInvoke`。

**教训**：WPF 任何「循环 + 耗时操作 + UI 更新」的组合，都要拆成「后台线程跑循环 → Dispatcher 回 UI」。

### 4.5 i18n 不对称（遗留问题）

**现象**：zh-CN 新增 13 个 key，en-US 只补了 10 个，**漏掉 `Actors` / `HasPoster` / `NoPoster`**——英文环境下这三个 key 会回退到 key 字符串本身。

**教训**：新增 i18n key 时两份语言文件必须同步改，建议写个构建前检查脚本对比 key 集合。此外「重启所有」「同步影片信息并发数」等文案直接硬编码在 XAML，未走 `DynamicResource`，应统一。

### 4.6 原作者本地路径依赖

见 3.8。接手第一件事永远是「让仓库在新机器上能 build」。

### 4.7 渲染并发竞态：轮询假等待导致闪退

**现象**：进度条没走完时点翻页，偶发闪退；快速连点翻页必现。

**根因**：`while (Rendering) { Cancel(); await Task.Delay(100); }` 的「等待」并不能串行化——多个 `Select()` 会同时醒来各自启动渲染，两个循环并发写 `ObservableCollection`，一个清项、一个按旧索引访问 → 越界异常。

**修复**：见 3.9，版本号 + 单向链表式等待（`await prevTask`）。

**教训**：凡是「打断 + 重入」的异步流程，控制权只能有一个入口（single-flight），任何「等待标志位变 false」式的轮询都是假的互斥。

### 4.8 无上限内存缓存导致越用越慢

**现象**：翻页越多，进度条越慢，重启后恢复。

**根因**：`MemoryCache.Default` 无内存上限（默认近全物理内存），滑动过期 10 分钟内翻页越多驻留的 BitmapImage 越多，LOH 压力与 GC 成本持续上升。

**修复**：见 3.9，专属缓存实例 + `CacheMemoryLimitMegabytes` / `PhysicalMemoryLimitPercentage` 上限 + 图片 `Freeze()`。

**教训**：所有「跨页共享的和谐缓存」都要带头寸上限；WPF 图片解码后的像素缓冲都在 LOH，必须控制驻留总量。

### 4.9 刮削日志判读：响应码 200 只代表「页面拿到」，不代表「信息有效」

**现象**：同步日志出现 `响应码: 200 (成功获取资源)`，接着却是 `2.1 校验信息不通过` + `Cancel`，看起来「抓到了但程序判失败」，容易误诊。

**原理**：`DownLoadTask.CheckDataInfo`（[DownLoadTask.cs:377-409](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs#L377-L409)）的通过条件只有一条——`dict` 里存在**非空 Title**（:392）。JavBus 等站点对未收录番号（如无码流出 NLD-032 不在 javbus）仍会返回 HTTP 200，但页面无详情结构（日志特征：`StarNode: 0`、`actors: 0`、无 `<h3>` 标题节点），Bus2 爬虫解析不出 Title，`result["Error"]` 虽为空串但校验必然失败。日志中的 `[E] 200` / `[W] 成功获取资源` 是 `CheckDataInfo` 把 StatusCode 套 `StatusCodeToMessage` 打的输出（:395-401），并非程序报错。

**判读**：「校验信息不通过」+ Cancel 是**正常保护**——宁可不写库也不写垃圾数据。区分两种情况：
- **只有个别番号这样**（其他正常）→ 该站没收录这部（多为无码/流出番号），换刮削器或手工编辑；
- **所有影片都这样**（每条都 actors: 0、无 Title）→ 镜像域名被反爬，返回拦截页，换镜像（Bus2 支持 `dataInfo["Url"]` 覆盖 baseUrl，见 3.2）或换代理。

---

## 五、构建与发布流程

### 5.1 构建
- 解决方案：`Jvedio-WPF/Jvedio.sln`
- 主项目：`Jvedio-WPF/Jvedio/Jvedio.csproj`
- 爬虫插件单独编译：`Core/Crawler/Bus2/BusCrawler/BusCrawler.csproj` → 产出 `BusCrawler.dll`
- 编译产物参考 `build-output/`（`Jvedio24.exe` / `Jvedio25.exe` / `BusCrawler.dll`）
- **无 .NET Framework 4.7.2 targeting pack 的机器**（只有 VS BuildTools 时最常见）：从 nuget.org 下载 `Microsoft.NETFramework.ReferenceAssemblies.net472` 包解压（`https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net472/1.0.3`），MSBuild 加参数 `/p:TargetFrameworkRootPath=<解压目录>\build` 即可编译，**无需安装 SDK/开发者工具包**（2026-08-10 实测：BuildTools-only 环境用此法编译 sln 通过）。

### 5.2 发布完整 zip（已验证流程）

> **核心教训：release 不能只传单个 exe。** 接手初期曾在 release 只上传 2MB 的 `Jvedio.exe`，但项目没有用 Costura/ILRepack 合并依赖，exe 旁边必须跟着 40+ 个 dll（`SuperControls.Style.dll` 2.3MB、`MediaInfo.dll` 3MB、`EntityFramework.dll` 4.9MB 等）+ `x64/x86/SQLite.Interop.dll` 原生库 + `Jvedio.exe.config` + `plugins/` 插件目录。用户只下 exe 会因缺 dll 无法启动。对照原作者 `hitchao/Jvedio` 的 `Jvedio-5.4.zip` 就是 10.11MB 的完整包。

**打包脚本**：[build-output/pack_release.ps1](file:///a:/Trae/repository/Jvedio-1/build-output/pack_release.ps1)（已沉淀，发版时改版本号重跑即可）

**打包流程**（脚本自动完成）：
1. 源：`Jvedio-WPF/Jvedio/bin/Release/`（exe 哈希与 `build-output/Jvedio25.exe` 一致即最新编译版，无需重新编译）
2. 复制到 staging 目录，剔除：`app.publish/`、`*.pdb`、`*.xml`、旧版 exe（`Jvedio20~23.exe`）、测试样本 txt
3. **修正 BusCrawler.dll 缺失**：`plugins/crawlers/bus/main.json` 的 `"Files": ["./BusCrawler.dll"]` 指向 `./BusCrawler.dll`，但 bus 目录常只有旧版 `BusCrawler v2可用.dll`——必须把最新 `build-output/BusCrawler.dll` 复制为 `bus/BusCrawler.dll`，否则爬虫加载失败
4. `Compress-Archive` 打成 `Jvedio-{版本}.zip`（含一层版本号外层目录，仿原版）
5. 产物约 10.5MB，63 个条目，结构与原版 `Jvedio-5.4.zip` 对齐

**打包内容清单**（用户解压即得）：
- `Jvedio.exe` + `Jvedio.exe.config` + `Jvedio.ico`
- 40+ 依赖 dll（SuperUtils / SuperControls.Style / MediaInfo / EntityFramework / System.Data.SQLite / Newtonsoft.Json / HtmlAgilityPack …）
- `x64/` `x86/` SQLite 原生库
- `AvalonEdit/Highlighting/` 语法高亮规则
- `plugins/crawlers/bus/`（Bus2 爬虫：`BusCrawler.dll` + `main.json` + `config.json`）
- 运行环境：.NET Framework 4.7.2（Win10 1803+ 自带）

### 5.3 上传到 Release（GitHub API 方式）

gh 未登录时，可从 git credential helper 提取 token，直接调 GitHub API（无需手动 `gh auth login`）：

**提取 token**（git push 能成功即说明凭证已缓存）：
```powershell
Set-Content $credFile -Value "protocol=https","host=github.com","" -Encoding ASCII
$raw = cmd /c "git credential fill < `"$credFile`" 2>&1"   # 必须用文件重定向，PowerShell 管道传多行会丢换行
$token = ($raw | Select-String "^password=(.+)$").Matches[0].Groups[1].Value
```

**上传 zip asset**（Upload API，host 是 `uploads.github.com`）：
```powershell
$rel = Invoke-RestMethod "https://api.github.com/repos/4965898/Jvedio/releases/tags/5.4.0.5"
$uploadBase = $rel.upload_url -replace '\{\?name,label\}',''
$bytes = [System.IO.File]::ReadAllBytes("Jvedio-5.4.0.5.zip")
Invoke-RestMethod "$uploadBase`?name=Jvedio-5.4.0.5.zip" -Method Post `
  -Headers @{ Authorization = "Bearer $token" } -Body $bytes -ContentType "application/zip"
```

**删除旧的不完整 asset**（避免用户误下到不能跑的单 exe）：
```powershell
$old = $rel.assets | Where-Object { $_.name -eq "Jvedio.exe" }
Invoke-RestMethod "https://api.github.com/repos/4965898/Jvedio/releases/assets/$($old.id)" -Method Delete `
  -Headers @{ Authorization = "Bearer $token" }
```

**更新 Release Body**（PATCH，新内容 + `---` + 原 body 保留历史）：
```powershell
$payload = @{ body = $newBody } | ConvertTo-Json -Depth 5
$bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)   # 必须 UTF-8 字节，否则中文乱码
Invoke-RestMethod "https://api.github.com/repos/4965898/Jvedio/releases/$($rel.id)" -Method Patch `
  -Headers @{ Authorization = "Bearer $token" } -Body $bytes -ContentType "application/json; charset=utf-8"
```

**踩坑：API CDN 缓存不一致**
- PATCH 用 `/releases/{id}` 端点，响应立即返回更新后的 body（810 字符）✅
- 但随后 GET `/releases/tags/{tag}` 端点仍返回旧 body（23 字符）——这是 CDN 缓存延迟，非更新失败
- **验证更新结果必须用 `/releases/{id}` 端点**，不要用 `/releases/tags/{tag}`，否则会误判失败

### 5.4 版本迭代流程（下次发版清单）

1. 改代码 → 重新编译 Release（`Jvedio-WPF/Jvedio/Jvedio.csproj`）
2. 爬虫有改动则单独编译 `Core/Crawler/Bus2/BusCrawler/BusCrawler.csproj` → 产出 `BusCrawler.dll`
3. 改 `build-output/pack_release.ps1` 里的版本号字符串（如 `5.4.0.5` → `5.4.0.6`）→ 运行生成 `Jvedio-5.4.0.6.zip`
4. 源码 commit + push：`git add Jvedio-WPF/ && git commit -m "..." && git push origin master`
5. 建 tag + release（tag 命名 `5.4.0.6`，title「自改5.4.0.6」）
6. 用 5.3 的 API 方式上传 zip + 写 Release Body（下载指引 + 改进内容 + 原 body 保留）
7. 如有旧的不完整 asset，删除

> **源码已入库**：2026-08-09 起 origin 已切到 `4965898/Jvedio`，源码改动已 commit（`e5a8e36`）并 push，不再走「只发 exe 不提交源码」的旧流程。后续发版务必同步 commit。

### 5.5 本地部署迭代流程（2026-08-16 起）

用户部署目录：`E:\Jvedio-5.3.1\`（完整包，exe 与 40+ dll 同目录，替换单个 exe 即可）。为便于区分版本，**文件名与版本号双迭代**：
1. **exe 文件名**：主号 + 小版本（`Jvedio29.5.exe` → `Jvedio29.6.exe` …，每迭代一版 +0.1；跨大版本再进 `Jvedio30.1`）。当前基线：`Jvedio29.15.exe` = 29 号的第 15 次迭代（对应内部版本 5.4.1.7）。
2. **内部版本号**：改 `Jvedio-WPF/Jvedio/Properties/AssemblyInfo.cs` 的 `AssemblyVersion` / `AssemblyFileVersion` 小版本 +1（如 5.4.1.6 → 5.4.1.7），日志 `app init, version:` 与文件属性可见。
3. 编译 Release。
4. 跑 `build-output/deploy.ps1 -Version 5.4.0.8 -ExeName Jvedio29.6.exe`（自动复制到 build-output 存档 + `E:\Jvedio-5.3.1\`）。
5. 维护日志同步更新版本说明。

---

## 六、遗留问题与改进建议

> 以下为尚未解决的问题。「源码未 commit」「origin 指向 hitchao」两项已于 2026-08-09 解决（见 1.2、5.4）。

| 优先级 | 问题 | 建议 |
|---|---|---|
| 中 | en-US 漏 3 个 i18n key | 补齐，并加构建前 key 对比检查 |
| 中 | 部分新文案硬编码未走 `DynamicResource` | 统一走资源字典 |
| 中 | `SyncConcurrency` 无范围校验 | 加 `Min=1, Max=10` 校验，防用户填 0 或过大被封 |
| ~~已实现~~ | ~~刮削获得海报/缩略图后，`common_picture_exist` 索引不及时~~ | ✅ 已实现（2026-08-10，见 3.10）：`Core/Tasks/ImageIndexManager.cs` 静默累计刮削/截图成功数，达阈值（`Settings.AutoRebuildImageIndexCount`，默认 10，选项-库可改，0=关闭）后在后台整库重建图片索引，单飞防并发 + 阈值防抖，失败仅记日志 |
| ~~已实现~~ | ~~启动时索引「库关联目录」耗几秒~十几秒，阻塞进入主界面~~ | ✅ 已实现（2026-08-10，见 3.11）：`WindowStartUp.LoadDataBase` 不再 `while` 等待，扫描任务注册进 `App.ScanManager` 后台运行，主窗口立即打开；完成后回调静默刷新统计/加载；右下角扫描按钮状态圈：运行中=旋转高亮圆圈，完成=绿圈白勾 |
| ~~已实现~~ | ~~原仓库 issues 中可修的 bug 与紧迫功能~~ | ✅ 已批量实现（2026-08-16，见 3.27，发布 5.4.1.8/Jvedio29.16）：头像拉伸（#436）、右键菜单方向（#398）、名称排序（#437/#362）、URL 双前缀（#421/#371）、uniqueid（#425）、NFO 兜底（#415/#381）、-U/-UC 标记（#424）、页码记忆（#430/#241）、NFO Kodi 导出（#429/#388）、头像独立目录（#445/#338/#270）、CSV 导出（#346/#212）、ISO/.strm（#401/#200） |
| 低 | `bus-fixed` 示例目录为空 | 删除或补上修复后的 main.json |
| 低 | `LULU-430.txt` 测试样本残留仓库根 | 移到 `Document/爬虫插件示例/` 或加入 `.gitignore` |
| 低 | 多文件 BOM 被清理触发 LF/CRLF 警告 | 统一 `.gitattributes` 规定行尾 |

---

## 七、开发风格总结

接手这批改动呈现明显的「**维护者向稳定性与可用性要价值**」特征：

1. **数据库正确性优先**：把唯一约束设计缺陷当头等大事，迁移 + 检测 + 落库三处一致改动，无半成品。
2. **功能闭环意识强**：图片存在性筛选/删除横跨 i18n、VideoList、详情页、设置页索引重建、DownloadManager 回调，形成统一 `UpdateImageIndex` 维护网络。
3. **防御式编程**：大量 `try/catch + Logger`，关键路径不阻塞 UI；批量重试分批 + 异步轮询，避免雪崩。
4. **最小改动**：保留原有方法签名与控件风格，新增功能沿用既有 `NullToVisibilityConverter`、`SearchBox`、`ButtonWarning` 等模式，不重造轮子。
5. **可移植构建**：清理原作者本地路径依赖是接手第一件事。
6. **不足**：i18n 不对称、文案硬编码、并发数无校验——是后续要补的功课（源码入库已于 2026-08-09 完成，见 1.2）。

整体主线一句话：**修复图片存在性索引的数据正确性，并基于该索引完善筛选/删除/批量重试/可调并发等运维型功能**，没有引入新依赖、没有大改架构，是典型的务实的维护迭代。

2026-08-10 翻页优化补一条：**性能问题先量化再动手**——「越翻越慢」用二分定位到缓存累积与逐条 Dispatcher 往返，「闪退」从并发入口反推竞态条件，修复采用「最小架构改动换最大稳定性收益」的版本号单飞方案，与既有风格一致。

2026-08-10 第二轮补一条：**「后台化 + 静默 + 单飞」是运维型功能的三件套**——ImageIndexManager 以「阈值防抖 + 单飞重建」自动闭环索引时效，启动扫描改成「先开主界面 + 回调补齐」不阻塞进库，均以「绝不打扰/打断主流程」为底线，延续同一维护风格。

---

## 附录：关键文件索引

| 主题 | 文件 |
|---|---|
| 数据库迁移 | [Sqlite.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/DataBase/Tables/Sqlite.cs)、[app_datas.sql](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Data/Sql/app_datas.sql)、[MapperManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Mapper/MapperManager.cs) |
| Bus2 爬虫 | [Class1.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/Bus2/BusCrawler/Class1.cs)、[CrawlerHeader.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/CrawlerHeader.cs)、[CrawlerServer.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Crawler/CrawlerServer.cs)、[bus/main.json](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Document/爬虫插件示例/bus/main.json) |
| 下载层 | [VideoDownLoader.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/VideoDownLoader.cs)、[DownLoadTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs)、[DownloadManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Tasks/DownloadManager.cs) |
| 筛选 | [Filter.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/Filter.xaml)、[Filter.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/Filter.xaml.cs)、[VieModel_VideoList.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/ViewModels/VieModel_VideoList.cs) |
| 视频列表 | [VideoList.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml)、[VideoList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/VideoList.xaml.cs) |
| 图片缓存 | [ImageCache.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Media/ImageCache.cs)、[MetaData.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/MetaData.cs)（默认图 Freeze） |
| 图片索引重建 | [ImageIndexManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Tasks/ImageIndexManager.cs) |
| 后台扫描/状态圈 | [WindowStartUp.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/WindowStartUp.xaml.cs)、[Window_Main.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Main.xaml)（扫描状态圈）、[Window_Main.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Main.xaml.cs)（OnBackgroundScanComplete） |
| 关联数据 | [AssociationMapper.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Mapper/Common/AssociationMapper.cs)、[VieModel_SearchAsso.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Dialog/VieModels/VieModel_SearchAsso.cs) |
| 标签戳 | [TagStamp.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/CommonSQL/TagStamp.cs)、[Video.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Entity/Data/Video.cs)（SetTagStamps） |
| 设置 | [Window_Settings.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Windows/Window_Settings.xaml)、[VieModel_Settings.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/ViewModels/VieModel_Settings.cs)、[Settings.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/WindowConfig/Settings.cs)、[ProxyConfig.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Config/Common/ProxyConfig.cs) |
| 任务列表 | [TaskList.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/Tasks/TaskList.xaml)、[TaskList.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/UserControls/Tasks/TaskList.xaml.cs)、[TabItemManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/ViewModels/TabItemManager.cs) |
| 任务持久化 | [DownloadManager.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Tasks/DownloadManager.cs)（SaveTasksToFile/RestoreTasksFromFile）、[DownLoadTask.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Net/DownLoadTask.cs)（DoWork null 防护）、[WindowStartUp.xaml.cs](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/WindowStartUp.xaml.cs)（启动恢复） |
| 国际化 | [zh-CN.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Lang/zh-CN.xaml)、[en-US.xaml](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Core/Lang/en-US.xaml) |
| 构建 | [Jvedio.csproj](file:///a:/Trae/repository/Jvedio-1/Jvedio-WPF/Jvedio/Jvedio.csproj) |
