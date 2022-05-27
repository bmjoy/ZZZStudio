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
        public static KeyValuePair<string, long> Pick(this Dictionary<string, long> dict, string path)
        {
            var source = Convert.ToInt64(Path.GetFileNameWithoutExtension(path));

            var distance = new List<long>(dict.Count);
            foreach (var pair in dict)
            {
                var target = Convert.ToInt64(Path.GetFileNameWithoutExtension(pair.Key));
                var diff = target - source;
                distance.Add(Math.Abs(diff));
            }

            var idx = distance.Select((x, i) => (x, i)).Min().i;
            return dict.ElementAt(idx);
        }
    }
}
