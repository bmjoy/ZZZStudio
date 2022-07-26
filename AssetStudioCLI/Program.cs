using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using AssetStudio;
using System.Globalization;

namespace AssetStudioCLI 
{
    public class Program
    {
        public static AssetsManager AssetsManager = new AssetsManager();
        public static List<AssetItem> exportableAssets = new List<AssetItem>();
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length > 2)
                {
                    var inputPath = args[0];
                    var outputPath = args[1];
                    ClassIDType[] formats = Array.Empty<ClassIDType>();
                    if (args.Length > 3)
                    {
                        var formatArr = args.Skip(2).ToArray();
                        formats = Array.ConvertAll(formatArr, value => (ClassIDType)Enum.Parse(typeof(ClassIDType), value, true));
                    }

                    Logger.Default = new ConsoleLogger();
                    if (Directory.Exists(inputPath))
                    {
                        AssetsManager.LoadFolder(inputPath);
                    }
                    else
                    {
                        AssetsManager.LoadFiles(inputPath);
                    }
                    BuildAssetData(formats);
                    ExportAssets(outputPath, exportableAssets);
                }
                else
                {
                    throw new ArgumentException("Wrong input !!");
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                ShowHelp();
            }
        }
        public static void BuildAssetData(ClassIDType[] formats)
        {
            string productName = null;
            var objectCount = AssetsManager.assetsFileList.Sum(x => x.Objects.Count);
            var objectAssetItemDic = new Dictionary<AssetStudio.Object, AssetItem>(objectCount);
            var containers = new List<(PPtr<AssetStudio.Object>, string)>();
            int i = 0;
            foreach (var assetsFile in AssetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    var assetItem = new AssetItem(asset);
                    objectAssetItemDic.Add(asset, assetItem);
                    assetItem.UniqueID = " #" + i;
                    var exportable = formats.Length > 0 ? formats.Contains(assetItem.Asset.type) : true;
                    switch (asset)
                    {
                        case GameObject m_GameObject:
                            assetItem.Text = m_GameObject.m_Name;
                            break;
                        case Texture2D m_Texture2D:
                            if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                            assetItem.Text = m_Texture2D.m_Name;
                            break;
                        case AudioClip m_AudioClip:
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                                assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                            assetItem.Text = m_AudioClip.m_Name;
                            break;
                        case VideoClip m_VideoClip:
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                                assetItem.FullSize = asset.byteSize + (long)m_VideoClip.m_ExternalResources.m_Size;
                            assetItem.Text = m_VideoClip.m_Name;
                            break;
                        case Shader m_Shader:
                            assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
                            break;
                        case Mesh _:
                        case TextAsset _:
                        case AnimationClip _:
                        case Font _:
                        case MovieTexture _:
                        case Sprite _:
                            assetItem.Text = ((NamedObject)asset).m_Name;
                            break;
                        case Animator m_Animator:
                            if (m_Animator.m_GameObject.TryGet(out var gameObject))
                            {
                                assetItem.Text = gameObject.m_Name;
                            }
                            break;
                        case MonoBehaviour m_MonoBehaviour:
                            if (m_MonoBehaviour.m_Name == "" && m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                            {
                                assetItem.Text = m_Script.m_ClassName;
                            }
                            else
                            {
                                assetItem.Text = m_MonoBehaviour.m_Name;
                            }
                            break;
                        case PlayerSettings m_PlayerSettings:
                            productName = m_PlayerSettings.productName;
                            break;
                        case AssetBundle m_AssetBundle:
                            foreach (var m_Container in m_AssetBundle.Container)
                            {
                                var preloadIndex = m_Container.Value.preloadIndex;
                                var preloadSize = m_Container.Value.preloadSize;
                                var preloadEnd = preloadIndex + preloadSize;
                                for (int k = preloadIndex; k < preloadEnd; k++)
                                {
                                    if (long.TryParse(m_Container.Key, out var containerValue))
                                    {
                                        var last = unchecked((uint)containerValue);
                                        var path = ResourceIndex.GetBundlePath(last);
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            containers.Add((m_AssetBundle.PreloadTable[k], path));
                                            continue;
                                        }
                                    }
                                    containers.Add((m_AssetBundle.PreloadTable[k], m_Container.Key));
                                }
                            }
                            assetItem.Text = m_AssetBundle.m_Name;
                            break;
                        case IndexObject m_IndexObject:
                            assetItem.Text = "IndexObject";
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }
                            break;
                        case MiHoYoBinData m_MiHoYoBinData:
                            if (m_MiHoYoBinData.assetsFile.ObjectsDic.TryGetValue(2, out var obj) && obj is IndexObject indexObject)
                            {
                                if (indexObject.Names.TryGetValue(m_MiHoYoBinData.m_PathID, out var binName))
                                {
                                    string path = "";
                                    if (Path.GetExtension(assetsFile.originalPath) == ".blk")
                                    {
                                        var blkName = Path.GetFileNameWithoutExtension(assetsFile.originalPath);
                                        var blk = Convert.ToUInt64(blkName);
                                        var lastHex = uint.Parse(binName, NumberStyles.HexNumber);
                                        var blkHash = (blk << 32) | lastHex;
                                        var index = ResourceIndex.GetAssetIndex(blkHash);
                                        var bundleInfo = ResourceIndex.GetBundleInfo(index);
                                        path = bundleInfo != null ? bundleInfo.Path : "";
                                    }
                                    else
                                    {
                                        var last = uint.Parse(binName, NumberStyles.HexNumber);
                                        path = ResourceIndex.GetBundlePath(last) ?? "";
                                    }
                                    assetItem.Container = path;
                                    assetItem.Text = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : binName;
                                }
                            }
                            else assetItem.Text = string.Format("BinFile #{0}", assetItem.m_PathID);
                            break;
                        case NamedObject m_NamedObject:
                            assetItem.Text = m_NamedObject.m_Name;
                            break;
                    }
                    if (assetItem.Text == "")
                    {
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                    }
                    if (exportable)
                    {
                        exportableAssets.Add(assetItem);
                    }
                }
            }
            foreach ((var pptr, var container) in containers)
            {
                if (pptr.TryGet(out var obj))
                {
                    objectAssetItemDic[obj].Container = container;
                }
            }
            containers.Clear();
        }
        public static void ExportAssets(string savePath, List<AssetItem> toExportAssets)
        {
            int toExportCount = toExportAssets.Count;
            int exportedCount = 0;
            int i = 0;
            Progress.Reset();
            foreach (var asset in toExportAssets)
            {
                string exportPath;
                exportPath = Path.Combine(savePath, asset.TypeString);
                exportPath += Path.DirectorySeparatorChar;
                if (Exporter.ExportConvertFile(asset, exportPath))
                {
                    exportedCount++;
                }

                Progress.Report(++i, toExportCount);
            }
        }
        
        public static void ShowHelp()
        {
            var versionString = Assembly.GetEntryAssembly()?
                                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                                    .InformationalVersion
                                                    .ToString();
        
        Console.WriteLine(@"AssetStudioCLI v{0}
------------------------
Usage:
    AssetStudioCLI input_path output_path [formats ...]
", versionString);
        }
    }
}