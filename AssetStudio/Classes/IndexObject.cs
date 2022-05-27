using System.Collections.Generic;
using Newtonsoft.Json;

namespace AssetStudio
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Index
    {
        [JsonProperty]
        public PPtr<Object> Object;
        [JsonProperty]
        public ulong Size;

        public Index(ObjectReader reader)
        {

            Object = new PPtr<Object>(reader);
            Size = reader.ReadUInt64();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class IndexObject : NamedObject
    {
        public static bool Exportable;

        [JsonProperty]
        public int Count;
        [JsonProperty]
        public KeyValuePair<string, Index>[] AssetMap;
        [JsonProperty]
        public Dictionary<long, string> Names = new Dictionary<long, string>();

        public IndexObject(ObjectReader reader) : base(reader)
        {
            Count = reader.ReadInt32();
            AssetMap = new KeyValuePair<string, Index>[Count];
            for (int i = 0; i < Count; i++)
            {
                var key = reader.ReadAlignedString();
                var value = new Index(reader);

                AssetMap[i] = new KeyValuePair<string, Index>(key, value);

                if (value.Object.m_FileID == 0)
                    Names.Add(value.Object.m_PathID, key);
            }
        }
    }

    
}
