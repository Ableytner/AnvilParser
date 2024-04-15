using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilParser
{
    internal class OldBlock : BaseBlock
    {
        public OldBlock(int id, int data)
        {
            Id = id;
            Data = data;
        }

        public int Id { get; set; }
        public int Data { get; set; }

        public override string ToString()
        {
            return $"{Id} ({Data})";
        }
    }
}
