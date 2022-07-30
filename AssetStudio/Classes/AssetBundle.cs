using Newtonsoft.Json;
using System.Collections.Generic;

namespace AssetStudio
{
    public class AssetInfo
    {
        public int preloadIndex;
        public int preloadSize;
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
        public static bool Exportable;

        [JsonProperty]
        public PPtr<Object>[] PreloadTable;
        [JsonProperty]
        public KeyValuePair<string, AssetInfo>[] Container;
        [JsonProperty]
        public string AssetBundleName;
        [JsonProperty]
        public int DependencyCount;
        [JsonProperty]
        public string[] Dependencies;

        public AssetBundle(ObjectReader reader) : base(reader)
        {
            var m_PreloadTableSize = reader.ReadInt32();
            PreloadTable = new PPtr<Object>[m_PreloadTableSize];
            for (int i = 0; i < m_PreloadTableSize; i++)
            {
                PreloadTable[i] = new PPtr<Object>(reader);
            }

            var m_ContainerSize = reader.ReadInt32();
            Container = new KeyValuePair<string, AssetInfo>[m_ContainerSize];
            for (int i = 0; i < m_ContainerSize; i++)
            {
                Container[i] = new KeyValuePair<string, AssetInfo>(reader.ReadAlignedString(), new AssetInfo(reader));
            }

            AssetBundleName = reader.ReadAlignedString();
            DependencyCount = reader.ReadInt32();
            Dependencies = new string[DependencyCount];
            for (int k = 0; k < DependencyCount; k++)
            {
                Dependencies[k] = reader.ReadAlignedString();
            }
        }
    }
}
