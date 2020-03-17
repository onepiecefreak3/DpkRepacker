using System;
using System.Collections.Generic;
using System.Text;
using Komponent.IO.Attributes;

namespace DpkRepacker.Models
{
    class FileEntry
    {
        [FixedLength(0x16)]
        public string fileName;

        public short nameSum;
        public int fileOffset;
        public int compresedSize;
        public int uncompressedSize;
    }
}
