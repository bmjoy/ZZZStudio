using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetStudio
{
    public static class ListExtension
    {
        public static string Pick(this List<string> list, string path)
        {
            var name = Path.GetFileName(Path.GetDirectoryName(path));
            var source = Convert.ToInt64(name);

            var distance = new List<long>(list.Count);
            foreach (var item in list)
            {
                name = Path.GetFileName(Path.GetDirectoryName(item));
                var target = Convert.ToInt64(name);
                var diff = target - source;
                distance.Add(Math.Abs(diff));
            }

            var idx = distance.Select((x, i) => (x, i)).DefaultIfEmpty().Min().i;
            return list.ElementAtOrDefault(idx);
        }
    }
}
