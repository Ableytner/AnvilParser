using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilParser
{
    internal class OldBlock : BaseBlock
    {
        public OldBlock(int id)
        {
            Id = id.ToString();
            Data = 0;
        }

        public OldBlock(int id, int data)
        {
            Id = id.ToString();
            Data = data;
        }

        public int Data { get; set; }

        public override string ToString()
        {
            return $"{Id} ({Data})";
        }
    }
}
