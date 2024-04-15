using fNbt;

namespace AnvilParser
{
    public class Region
    {
        public Region(byte[] data)
        {
            this.data = data;
        }

        public static Region FromFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            return new Region(data);
        }

        private byte[] data;

        public Chunk GetChunk(int chunkX, int chunkZ)
        {
            return Chunk.FromRegion(this, chunkX, chunkZ);
        }

        public NbtFile? ChunkData(int chunkX, int chunkZ)
        {
            var off = ChunkLocation(chunkX, chunkZ);
            // (0, 0) means it hasn't generated yet, aka it doesn't exist yet
            if (off == new Tuple<uint, uint>(0, 0))
            {
                return null;
            }
            int offset = (int)(off.Item1 * 4096);
            uint length = ByteHelper.ToUInt32(data, offset);
            uint compression = ByteHelper.ToUInt8(data, offset + 4); // 2 most of the time
            if (compression == 1)
            {
                throw new Exception("GZip is not supported");
            }

            byte[] compressedData = data.Skip(offset + 5).Take((int)length - 1).ToArray();

            NbtFile file = new NbtFile();
            file.LoadFromBuffer(compressedData, 0, compressedData.Length, NbtCompression.AutoDetect);
            return file;
        }

        private Tuple<uint, uint> ChunkLocation(int chunkX, int chunkZ)
        {
            uint bOff = HeaderOffset(chunkX, chunkZ);
            uint off = ByteHelper.ToUInt24(data, (int)bOff);
            uint sectors = ByteHelper.ToUInt8(data, (int)(bOff + 3));
            return new Tuple<uint, uint>(off, sectors);
        }

        // Returns the byte offset for the given chunk in the header
        private uint HeaderOffset(int chunkX, int chunkZ)
        {
            return (uint)(4 * (chunkX % 32 + chunkZ % 32 * 32));
        }

        public void TestPrint()
        {
            Console.WriteLine("hallo");
        }
    }
}
