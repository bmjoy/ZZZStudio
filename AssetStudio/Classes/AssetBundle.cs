using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AssetStudio
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AssetInfo
    {
        [JsonProperty(PropertyName = "PreloadIndex")]
        public int preloadIndex;
        [JsonProperty(PropertyName = "PreloadSize")]
        public int preloadSize;
        [JsonProperty(PropertyName = "Asset")]
        public PPtr<Object> asset;

        public AssetInfo(ObjectReader reader)
        {
            preloadIndex = reader.ReadInt32();
            preloadSize = reader.ReadInt32();
            asset = new PPtr<Object>(reader);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AssetBundle : NamedObject
    {
        [JsonProperty(PropertyName = "PreloadTable")]
        public PPtr<Object>[] m_PreloadTable;
        [JsonProperty(PropertyName = "Container")]
        public KeyValuePair<string, AssetInfo>[] m_Container;
        [JsonProperty(PropertyName = "MainAsset")]
        public AssetInfo m_MainAsset;
        [JsonProperty(PropertyName = "RuntimeComaptability")]
        public uint m_RuntimeComaptability;
        [JsonProperty(PropertyName = "AssetBundleName")]
        public string m_AssetBundleName;
        [JsonProperty(PropertyName = "DependencyCount")]
        public int m_DependencyCount;
        [JsonProperty(PropertyName = "Dependencies")]
        public string[] m_Dependencies;
        [JsonProperty(PropertyName = "IsStreamedScenessetBundle")]
        public bool m_IsStreamedScenessetBundle;
        [JsonProperty(PropertyName = "ExplicitDataLayout")]
        public int m_ExplicitDataLayout;
        [JsonProperty(PropertyName = "PathFlags")]
        public int m_PathFlags;
        [JsonProperty(PropertyName = "SceneHashCount")]
        public int m_SceneHashCount;
        [JsonProperty(PropertyName = "SceneHashes")]
        public KeyValuePair<string, string>[] m_SceneHashes;

        public static bool Exportable;

        public AssetBundle(ObjectReader reader) : base(reader)
        {
            var m_PreloadTableSize = reader.ReadInt32();
            m_PreloadTable = new PPtr<Object>[m_PreloadTableSize];
            for (int i = 0; i < m_PreloadTableSize; i++)
            {
                m_PreloadTable[i] = new PPtr<Object>(reader);
            }

            var m_ContainerSize = reader.ReadInt32();
            m_Container = new KeyValuePair<string, AssetInfo>[m_ContainerSize];
            for (int i = 0; i < m_ContainerSize; i++)
            {
                m_Container[i] = new KeyValuePair<string, AssetInfo>(reader.ReadAlignedString(), new AssetInfo(reader));
            }

            m_MainAsset = new AssetInfo(reader);
            m_RuntimeComaptability = reader.ReadUInt32();
            m_AssetBundleName = reader.ReadAlignedString();
            m_DependencyCount = reader.ReadInt32();
            m_Dependencies = new string[m_DependencyCount];
            for (int k = 0; k < m_DependencyCount; k++)
            {
                m_Dependencies[k] = reader.ReadAlignedString();
            }
            reader.AlignStream();
            m_IsStreamedScenessetBundle = reader.ReadBoolean();
            reader.AlignStream();
            m_ExplicitDataLayout = reader.ReadInt32();
            m_PathFlags = reader.ReadInt32();
            m_SceneHashCount = reader.ReadInt32();
            m_SceneHashes = new KeyValuePair<string, string>[m_SceneHashCount];
            for (int l = 0; l < m_SceneHashCount; l++)
            {
                m_SceneHashes[l] = new KeyValuePair<string, string>(reader.ReadAlignedString(), reader.ReadAlignedString());
            }
        }
    }
}
