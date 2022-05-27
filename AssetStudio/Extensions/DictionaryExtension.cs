using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetStudio
{
    public static class DictionaryExtension
    {
        public static KeyValuePair<string, long> Pick(this Dictionary<string, long> dict)
        {
            if (!ResourceIndex.Loaded) 
                return dict.FirstOrDefault();

            return dict.OrderBy(x =>
            {
                var index = ResourceIndex.BlockSortList.IndexOf(Convert.ToInt32(Path.GetFileNameWithoutExtension(x.Key)));
                return index < 0 ? int.MaxValue : index;
            }).FirstOrDefault();
        }
    }
}
