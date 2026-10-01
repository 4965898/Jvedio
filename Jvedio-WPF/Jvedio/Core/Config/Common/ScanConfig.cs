using Jvedio.Core.Config.Base;

namespace Jvedio.Core.Config
{
    public class ScanConfig : AbstractConfig
    {
        public const double DEFAULT_MIN_FILE_SIZE = 0;       // 1 MB

        private ScanConfig() : base("ScanConfig")
        {
            CopyNFOPicture = true;
            CopyNFOPreview = true;
            CopyNFOActorPicture = true;
            CopyNFOScreenShot = true;

            CopyNFOActorPath = ".actor";
            CopyNFOPreviewPath = ".preview";
            CopyNFOScreenShotPath = ".screenshot";

            FetchVID = true;
            LoadDataAfterScan = true;
            DataExistsIndexAfterScan = true;
            ImageExistsIndexAfterScan = true;
            ScanNfo = true;
        }

        private static ScanConfig _instance = null;

        public static ScanConfig CreateInstance()
        {
            if (_instance == null)
                _instance = new ScanConfig();

            return _instance;
        }

        public double _MinFileSize = DEFAULT_MIN_FILE_SIZE;

        public double MinFileSize {
            get {
                return _MinFileSize;
            }

            set {
                if (value < 0)
                    _MinFileSize = DEFAULT_MIN_FILE_SIZE;
                else
                    _MinFileSize = value;
            }
        }

        public bool ScanOnStartUp { get; set; }

        public bool CopyNFOOverwriteImage { get; set; }
        public bool CopyNFOPicture { get; set; }
        public bool CopyNFOActorPicture { get; set; }
        public bool CopyNFOPreview { get; set; }
        public bool CopyNFOScreenShot { get; set; }
        public string CopyNFOActorPath { get; set; }
        public string CopyNFOScreenShotPath { get; set; }
        public string CopyNFOPreviewPath { get; set; }

        public bool FetchVID { get; set; }
        public bool LoadDataAfterScan { get; set; }
        public bool DataExistsIndexAfterScan { get; set; }
        public bool ImageExistsIndexAfterScan { get; set; }

        /// <summary>
        /// 扫描/拖放导入完成后对新增影片自动开始同步信息（刮削）。
        /// 默认关闭：避免一次大扫描意外打站点
        /// </summary>
        public bool _ScrapeAfterScan;
        public bool ScrapeAfterScan {
            get { return _ScrapeAfterScan; }
            set {
                _ScrapeAfterScan = value;
                RaisePropertyChanged();
            }
        }

        public string NFOParseConfig { get; set; }
        public bool _ScanNfo;
        public bool ScanNfo {
            get { return _ScanNfo; }
            set {
                _ScanNfo = value;
                RaisePropertyChanged();
            }
        }
    }
}
