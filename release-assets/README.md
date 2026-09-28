# Release inputs

`plugins/crawlers/bus` and `plugins/crawlers/db` contain plugin metadata and images; their DLLs are rebuilt from source in CI.

`plugins/crawlers/fc2/FC2Crawler.dll` and `plugins/crawlers/library/LibraryCrawler.dll` are carried forward from the 5.4.1.48 full release because this repository does not contain their source. Replace them with source builds when those projects become available.

The release packer copies these files into a clean staging directory and verifies the resulting ZIP. Do not put user data, credentials, or local plugin settings here.
