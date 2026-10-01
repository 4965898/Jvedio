using Jvedio.Entity;
using SuperControls.Style;
using SuperUtils.Framework.Tasks;
using SuperUtils.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Tasks
{
    /// <summary>
    /// 单部影片的重命名任务：按重命名规则生成新文件名并移动文件，更新库内路径。
    /// 原实现整批在 UI 线程同步循环（几千部时卡界面、无法取消），收编进任务页（见维护日志 3.52）。
    /// </summary>
    public class RenameTask : AbstractTask
    {
        public long DataID { get; set; }

        /// <summary>重命名后的主文件路径（成功时非空，供列表刷新）</summary>
        public string NewPath { get; private set; }

        /// <summary>分段视频重命名后的 SubSection（用分隔符拼接，无分段为空）</summary>
        public string NewSubSection { get; private set; }

        public RenameTask(Video video) : base()
        {
            DataID = video.DataID;
            Title = string.IsNullOrEmpty(video.VID) ? video.Title : video.VID;
            if (string.IsNullOrEmpty(Title))
                Title = System.IO.Path.GetFileNameWithoutExtension(video.Path);
            StatusText = LangManager.GetValueByKey("RenameWaiting");
        }

        public override void DoWork()
        {
            Task.Run(() => {
                try {
                    Progress = 0;
                    StartWatch();
                    Status = TaskStatus.Running;
                    StatusText = $"{LangManager.GetValueByKey("Renaming")}...";

                    if (Token.IsCancellationRequested) {
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("Cancel");
                        return;
                    }

                    Video video = videoMapper.SelectVideoByID(DataID);
                    if (video == null) {
                        Message = $"不存在 DataID={DataID} 的资源";
                        Logger.Error(Message);
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("RenameFail");
                        return;
                    }

                    string[] newPath = null;
                    try {
                        newPath = video.ToFileName();
                    } catch (Exception ex) {
                        Message = ex.Message;
                        Logger.Error(ex.Message);
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("RenameFail");
                        return;
                    }

                    if (newPath == null || newPath.Length == 0) {
                        Message = LangManager.GetValueByKey("Message_SetRenameRule");
                        FinalizeWithCancel();
                        StatusText = LangManager.GetValueByKey("RenameFail");
                        return;
                    }

                    if (video.HasSubSection) {
                        RenameSubSection(video, newPath);
                    } else {
                        RenameSingle(video, newPath[0]);
                    }

                    if (Success) {
                        Progress = 100;
                        StopWatch();
                    }
                } catch (Exception ex) {
                    Message = ex.Message;
                    Logger.Error(ex.Message);
                    FinalizeWithCancel();
                    StatusText = LangManager.GetValueByKey("RenameFail");
                }
                OnCompleted(null);
            });
        }

        private void RenameSingle(Video video, string target)
        {
            string origin = video.Path;
            if (origin.Equals(target)) {
                // 文件名已符合规则，无需移动
                Success = true;
                Status = TaskStatus.RanToCompletion;
                StatusText = LangManager.GetValueByKey("RenameUnchanged");
                return;
            }
            if (File.Exists(target)) {
                Message = $"{LangManager.GetValueByKey("SameFileNameExists")} => {target}";
                FinalizeWithCancel();
                StatusText = LangManager.GetValueByKey("RenameFail");
                return;
            }
            try {
                File.Move(origin, target);
            } catch (Exception ex) {
                Message = ex.Message;
                Logger.Error(ex.Message);
                FinalizeWithCancel();
                StatusText = LangManager.GetValueByKey("RenameFail");
                return;
            }
            NewPath = target;
            metaDataMapper.UpdateFieldById("Path", target, DataID);
            DataIndexManager.MarkPathExists(DataID);
            Success = true;
            Status = TaskStatus.RanToCompletion;
            StatusText = LangManager.GetValueByKey("RenameSuccess");
        }

        private void RenameSubSection(Video video, string[] newPath)
        {
            string[] oldPaths = video.SubSectionList.Select(arg => arg.Value).ToArray();
            bool changed = false;
            for (int i = 0; i < Math.Min(newPath.Length, oldPaths.Length); i++) {
                if (!newPath[i].Equals(oldPaths[i])) {
                    changed = true;
                    break;
                }
            }
            if (!changed) {
                Success = true;
                Status = TaskStatus.RanToCompletion;
                StatusText = LangManager.GetValueByKey("RenameUnchanged");
                return;
            }

            List<string> finalPaths = new List<string>();
            bool success = false;
            for (int i = 0; i < newPath.Length; i++) {
                string target = newPath[i];
                if (File.Exists(target)) {
                    Logger.Error($"{LangManager.GetValueByKey("SameFileNameExists")} => {target}");
                    finalPaths.Add(oldPaths[i]); // 换回原来的
                    continue;
                }
                try {
                    File.Move(video.SubSectionList[i].ToString(), target);
                    success = true;
                    finalPaths.Add(target);
                } catch (Exception ex) {
                    Message = ex.Message;
                    Logger.Error(ex.Message);
                    finalPaths.Add(oldPaths[i]); // 换回原来的
                }
            }

            if (!success) {
                FinalizeWithCancel();
                StatusText = LangManager.GetValueByKey("RenameFail");
                return;
            }

            NewPath = finalPaths[0];
            NewSubSection = string.Join(ConstValues.SeparatorString, finalPaths);
            metaDataMapper.UpdateFieldById("Path", NewPath, DataID);
            videoMapper.UpdateFieldById("SubSection", NewSubSection, DataID);
            DataIndexManager.MarkPathExists(DataID);
            Success = true;
            Status = TaskStatus.RanToCompletion;
            StatusText = LangManager.GetValueByKey("RenameSuccess");
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is RenameTask other)
                return other.DataID.Equals(DataID);
            return false;
        }

        public override int GetHashCode()
        {
            return DataID.GetHashCode();
        }
    }
}
