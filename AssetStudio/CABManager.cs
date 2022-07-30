using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AssetStudio
{
    public static class CABManager
    {
        public static HashSet<string> Files = new HashSet<string>();
        public static Dictionary<string, ENCREntry> ENCRMap = new Dictionary<string, ENCREntry>();

        public static void BuildENCRMap(List<string> files)
        {
            Logger.Info(string.Format("Building ENCRMap"));
            try
            {
                ENCRMap.Clear();
                Progress.Reset();
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
                                var assetsFile = new SerializedFile(cabReader, null);
                                var dependancies = assetsFile.m_Externals.Select(x => x.fileName).ToList();
                                ENCRMap.Add(cab.path, new ENCREntry(file, dependancies));
                            }
                        }
                    }

                    Logger.Info($"[{i + 1}/{files.Count}] Processed {Path.GetFileName(file)}");
                    Progress.Report(i + 1, files.Count);
                }

                ENCRMap = ENCRMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                var outputFile = new FileInfo(@"ENCRMap.bin");

                using (var binaryFile = outputFile.Create())
                using (var writer = new BinaryWriter(binaryFile))
                {
                    writer.Write(ENCRMap.Count);
                    foreach (var cab in ENCRMap)
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
                Logger.Info($"ENCRMap build successfully !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"ENCRMap was not build, {e.Message}");
            }
        }

        public static void LoadENCRMap()
        {
            Logger.Info(string.Format("Loading ENCRMap"));
            try
            {
                ENCRMap.Clear();
                using (var binaryFile = File.OpenRead("ENCRMap.bin"))
                using (var reader = new BinaryReader(binaryFile))
                {
                    var count = reader.ReadInt32();
                    ENCRMap = new Dictionary<string, ENCREntry>(count);
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
                        ENCRMap.Add(cab, new ENCREntry(path, dependencies));
                    }
                }
                Logger.Info(string.Format("Loaded ENCRMap !!"));
            }
            catch (Exception e)
            {
                Logger.Warning($"ENCRMap was not loaded, {e.Message}");
            }
        }

        public static void AddCabOffset(string cab)
        {
            if (ENCRMap.TryGetValue(cab, out var encrEntry))
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

        public static bool FindCABFromENCR(string path, out List<string> cabs)
        {
            cabs = new List<string>();
            foreach (var pair in ENCRMap)
            {
                if (pair.Value.Path.Contains(path))
                {
                    cabs.Add(pair.Key);
                }
            }
            return cabs.Count != 0;
        }

        public static void ProcessENCRFiles(ref string[] files)
        {
            var newFiles = files.ToList();
            foreach (var file in files)
            {
                if (!Files.Contains(file))
                {
                    Files.Add(file);
                }
                if (FindCABFromENCR(file, out var cabs))
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
            if (Path.GetExtension(file) == ".unity3d")
            {
                ProcessENCRFiles(ref files);
            }
        }
    }
    public class ENCREntry : IComparable<ENCREntry>
    {
        public string Path;
        public List<string> Dependencies;
        public ENCREntry(string path, List<string> dependencies)
        {
            Path = path;
            Dependencies = dependencies;
        }
        public int CompareTo(ENCREntry other)
        {
            if (other == null) return 1;

            int result;
            if (other == null)
                throw new ArgumentException("Object is not a ENCREntry");

            result = Path.CompareTo(other.Path);

            return result;
        }
    }
}
