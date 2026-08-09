using SuperUtils.Media;
using System;
using System.Collections.Specialized;
using System.Runtime.Caching;

namespace Jvedio.Core.Media
{
    public static class ImageCache
    {
        /// <summary>
        /// 默认图片缓存时长
        /// </summary>
        public const long DEFAULT_CACHE_EXPIRATION = 10;

        /// <summary>
        /// 图片缓存内存上限（MB），避免翻页后图片无限驻留导致 GC 压力越来越大
        /// </summary>
        private const int CACHE_MEMORY_LIMIT_MB = 512;

        private static MemoryCache _Cache { get; set; }

        static ImageCache()
        {
            NameValueCollection config = new NameValueCollection();
            config["CacheMemoryLimitMegabytes"] = CACHE_MEMORY_LIMIT_MB.ToString();
            config["PhysicalMemoryLimitPercentage"] = "25";
            _Cache = new MemoryCache("JvedioImageCache", config);
        }

        public static System.Windows.Media.Imaging.BitmapImage Get(string path, int DecodePixelWidth = 0)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            object o = _Cache.Get(path);
            if (o != null && o is System.Windows.Media.Imaging.BitmapImage image)
                return image;

            // 读取该文件，加入缓存
            System.Windows.Media.Imaging.BitmapImage img = ImageHelper.ReadImageFromFile(path, DecodePixelWidth);

            if (!ConfigManager.Settings.ImageCache)
                return img;

            if (img == null)
                return null;
            Add(path, img);
            return img;
        }

        private static bool Add(string path, System.Windows.Media.Imaging.BitmapImage image)
        {
            if (_Cache.Contains(path))
                return true;
            // 后台线程解码的图片需要 Freeze 后才能跨线程使用
            if (image.CanFreeze)
                image.Freeze();
            CacheItem item = new CacheItem(path, image);
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.SlidingExpiration = TimeSpan.FromMinutes(ConfigManager.Settings.CacheExpiration);
            _Cache.Add(item, policy);

            return true;
        }

        public static void Remove(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (_Cache.Contains(path)) {
                _Cache.Remove(path);
            }
        }

        public static void Clear()
        {
            _Cache?.Trim(100);
        }
    }
}