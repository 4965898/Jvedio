using Jvedio.Entity.Base;
using Jvedio.Entity.CommonSQL;
using Jvedio.Mapper.BaseMapper;
using System.Collections.Generic;
using System.Linq;

namespace Jvedio.Mapper
{
    public class AssociationMapper : BaseMapper<Association>
    {
        private readonly object _Lock = new object();

        /// <summary>
        /// MainDataID -> SubDataID 列表（正向邻接表）
        /// </summary>
        private Dictionary<long, List<long>> _Children = null;

        /// <summary>
        /// SubDataID -> MainDataID 列表（反向邻接表）
        /// </summary>
        private Dictionary<long, List<long>> _Parents = null;

        /// <summary>
        /// 关联数据变更后调用，使缓存失效
        /// </summary>
        public void InvalidateCache()
        {
            lock (_Lock) {
                _Children = null;
                _Parents = null;
            }
        }

        private void InitAdjacencyList()
        {
            lock (_Lock) {
                if (_Children != null && _Parents != null)
                    return;
                List<Association> list = SelectList();
                Dictionary<long, List<long>> children = new Dictionary<long, List<long>>();
                Dictionary<long, List<long>> parents = new Dictionary<long, List<long>>();
                if (list != null && list.Count > 0) {
                    foreach (Association item in list) {
                        if (!children.TryGetValue(item.MainDataID, out List<long> subs)) {
                            subs = new List<long>();
                            children.Add(item.MainDataID, subs);
                        }
                        subs.Add(item.SubDataID);
                        if (!parents.TryGetValue(item.SubDataID, out List<long> pars)) {
                            pars = new List<long>();
                            parents.Add(item.SubDataID, pars);
                        }
                        pars.Add(item.MainDataID);
                    }
                }
                _Children = children;
                _Parents = parents;
            }
        }

        /// <summary>
        /// 获取与 dataID 直接/间接关联的所有 DataID（不含自身），
        /// 邻接表只在首次调用或关联变更时查询一次数据库
        /// </summary>
        /// <param name="dataID"></param>
        /// <returns></returns>
        public HashSet<long> GetAssociationDatas(long dataID)
        {
            InitAdjacencyList();
            Dictionary<long, List<long>> children = _Children;
            Dictionary<long, List<long>> parents = _Parents;
            HashSet<long> set = new HashSet<long>();
            HashSet<long> visited = new HashSet<long>();
            Queue<long> queue = new Queue<long>();
            queue.Enqueue(dataID);
            visited.Add(dataID);
            while (queue.Count > 0) {
                long cur = queue.Dequeue();
                if (children != null && children.TryGetValue(cur, out List<long> subs)) {
                    foreach (long sub in subs) {
                        if (visited.Add(sub)) {
                            set.Add(sub);
                            queue.Enqueue(sub);
                        }
                    }
                }
                if (parents != null && parents.TryGetValue(cur, out List<long> pars)) {
                    foreach (long par in pars) {
                        if (visited.Add(par)) {
                            set.Add(par);
                            queue.Enqueue(par);
                        }
                    }
                }
            }
            set.Remove(dataID);
            return set;
        }
    }
}