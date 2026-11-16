using Jvedio.Entity;
using SuperUtils.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Jvedio.MapperManager;

namespace Jvedio.Core.Merge
{
    /// <summary>
    /// 重复演员合并：把若干「被合并」演员并入一个目标演员——
    /// 影片关联迁移（insert or ignore 天然去重）、目标空缺资料/评分/头像取自被合并者（不覆盖已填），
    /// 最后删除被合并条目。同名重复（ActorName 相同）与异名并入均可。
    /// </summary>
    public static class ActorMergeHelper
    {
        /// <summary>
        /// 执行合并。sourceIds 与 targetId 相同的项自动跳过。
        /// 返回实际合并条数；全部被跳过返回 0；失败抛异常（调用方提示）。
        /// </summary>
        public static int Merge(List<long> sourceIds, long targetId)
        {
            if (sourceIds == null || sourceIds.Count == 0)
                return 0;
            ActorInfo target = ActorInfo.GetById(targetId);
            if (target == null)
                throw new Exception("目标演员不存在或已被删除");

            int merged = 0;
            foreach (long sourceId in sourceIds.Distinct()) {
                if (sourceId == targetId)
                    continue;
                MergeOne(sourceId, target);
                merged++;
            }
            if (merged > 0) {
                target.UpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                actorMapper.UpdateById(target);
                App.Logger.Info($"演员合并完成：{merged} 位并入「{target.ActorName}」(ActorID={targetId})");
            }
            return merged;
        }

        private static void MergeOne(long sourceId, ActorInfo target)
        {
            ActorInfo source = ActorInfo.GetById(sourceId);
            if (source == null)
                return;

            // 1. 影片关联迁移：unique(ActorID,DataID) + insert or ignore 保证不重复
            metaDataMapper.ExecuteNonQuery(
                $"insert or ignore into metadata_to_actor (ActorID,DataID) " +
                $"select {target.ActorID}, DataID from metadata_to_actor where ActorID={sourceId}");
            metaDataMapper.ExecuteNonQuery($"delete from metadata_to_actor where ActorID={sourceId}");

            // 2. 目标空缺资料取自被合并者（永不覆盖已填字段，日文原名不参与合并）
            bool changed = false;
            if (string.IsNullOrEmpty(target.ActorNameEN) && !string.IsNullOrEmpty(source.ActorNameEN)) { target.ActorNameEN = source.ActorNameEN; changed = true; }
            if (string.IsNullOrEmpty(target.Country) && !string.IsNullOrEmpty(source.Country)) { target.Country = source.Country; changed = true; }
            if (string.IsNullOrEmpty(target.Nation) && !string.IsNullOrEmpty(source.Nation)) { target.Nation = source.Nation; changed = true; }
            if (string.IsNullOrEmpty(target.BirthPlace) && !string.IsNullOrEmpty(source.BirthPlace)) { target.BirthPlace = source.BirthPlace; changed = true; }
            if (string.IsNullOrEmpty(target.Birthday) && !string.IsNullOrEmpty(source.Birthday)) { target.Birthday = source.Birthday; changed = true; }
            if (string.IsNullOrEmpty(target.BloodType) && !string.IsNullOrEmpty(source.BloodType)) { target.BloodType = source.BloodType; changed = true; }
            if (string.IsNullOrEmpty(target.Hobby) && !string.IsNullOrEmpty(source.Hobby)) { target.Hobby = source.Hobby; changed = true; }
            if (string.IsNullOrEmpty(target.ShoeSize) && !string.IsNullOrEmpty(source.ShoeSize)) { target.ShoeSize = source.ShoeSize; changed = true; }
            if (target.Height <= 0 && source.Height > 0) { target.Height = source.Height; changed = true; }
            if (target.Weight <= 0 && source.Weight > 0) { target.Weight = source.Weight; changed = true; }
            if (target.Chest <= 0 && source.Chest > 0) { target.Chest = source.Chest; changed = true; }
            if (target.Waist <= 0 && source.Waist > 0) { target.Waist = source.Waist; changed = true; }
            if (target.Hipline <= 0 && source.Hipline > 0) { target.Hipline = source.Hipline; changed = true; }
            if (target.Age <= 0 && source.Age > 0) { target.Age = source.Age; changed = true; }
            if ((target.Cup == 'Z' || target.Cup == '\0') && source.Cup >= 'A' && source.Cup < 'Z') { target.Cup = source.Cup; changed = true; }
            if (target.Grade <= 0 && source.Grade > 0) { target.Grade = source.Grade; changed = true; }
            if (string.IsNullOrEmpty(target.WebType) && !string.IsNullOrEmpty(source.WebType)) { target.WebType = source.WebType; changed = true; }
            if (string.IsNullOrEmpty(target.WebUrl) && !string.IsNullOrEmpty(source.WebUrl)) { target.WebUrl = source.WebUrl; changed = true; }
            if (string.IsNullOrEmpty(target.ImageUrl) && !string.IsNullOrEmpty(source.ImageUrl)) { target.ImageUrl = source.ImageUrl; changed = true; }

            // 3. 头像文件：目标没有本地头像时把「源ID_名字.ext」迁给目标；
            //    共享同名文件（{名字}.jpg）不动——目标与被合并者同名时本就共用
            MergeAvatarFile(source, target);

            // 4. 删除被合并条目
            actorMapper.DeleteById(sourceId);
            if (changed)
                App.Logger.Info($"演员合并：资料字段取自「{source.ActorName}」(ActorID={sourceId})");
        }

        /// <summary>
        /// 头像迁移规则：目标已有本地头像（含同名共享文件）→ 删除源专属文件防孤儿；
        /// 目标没有 → 源的「ID_名字.ext」改名给目标（共享同名文件留给目标继续用，不动）。
        /// 永不删除「{名字}.ext」共享文件，避免误伤目标头像。
        /// </summary>
        private static void MergeAvatarFile(ActorInfo source, ActorInfo target)
        {
            try {
                string sourceExpected = Path.GetFullPath(source.GetImagePath());
                string dir = Path.GetDirectoryName(sourceExpected);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    return;

                string sourceSpecific = FindIdSpecificFile(dir, source.ActorID, source.ActorName);
                bool targetHas = File.Exists(Path.GetFullPath(target.GetImagePath()));

                if (targetHas) {
                    if (!string.IsNullOrEmpty(sourceSpecific) && File.Exists(sourceSpecific)) {
                        FileHelper.TryDeleteFile(sourceSpecific);
                        App.Logger.Info($"演员合并：删除被合并者头像 {sourceSpecific}");
                    }
                } else if (!string.IsNullOrEmpty(sourceSpecific) && File.Exists(sourceSpecific)) {
                    string destination = Path.GetFullPath(target.GetImagePath());
                    if (!string.IsNullOrEmpty(destination) && !File.Exists(destination)) {
                        string destDir = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        File.Move(sourceSpecific, destination);
                        App.Logger.Info($"演员合并：头像迁移 {sourceSpecific} → {destination}");
                    }
                }
            } catch (Exception ex) {
                // 头像文件处理失败不阻断合并（数据库关联已迁移完成）
                App.Logger.Error($"演员合并头像处理失败（{source.ActorName}→{target.ActorName}）：{ex.Message}");
            }
        }

        /// <summary>在头像目录里找「{actorID}_{actorName}」开头的专属头像文件（任意图片扩展名）</summary>
        private static string FindIdSpecificFile(string dir, long actorID, string actorName)
        {
            string prefix = $"{actorID}_{actorName}";
            try {
                foreach (string file in Directory.GetFiles(dir, prefix + ".*")) {
                    if (Jvedio.Core.Scan.ScanTask.PICTURE_EXTENSIONS_LIST.Contains(Path.GetExtension(file).ToLower()))
                        return file;
                }
            } catch (Exception ex) {
                App.Logger.Error($"查找演员头像失败：{ex.Message}");
            }
            return null;
        }
    }
}
