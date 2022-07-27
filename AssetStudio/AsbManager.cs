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
        public static Dictionary<string, HashSet<long>> offsets = new Dictionary<string, HashSet<long>>();

        public static void BuildBLKMap(List<string> files)
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
                                        if (!BLKMap.ContainsKey(asb.AssetBundleName))
                                        {
                                            BLKMap.Add(asb.m_Name, new BLKEntry());
                                            BLKMap[asb.m_Name].Dependancies.AddRange(asb.Dependencies);
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
                var locationPair = asbEntry.Location.Pick(offsets.LastOrDefault().Key);
                var path = locationPair.Key;
                if (!offsets.ContainsKey(path))
                    offsets.Add(path, new HashSet<long>());
                offsets[path].Add(locationPair.Value);
                foreach (var dep in asbEntry.Dependancies)
                    AddCabOffset(dep);
            }
        }

        public static bool FindAsbFromBLK(string path, out HashSet<string> asbs)
        {
            asbs = new HashSet<string>();
            foreach (var pair in BLKMap)
                if (pair.Value.Location.ContainsKey(path))
                    asbs.Add(pair.Key);
            return asbs.Count != 0;
        }

        public static void ProcessBLKFiles(ref string[] files)
        {
            var newFiles = files.ToList();
            foreach (var file in files)
            {
                if (!offsets.ContainsKey(file))
                    offsets.Add(file, new HashSet<long>());
                if (FindAsbFromBLK(file, out var asbs))
                    foreach (var asb in asbs)
                        AddCabOffset(asb);
            }
            newFiles.AddRange(offsets.Keys.ToList());
            files = newFiles.ToArray();
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
