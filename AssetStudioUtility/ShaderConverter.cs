using K4os.Compression.LZ4;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetStudio
{
    public static class ShaderConverter
    {
        public static byte[] Convert(this Shader shader)
        {
            using(var ms = new MemoryStream())
            using(TextWriter writer = new StreamWriter(ms))
            {
                writer.Write(header);
                if (shader.m_SubProgramBlob != null) //5.3 - 5.4
                {
                    var decompressedBytes = new byte[shader.decompressedSize];
                    LZ4Codec.Decode(shader.m_SubProgramBlob, decompressedBytes);
                    using (var blobReader = new EndianBinaryReader(new MemoryStream(decompressedBytes), EndianType.LittleEndian))
                    {
                        var program = new ShaderProgram(blobReader, shader.version);
                        program.Read(blobReader, 0);
                        writer.Write(program.Export(Encoding.UTF8.GetString(shader.m_Script)));
                        return ms.ToArray();
                    }
                }

                if (shader.compressedBlob != null) //5.5 and up
                {
                    ConvertSerializedShader(shader, writer);
                    return ms.ToArray();
                }

                writer.Write(Encoding.UTF8.GetString(shader.m_Script));
                return ms.ToArray();
            }
        }

        private static void ConvertSerializedShader(Shader shader, TextWriter writer)
        {
            var length = shader.platforms.Length;
            var shaderPrograms = new ShaderProgram[length];
            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < shader.offsets[i].Length; j++)
                {
                    var offset = shader.offsets[i][j];
                    var decompressedLength = shader.decompressedLengths[i][j];
                    var decompressedBytes = new byte[decompressedLength];
                    Buffer.BlockCopy(shader.compressedBlob, (int)offset, decompressedBytes, 0, (int)decompressedLength);
                    using (var blobReader = new EndianBinaryReader(new MemoryStream(decompressedBytes), EndianType.LittleEndian))
                    {
                        if (j == 0)
                        {
                            shaderPrograms[i] = new ShaderProgram(blobReader, shader.version);
                        }
                        shaderPrograms[i].Read(blobReader, j);
                    }
                }
            }

            ConvertSerializedShader(shader.m_ParsedForm, shader.platforms, shaderPrograms, writer);
        }

        private static void ConvertSerializedShader(SerializedShader m_ParsedForm, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms, TextWriter writer)
        {
            writer.Write($"Shader \"{m_ParsedForm.m_Name}\" {{\n");

            ConvertSerializedProperties(m_ParsedForm.m_PropInfo, writer);

            foreach (var m_SubShader in m_ParsedForm.m_SubShaders)
            {
                ConvertSerializedSubShader(m_SubShader, platforms, shaderPrograms, writer);
            }

            if (!string.IsNullOrEmpty(m_ParsedForm.m_FallbackName))
            {
                writer.Write($"Fallback \"{m_ParsedForm.m_FallbackName}\"\n");
            }

            if (!string.IsNullOrEmpty(m_ParsedForm.m_CustomEditorName))
            {
                writer.Write($"CustomEditor \"{m_ParsedForm.m_CustomEditorName}\"\n");
            }

            writer.Write("}");
        }

        private static void ConvertSerializedSubShader(SerializedSubShader m_SubShader, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms, TextWriter writer)
        {
            writer.Write("SubShader {\n");
            if (m_SubShader.m_LOD != 0)
            {
                writer.Write($" LOD {m_SubShader.m_LOD}\n");
            }

            ConvertSerializedTagMap(m_SubShader.m_Tags, 1, writer);

            foreach (var m_Passe in m_SubShader.m_Passes)
            {
                ConvertSerializedPass(m_Passe, platforms, shaderPrograms, writer);
            }
            writer.Write("}\n");
        }

        private static void ConvertSerializedPass(SerializedPass m_Passe, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms, TextWriter writer)
        {
            switch (m_Passe.m_Type)
            {
                case PassType.Normal:
                    writer.Write(" Pass ");
                    break;
                case PassType.Use:
                    writer.Write(" UsePass ");
                    break;
                case PassType.Grab:
                    writer.Write(" GrabPass ");
                    break;
            }
            if (m_Passe.m_Type == PassType.Use)
            {
                writer.Write($"\"{m_Passe.m_UseName}\"\n");
            }
            else
            {
                writer.Write("{\n");

                if (m_Passe.m_Type == PassType.Grab)
                {
                    if (!string.IsNullOrEmpty(m_Passe.m_TextureName))
                    {
                        writer.Write($"  \"{m_Passe.m_TextureName}\"\n");
                    }
                }
                else
                {
                    ConvertSerializedShaderState(m_Passe.m_State, writer);

                    if (m_Passe.progVertex.m_SubPrograms.Length > 0)
                    {
                        writer.Write("Program \"vp\" {\n");
                        ConvertSerializedSubPrograms(m_Passe.progVertex.m_SubPrograms, platforms, shaderPrograms, writer);
                        writer.Write("}\n");
                    }

                    if (m_Passe.progFragment.m_SubPrograms.Length > 0)
                    {
                        writer.Write("Program \"fp\" {\n");
                        ConvertSerializedSubPrograms(m_Passe.progFragment.m_SubPrograms, platforms, shaderPrograms, writer);
                        writer.Write("}\n");
                    }

                    if (m_Passe.progGeometry.m_SubPrograms.Length > 0)
                    {
                        writer.Write("Program \"gp\" {\n");
                        ConvertSerializedSubPrograms(m_Passe.progGeometry.m_SubPrograms, platforms, shaderPrograms, writer);
                        writer.Write("}\n");
                    }

                    if (m_Passe.progHull.m_SubPrograms.Length > 0)
                    {
                        writer.Write("Program \"hp\" {\n");
                        ConvertSerializedSubPrograms(m_Passe.progHull.m_SubPrograms, platforms, shaderPrograms, writer);
                        writer.Write("}\n");
                    }

                    if (m_Passe.progDomain.m_SubPrograms.Length > 0)
                    {
                        writer.Write("Program \"dp\" {\n");
                        ConvertSerializedSubPrograms(m_Passe.progDomain.m_SubPrograms, platforms, shaderPrograms, writer);
                        writer.Write("}\n");
                    }

                    if (m_Passe.progRayTracing?.m_SubPrograms.Length > 0)
                    {
                        writer.Write("Program \"rtp\" {\n");
                        ConvertSerializedSubPrograms(m_Passe.progRayTracing.m_SubPrograms, platforms, shaderPrograms, writer);
                        writer.Write("}\n");
                    }
                }
                writer.Write("}\n");
            }
        }

        private static void ConvertSerializedSubPrograms(SerializedSubProgram[] m_SubPrograms, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms, TextWriter writer)
        {
            var groups = m_SubPrograms.GroupBy(x => x.m_BlobIndex);
            foreach (var group in groups)
            {
                var programs = group.GroupBy(x => x.m_GpuProgramType);
                foreach (var program in programs)
                {
                    for (int i = 0; i < platforms.Length; i++)
                    {
                        var platform = platforms[i];
                        if (CheckGpuProgramUsable(platform, program.Key))
                        {
                            var subPrograms = program.ToList();
                            var isTier = subPrograms.Count > 1;
                            foreach (var subProgram in subPrograms)
                            {
                                writer.Write($"SubProgram \"{GetPlatformString(platform)} ");
                                if (isTier)
                                {
                                    writer.Write($"hw_tier{subProgram.m_ShaderHardwareTier:00} ");
                                }
                                writer.Write("\" {\n");
                                writer.Write(shaderPrograms[i].m_SubPrograms[subProgram.m_BlobIndex].Export());
                                writer.Write("\n}\n");
                            }
                            break;
                        }
                    }
                }
            }
        }

        private static void ConvertSerializedShaderState(SerializedShaderState m_State, TextWriter writer)
        {
            if (!string.IsNullOrEmpty(m_State.m_Name))
            {
                writer.Write($"  Name \"{m_State.m_Name}\"\n");
            }
            if (m_State.m_LOD != 0)
            {
                writer.Write($"  LOD {m_State.m_LOD}\n");
            }

            ConvertSerializedTagMap(m_State.m_Tags, 2, writer);

            ConvertSerializedShaderRTBlendState(m_State.rtBlend, m_State.rtSeparateBlend, writer);

            if (m_State.alphaToMask.val > 0f)
            {
                writer.Write("  AlphaToMask On\n");
            }

            if (m_State.zClip?.val != 1f) //ZClip On
            {
                writer.Write("  ZClip Off\n");
            }

            if (m_State.zTest.val != 4f) //ZTest LEqual
            {
                writer.Write("  ZTest ");
                switch (m_State.zTest.val) //enum CompareFunction
                {
                    case 0f: //kFuncDisabled
                        writer.Write("Off");
                        break;
                    case 1f: //kFuncNever
                        writer.Write("Never");
                        break;
                    case 2f: //kFuncLess
                        writer.Write("Less");
                        break;
                    case 3f: //kFuncEqual
                        writer.Write("Equal");
                        break;
                    case 5f: //kFuncGreater
                        writer.Write("Greater");
                        break;
                    case 6f: //kFuncNotEqual
                        writer.Write("NotEqual");
                        break;
                    case 7f: //kFuncGEqual
                        writer.Write("GEqual");
                        break;
                    case 8f: //kFuncAlways
                        writer.Write("Always");
                        break;
                }

                writer.Write("\n");
            }

            if (m_State.zWrite.val != 1f) //ZWrite On
            {
                writer.Write("  ZWrite Off\n");
            }

            if (m_State.culling.val != 2f) //Cull Back
            {
                writer.Write("  Cull ");
                switch (m_State.culling.val) //enum CullMode
                {
                    case 0f: //kCullOff
                        writer.Write("Off");
                        break;
                    case 1f: //kCullFront
                        writer.Write("Front");
                        break;
                }
                writer.Write("\n");
            }

            if (m_State.offsetFactor.val != 0f || m_State.offsetUnits.val != 0f)
            {
                writer.Write($"  Offset {m_State.offsetFactor.val}, {m_State.offsetUnits.val}\n");
            }

            if (m_State.stencilRef.val != 0f ||
                m_State.stencilReadMask.val != 255f ||
                m_State.stencilWriteMask.val != 255f ||
                m_State.stencilOp.pass.val != 0f ||
                m_State.stencilOp.fail.val != 0f ||
                m_State.stencilOp.zFail.val != 0f ||
                m_State.stencilOp.comp.val != 8f ||
                m_State.stencilOpFront.pass.val != 0f ||
                m_State.stencilOpFront.fail.val != 0f ||
                m_State.stencilOpFront.zFail.val != 0f ||
                m_State.stencilOpFront.comp.val != 8f ||
                m_State.stencilOpBack.pass.val != 0f ||
                m_State.stencilOpBack.fail.val != 0f ||
                m_State.stencilOpBack.zFail.val != 0f ||
                m_State.stencilOpBack.comp.val != 8f)
            {
                writer.Write("  Stencil {\n");
                if (m_State.stencilRef.val != 0f)
                {
                    writer.Write($"   Ref {m_State.stencilRef.val}\n");
                }
                if (m_State.stencilReadMask.val != 255f)
                {
                    writer.Write($"   ReadMask {m_State.stencilReadMask.val}\n");
                }
                if (m_State.stencilWriteMask.val != 255f)
                {
                    writer.Write($"   WriteMask {m_State.stencilWriteMask.val}\n");
                }
                if (m_State.stencilOp.pass.val != 0f ||
                    m_State.stencilOp.fail.val != 0f ||
                    m_State.stencilOp.zFail.val != 0f ||
                    m_State.stencilOp.comp.val != 8f)
                {
                    writer.Write(ConvertSerializedStencilOp(m_State.stencilOp, ""));
                }
                if (m_State.stencilOpFront.pass.val != 0f ||
                    m_State.stencilOpFront.fail.val != 0f ||
                    m_State.stencilOpFront.zFail.val != 0f ||
                    m_State.stencilOpFront.comp.val != 8f)
                {
                    writer.Write(ConvertSerializedStencilOp(m_State.stencilOpFront, "Front"));
                }
                if (m_State.stencilOpBack.pass.val != 0f ||
                    m_State.stencilOpBack.fail.val != 0f ||
                    m_State.stencilOpBack.zFail.val != 0f ||
                    m_State.stencilOpBack.comp.val != 8f)
                {
                    writer.Write(ConvertSerializedStencilOp(m_State.stencilOpBack, "Back"));
                }
                writer.Write("  }\n");
            }

            if (m_State.fogMode != FogMode.Unknown ||
                m_State.fogColor.x.val != 0f ||
                m_State.fogColor.y.val != 0f ||
                m_State.fogColor.z.val != 0f ||
                m_State.fogColor.w.val != 0f ||
                m_State.fogDensity.val != 0f ||
                m_State.fogStart.val != 0f ||
                m_State.fogEnd.val != 0f)
            {
                writer.Write("  Fog {\n");
                if (m_State.fogMode != FogMode.Unknown)
                {
                    writer.Write("   Mode ");
                    switch (m_State.fogMode)
                    {
                        case FogMode.Disabled:
                            writer.Write("Off");
                            break;
                        case FogMode.Linear:
                            writer.Write("Linear");
                            break;
                        case FogMode.Exp:
                            writer.Write("Exp");
                            break;
                        case FogMode.Exp2:
                            writer.Write("Exp2");
                            break;
                    }
                    writer.Write("\n");
                }
                if (m_State.fogColor.x.val != 0f ||
                    m_State.fogColor.y.val != 0f ||
                    m_State.fogColor.z.val != 0f ||
                    m_State.fogColor.w.val != 0f)
                {
                    writer.Write(string.Format("   Color ({0},{1},{2},{3})\n",
                        m_State.fogColor.x.val.ToString(CultureInfo.InvariantCulture),
                        m_State.fogColor.y.val.ToString(CultureInfo.InvariantCulture),
                        m_State.fogColor.z.val.ToString(CultureInfo.InvariantCulture),
                        m_State.fogColor.w.val.ToString(CultureInfo.InvariantCulture)));
                }
                if (m_State.fogDensity.val != 0f)
                {
                    writer.Write($"   Density {m_State.fogDensity.val.ToString(CultureInfo.InvariantCulture)}\n");
                }
                if (m_State.fogStart.val != 0f ||
                    m_State.fogEnd.val != 0f)
                {
                    writer.Write($"   Range {m_State.fogStart.val.ToString(CultureInfo.InvariantCulture)}, {m_State.fogEnd.val.ToString(CultureInfo.InvariantCulture)}\n");
                }
                writer.Write("  }\n");
            }

            if (m_State.lighting)
            {
                writer.Write($"  Lighting {(m_State.lighting ? "On" : "Off")}\n");
            }

            writer.Write($"  GpuProgramID {m_State.gpuProgramID}\n");
        }

        private static string ConvertSerializedStencilOp(SerializedStencilOp stencilOp, string suffix)
        {
            var sb = new StringBuilder();
            sb.Append($"   Comp{suffix} {ConvertStencilComp(stencilOp.comp)}\n");
            sb.Append($"   Pass{suffix} {ConvertStencilOp(stencilOp.pass)}\n");
            sb.Append($"   Fail{suffix} {ConvertStencilOp(stencilOp.fail)}\n");
            sb.Append($"   ZFail{suffix} {ConvertStencilOp(stencilOp.zFail)}\n");
            return sb.ToString();
        }

        private static string ConvertStencilOp(SerializedShaderFloatValue op)
        {
            switch (op.val)
            {
                case 0f:
                default:
                    return "Keep";
                case 1f:
                    return "Zero";
                case 2f:
                    return "Replace";
                case 3f:
                    return "IncrSat";
                case 4f:
                    return "DecrSat";
                case 5f:
                    return "Invert";
                case 6f:
                    return "IncrWrap";
                case 7f:
                    return "DecrWrap";
            }
        }

        private static string ConvertStencilComp(SerializedShaderFloatValue comp)
        {
            switch (comp.val)
            {
                case 0f:
                    return "Disabled";
                case 1f:
                    return "Never";
                case 2f:
                    return "Less";
                case 3f:
                    return "Equal";
                case 4f:
                    return "LEqual";
                case 5f:
                    return "Greater";
                case 6f:
                    return "NotEqual";
                case 7f:
                    return "GEqual";
                case 8f:
                default:
                    return "Always";
            }
        }

        private static void ConvertSerializedShaderRTBlendState(SerializedShaderRTBlendState[] rtBlend, bool rtSeparateBlend, TextWriter writer)
        {
            for (var i = 0; i < rtBlend.Length; i++)
            {
                var blend = rtBlend[i];
                if (blend.srcBlend.val != 1f ||
                    blend.destBlend.val != 0f ||
                    blend.srcBlendAlpha.val != 1f ||
                    blend.destBlendAlpha.val != 0f)
                {
                    writer.Write("  Blend ");
                    if (i != 0 || rtSeparateBlend)
                    {
                        writer.Write($"{i} ");
                    }
                    writer.Write($"{ConvertBlendFactor(blend.srcBlend)} {ConvertBlendFactor(blend.destBlend)}");
                    if (blend.srcBlendAlpha.val != 1f ||
                        blend.destBlendAlpha.val != 0f)
                    {
                        writer.Write($", {ConvertBlendFactor(blend.srcBlendAlpha)} {ConvertBlendFactor(blend.destBlendAlpha)}");
                    }
                    writer.Write("\n");
                }

                if (blend.blendOp.val != 0f ||
                    blend.blendOpAlpha.val != 0f)
                {
                    writer.Write("  BlendOp ");
                    if (i != 0 || rtSeparateBlend)
                    {
                        writer.Write($"{i} ");
                    }
                    writer.Write(ConvertBlendOp(blend.blendOp));
                    if (blend.blendOpAlpha.val != 0f)
                    {
                        writer.Write($", {ConvertBlendOp(blend.blendOpAlpha)}");
                    }
                    writer.Write("\n");
                }

                var val = (int)blend.colMask.val;
                if (val != 0xf)
                {
                    writer.Write("  ColorMask ");
                    if (val == 0)
                    {
                        writer.Write(0);
                    }
                    else
                    {
                        if ((val & 0x2) != 0)
                        {
                            writer.Write("R");
                        }
                        if ((val & 0x4) != 0)
                        {
                            writer.Write("G");
                        }
                        if ((val & 0x8) != 0)
                        {
                            writer.Write("B");
                        }
                        if ((val & 0x1) != 0)
                        {
                            writer.Write("A");
                        }
                    }
                    writer.Write($" {i}\n");
                }
            }
        }

        private static string ConvertBlendOp(SerializedShaderFloatValue op)
        {
            switch (op.val)
            {
                case 0f:
                default:
                    return "Add";
                case 1f:
                    return "Sub";
                case 2f:
                    return "RevSub";
                case 3f:
                    return "Min";
                case 4f:
                    return "Max";
                case 5f:
                    return "LogicalClear";
                case 6f:
                    return "LogicalSet";
                case 7f:
                    return "LogicalCopy";
                case 8f:
                    return "LogicalCopyInverted";
                case 9f:
                    return "LogicalNoop";
                case 10f:
                    return "LogicalInvert";
                case 11f:
                    return "LogicalAnd";
                case 12f:
                    return "LogicalNand";
                case 13f:
                    return "LogicalOr";
                case 14f:
                    return "LogicalNor";
                case 15f:
                    return "LogicalXor";
                case 16f:
                    return "LogicalEquiv";
                case 17f:
                    return "LogicalAndReverse";
                case 18f:
                    return "LogicalAndInverted";
                case 19f:
                    return "LogicalOrReverse";
                case 20f:
                    return "LogicalOrInverted";
            }
        }

        private static string ConvertBlendFactor(SerializedShaderFloatValue factor)
        {
            switch (factor.val)
            {
                case 0f:
                    return "Zero";
                case 1f:
                default:
                    return "One";
                case 2f:
                    return "DstColor";
                case 3f:
                    return "SrcColor";
                case 4f:
                    return "OneMinusDstColor";
                case 5f:
                    return "SrcAlpha";
                case 6f:
                    return "OneMinusSrcColor";
                case 7f:
                    return "DstAlpha";
                case 8f:
                    return "OneMinusDstAlpha";
                case 9f:
                    return "SrcAlphaSaturate";
                case 10f:
                    return "OneMinusSrcAlpha";
            }
        }

        private static void ConvertSerializedTagMap(SerializedTagMap m_Tags, int intent, TextWriter writer)
        {
            if (m_Tags.tags.Length > 0)
            {
                writer.Write(new string(' ', intent));
                writer.Write("Tags { ");
                foreach (var pair in m_Tags.tags)
                {
                    writer.Write($"\"{pair.Key}\" = \"{pair.Value}\" ");
                }
                writer.Write("}\n");
            }
        }

        private static void ConvertSerializedProperties(SerializedProperties m_PropInfo, TextWriter writer)
        {
            writer.Write("Properties {\n");
            foreach (var m_Prop in m_PropInfo.m_Props)
            {
                ConvertSerializedProperty(m_Prop, writer);
            }
            writer.Write("}\n");
        }

        private static void ConvertSerializedProperty(SerializedProperty m_Prop, TextWriter writer)
        {
            foreach (var m_Attribute in m_Prop.m_Attributes)
            {
                writer.Write($"[{m_Attribute}] ");
            }
            //TODO Flag
            writer.Write($"{m_Prop.m_Name} (\"{m_Prop.m_Description}\", ");
            switch (m_Prop.m_Type)
            {
                case SerializedPropertyType.Color:
                    writer.Write("Color");
                    break;
                case SerializedPropertyType.Vector:
                    writer.Write("Vector");
                    break;
                case SerializedPropertyType.Float:
                    writer.Write("Float");
                    break;
                case SerializedPropertyType.Range:
                    writer.Write($"Range({m_Prop.m_DefValue[1]}, {m_Prop.m_DefValue[2]})");
                    break;
                case SerializedPropertyType.Texture:
                    switch (m_Prop.m_DefTexture.m_TexDim)
                    {
                        case TextureDimension.Any:
                            writer.Write("any");
                            break;
                        case TextureDimension.Tex2D:
                            writer.Write("2D");
                            break;
                        case TextureDimension.Tex3D:
                            writer.Write("3D");
                            break;
                        case TextureDimension.Cube:
                            writer.Write("Cube");
                            break;
                        case TextureDimension.Tex2DArray:
                            writer.Write("2DArray");
                            break;
                        case TextureDimension.CubeArray:
                            writer.Write("CubeArray");
                            break;
                    }
                    break;
            }
            writer.Write(") = ");
            switch (m_Prop.m_Type)
            {
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Vector:
                    writer.Write($"({m_Prop.m_DefValue[0]},{m_Prop.m_DefValue[1]},{m_Prop.m_DefValue[2]},{m_Prop.m_DefValue[3]})");
                    break;
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Range:
                    writer.Write(m_Prop.m_DefValue[0]);
                    break;
                case SerializedPropertyType.Texture:
                    writer.Write($"\"{m_Prop.m_DefTexture.m_DefaultName}\" {{ }}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            writer.Write("\n");
        }

        private static bool CheckGpuProgramUsable(ShaderCompilerPlatform platform, ShaderGpuProgramType programType)
        {
            switch (platform)
            {
                case ShaderCompilerPlatform.GL:
                    return programType == ShaderGpuProgramType.GLLegacy;
                case ShaderCompilerPlatform.D3D9:
                    return programType == ShaderGpuProgramType.DX9VertexSM20
                        || programType == ShaderGpuProgramType.DX9VertexSM30
                        || programType == ShaderGpuProgramType.DX9PixelSM20
                        || programType == ShaderGpuProgramType.DX9PixelSM30;
                case ShaderCompilerPlatform.Xbox360:
                case ShaderCompilerPlatform.PS3:
                case ShaderCompilerPlatform.PSP2:
                case ShaderCompilerPlatform.PS4:
                case ShaderCompilerPlatform.XboxOne:
                case ShaderCompilerPlatform.N3DS:
                case ShaderCompilerPlatform.WiiU:
                case ShaderCompilerPlatform.Switch:
                case ShaderCompilerPlatform.XboxOneD3D12:
                case ShaderCompilerPlatform.GameCoreXboxOne:
                case ShaderCompilerPlatform.GameCoreScarlett:
                case ShaderCompilerPlatform.PS5:
                    return programType == ShaderGpuProgramType.ConsoleVS
                        || programType == ShaderGpuProgramType.ConsoleFS
                        || programType == ShaderGpuProgramType.ConsoleHS
                        || programType == ShaderGpuProgramType.ConsoleDS
                        || programType == ShaderGpuProgramType.ConsoleGS;
                case ShaderCompilerPlatform.PS5NGGC:
                    return programType == ShaderGpuProgramType.PS5NGGC;
                case ShaderCompilerPlatform.D3D11:
                    return programType == ShaderGpuProgramType.DX11VertexSM40
                        || programType == ShaderGpuProgramType.DX11VertexSM50
                        || programType == ShaderGpuProgramType.DX11PixelSM40
                        || programType == ShaderGpuProgramType.DX11PixelSM50
                        || programType == ShaderGpuProgramType.DX11GeometrySM40
                        || programType == ShaderGpuProgramType.DX11GeometrySM50
                        || programType == ShaderGpuProgramType.DX11HullSM50
                        || programType == ShaderGpuProgramType.DX11DomainSM50;
                case ShaderCompilerPlatform.GLES20:
                    return programType == ShaderGpuProgramType.GLES;
                case ShaderCompilerPlatform.NaCl: //Obsolete
                    throw new NotSupportedException();
                case ShaderCompilerPlatform.Flash: //Obsolete
                    throw new NotSupportedException();
                case ShaderCompilerPlatform.D3D11_9x:
                    return programType == ShaderGpuProgramType.DX10Level9Vertex
                        || programType == ShaderGpuProgramType.DX10Level9Pixel;
                case ShaderCompilerPlatform.GLES3Plus:
                    return programType == ShaderGpuProgramType.GLES31AEP
                        || programType == ShaderGpuProgramType.GLES31
                        || programType == ShaderGpuProgramType.GLES3;
                case ShaderCompilerPlatform.PSM: //Unknown
                    throw new NotSupportedException();
                case ShaderCompilerPlatform.Metal:
                    return programType == ShaderGpuProgramType.MetalVS
                        || programType == ShaderGpuProgramType.MetalFS;
                case ShaderCompilerPlatform.OpenGLCore:
                    return programType == ShaderGpuProgramType.GLCore32
                        || programType == ShaderGpuProgramType.GLCore41
                        || programType == ShaderGpuProgramType.GLCore43;
                case ShaderCompilerPlatform.Vulkan:
                    return programType == ShaderGpuProgramType.SPIRV;
                default:
                    throw new NotSupportedException();
            }
        }

        public static string GetPlatformString(ShaderCompilerPlatform platform)
        {
            switch (platform)
            {
                case ShaderCompilerPlatform.GL:
                    return "openGL";
                case ShaderCompilerPlatform.D3D9:
                    return "d3d9";
                case ShaderCompilerPlatform.Xbox360:
                    return "xbox360";
                case ShaderCompilerPlatform.PS3:
                    return "ps3";
                case ShaderCompilerPlatform.D3D11:
                    return "d3d11";
                case ShaderCompilerPlatform.GLES20:
                    return "gles";
                case ShaderCompilerPlatform.NaCl:
                    return "glesdesktop";
                case ShaderCompilerPlatform.Flash:
                    return "flash";
                case ShaderCompilerPlatform.D3D11_9x:
                    return "d3d11_9x";
                case ShaderCompilerPlatform.GLES3Plus:
                    return "gles3";
                case ShaderCompilerPlatform.PSP2:
                    return "psp2";
                case ShaderCompilerPlatform.PS4:
                    return "ps4";
                case ShaderCompilerPlatform.XboxOne:
                    return "xboxone";
                case ShaderCompilerPlatform.PSM:
                    return "psm";
                case ShaderCompilerPlatform.Metal:
                    return "metal";
                case ShaderCompilerPlatform.OpenGLCore:
                    return "glcore";
                case ShaderCompilerPlatform.N3DS:
                    return "n3ds";
                case ShaderCompilerPlatform.WiiU:
                    return "wiiu";
                case ShaderCompilerPlatform.Vulkan:
                    return "vulkan";
                case ShaderCompilerPlatform.Switch:
                    return "switch";
                case ShaderCompilerPlatform.XboxOneD3D12:
                    return "xboxone_d3d12";
                case ShaderCompilerPlatform.GameCoreXboxOne:
                    return "xboxone";
                case ShaderCompilerPlatform.GameCoreScarlett:
                    return "xbox_scarlett";
                case ShaderCompilerPlatform.PS5:
                    return "ps5";
                case ShaderCompilerPlatform.PS5NGGC:
                    return "ps5_nggc";
                default:
                    return "unknown";
            }
        }

        private static string header = "//////////////////////////////////////////\n" +
                                      "//\n" +
                                      "// NOTE: This is *not* a valid shader file\n" +
                                      "//\n" +
                                      "///////////////////////////////////////////\n";
    }

    public class ShaderSubProgramEntry
    {
        public int Offset;
        public int Length;
        public int Segment;

        public ShaderSubProgramEntry(BinaryReader reader, int[] version)
        {
            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
            if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
            {
                Segment = reader.ReadInt32();
            }
        }
    }

    public class ShaderProgram
    {
        public ShaderSubProgramEntry[] entries;
        public ShaderSubProgram[] m_SubPrograms;

        public ShaderProgram(EndianBinaryReader reader, int[] version)
        {
            var subProgramsCapacity = reader.ReadInt32();
            entries = new ShaderSubProgramEntry[subProgramsCapacity];
            for (int i = 0; i < subProgramsCapacity; i++)
            {
                entries[i] = new ShaderSubProgramEntry(reader, version);
            }
            m_SubPrograms = new ShaderSubProgram[subProgramsCapacity];
        }

        public string Export(string shader)
        {
            var evaluator = new MatchEvaluator(match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                return m_SubPrograms[index].Export();
            });
            shader = Regex.Replace(shader, "GpuProgramIndex (.+)", evaluator);
            return shader;
        }
        public void Read(EndianBinaryReader reader, int segment)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Segment == segment)
                {
                    reader.BaseStream.Position = entry.Offset;
                    m_SubPrograms[i] = new ShaderSubProgram(reader);
                }
            }
        }
    }

    public class ShaderSubProgram
    {
        private int m_Version;
        public ShaderGpuProgramType m_ProgramType;
        public string[] m_Keywords;
        public string[] m_LocalKeywords;
        public byte[] m_ProgramCode;

        public ShaderSubProgram(EndianBinaryReader reader)
        {
            //LoadGpuProgramFromData
            //201509030 - Unity 5.3
            //201510240 - Unity 5.4
            //201608170 - Unity 5.5
            //201609010 - Unity 5.6, 2017.1 & 2017.2
            //201708220 - Unity 2017.3, Unity 2017.4 & Unity 2018.1
            //201802150 - Unity 2018.2 & Unity 2018.3
            //201806140 - Unity 2019.1~2021.1
            //202012090 - Unity 2021.2
            m_Version = reader.ReadInt32();
            m_ProgramType = (ShaderGpuProgramType)reader.ReadInt32();
            reader.BaseStream.Position += 12;
            if (m_Version >= 201608170)
            {
                reader.BaseStream.Position += 4;
            }
            var m_KeywordsSize = reader.ReadInt32();
            m_Keywords = new string[m_KeywordsSize];
            for (int i = 0; i < m_KeywordsSize; i++)
            {
                m_Keywords[i] = reader.ReadAlignedString();
            }
            if (m_Version >= 201806140 && m_Version < 202012090)
            {
                var m_LocalKeywordsSize = reader.ReadInt32();
                m_LocalKeywords = new string[m_LocalKeywordsSize];
                for (int i = 0; i < m_LocalKeywordsSize; i++)
                {
                    m_LocalKeywords[i] = reader.ReadAlignedString();
                }
            }
            m_ProgramCode = reader.ReadUInt8Array();
            reader.AlignStream();

            //TODO
        }

        public string Export()
        {
            var sb = new StringBuilder();
            if (m_Keywords.Length > 0)
            {
                sb.Append("Keywords { ");
                foreach (string keyword in m_Keywords)
                {
                    sb.Append($"\"{keyword}\" ");
                }
                sb.Append("}\n");
            }
            if (m_LocalKeywords != null && m_LocalKeywords.Length > 0)
            {
                sb.Append("Local Keywords { ");
                foreach (string keyword in m_LocalKeywords)
                {
                    sb.Append($"\"{keyword}\" ");
                }
                sb.Append("}\n");
            }

            sb.Append("\"");
            if (m_ProgramCode.Length > 0)
            {
                switch (m_ProgramType)
                {
                    case ShaderGpuProgramType.GLLegacy:
                    case ShaderGpuProgramType.GLES31AEP:
                    case ShaderGpuProgramType.GLES31:
                    case ShaderGpuProgramType.GLES3:
                    case ShaderGpuProgramType.GLES:
                    case ShaderGpuProgramType.GLCore32:
                    case ShaderGpuProgramType.GLCore41:
                    case ShaderGpuProgramType.GLCore43:
                        sb.Append(Encoding.UTF8.GetString(m_ProgramCode));
                        break;
                    case ShaderGpuProgramType.DX9VertexSM20:
                    case ShaderGpuProgramType.DX9VertexSM30:
                    case ShaderGpuProgramType.DX9PixelSM20:
                    case ShaderGpuProgramType.DX9PixelSM30:
                        {
                            /*var shaderBytecode = new ShaderBytecode(m_ProgramCode);
                            sb.Append(shaderBytecode.Disassemble());*/
                            sb.Append("// shader disassembly not supported on DXBC");
                            break;
                        }
                    case ShaderGpuProgramType.DX10Level9Vertex:
                    case ShaderGpuProgramType.DX10Level9Pixel:
                    case ShaderGpuProgramType.DX11VertexSM40:
                    case ShaderGpuProgramType.DX11VertexSM50:
                    case ShaderGpuProgramType.DX11PixelSM40:
                    case ShaderGpuProgramType.DX11PixelSM50:
                    case ShaderGpuProgramType.DX11GeometrySM40:
                    case ShaderGpuProgramType.DX11GeometrySM50:
                    case ShaderGpuProgramType.DX11HullSM50:
                    case ShaderGpuProgramType.DX11DomainSM50:
                        {
                            /*int start = 6;
                            if (m_Version == 201509030) // 5.3
                            {
                                start = 5;
                            }
                            var buff = new byte[m_ProgramCode.Length - start];
                            Buffer.BlockCopy(m_ProgramCode, start, buff, 0, buff.Length);
                            var shaderBytecode = new ShaderBytecode(buff);
                            sb.Append(shaderBytecode.Disassemble());*/
                            sb.Append("// shader disassembly not supported on DXBC");
                            break;
                        }
                    case ShaderGpuProgramType.MetalVS:
                    case ShaderGpuProgramType.MetalFS:
                        using (var reader = new BinaryReader(new MemoryStream(m_ProgramCode)))
                        {
                            var fourCC = reader.ReadUInt32();
                            if (fourCC == 0xf00dcafe)
                            {
                                int offset = reader.ReadInt32();
                                reader.BaseStream.Position = offset;
                            }
                            var entryName = reader.ReadStringToNull();
                            var buff = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                            sb.Append(Encoding.UTF8.GetString(buff));
                        }
                        break;
                    case ShaderGpuProgramType.SPIRV:
                        try
                        {
                            sb.Append(SpirVShaderConverter.Convert(m_ProgramCode));
                        }
                        catch (Exception e)
                        {
                            sb.Append($"// disassembly error {e.Message}\n");
                        }
                        break;
                    case ShaderGpuProgramType.ConsoleVS:
                    case ShaderGpuProgramType.ConsoleFS:
                    case ShaderGpuProgramType.ConsoleHS:
                    case ShaderGpuProgramType.ConsoleDS:
                    case ShaderGpuProgramType.ConsoleGS:
                        sb.Append(Encoding.UTF8.GetString(m_ProgramCode));
                        break;
                    default:
                        sb.Append($"//shader disassembly not supported on {m_ProgramType}");
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
