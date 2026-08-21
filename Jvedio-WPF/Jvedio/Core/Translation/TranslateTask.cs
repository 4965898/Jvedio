using Jvedio.Entity;
using SuperControls.Style;
using SuperUtils.Framework.Tasks;
using System;
using System.Threading.Tasks;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Translation
{
    /// <summary>
    /// 单部影片的标题翻译任务：翻译 Title → 写入 TitleCN
    /// </summary>
    public class TranslateTask : AbstractTask
    {
        public long DataID { get; set; }

        /// <summary>
        /// 翻译结果（成功时为翻译后的标题，供列表刷新显示）
        /// </summary>
        public string Result { get; private set; }

        private readonly string _SourceTitle;

        public TranslateTask(Video video) : base()
        {
            DataID = video.DataID;
            _SourceTitle = video.Title;
            Title = string.IsNullOrEmpty(video.VID) ? video.Title : video.VID;
            if (string.IsNullOrEmpty(Title))
                Title = System.IO.Path.GetFileNameWithoutExtension(video.Path);
            StatusText = LangManager.GetValueByKey("TranslateWaiting");
        }

        public override void DoWork()
        {
            Task.Run(async () => {
                try {
                    Result = null;
                    Progress = 0;
                    StartWatch();
                    Status = TaskStatus.Running;
                    StatusText = $"{LangManager.GetValueByKey("Translating")}...";
                    if (Token.IsCancellationRequested) {
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("Cancel"); // FinalizeWithCancel 会重置 StatusText
                        return;
                    }
                    string result = await TranslateManager.Translate(_SourceTitle);
                    if (Token.IsCancellationRequested) {
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("Cancel");
                        return;
                    }
                    if (string.IsNullOrEmpty(result)) {
                        Message = TranslateManager.LastError ?? LangManager.GetValueByKey("TranslateFail");
                        Logger.Error(Message);
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("TranslateFail");
                    } else {
                        Result = result;
                        metaDataMapper.UpdateFieldById("TitleCN", result, DataID);
                        Success = true;
                        Status = TaskStatus.RanToCompletion;
                        StatusText = LangManager.GetValueByKey("TranslateSuccess");
                        Progress = 100;
                    }
                    StopWatch();
                } catch (Exception ex) {
                    Message = ex.Message;
                    Logger.Error(ex.Message);
                    FinalizeWithCancel();
                    StatusText = LangManager.GetValueByKey("TranslateFail");
                }
                OnCompleted(null);
            });
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is TranslateTask other)
                return other.DataID.Equals(DataID);
            return false;
        }

        public override int GetHashCode()
        {
            return DataID.GetHashCode();
        }
    }
}