using Jvedio.Entity.CommonSQL;
using Jvedio.Mapper.BaseMapper;
using System.Collections.Generic;
using System.Linq;

namespace Jvedio.Mapper
{
    public class TagStampMapper : BaseMapper<TagStamp>
    {
        public static string GetTagSql()
        {
            return "SELECT common_tagstamp.*,count(common_tagstamp.TagID) as Count from metadata_to_tagstamp " +
                "join common_tagstamp " +
                "on metadata_to_tagstamp.TagID=common_tagstamp.TagID " +
                "join metadata " +
                "on metadata.DataID=metadata_to_tagstamp.DataID " +
                $"where metadata.DBId={ConfigManager.Main.CurrentDBId} and metadata.DataType={0} " +
                "GROUP BY common_tagstamp.TagID;";
        }

        public List<TagStamp> GetAllTagStamp()
        {
            // 已排序（SortOrder>=0）的在前按序号排；未排序（-1，含新建）的在后按 TagID 排
            return MapperManager.tagStampMapper.SelectList()
                .OrderBy(arg => arg.SortOrder < 0 ? int.MaxValue : arg.SortOrder)
                .ThenBy(arg => arg.TagID)
                .ToList();
        }
    }
}
