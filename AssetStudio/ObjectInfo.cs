using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public class ObjectInfo
    {
        public long byteStart;
        public uint byteSize;
        public int typeID;
        public int classID;
        public ushort isDestroyed;
        public byte stripped;

        public long m_PathID;
        public SerializedType serializedType;

        public bool HasExportableType()
        {
            return classID == (int)ClassIDType.GameObject
                || classID == (int)ClassIDType.Texture2D
                || classID == (int)ClassIDType.Mesh
                || classID == (int)ClassIDType.Shader
                || classID == (int)ClassIDType.TextAsset
                || classID == (int)ClassIDType.AnimationClip
                || classID == (int)ClassIDType.Animator
                || classID == (int)ClassIDType.Font
                || (classID == (int)ClassIDType.AssetBundle && AssetBundle.Exportable)
                || classID == (int)ClassIDType.Sprite;
        }
    }
}
