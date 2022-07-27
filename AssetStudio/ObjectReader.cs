using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public class ObjectReader : EndianBinaryReader
    {
        public SerializedFile assetsFile;
        public long m_PathID;
        public long byteStart;
        public uint byteSize;
        public ClassIDType type;
        public SerializedType serializedType;
        public BuildTarget platform;
        public SerializedFileFormatVersion m_Version;

        public int[] version => assetsFile.version;
        public BuildType buildType => assetsFile.buildType;

        public ObjectReader(EndianBinaryReader reader, SerializedFile assetsFile, ObjectInfo objectInfo) : base(reader.BaseStream, reader.Endian)
        {
            this.assetsFile = assetsFile;
            m_PathID = objectInfo.m_PathID;
            byteStart = objectInfo.byteStart;
            byteSize = objectInfo.byteSize;
            if (Enum.IsDefined(typeof(ClassIDType), objectInfo.classID))
            {
                type = (ClassIDType)objectInfo.classID;
            }
            else
            {
                type = ClassIDType.UnknownType;
            }
            serializedType = objectInfo.serializedType;
            platform = assetsFile.m_TargetPlatform;
            m_Version = assetsFile.header.m_Version;
        }

        public bool HasNamedObject()
        {
            return type == ClassIDType.Material
                || type == ClassIDType.Texture2D
                || type == ClassIDType.Mesh
                || type == ClassIDType.Shader
                || type == ClassIDType.TextAsset
                || type == ClassIDType.PhysicsMaterial2D
                || type == ClassIDType.ComputeShader
                || type == ClassIDType.AnimationClip
                || type == ClassIDType.AudioClip
                || type == ClassIDType.RenderTexture
                || type == ClassIDType.CustomRenderTexture
                || type == ClassIDType.Cubemap
                || type == ClassIDType.Avatar
                || type == ClassIDType.AnimatorController
                || type == ClassIDType.CGProgram
                || type == ClassIDType.MonoScript
                || type == ClassIDType.Texture3D
                || type == ClassIDType.Flare
                || type == ClassIDType.Font
                || type == ClassIDType.PhysicMaterial
                || type == ClassIDType.AssetBundle
                || type == ClassIDType.PreloadData
                || type == ClassIDType.MovieTexture
                || type == ClassIDType.TerrainData
                || type == ClassIDType.WebCamTexture
                || type == ClassIDType.SparseTexture
                || type == ClassIDType.SubstanceArchive
                || type == ClassIDType.ProceduralMaterial
                || type == ClassIDType.ProceduralTexture
                || type == ClassIDType.Texture2DArray
                || type == ClassIDType.CubemapArray
                || type == ClassIDType.ShaderVariantCollection
                || type == ClassIDType.Sprite
                || type == ClassIDType.AnimatorOverrideController
                || type == ClassIDType.BillboardAsset
                || type == ClassIDType.SpeedTreeWindAsset
                || type == ClassIDType.NavMeshData
                || type == ClassIDType.AudioMixer
                || type == ClassIDType.LightProbes
                || type == ClassIDType.SampleClip
                || type == ClassIDType.AudioMixerSnapshot
                || type == ClassIDType.AudioMixerGroup
                || type == ClassIDType.AssetBundleManifest
                || type == ClassIDType.AvatarMask
                || type == ClassIDType.VideoClip
                || type == ClassIDType.OcclusionCullingData;
        }

        public void Reset()
        {
            Position = byteStart;
        }
    }
}
