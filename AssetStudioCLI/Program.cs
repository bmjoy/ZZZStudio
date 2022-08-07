using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AssetStudio;
using CommandLine;

namespace AssetStudioCLI
{
    public class Program
    {
        public static AssetsManager AssetsManager = new AssetsManager() { ResolveDependencies = false };
        public static List<AssetItem> exportableAssets = new List<AssetItem>();
        public static void Main(string[] args)
        {
            var parser = new Parser(config =>
            {
                config.AutoHelp = true;
                config.AutoVersion = true;
                config.CaseInsensitiveEnumValues = true;
                config.HelpWriter = Console.Out;
            });
            parser.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       try
                       {
                           if (o.Verbose)
                               Logger.Default = new ConsoleLogger();

                           var inputPath = o.Input;
                           var outputPath = o.Output;
                           var types = o.Type.ToArray();
                           var filtes = o.Filter.ToArray();

                           var files = Directory.Exists(inputPath) ? Directory.GetFiles(inputPath, "*.bundle", SearchOption.AllDirectories) : new string[] { inputPath };
                           foreach (var file in files)
                           {
                               AssetsManager.LoadFiles(file);
                               BuildAssetData(types, filtes);
                               ExportAssets(outputPath, exportableAssets);
                               exportableAssets.Clear();
                               AssetsManager.Clear();
                           }
                       }
                       catch (Exception e)
                       {
                           Console.WriteLine(e.Message);
                           Console.WriteLine(e.StackTrace);
                       }
                   });

        }
        public static void BuildAssetData(ClassIDType[] formats, Regex[] filters)
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
                    assetItem.Text = "";
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
                                    containers.Add((m_AssetBundle.PreloadTable[k], m_Container.Key));
                                }
                            }
                            assetItem.Text = m_AssetBundle.m_Name;
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }
                            break;
                        case NamedObject m_NamedObject:
                            assetItem.Text = m_NamedObject.m_Name;
                            break;
                    }
                    if (assetItem.Text == "")
                    {
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                    }
                    var isMatchRegex = filters.Length == 0 || filters.Any(x => x.IsMatch(assetItem.Text));
                    var isFilteredType = formats.Length == 0 || formats.Contains(assetItem.Asset.type);
                    if (isMatchRegex && isFilteredType)
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
            foreach (var asset in toExportAssets)
            {
                string exportPath;
                exportPath = Path.Combine(savePath, asset.TypeString);
                exportPath += Path.DirectorySeparatorChar;
                Logger.Info($"[{exportedCount + 1}/{toExportCount}] Exporting {asset.TypeString}: {asset.Text}");
                try
                {
                    if (Exporter.ExportConvertFile(asset, exportPath))
                    {
                        exportedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info($"Export {asset.Type}:{asset.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                }
            }

            var statusText = exportedCount == 0 ? "Nothing exported." : $"Finished exporting {exportedCount} assets.";

            if (toExportCount > exportedCount)
            {
                statusText += $" {toExportCount - exportedCount} assets skipped (not extractable or files already exist)";
            }

            Logger.Info(statusText);
        }
    }

    public class Options
    {
        [Option('v', "verbose", HelpText = "Show log messages.")]
        public bool Verbose { get; set; }
        [Option('t', "type", HelpText = "Specify unity type(s).")]
        public IEnumerable<ClassIDType> Type { get; set; }
        [Option('f', "filter", HelpText = "Specify regex filter(s).")]
        public IEnumerable<Regex> Filter { get; set; }
        [Value(0, Required = true, MetaName = "input_path", HelpText = "Input file/folder.")]
        public string Input { get; set; }
        [Value(1, Required = true, MetaName = "output_path", HelpText = "Output folder.")]
        public string Output { get; set; }
    }
}