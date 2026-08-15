﻿using Jvedio.Core.DataBase;
using Jvedio.Core.DataBase.Tables;
using Jvedio.Mapper;
using System;
using System.Collections.Generic;
using static Jvedio.App;

namespace Jvedio
{
    public static class MapperManager
    {
        public static AppConfigMapper appConfigMapper { get; set; } = new AppConfigMapper();

        public static AppDatabaseMapper appDatabaseMapper { get; set; } = new AppDatabaseMapper();
        public static TranslationMapper translationMapper { get; set; } = new TranslationMapper();
        public static MagnetsMapper magnetsMapper { get; set; } = new MagnetsMapper();
        public static AIFaceMapper aIFaceMapper { get; set; } = new AIFaceMapper();
        public static TagStampMapper tagStampMapper { get; set; } = new TagStampMapper();
        public static SearchHistoryMapper searchHistoryMapper { get; set; } = new SearchHistoryMapper();

        public static MetaDataMapper metaDataMapper { get; set; } = new MetaDataMapper();
        public static VideoMapper videoMapper { get; set; } = new VideoMapper();
        public static PictureMapper pictureMapper { get; set; } = new PictureMapper();
        public static ComicMapper comicMapper { get; set; } = new ComicMapper();
        public static GameMapper gameMapper { get; set; } = new GameMapper();
        public static ActorMapper actorMapper { get; set; } = new ActorMapper();
        public static UrlCodeMapper urlCodeMapper { get; set; } = new UrlCodeMapper();
        public static AssociationMapper associationMapper { get; set; } = new AssociationMapper();

        private static bool Loaded { get; set; }

        public static void ResetInitState()
        {
            Loaded = false;
        }

        public static bool Init()
        {
            if (Loaded)
                return true;

            // todo 泛型似乎无法使用多态进行反射加载

            // 初始化数据库连接
            appDatabaseMapper.Init();
            translationMapper.Init();
            magnetsMapper.Init();
            aIFaceMapper.Init();
            tagStampMapper.Init();
            searchHistoryMapper.Init();

            foreach (string key in Sqlite.AppData.TABLES.Keys) {
                appDatabaseMapper.CreateTable(key, Sqlite.AppData.TABLES[key]);
            }

            appConfigMapper.InitSqlite(SqlManager.DEFAULT_SQLITE_CONFIG_PATH);

            foreach (string key in Sqlite.AppConfig.TABLES.Keys) {
                appConfigMapper.CreateTable(key, Sqlite.AppConfig.TABLES[key]);
            }

            metaDataMapper.Init();
            videoMapper.Init();
            pictureMapper.Init();
            comicMapper.Init();
            gameMapper.Init();
            actorMapper.Init();
            urlCodeMapper.Init();
            associationMapper.Init();

            foreach (string key in Sqlite.Actor.TABLES.Keys) {
                actorMapper.CreateTable(key, Sqlite.Actor.TABLES[key]);
            }

            foreach (string key in Sqlite.Data.TABLES.Keys) {
                metaDataMapper.CreateTable(key, Sqlite.Data.TABLES[key]);
            }

            // 新增列
            foreach (string sql in Sqlite.SQL.SqlCommands) {
                try {
                    metaDataMapper.ExecuteNonQuery(sql);
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            }

            // 修复 common_picture_exist 表的唯一约束
            try {
                string checkSql = "SELECT sql FROM sqlite_master WHERE type='table' AND name='common_picture_exist'";
                List<Dictionary<string, object>> result = metaDataMapper.Select(checkSql);
                string tableDef = "";
                if (result != null && result.Count > 0 && result[0].ContainsKey("sql"))
                    tableDef = result[0]["sql"]?.ToString() ?? "";
                if (tableDef.Contains("ImageType,Exist)") && !tableDef.Contains("ImageType)")) {
                    foreach (string sql in Sqlite.SQL.PictureExistMigration) {
                        metaDataMapper.ExecuteNonQuery(sql);
                    }
                }
            } catch (Exception ex) {
                Logger.Error(ex);
            }

            ApplySqlitePragmas();

            Loaded = true;
            Logger.Info("init mapper ok");
            return Loaded;
        }

        /// <summary>
        /// 为所有 SQLite 连接应用统一 PRAGMA：
        /// - journal_mode=WAL：读写不互斥，大幅降低刮削写库与 UI 读统计的锁冲突
        /// - busy_timeout=30000：写锁冲突时等待而不是立刻抛 "database is locked"
        /// - synchronous=NORMAL：WAL 模式下安全，减少 fsync 停顿
        /// 注意 busy_timeout/synchronous 是连接级设置，必须对每条连接逐一执行；
        /// journal_mode 是文件级设置，执行一次即持久生效。
        /// </summary>
        private static void ApplySqlitePragmas()
        {
            object[] mappers = new object[] {
                appDatabaseMapper, translationMapper, magnetsMapper, aIFaceMapper,
                tagStampMapper, searchHistoryMapper, metaDataMapper, videoMapper,
                pictureMapper, comicMapper, gameMapper, actorMapper, urlCodeMapper,
                associationMapper, appConfigMapper,
            };
            string[] pragmas = new string[] {
                "PRAGMA journal_mode=WAL;",
                "PRAGMA busy_timeout=30000;",
                "PRAGMA synchronous=NORMAL;",
            };
            foreach (object mapper in mappers) {
                if (mapper == null)
                    continue;
                var execute = mapper.GetType().GetMethod("ExecuteNonQuery", new Type[] { typeof(string) });
                if (execute == null)
                    continue;
                foreach (string sql in pragmas) {
                    try {
                        execute.Invoke(mapper, new object[] { sql });
                    } catch (Exception ex) {
                        Logger.Error($"apply pragma failed: {sql} => {ex.Message}");
                    }
                }
            }
            Logger.Info("apply sqlite pragmas ok (journal_mode=WAL, busy_timeout=30000, synchronous=NORMAL)");
        }

        public static void Dispose()
        {
            appConfigMapper.Dispose();

            appDatabaseMapper.Dispose();
            translationMapper.Dispose();
            magnetsMapper.Dispose();
            aIFaceMapper.Dispose();
            tagStampMapper.Dispose();
            searchHistoryMapper.Dispose();

            metaDataMapper.Dispose();
            videoMapper.Dispose();
            pictureMapper.Dispose();
            comicMapper.Dispose();
            gameMapper.Dispose();
            actorMapper.Dispose();
            urlCodeMapper.Dispose();
            associationMapper.Dispose();

            Logger.Info("dispose mapper ok");
        }
    }
}
