# Release process

Browser clipping: `browser-extension/README.md` describes installing the unpacked Chrome extension and enabling the receiver under Scan and Import. Run `npm test` in `browser-extension` with a sample directory and fixture output directory, then `test-browser-clipper.ps1` against the Release build. Pass `-ScratchRoot` to choose a drive for isolated test runtimes. `pack-browser-clipper.ps1 -ConnectorOnly` creates the connector ZIP for release; omit the switch to also copy a desktop preview EXE. The existing GitHub Actions workflow publishes the application ZIP and EXE; upload the verified connector ZIP to the same release and check all three asset names. Changing the workflow to automate that third upload requires a GitHub credential with workflow permission.

Label and statistics checks: run test-startup-regressions.ps1 -Library. LibraryRegression.cs creates isolated libraries, validates counts and library-scoped label operations, checks paging and unsaved changes through the WPF UI, and renders previews under build-output/library-previews/. Use -PreviewDirectory to choose another output folder. The default invocation still runs the startup and menu regression checks.

Video information checks: run `test-startup-regressions.ps1 -VideoInfo -PreviewDirectory build-output/video-info-previews`. This requires FFmpeg on PATH (or `-FFmpegPath`) and generates small videos in a temporary directory. It runs an x86 harness to match the bundled native MediaInfo library, checks empty-result invalidation, tab retries, movie navigation, UI-thread notifications, segmented paths and the actual detail controls. `-SampleVideoPath` additionally reads a supplied local video; all database writes stay in the isolated fixture. Preview images are copied to the selected output folder.

Change `AssemblyVersion` in `Jvedio-WPF/Jvedio/Properties/AssemblyInfo.cs` to a new four-part version and push the commit to `master`. GitHub Actions restores dependencies, rebuilds the application and maintained crawlers, runs the dispatcher stress check, assembles and verifies a complete `Jvedio-<version>.zip` and matching `Jvedio-<version>.exe`, then creates the matching tag and publishes both files. The main project builds LibraryCrawler through a project reference without adding it as an application assembly reference; the packing script runs its regression checks before creating publication assets. This uses the existing workflow and does not require workflow-file changes. The EXE needs an existing complete installation. Build artifacts are retained for each run.

The workflow refuses to republish a version already tagged at a different commit. It skips an old run if `master` has advanced before publication. A manually pushed version tag also runs the same build and release path.

To pack locally after building the application, BusCrawler, DBCrawler, and LibraryCrawler in Release mode:

```powershell
./scripts/pack-release.ps1 -Version 5.4.1.71
```

The ZIP and EXE are written to `artifacts/`, which Git ignores. Packing uses `msbuild` on PATH to locate Roslyn and the CI reference-assembly directory when available; pass `-CompilerPath` and `-ReferenceDirectory` for other local installations.

Library crawler checks: build `Jvedio-WPF/Jvedio/Core/Crawler/Library2/LibraryCrawler/LibraryCrawler.csproj`, then run `./scripts/test-library-crawler.ps1`. The harness discovers the real plugin DLL through the application, uses isolated databases and a loopback HTTP fixture, and verifies VID-only eligibility, 302/list search, detail parsing, task validation, cached remote IDs, not-found results, and explicit unsupported types. Pass `-CompilerPath` and `-ReferenceDirectory` when local defaults differ. To reproduce the old rejection, pass `-Legacy -PluginPath <old-1.4.0-DLL>`.

Startup, crawler-source persistence, staged recovery and menu keyboard regressions can be checked with `./scripts/test-startup-regressions.ps1`. It compiles `StartupRegression.cs` against the Release build and runs in a temporary runtime and user directory. Pass `-CompilerPath` for the Visual Studio Roslyn compiler and `-ReferenceDirectory` for the .NET Framework 4.7.2 reference assemblies when the local defaults differ. `test-backup-snapshot.ps1` separately checks WAL snapshots, ZIP restore, retention and both remote transports.

After the ZIP is published as the latest GitHub Release, CI runs `publish-update-feed.ps1` to update the original upgrade window's version data and file source in the `update-feed` branch. Use `-ValidateOnly` to generate and check the feed locally without pushing it.
