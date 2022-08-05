using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AssetStudio
{
    public static class CABManager
    {
        public static HashSet<string> Files = new HashSet<string>();
        public static Dictionary<string, ZZZEntry> ZZZMap = new Dictionary<string, ZZZEntry>();

        public static void BuildZZZMap(List<string> files)
        {
            Logger.Info(string.Format("Building ZZZMap"));
            try
            {
                ZZZMap.Clear();
                Progress.Reset();
                int collisions = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    using (var reader = new FileReader(file))
                    {
                        var bundlefile = new BundleFile(reader);
                        foreach (var cab in bundlefile.fileList)
                        {
                            var cabReader = new FileReader(cab.stream);
                            if (cabReader.FileType == FileType.AssetsFile)
                            {
                                if (ZZZMap.ContainsKey(cab.path))
                                {
                                    collisions++;
                                    continue;
                                }
                                var assetsFile = new SerializedFile(cabReader, null);
                                var dependancies = assetsFile.m_Externals.Select(x => x.fileName).ToList();
                                ZZZMap.Add(cab.path, new ZZZEntry(file, dependancies));
                            }
                        }
                    }

                    Logger.Info($"[{i + 1}/{files.Count}] Processed {Path.GetFileName(file)}");
                    Progress.Report(i + 1, files.Count);
                }

                ZZZMap = ZZZMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                var outputFile = new FileInfo(@"ZZZMap.bin");

                using (var binaryFile = outputFile.Create())
                using (var writer = new BinaryWriter(binaryFile))
                {
                    writer.Write(ZZZMap.Count);
                    foreach (var cab in ZZZMap)
                    {
                        writer.Write(cab.Key);
                        writer.Write(cab.Value.Path);
                        writer.Write(cab.Value.Dependencies.Count);
                        foreach(var dep in cab.Value.Dependencies)
                        {
                            writer.Write(dep);
                        }
                    }
                }
                Logger.Info($"ZZZMap build successfully, {collisions} Collisions Found !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"ZZZMap was not build, {e.Message}");
            }
        }

        public static void LoadZZZMap()
        {
            Logger.Info(string.Format("Loading ZZZMap"));
            try
            {
                ZZZMap.Clear();
                using (var binaryFile = File.OpenRead("ZZZMap.bin"))
                using (var reader = new BinaryReader(binaryFile))
                {
                    var count = reader.ReadInt32();
                    ZZZMap = new Dictionary<string, ZZZEntry>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var cab = reader.ReadString();
                        var path = reader.ReadString();
                        var depCount = reader.ReadInt32();
                        var dependencies = new List<string>(depCount);
                        for (int j = 0; j < depCount; j++)
                        {
                            var dep = reader.ReadString();
                            dependencies.Add(dep);
                        }
                        ZZZMap.Add(cab, new ZZZEntry(path, dependencies));
                    }
                }
                Logger.Info(string.Format("Loaded ZZZMap !!"));
            }
            catch (Exception e)
            {
                Logger.Warning($"ZZZMap was not loaded, {e.Message}");
            }
        }

        public static void AddCabOffset(string cab)
        {
            if (ZZZMap.TryGetValue(cab, out var encrEntry))
            {
                if (!Files.Contains(encrEntry.Path))
                {
                    Files.Add(encrEntry.Path);
                }
                foreach (var dep in encrEntry.Dependencies)
                {
                    AddCabOffset(dep);
                }
            }
        }

        public static bool FindCABFromZZZ(string path, out List<string> cabs)
        {
            cabs = new List<string>();
            foreach (var pair in ZZZMap)
            {
                if (pair.Value.Path.Contains(path))
                {
                    cabs.Add(pair.Key);
                }
            }
            return cabs.Count != 0;
        }

        public static void ProcessZZZFiles(ref string[] files)
        {
            var newFiles = files.ToList();
            foreach (var file in files)
            {
                if (!Files.Contains(file))
                {
                    Files.Add(file);
                }
                if (FindCABFromZZZ(file, out var cabs))
                {
                    foreach (var cab in cabs)
                    {
                        AddCabOffset(cab);
                    }
                }
            }
            newFiles.AddRange(Files);
            files = newFiles.ToArray();
        }

        public static void ProcessDependancies(ref string[] files)
        {
            Logger.Info("Resolving Dependancies...");
            var file = files.FirstOrDefault();
            if (Path.GetExtension(file) == ".bundle")
            {
                ProcessZZZFiles(ref files);
            }
        }
    }
    public class ZZZEntry : IComparable<ZZZEntry>
    {
        public string Path;
        public List<string> Dependencies;
        public ZZZEntry(string path, List<string> dependencies)
        {
            Path = path;
            Dependencies = dependencies;
        }
        public int CompareTo(ZZZEntry other)
        {
            if (other == null) return 1;

            int result;
            if (other == null)
                throw new ArgumentException("Object is not a ZZZEntry");

            result = Path.CompareTo(other.Path);

            return result;
        }
    }
}
