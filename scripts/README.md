# Release process

Label and statistics checks: run test-startup-regressions.ps1 -Library. LibraryRegression.cs creates isolated libraries, validates counts and library-scoped label operations, checks paging and unsaved changes through the WPF UI, and renders previews under build-output/library-previews/. Use -PreviewDirectory to choose another output folder. The default invocation still runs the startup and menu regression checks.

Change `AssemblyVersion` in `Jvedio-WPF/Jvedio/Properties/AssemblyInfo.cs` to a new four-part version and push the commit to `master`. GitHub Actions restores dependencies, rebuilds the application and maintained crawlers, runs the dispatcher stress check, assembles and verifies a complete `Jvedio-<version>.zip` and matching `Jvedio-<version>.exe`, then creates the matching tag and publishes both files. The EXE needs an existing complete installation. Build artifacts are retained for each run.

The workflow refuses to republish a version already tagged at a different commit. It skips an old run if `master` has advanced before publication. A manually pushed version tag also runs the same build and release path.

To pack locally after building the application, BusCrawler, and DBCrawler in Release mode:

```powershell
./scripts/pack-release.ps1 -Version 5.4.1.60
```

The ZIP and EXE are written to `artifacts/`, which Git ignores.

Startup, crawler-source persistence, staged recovery and menu keyboard regressions can be checked with `./scripts/test-startup-regressions.ps1`. It compiles `StartupRegression.cs` against the Release build and runs in a temporary runtime and user directory. Pass `-CompilerPath` for the Visual Studio Roslyn compiler and `-ReferenceDirectory` for the .NET Framework 4.7.2 reference assemblies when the local defaults differ. `test-backup-snapshot.ps1` separately checks WAL snapshots, ZIP restore, retention and both remote transports.

After the ZIP is published as the latest GitHub Release, CI runs `publish-update-feed.ps1` to update the original upgrade window's version data and file source in the `update-feed` branch. Use `-ValidateOnly` to generate and check the feed locally without pushing it.
