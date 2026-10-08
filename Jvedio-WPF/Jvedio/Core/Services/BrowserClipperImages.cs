using Jvedio.Core.Enums;
using Jvedio.Core.Library;
using Jvedio.Core.Media;
using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using static Jvedio.Core.Library.LibraryDatabase;

namespace Jvedio.Core.Clipper
{
    public sealed class BrowserClipImage
    {
        public long LibraryId { get; set; }
        public long DataId { get; set; }
        public long ActorId { get; set; }
        public string Kind { get; set; }
        public int Index { get; set; }
        public string Data { get; set; }
    }
    public static class BrowserClipperImages
    {
        public static int Save(BrowserClipImage request)
        {
            if (Main.CurrentDataBaseType != DataBaseType.SQLite) throw new ArgumentException("浏览器剪藏图片仅支持 SQLite 影片库。");
            if (request == null || request.DataId <= 0 || (request.Data?.Length ?? 0) > 12000000) throw new ArgumentException("无效的图片数据。");
            var config = ConfigManager.BrowserClipperConfig;
            if (!config.SaveImages || request.Kind == "cover" && !config.SaveCoverImages || request.Kind == "actor" && !config.SaveActorImages || request.Kind == "preview" && !config.SavePreviewImages)
                throw new ArgumentException("软件已关闭该类图片的保存。");
            using (var connection = Open(true))
            using (var command = MakeCommand(connection, null, "select count(*) from metadata where DataID=@id and DBId=@db and DataType=0", "@id", request.DataId, "@db", request.LibraryId))
                if (Convert.ToInt64(command.ExecuteScalar()) != 1) throw new ArgumentException("图片所属影片库不匹配。");
            var video = MapperManager.videoMapper.SelectVideoByID(request.DataId);
            byte[] bytes;
            try { bytes = Convert.FromBase64String(request.Data ?? ""); }
            catch (FormatException) { throw new ArgumentException("图片编码无效。"); }
            if (bytes.Length == 0 || bytes.Length > 8000000) throw new ArgumentException("图片大小超出范围。");
            using (var input = new MemoryStream(bytes)) {
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.First();
                if ((long)frame.PixelWidth * frame.PixelHeight > 40000000) throw new ArgumentException("图片尺寸过大。");
                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(frame));
                using (var output = new MemoryStream()) { encoder.Save(output); bytes = output.ToArray(); }
            }
            string[] targets;
            if (request.Kind == "cover") targets = new[] { video.GetBigImage(".jpg", false), video.GetSmallImage(".jpg", false) };
            else if (request.Kind == "preview" && request.Index >= 0 && request.Index < 100)
                targets = new[] { Path.Combine(video.GetExtraImage(), "clip-preview-" + (request.Index + 1) + ".jpg") };
            else if (request.Kind == "actor" && request.ActorId > 0) {
                using (var connection = Open(true))
                using (var command = MakeCommand(connection, null, "select count(*) from metadata_to_actor where DataID=@id and ActorID=@actor", "@id", request.DataId, "@actor", request.ActorId))
                    if (Convert.ToInt64(command.ExecuteScalar()) == 0) throw new ArgumentException("演员与影片不匹配。");
                var actor = Jvedio.Entity.ActorInfo.GetById(request.ActorId);
                targets = new[] { actor.GetImagePath(video.Path, ".jpg", false) };
            } else throw new ArgumentException("无效的图片类型。");
            int saved = 0;
            foreach (string target in targets.Distinct(StringComparer.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("图片保存目录不可用。");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target) || new FileInfo(target).Length == 0) { File.WriteAllBytes(target, bytes); saved++; }
                ImageCache.Remove(target);
            }
            using (var connection = Open()) {
                Execute(connection, null, "insert or replace into common_picture_exist(DataID,PathType,ImageType,Exist) values(@id,@path,0,@small),(@id,@path,1,@big)",
                    "@id", request.DataId, "@path", ConfigManager.Settings.PicPathMode, "@small", File.Exists(video.GetSmallImage()) ? 1 : 0, "@big", File.Exists(video.GetBigImage()) ? 1 : 0);
            }
            return saved;
        }
    }
}
