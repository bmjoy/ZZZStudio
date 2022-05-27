using System;
using System.IO;
using System.Collections.Generic;

namespace AssetStudio
{
    public partial class BlkFile
    {
        private Dictionary<long, Mhy0File> _files = null;
        public Dictionary<long, Mhy0File> Files => _files;
        public BlkFile(FileReader reader)
        {
            reader.Endian = EndianType.LittleEndian;

            var magic = reader.ReadStringToNull();
            if (magic != "blk")
                throw new Exception("not a blk");

            var count = reader.ReadInt32();
            var key = reader.ReadBytes(count);
            reader.ReadBytes(count);

            var blockSize = reader.ReadUInt16();
            var data = reader.ReadBytes((int)(reader.Length - reader.Position));

            data = Crypto.Decrypt(key, data, blockSize);

            _files = new Dictionary<long, Mhy0File>();
            using (var ms = new MemoryStream(data))
            using (var subReader = new EndianBinaryReader(ms, reader.Endian))
            {
                long pos = -1;
                try
                {
                    if (reader.MHY0Pos.Length != 0)
                    {
                        for (int i = 0; i < reader.MHY0Pos.Length; i++)
                        {
                            pos = reader.MHY0Pos[i];
                            subReader.Position = pos;
                            var mhy0 = new Mhy0File(subReader, reader.FullPath);
                            Files.Add(pos, mhy0);
                        }
                    }
                    else
                    {
                        while (subReader.Position != subReader.BaseStream.Length)
                        {
                            pos = subReader.Position;
                            var mhy0 = new Mhy0File(subReader, reader.FullPath);
                            Files.Add(pos, mhy0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load a mhy0 at {string.Format("0x{0:x8}", pos)} in {Path.GetFileName(reader.FullPath)}");
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
