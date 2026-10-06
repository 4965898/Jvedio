using Jvedio.Core.Enums;
using Jvedio.Entity;
using Jvedio.Mapper.BaseMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jvedio.Mapper
{
    public class MetaDataMapper : BaseMapper<MetaData>
    {
        public int deleteDataByIds(List<string> idList)
        {
            if (idList == null || idList.Count == 0)
                return 0;
            int c1 = MapperManager.metaDataMapper.DeleteByIds(idList);
            int c2 = 0;
            DataType dataType = Main.CurrentDataType;
            if (dataType == DataType.Picture) {
                c2 = MapperManager.pictureMapper.DeleteByIds(idList);
            } else if (dataType == DataType.Comics) {
                c2 = MapperManager.comicMapper.DeleteByIds(idList);
            } else if (dataType == DataType.Game) {
                c2 = MapperManager.gameMapper.DeleteByIds(idList);
            }

            StringBuilder builder = new StringBuilder();
            string ids = string.Join(",", idList);
            builder.Append("begin;");
            builder.Append($"delete from metadata_to_translation where DataID in ({ids});");
            builder.Append($"delete from metadata_to_tagstamp where DataID in ({ids});");
            builder.Append($"delete from metadata_to_actor where DataID in ({ids});");
            builder.Append($"delete from metadata_to_label where DataID in ({ids});");
            builder.Append($"delete from metadata_to_translation where DataID in ({ids});");
            builder.Append("commit;");

            if (dataType == DataType.Picture) {
                MapperManager.pictureMapper.ExecuteNonQuery(builder.ToString());
            } else if (dataType == DataType.Comics) {
                MapperManager.comicMapper.ExecuteNonQuery(builder.ToString());
            } else if (dataType == DataType.Game) {
                MapperManager.gameMapper.ExecuteNonQuery(builder.ToString());
            }

            if (c1 == c2 && idList.Count == c1)
                return c1;
            else {
                // todo 日志
            }

            return 0;
        }

        public void SaveLabel(MetaData metaData)
        {
            Jvedio.Core.Library.LibraryLabelService.SetLabels(metaData.DataID, metaData.LabelList?.Select(item => item.Value));
        }
    }
}
