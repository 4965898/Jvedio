# Release process

Change `AssemblyVersion` in `Jvedio-WPF/Jvedio/Properties/AssemblyInfo.cs` to a new four-part version and push the commit to `master`. GitHub Actions restores dependencies, rebuilds the application and maintained crawlers, runs the dispatcher stress check, assembles and verifies a complete `Jvedio-<version>.zip`, then creates the matching tag and publishes a Release with that ZIP. A build artifact is also retained for each run.

The workflow refuses to republish a version already tagged at a different commit. It skips an old run if `master` has advanced before publication. A manually pushed version tag also runs the same build and release path.

To pack locally after building the application, BusCrawler, and DBCrawler in Release mode:

```powershell
./scripts/pack-release.ps1 -Version 5.4.1.52
```

The package is written to `artifacts/`, which Git ignores.

After the ZIP is published as the latest GitHub Release, CI runs `publish-update-feed.ps1` to update the original upgrade window's version data and file source in the `update-feed` branch. Use `-ValidateOnly` to generate and check the feed locally without pushing it.
