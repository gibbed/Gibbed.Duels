/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.IO;
using Gibbed.IO;

namespace Gibbed.Duels.FileFormats.Wad
{
    public class FileEntry
    {
        public readonly DirectoryEntry Directory;
        public string Name;
        public uint Size;
        public uint Flags;
        public uint Unknown0C;

        public FileEntry(DirectoryEntry directory)
        {
            if (directory == null)
            {
                throw new ArgumentNullException("directory");
            }

            this.Directory = directory;
        }

        public int OffsetIndex
        {
            get { return (int)(this.Flags & 0x00FFFFFFu); }
            set
            {
                this.Flags &= ~0x00FFFFFFu;
                this.Flags |= ((uint)value & 0x00FFFFFFu);
            }
        }

        public byte OffsetCount
        {
            get { return (byte)((this.Flags & 0xFF000000u) >> 24); }
            set
            {
                this.Flags &= ~0xFF000000u;
                this.Flags |= ((uint)value << 24) & 0xFF000000u;
            }
        }

        public void Serialize(Stream output, Endian endian, IStringTable stringTable)
        {
            output.WriteValueU32(stringTable.Put(this.Name));
            output.WriteValueU32(this.Size, endian);
            output.WriteValueU32(this.Flags, endian);
            output.WriteValueU32(this.Unknown0C, endian);
        }

        public void Deserialize(Stream input, Endian endian, IStringTable stringTable)
        {
            var nameIndex = input.ReadValueU32(endian);
            this.Name = stringTable.Get(nameIndex);

            this.Size = input.ReadValueU32(endian);
            this.Flags = input.ReadValueU32(endian);
            this.Unknown0C = input.ReadValueU32(endian);

            if (this.OffsetCount != 1)
            {
                throw new FormatException();
            }

            if (this.Unknown0C != 0)
            {
                throw new FormatException();
            }
        }

        public override string ToString()
        {
            if (this.Directory != null)
            {
                return Path.Combine(this.Directory.ToString(), this.Name);
            }

            return this.Name;
        }
    }
}
