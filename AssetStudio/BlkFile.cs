using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetStudio
{
    public partial class BlkFile
    {
        private List<Mhy0File> _files = null;
        public List<Mhy0File> Files => _files;
        public BlkFile(FileReader reader)
        {
            reader.Endian = EndianType.LittleEndian;

            var magic = reader.ReadUInt32();
            if (magic != 0x006B6C62)
                throw new Exception("not a blk");

            var count = reader.ReadInt32();
            var key = reader.ReadBytes(count);
            reader.ReadBytes(count);
            KeyScramble(key);

            var xorpadSize = reader.ReadUInt16();
            var data = reader.ReadBytes((int)(reader.Length - reader.Position));

            var xorpad = CreateDecryptVector(key, data, xorpadSize);
            for (int i = 0; i < data.Length; i++)
                data[i] ^= xorpad[i & 0xFFF];

            var memReader = new EndianBinaryReader(new MemoryStream(data), reader.Endian);
            _files = new List<Mhy0File>();
            if (reader.MHY0Pos.Length != 0)
            {
                try
                {
                    foreach (var pos in reader.MHY0Pos)
                    {
                        memReader.Position = pos;
                        _files.Add(new Mhy0File(memReader, reader.FullPath));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to load a mhy0 in {Path.GetFileName(reader.FullPath)}");
                    Logger.Warning(ex.Message);
                }
                finally
                {
                    memReader.Dispose();
                }
            }
            else
            {
                while (memReader.Position != memReader.BaseStream.Length)
                {
                    try
                    {
                        _files.Add(new Mhy0File(memReader, reader.FullPath));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to load a mhy0 in {Path.GetFileName(reader.FullPath)}");
                        Logger.Warning(ex.Message);
                        break;
                    }
                }
                memReader.Dispose();
            }
        }

        public static byte XorCombine(byte[] bytes, int offset) => bytes.Skip(offset).Take(0x10).Aggregate((b, v) => (byte)(b ^ v));

        private static void KeyScramble(byte[] key)
        {
            // key_scramble1
            for (uint i = 0; i < 0x10; i++)
                key[i] = ScrambleConstants.KeyScrambleTable[((i & 3) << 8) | key[i]];

            // key_scramble2
            var expandedKey = new byte[0x100];
            for (uint i = 0; i < 0x10; i++)
                expandedKey[i * 0x10] = key[i];
            for (uint i = 0; i < expandedKey.Length; i++)
                expandedKey[i] ^= (byte)(ScrambleConstants.ExpandKeyTable[i] ^ ScrambleConstants.StackStuff[i]);

            uint[] scratch;
            byte[] scratchByte = new byte[0x10];
            for (ulong i = 1; i < 0xA; i++)
            {
                scratch = new uint[4];
                for (int j = 0; j < scratch.Length; j++)
                {
                    scratch[j] ^= ScrambleConstants.GetMagicConst(0, expandedKey, j);
                    scratch[j] ^= ScrambleConstants.GetMagicConst(1, expandedKey, j);
                    scratch[j] ^= ScrambleConstants.GetMagicConst(2, expandedKey, j);
                    scratch[j] ^= ScrambleConstants.GetMagicConst(3, expandedKey, j);
                }

                expandedKey = new byte[0x100];
                Buffer.BlockCopy(scratch, 0, scratchByte, 0, scratchByte.Length);
                for (int j = 0; j < scratchByte.Length; j++)
                    expandedKey[j * 0x10] = scratchByte[j];
                for (uint j = 0; j < expandedKey.Length; j++)
                {
                    ulong idx = j + (i << 8);
                    expandedKey[j] ^= (byte)(ScrambleConstants.ExpandKeyTable[idx] ^ ScrambleConstants.StackStuff[idx]);
                }
            }

            for (int i = 0; i < scratchByte.Length; i++)
            {
                byte b = XorCombine(expandedKey, 0x10 * ScrambleConstants.IndexScramble[i]);
                scratchByte[i] = (byte)(~b ^ ScrambleConstants.MagicShuffle[b]);
            }

            expandedKey = new byte[0x100];
            for (int i = 0; i < scratchByte.Length; i++)
                expandedKey[i * 0x10] = scratchByte[i];
            for (int i = 0; i < expandedKey.Length; i++)
                expandedKey[i] ^= (byte)(ScrambleConstants.MagicShuffle2[i] ^ ScrambleConstants.StackStuff[i + 0xA00]);
            for (int i = 0; i < 0x10; i++)
                key[i] = XorCombine(expandedKey, 0x10 * i);
            for (int i = 0; i < 0x10; i++)
                key[i] ^= ScrambleConstants.InitialKey[i];
        }

        private byte[] CreateDecryptVector(byte[] key, byte[] encryptedData, ushort blockSize)
        {
            long keySeed = -1;
            blockSize = Math.Min(blockSize, (ushort)0x1000);
            
            for (int i = 0; i < blockSize >> 3; i++)
            {
                var vec = BitConverter.ToInt64(encryptedData, i * 8);
                keySeed ^= vec;
            }

            var keyLow = BitConverter.ToUInt64(key, 0);
            var keyHigh = BitConverter.ToUInt64(key, 8);
            var seed = keyLow ^ keyHigh ^ (ulong)keySeed ^ ScrambleConstants.ConstKeySeed;

            var rand = new MT19937_64(seed);
            var xorpad = new byte[0x1000];
            for (int i = 0; i < xorpad.Length >> 3; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(rand.Int63()), 0, xorpad, i * 8, 8);

            return xorpad;
        }
    }
}
