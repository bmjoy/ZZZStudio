using K4os.Compression.LZ4;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public partial class Mhy0File
    {
        public class Header
        {
            public int headerSize;
            public byte[] headerData;
            public int bundleCount;
            public int blockCount;
        }

        public class StorageBlock
        {
            public int compressedSize;
            public int uncompressedSize;
        }

        public class Node
        {
            public long offset;
            public long size;
            public string path;
        }

        public Header m_Header;
        public long OriginalPos;
        private StorageBlock[] m_BlocksInfo;
        private Node[] m_DirectoryInfo;

        public StreamFile[] fileList;
        private static void Scramble2(byte[] input, int offset)
        {
            byte[] key = new byte[0x10];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 0x10; j++)
                    key[j] = input[offset + ScrambleConstants.Mhy0IndexScramble[0x20 + -0x10 * i + j]];

                Buffer.BlockCopy(key, 0, input, offset, 0x10);
                for (int j = 0; j < 0x10; j++)
                {
                    byte b = input[offset + j];
                    int idx = j % 8;
                    if (b == 0 || ScrambleConstants.Mhy0ConstKey1[idx] == 0)
                        b = (byte)(ScrambleConstants.KeyScrambleTable[j % 4 * 0x100] ^ ScrambleConstants.Mhy0ConstKey[idx]);
                    else
                        b = (byte)(ScrambleConstants.Mhy0ConstKey[idx] ^ ScrambleConstants.KeyScrambleTable[j % 4 * 0x100 | ScrambleConstants.Mhy0Table1[(ScrambleConstants.Mhy0Table2[ScrambleConstants.Mhy0ConstKey1[idx]] + ScrambleConstants.Mhy0Table2[b]) % 0xFF]]);
                    input[offset + j] = b;
                }
            }
        }

        private static void Scramble(byte[] input, int offset, ulong blockSize, ulong entrySize)
        {
            var size = (int)((entrySize + 0xF) & 0xFFFFFFF0);
            for (int i = 0; i < size; i += 0x10)
                Scramble2(input, offset + i + 4);
            for (int i = 0; i < 4; i++)
                input[offset + i] ^= input[offset + i + 4];

            ulong curEntry = (ulong)size + 4;
            var finished = false;
            while (curEntry < blockSize && !finished)
            {
                for (ulong i = 0; i < entrySize; i++)
                {
                    input[(ulong)offset + i + curEntry] ^= input[(ulong)offset + i + 4];
                    if (i + curEntry >= blockSize - 1)
                    {
                        finished = true;
                        break;
                    }
                }
                curEntry += entrySize;
            }
        }
        private static int ReadScrambledInt1(byte[] a, int offset)
        {
            return a[offset + 1] | (a[offset + 6] << 8) | (a[offset + 3] << 0x10) | (a[offset + 2] << 0x18);
        }

        private static int ReadScrambledInt2(byte[] a, int offset)
        {
            return a[offset + 2] | (a[offset + 4] << 8) | (a[offset + 0] << 0x10) | (a[offset + 5] << 0x18);
        }

        private static int CalcOffset(int value) => value * 0x113 + 6;

        private byte[] DecompressHeader(byte[] data)
        {
            var decompressedSize = ReadScrambledInt1(data, 0x20);
            var decompressed = new byte[decompressedSize];

            var numWrite = LZ4Codec.Decode(data, 0x27, data.Length - 0x27, decompressed, 0, decompressedSize);
            if (numWrite != decompressedSize)
                throw new IOException($"{string.Format("0x{0:x8}", OriginalPos)} doesn't point to a valid mhy0, Lz4 decompression error, write {numWrite} bytes but expected {decompressedSize} bytes");

            return decompressed;
        }

        private void ReadHeader(EndianBinaryReader reader)
        {
            OriginalPos = reader.Position;
            var magic = reader.ReadUInt32();
            if (magic != 0x3079686D)
                throw new Exception($"not a mhy0 at {string.Format("0x{ 0:x8 }", OriginalPos)}");

            m_Header = new Header();
            m_Header.headerSize = reader.ReadInt32();
            m_Header.headerData = reader.ReadBytes(m_Header.headerSize);

            Scramble(m_Header.headerData, 0, 0x39, 0x1C);
            m_Header.headerData = DecompressHeader(m_Header.headerData);
        }
        private void ReadBlocksInfoAndDirectory(EndianBinaryReader reader)
        {
            m_Header.bundleCount = ReadScrambledInt2(m_Header.headerData, 0);
            m_Header.blockCount = ReadScrambledInt2(m_Header.headerData, CalcOffset(m_Header.bundleCount));

            m_BlocksInfo = new StorageBlock[m_Header.blockCount];
            for (int i = 0; i < m_Header.blockCount; i++)
            {
                var offset = i * 13 + CalcOffset(m_Header.bundleCount);
                m_BlocksInfo[i] = new StorageBlock
                {
                    uncompressedSize = ReadScrambledInt1(m_Header.headerData, offset + 0xC),
                    compressedSize = ReadScrambledInt2(m_Header.headerData, offset + 6)
                };
            }

            m_DirectoryInfo = new Node[m_Header.bundleCount];
            for (int i = 0; i < m_Header.bundleCount; i++)
            {
                var offset = CalcOffset(i);
                m_DirectoryInfo[i] = new Node
                {
                    offset = ReadScrambledInt2(m_Header.headerData, offset + 0x100 + 6),
                    size = ReadScrambledInt1(m_Header.headerData, offset + 0x100 + 0xC),
                    path = Encoding.UTF8.GetString(m_Header.headerData.Skip(offset).TakeWhile(b => !b.Equals(0)).ToArray()),
                };
            }
        }

        private Stream CreateBlocksStream(string path)
        {
            Stream blocksStream;
            var uncompressedSizeSum = m_BlocksInfo.Sum(x => x.uncompressedSize);
            if (uncompressedSizeSum >= int.MaxValue)
                blocksStream = new FileStream(path + ".temp", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            else
                blocksStream = new MemoryStream(uncompressedSizeSum);
            return blocksStream;
        }

        private void ReadBlocks(EndianBinaryReader reader, Stream blocksStream)
        {
            reader.Position = OriginalPos + m_Header.headerSize + 8;
            foreach (var blockInfo in m_BlocksInfo)
            {
                var compressedSize = blockInfo.compressedSize;
                var compressedBytes = BigArrayPool<byte>.Shared.Rent(compressedSize);
                reader.Read(compressedBytes, 0, compressedSize);
                if (compressedSize < 0x10)
                    throw new Exception($"Wrong compressed length: {compressedSize}");
                Scramble(compressedBytes, 0, (ulong)Math.Min(compressedBytes.Length, 0x21), 8);
                var uncompressedSize = blockInfo.uncompressedSize;
                var uncompressedBytes = BigArrayPool<byte>.Shared.Rent(uncompressedSize);
                var numWrite = LZ4Codec.Decode(compressedBytes, 0xC, compressedSize - 0xC, uncompressedBytes, 0, uncompressedSize);
                if (numWrite != uncompressedSize)
                    throw new IOException($"{string.Format("0x{0:x8}", OriginalPos)} doesn't point to a valid mhy0, Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                blocksStream.Write(uncompressedBytes, 0, uncompressedSize);
                BigArrayPool<byte>.Shared.Return(compressedBytes);
                BigArrayPool<byte>.Shared.Return(uncompressedBytes);
            }
        }

        private void ReadFiles(Stream blocksStream, string path)
        {
            fileList = new StreamFile[m_DirectoryInfo.Length];
            for (int i = 0; i < m_DirectoryInfo.Length; i++)
            {
                var node = m_DirectoryInfo[i];
                var file = new StreamFile();
                fileList[i] = file;
                file.path = node.path;
                file.fileName = Path.GetFileName(node.path);
                if (node.size >= int.MaxValue)
                {
                    var extractPath = path + "_unpacked" + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(extractPath);
                    file.stream = new FileStream(extractPath + file.fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                else
                    file.stream = new MemoryStream((int)node.size);
                blocksStream.Position = node.offset;
                blocksStream.CopyTo(file.stream, node.size);
                file.stream.Position = 0;
            }
        }

        public Mhy0File(EndianBinaryReader reader, string path)
        {
            ReadHeader(reader);
            ReadBlocksInfoAndDirectory(reader);
            using (var blocksStream = CreateBlocksStream(path))
            {
                ReadBlocks(reader, blocksStream);
                ReadFiles(blocksStream, path);
            }
        }
    }
}