using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AssetStudio
{
    public static class CABManager
    {
        public static Dictionary<string, WMVEntry> WMVMap = new Dictionary<string, WMVEntry>();

        public static void BuildWMVMap(List<string> files)
        {
            Logger.Info(string.Format("Building WMVMap"));
            try
            {
                WMVMap.Clear();
                Progress.Reset();
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    using (var reader = new FileReader(file))
                    {
                        var pos = reader.Position;
                        var bundlefile = new BundleFile(reader);
                        foreach (var cab in bundlefile.fileList)
                        {
                            var cabReader = new FileReader(cab.stream);
                            if (cabReader.FileType == FileType.AssetsFile)
                            {
                                WMVMap.Add(cab.path, new WMVEntry(file, pos));
                            }
                        }
                    }

                    Logger.Info($"[{i + 1}/{files.Count}] Processed {Path.GetFileName(file)}");
                    Progress.Report(i + 1, files.Count);
                }

                WMVMap = WMVMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                var outputFile = new FileInfo(@"WMVMap.bin");

                using (var binaryFile = outputFile.Create())
                using (var writter = new BinaryWriter(binaryFile))
                {
                    writter.Write(WMVMap.Count);
                    foreach (var cab in WMVMap)
                    {
                        writter.Write(cab.Key);
                        writter.Write(cab.Value.Path);
                        writter.Write(cab.Value.Offset);
                    }
                }
                Logger.Info($"WMVMap build successfully !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"WMVMap was not build, {e.Message}");
            }
        }

        public static void LoadWMVMap()
        {
            Logger.Info(string.Format("Loading WMVMap"));
            try
            {
                WMVMap.Clear();
                using (var binaryFile = File.OpenRead("WMVMap.bin"))
                using (var reader = new BinaryReader(binaryFile))
                {
                    var count = reader.ReadInt32();
                    WMVMap = new Dictionary<string, WMVEntry>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var cab = reader.ReadString();
                        var path = reader.ReadString();
                        var offset = reader.ReadInt64();
                        WMVMap.Add(cab, new WMVEntry(path, offset));
                    }
                }
                Logger.Info(string.Format("Loaded WMVMap !!"));
            }
            catch (Exception e)
            {
                Logger.Warning($"WMVMap was not loaded, {e.Message}");
            }
        }
    }
    public class WMVEntry : IComparable<WMVEntry>
    {
        public string Path;
        public long Offset;
        public WMVEntry(string path, long offset)
        {
            Path = path;
            Offset = offset;
        }
        public int CompareTo(WMVEntry other)
        {
            if (other == null) return 1;

            int result;
            if (other == null)
                throw new ArgumentException("Object is not a WMVEntry");

            result = Path.CompareTo(other.Path);

            if (result == 0)
                result = Offset.CompareTo(other.Offset);

            return result;
        }
    }
}
