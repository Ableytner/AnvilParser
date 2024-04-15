using fNbt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AnvilParser
{
    public class Chunk
    {
        public Chunk(NbtCompound nbtData)
        {
            if (nbtData.Get<NbtInt>("DataVersion") != null)
            {
                Version = nbtData.Get<NbtInt>("DataVersion").Value;
            }
            else
            {
                // Version is pre-1.9 snapshot 15w32a, so world does not have a Data Version.
                // See https://minecraft.wiki/w/Data_version
                Version = Versions.VERSION_PRE_15w32a;
            }

            if (Version >= Versions.VERSION_21w43a)
            {
                Data = nbtData;
                TileEntities = Data.Get<NbtList>("block_entities");
            }
            else
            {
                Data = nbtData.Get<NbtCompound>("Level");
                TileEntities = Data.Get<NbtList>("TileEntities");
            }

            X = Data.Get<NbtInt>("xPos").Value;
            Z = Data.Get<NbtInt>("zPos").Value;
        }

        public static Chunk FromRegion(Region region, int chunkX,  int chunkZ)
        {
            NbtFile nbtFile = region.ChunkData(chunkX, chunkZ) ?? throw new Exception($"Could not find chunk ({chunkX}, {chunkZ})");
            return new Chunk(nbtFile.RootTag);
        }

        public int Version { get; set; }

        public NbtCompound Data { get; set; }
        public NbtList TileEntities { get; set; }

        public int X { get; set; }
        public int Z { get; set; }

        public BaseBlock GetBlock(int X, int Y, int Z)
        {
            if (X < 0 || X > 15)
            {
                throw new Exception($"X ({X}) must be in range of 0 to 15");
            }
            if (Z < 0 || Z > 15)
            {
                throw new Exception($"Z ({Z}) must be in range of 0 to 15");
            }

            int sectionRangeStart, sectionRangeStop;
            if (Version > Versions.VERSION_17w47a)
            {
                sectionRangeStart = -4;
                sectionRangeStop = 20;
            }
            else
            {
                sectionRangeStart = 0;
                sectionRangeStop = 16;
            }
            if (Y / 16 < sectionRangeStart || Y / 16 > sectionRangeStop)
            {
                throw new Exception($"Y ({Y}) must be in range of {sectionRangeStart * 16} to {sectionRangeStop * 16 - 1}");
            }

            NbtCompound section = GetSection(Y / 16);
            Y %= 16;

            // Explained in depth here https://minecraft.wiki/w/index.php?title=Chunk_format&oldid=1153403#Block_format
            if (Version < Versions.VERSION_17w47a)
            {
                if (section == null || !section.Contains("Blocks"))
                {
                    return new OldBlock(0);
                }

                int inDex = Y * 16 * 16 + Z * 16 + X;

                int block_id = section.Get<NbtIntArray>("Blocks")[inDex];
                if (section.Contains("Add"))
                {
                    block_id += Nibble(section.Get<NbtByteArray>("Add").Value, inDex) << 8;
                }

                int block_data = Nibble(section.Get<NbtByteArray>("Data").Value, inDex);

                BaseBlock block = new OldBlock(block_id,  block_data);
                return block;
            }

            // If its an empty section its most likely an air block
            if (section == null)
            {
                return Block.FromName("minecraft:air");
            }
            long[] states;
            try
            {
                states = StatesFromSection(section);
            }
            catch
            {
                return Block.FromName("minecraft:air");
            }

            // Number of bits each block is on BlockStates
            // Cannot be lower than 4
            NbtList palette = PaletteFromSection(section);

            int bits = Math.Max(ByteHelper.BitCount(palette.Count - 1), 4);

            // Get index on the block list with the order YZX
            int index = Y * 16 * 16 + Z * 16 + X;
            // in 20w17a and newer blocks cannot occupy more than one element on the BlockStates array
            bool stretches = Version < Versions.VERSION_20w17a;

            int state;
            if (stretches)
            {
                state = index * bits / 64;
            }
            {
                state = index / (64 / bits);
            }

            long data = states[state];

            long shifted_data;
            if (stretches)
            {
                // shift the number to the right to remove the left over bits
                // and shift so the i'th block is the first one
                shifted_data = data >> ((bits * index) % 64);
            }
            else
            {
                shifted_data = data >> (index % (64 / bits) * bits);
            }

            // if there aren't enough bits it means the rest are in the next number
            if (stretches && (64 - ((bits * index) % 64 ) < bits))
            {
                data = states[state + 1];

                // get how many bits are from a palette index of the next block
                int leftover = (bits - ((state + 1) * 64 % bits)) % bits;

                // Make sure to keep the length of the bits in the first state
                // Example: bits is 5, and leftover is 3
                // Next state                Current state (already shifted)
                // 0b101010110101101010010   0b01
                // will result in bin_append(0b010, 0b01, 2) = 0b01001
                shifted_data = BinAppend(data & (long)(Math.Pow(2, leftover) - 1), shifted_data, bits - leftover);
            }

            // get `bits` least significant bits
            // which are the palette index
            int paletteId = (int)(shifted_data & (int)(Math.Pow(2, bits) - 1));
            return Block.FromPalette((NbtCompound)palette[paletteId]);
        }

        public IEnumerable<BaseBlock> StreamBlocks()
        {
            NbtCompound section = GetSection(0);
            int index = 0;

            if (Version < Versions.VERSION_17w47a)
            {
                if (section == null || !section.Contains("Blocks"))
                {
                    BaseBlock airBlockOld = new OldBlock(0);
                    for (int i = 0; i < 4096; i++)
                    {
                        yield return airBlockOld;
                    }
                    yield break;
                }

                while (index < 4096)
                {
                    int block_id = section.Get<NbtIntArray>("Blocks")[index];
                    if (section.Contains("Add"))
                    {
                        block_id += Nibble(section.Get<NbtByteArray>("Add").Value, index) << 8;
                    }

                    int block_data = Nibble(section.Get<NbtByteArray>("Data").Value, index);

                    BaseBlock block = new OldBlock(block_id, block_data);
                    yield return block;

                    index++;
                }

                yield break;
            }

            BaseBlock airBlock = Block.FromName("minecraft:air");
            if (section == null)
            {
                for (int i = 0; i < 4096; i++)
                {
                    yield return airBlock;
                }
                yield break;
            }
            long[]? states = null;
            try
            {
                states = StatesFromSection(section);
            }
            catch { }
            if (states == null)
            {
                for (int i = 0; i < 4096; i++)
                {
                    yield return airBlock;
                }
                yield break;
            }

            NbtList palette = PaletteFromSection(section);

            int bits = Math.Max(ByteHelper.BitCount(palette.Count - 1), 4);

            bool stretches = Version < Versions.VERSION_20w17a;

            int state;
            if (stretches)
            {
                state = index * bits / 64;
            }
            {
                state = index / (64 / bits);
            }

            long data = states[state];

            uint bitsMask = (uint)(Math.Pow(2, bits) - 1);

            int offset;
            if (stretches)
            {
                offset = (bits * index) % 64;
            }
            else
            {
                offset = index % (64 / bits) * bits;
            }

            int dataLen = 64 - offset;
            data >>= offset;

            long newData;
            while (index < 4096)
            {
                if (dataLen < bits)
                {
                    state += 1;
                    newData = states[state];

                    if (stretches)
                    {
                        int leftover = dataLen;
                        dataLen += 64;

                        data = BinAppend(newData, data, leftover);
                    }
                    else
                    {
                        data = newData;
                        dataLen = 64;
                    }
                }

                int paletteId = (int)(data & bitsMask);
                yield return Block.FromPalette((NbtCompound)palette[paletteId]);

                index++;
                data >>= bits;
                dataLen -= bits;
            }
        }

        private NbtCompound? GetSection(int Y)
        {
            int sectionRangeStart, sectionRangeStop;
            if (Version > Versions.VERSION_17w47a)
            {
                sectionRangeStart = -4;
                sectionRangeStop = 20;
            }
            else
            {
                sectionRangeStart = 0;
                sectionRangeStop = 16;
            }
            if (Y / 16 < sectionRangeStart || Y / 16 > sectionRangeStop)
            {
                throw new Exception($"Y ({Y}) must be in range of {sectionRangeStart * 16} to {sectionRangeStop * 16 - 1}");
            }

            NbtList sections;
            if (Data.Contains("sections"))
            {
                sections = Data.Get<NbtList>("sections");
            }
            else if (Data.Contains("Sections"))
            {
                sections = Data.Get<NbtList>("Sections");
            }
            else
            {
                return null;
            }

            foreach (NbtCompound section in sections)
            {
                if (section.Get<NbtByte>("Y").Value == Y)
                {
                    return section;
                }
            }

            return null;
        }

        private static int Nibble(byte[] data, int index)
        {
            byte value = data[index / 2];
            if (index % 2 != 0)
            {
                return value >> 4;
            }
            else
            {
                return value & 0b1111;
            }
        }

        //Appends number a to the left of b
        //bin_append(0b1, 0b10) = 0b110
        private static int BinAppend(int a, int b)
        {
            int length = ByteHelper.BitCount(b);
            return (a << length) | b;
        }

        private static long BinAppend(long a, long b)
        {
            int length = ByteHelper.BitCount((int)b);
            return (a << length) | b;
        }

        //Appends number a to the left of b
        //bin_append(0b1, 0b10) = 0b110
        private static int BinAppend(int a, int b, int length)
        {
            return (a << length) | b;
        }

        private static long BinAppend(long a, long b, int length)
        {
            return (a << length) | b;
        }

        private static long[] StatesFromSection(NbtCompound section)
        {
            // BlockStates is an array of 64 bit numbers that holds the blocks index on the palette list

            long[] states;
            if (section.Contains("block_states"))
            {
                states = section.Get<NbtCompound>("block_states").Get<NbtLongArray>("data").Value;
            }
            else
            {
                states = section.Get<NbtLongArray>("BlockStates").Value;
            }

            // makes sure the number is unsigned
            // by adding 2^64
            // could also use ctypes.c_ulonglong(n).value but that'd require an extra import
            long[] sortedStates = new long[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] >= 0)
                {
                    sortedStates[i] = states[i];
                }
                else
                {
                    // code from original anvil-parser, idk if it is needed
                    ///sortedStates[i] = (int)(states[i] * Math.Pow(2, 64));
                    sortedStates[i] = states[i];
                }
            }
            return sortedStates;
        }

        private static NbtList PaletteFromSection(NbtCompound section)
        {
            if (section.Contains("block_states"))
            {
                return section.Get<NbtCompound>("block_states").Get<NbtList>("palette");
            }
            else
            {
                return section.Get<NbtList>("Palette");
            }

        }
    }
}
