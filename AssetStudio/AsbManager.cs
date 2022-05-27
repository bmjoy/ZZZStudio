using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace AssetStudio
{
    public class BLKEntry
    {
        public Dictionary<string, long> Location = new Dictionary<string, long>();
        public List<string> Dependancies = new List<string>();
    }
    public static class AsbManager
    {
        public static Dictionary<string, BLKEntry> BLKMap = new Dictionary<string, BLKEntry>();
        public static Dictionary<string, List<long>> offsets = new Dictionary<string, List<long>>();

        public static void BuildBLKMap(string path, List<string> files)
        {
            Logger.Info(string.Format("Building BLKMap"));
            try
            {
                BLKMap.Clear();
                Progress.Reset();
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    using (var reader = new FileReader(file))
                    {
                        var blkfile = new BlkFile(reader);
                        foreach (var kvp in blkfile.Files)
                        {
                            foreach (var f in kvp.Value.FileList)
                            {
                                var cabReader = new FileReader(f.stream);
                                if (cabReader.FileType == FileType.AssetsFile)
                                {
                                    var assetsFile = new SerializedFile(cabReader, null);
                                    var objects = assetsFile.m_Objects.Where(x => x.classID == (int)ClassIDType.AssetBundle).ToArray();
                                    foreach (var obj in objects)
                                    {
                                        var objectReader = new ObjectReader(assetsFile.reader, assetsFile, obj);
                                        var asb = new AssetBundle(objectReader);
                                        if (!BLKMap.ContainsKey(asb.m_AssetBundleName))
                                        {
                                            BLKMap.Add(asb.m_Name, new BLKEntry());
                                            BLKMap[asb.m_Name].Dependancies.AddRange(asb.m_Dependencies);
                                        }    
                                        BLKMap[asb.m_Name].Location.Add(file, kvp.Key); 
                                    }
                                }
                            }
                        }
                    }
                    Logger.Info($"[{i + 1}/{files.Count}] Processed {Path.GetFileName(file)}");
                    Progress.Report(i + 1, files.Count);
                }

                BLKMap = BLKMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                var outputFile = new FileInfo(@"BLKMap.bin");

                using (var binaryFile = outputFile.Create())
                using (var writter = new BinaryWriter(binaryFile))
                {
                    writter.Write(BLKMap.Count);
                    foreach (var blk in BLKMap)
                    {
                        writter.Write(blk.Key);
                        writter.Write(blk.Value.Dependancies.Count);
                        foreach (var dep in blk.Value.Dependancies)
                            writter.Write(dep);
                        writter.Write(blk.Value.Location.Count);
                        foreach (var location in blk.Value.Location)
                        {
                            writter.Write(location.Key);
                            writter.Write(location.Value);
                        }
                    }
                }
                Logger.Info($"BLKMap build successfully !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"BLKMap was not build, {e.Message}");
            }
        }
        public static void LoadBLKMap()
        {
            Logger.Info(string.Format("Loading BLKMap"));
            try
            {
                BLKMap.Clear();
                using (var binaryFile = File.OpenRead("BLKMap.bin"))
                using (var reader = new BinaryReader(binaryFile))
                {
                    var count = reader.ReadInt32();
                    BLKMap = new Dictionary<string, BLKEntry>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var asb = reader.ReadString();
                        BLKMap.Add(asb, new BLKEntry());
                        var depCount = reader.ReadInt32();
                        for (int j = 0; j < depCount; j++)
                        {
                            var dep = reader.ReadString();
                            BLKMap[asb].Dependancies.Add(dep);
                        }
                        var locationCount = reader.ReadInt32();
                        for (int j = 0; j < locationCount; j++)
                        {
                            var path = reader.ReadString();
                            var offset = reader.ReadInt64();
                            BLKMap[asb].Location.Add(path, offset);
                        }
                        
                    }
                }
                Logger.Info(string.Format("Loaded BLKMap !!"));
            }
            catch (Exception e)
            {
                Logger.Warning($"BLKMap was not loaded, {e.Message}");
            }
        }
        public static void AddCabOffset(string asb)
        {
            if (BLKMap.TryGetValue(asb, out var asbEntry))
            {
                var locationPair = asbEntry.Location.Pick();
                var path = locationPair.Key;
                if (!offsets.ContainsKey(path))
                    offsets.Add(path, new List<long>());
                offsets[path].Add(locationPair.Value);
            }
        }

        public static void FindAsbFromBLK(string path, ref List<string> asbs)
        {
            var fileName = Path.GetFileName(path);
            foreach (var pair in BLKMap)
            {
                if (pair.Value.Location.Keys.Select(x => Path.GetFileName(x)).Contains(fileName))
                {
                    asbs.Add(pair.Key);
                    asbs.AddRange(pair.Value.Dependancies);
                }
            }
        }

        public static void ProcessBLKFiles(ref string[] files)
        {
            var newFiles = new List<string>();
            var asbs = new List<string>();
            foreach (var file in files)
                FindAsbFromBLK(file, ref asbs);

            asbs = asbs.Distinct().ToList();
            asbs.ForEach(AddCabOffset);

            offsets = offsets.ToDictionary(x => x.Key, x => x.Value.OrderBy(y => y).ToList());
            newFiles.AddRange(offsets.Keys.ToList());

            if (!ResourceIndex.Loaded)
            {
                files = newFiles.ToArray();
                return;
            }

            files = newFiles.OrderBy(x =>
            {
                var index = ResourceIndex.BlockSortList.IndexOf(Convert.ToInt32(Path.GetFileNameWithoutExtension(x)));
                return index < 0 ? int.MaxValue : index;
            }).ToArray();
        }

        public static void ProcessDependancies(ref string[] files)
        {
            Logger.Info("Resolving Dependancies...");
            var file = files.FirstOrDefault();
            if (Path.GetExtension(file) == ".blk")
            {
                ProcessBLKFiles(ref files);
            }
        }
    }
}
