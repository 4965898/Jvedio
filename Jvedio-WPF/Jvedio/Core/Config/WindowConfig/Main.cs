using Jvedio.Core.Config.Base;
using System;
using System.Windows;

namespace Jvedio.Core.WindowConfig
{
    public class Main : AbstractConfig
    {
        public const int MAX_IMAGE_WIDTH = 800;
        private Main() : base($"WindowConfig.Main")
        {
            Width = SystemParameters.WorkArea.Width * 0.8;
            Height = SystemParameters.WorkArea.Height * 0.8;
            SideGridWidth = 200;
            FirstRun = true;
            ShowSearchHistory = true;
            SideDefaultExpanded = true;
            SideClassifyExpanded = true;
            SideTagStampExpanded = true;
            PaginationCombobox = true;

            DisplayFunBar = true;
            DisplayNavigation = false;
            DisplayPage = true;
            DisplaySearchBox = true;
            DisplayStatusBar = true;

            DetailWindowShowAllMovie = false;
            ScrollSpeedFactor = 1.5;
        }

        private static Main _instance = null;

        public static Main CreateInstance()
        {
            if (_instance == null)
                _instance = new Main();

            return _instance;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public long CurrentDBId { get; set; }


        public double SideGridWidth { get; set; }

        public bool FirstRun { get; set; }

        public bool ShowSearchHistory { get; set; }
        public bool SideDefaultExpanded { get; set; }
        public bool SideClassifyExpanded { get; set; }
        public bool SideTagStampExpanded { get; set; }


        public string LatestNotice { get; set; }

        public bool _DisplayFunBar;
        public bool DisplayFunBar {
            get { return _DisplayFunBar; }
            set {
                _DisplayFunBar = value;
                RaisePropertyChanged();
            }
        }
        public bool _DisplayNavigation;
        public bool DisplayNavigation {
            get { return _DisplayNavigation; }
            set {
                _DisplayNavigation = value;
                RaisePropertyChanged();
            }
        }
        public bool _DisplayPage;
        public bool DisplayPage {
            get { return _DisplayPage; }
            set {
                _DisplayPage = value;
                RaisePropertyChanged();
            }
        }
        public bool _DisplaySearchBox;
        public bool DisplaySearchBox {
            get { return _DisplaySearchBox; }
            set {
                _DisplaySearchBox = value;
                RaisePropertyChanged();
            }
        }
        public bool _DisplayStatusBar;
        public bool DisplayStatusBar {
            get { return _DisplayStatusBar; }
            set {
                _DisplayStatusBar = value;
                RaisePropertyChanged();
            }
        }
        public bool _PaginationCombobox;
        public bool PaginationCombobox {
            get { return _PaginationCombobox; }
            set {
                _PaginationCombobox = value;
                RaisePropertyChanged();
            }
        }
        public bool _DetailWindowShowAllMovie;
        public bool DetailWindowShowAllMovie {
            get { return _DetailWindowShowAllMovie; }
            set {
                _DetailWindowShowAllMovie = value;
                RaisePropertyChanged();
            }
        }
        public double _ScrollSpeedFactor;
        public double ScrollSpeedFactor {
            get { return _ScrollSpeedFactor; }
            set {
                // 与滑条范围一致（0.1~3.0），手动输入越界时收敛，防滚轮速度异常
                _ScrollSpeedFactor = Math.Min(3.0, Math.Max(0.1, value));
                RaisePropertyChanged();
            }
        }
    }
}
