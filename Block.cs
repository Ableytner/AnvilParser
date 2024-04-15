using fNbt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilParser
{
    internal class Block : BaseBlock
    {
        public Block(string blockId)
        {
            Namespace = "minecraft";
            Id = blockId;
            Properties = new Dictionary<string, string>();
        }

        public Block(string nameSpace, string blockId)
        {
            Namespace = nameSpace;
            Id = blockId;
            Properties = new Dictionary<string, string>();
        }

        public Block(string nameSpace, string blockId, Dictionary<string, string> properties)
        {
            Namespace = nameSpace;
            Id = blockId;
            Properties = properties;
        }

        public static Block FromName(string name)
        {
            string[] nameSplit = name.Split(':');
            return new Block(nameSplit[0], nameSplit[1]);
        }

        public static Block FromName(string name, Dictionary<string, string> properties)
        {
            string[] nameSplit = name.Split(':');
            return new Block(nameSplit[0], nameSplit[1], properties);
        }

        public static Block FromPalette(NbtCompound tag)
        {
            string name = tag.Get<NbtString>("Name").Value;

            Dictionary<string, string> properties = new Dictionary<string, string>();

            NbtCompound propertiesCompound = tag.Get<NbtCompound>("Properties");
            if (propertiesCompound != null)
            {
                string[] names = propertiesCompound.Names.ToArray();
                foreach (var n in names)
                {
                    properties[n] = propertiesCompound.Get<NbtString>(n).Value;
                }
            }

            return FromName(name, properties);
        }

        public string Namespace { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public override string ToString()
        {
            string properties = "{" + string.Join(", ", Properties.Select(kvp => $"{kvp.Key}: {kvp.Value}")) + "}";
            return $"{Namespace}:{Id} ({properties})";
        }
    }
}
