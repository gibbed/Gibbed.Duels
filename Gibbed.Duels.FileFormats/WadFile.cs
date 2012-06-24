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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.IO;

namespace Gibbed.Duels.FileFormats
{
    public class WadFile
    {
        public readonly Endian Endian = Endian.Little;
        public ushort Version;

        public byte[] HeaderXml;
        public Wad.ArchiveFlags Flags;

        public readonly List<Wad.DataType> DataTypes = new List<Wad.DataType>();

        public readonly List<uint> DataOffsets = new List<uint>();
        public readonly List<Wad.DirectoryEntry> Directories = new List<Wad.DirectoryEntry>();

        public IEnumerable<Wad.FileEntry> AllFiles
        {
            get { return this.Directories.SelectMany(d => d.AllFiles); }
        }

        public int TotalDirectoryCount
        {
            get { return this.Directories.Count + this.Directories.Sum(d => d.TotalDirectoryCount); }
        }

        public int TotalFileCount
        {
            get { return this.Directories.Sum(d => d.TotalFileCount); }
        }

        private static bool IsValidVersion(ushort version)
        {
            return
                (version >= 0x100 && version <= 0x101) ||
                (version >= 0x200 && version <= 0x202);
        }

        public static bool IsBadHeader(Stream input, out ushort magic, out ushort version, out string reason)
        {
            reason = null;
            magic = input.ReadValueU16(Endian.Little);
            version = input.ReadValueU16(Endian.Little);

            if (magic != 0x1234)
            {
                reason = "invalid WAD magic";
                return true;
            }

            if (IsValidVersion(version) == false)
            {
                reason = "invalid or unsupported version";
                return true;
            }

            return false;
        }

        public void Serialize(Stream output)
        {
            output.WriteValueU16(0x1234, Endian.Little);
            this.Serialize(output, Endian.Little);
        }

        private void Serialize(Stream output, Endian endian)
        {
            output.WriteValueU16(this.Version, endian);

            if (this.Version == 0x101 || this.Version >= 0x200)
            {
                output.WriteValueEnum<Wad.ArchiveFlags>(this.Flags, endian);
            }

            if (this.Version >= 0x202)
            {
                if (this.HeaderXml == null)
                {
                    output.WriteValueU32(0, endian);
                }
                else
                {
                    output.WriteValueS32(this.HeaderXml.Length, endian);
                    output.WriteBytes(this.HeaderXml);
                }
            }

            using (var fileTableData = new MemoryStream())
            {
                using (var stringTableData = new MemoryStream())
                {
                    var stringTable = new StringTableWriter(stringTableData);

                    foreach (var dir in this.Directories)
                    {
                        dir.Serialize(fileTableData, endian, stringTable);
                    }
                    fileTableData.Position = 0;

                    stringTableData.SetLength(stringTableData.Length.Align(16));
                    stringTableData.Position = 0;

                    output.WriteValueU32((uint)stringTableData.Length, endian);

                    if (this.Version >= 0x200)
                    {
                        output.WriteFromStream(stringTableData, stringTableData.Length);
                    }

                    if ((this.Flags & Wad.ArchiveFlags.HasDataTypes) == Wad.ArchiveFlags.HasDataTypes)
                    {
                        output.WriteValueS32(this.DataTypes.Count, endian);
                        foreach (var dataType in this.DataTypes)
                        {
                            dataType.Serialize(output, endian);
                        }
                    }

                    output.WriteValueS32(this.TotalFileCount, endian);
                    output.WriteValueS32(this.TotalDirectoryCount, endian);

                    if (this.Version >= 0x200)
                    {
                        output.WriteValueS32(this.DataOffsets.Count, endian);
                        for (int i = 0; i < this.DataOffsets.Count; i++)
                        {
                            output.WriteValueU32(this.DataOffsets[i], endian);
                        }
                    }

                    if (this.Version == 0x100)
                    {
                        output.WriteFromStream(stringTableData, stringTableData.Length);
                    }

                    output.WriteFromStream(fileTableData, fileTableData.Length);
                }
            }
        }

        public void Deserialize(Stream input)
        {
            if (input.ReadValueU16(Endian.Little) != 0x1234)
            {
                throw new FormatException("not a wad file");
            }

            this.Deserialize(input, Endian.Little);
        }

        private void Deserialize(Stream input, Endian endian)
        {
            var version = input.ReadValueU16(endian);
            if (IsValidVersion(version) == false)
            {
                throw new FormatException("invalid or unsupported wad version");
            }
            this.Version = version;

            if (version == 0x101 || version >= 0x200)
            {
                this.Flags = input.ReadValueEnum<Wad.ArchiveFlags>(endian);
            }
            else
            {
                this.Flags = Wad.ArchiveFlags.None;
            }

            if (version >= 0x202)
            {
                var headerXmlLength = input.ReadValueU32(endian);
                this.HeaderXml = input.ReadBytes(headerXmlLength);
            }

            var stringTableSize = input.ReadValueU32(endian);
            using (var stringTableData = new MemoryStream())
            {
                if (version >= 0x200)
                {
                    stringTableData.WriteFromStream(input, stringTableSize);
                    stringTableData.Position = 0;
                }

                this.DataTypes.Clear();
                if ((this.Flags & Wad.ArchiveFlags.HasDataTypes) == Wad.ArchiveFlags.HasDataTypes)
                {
                    uint count = input.ReadValueU32(endian);
                    for (uint i = 0; i < count; i++)
                    {
                        var item = new Wad.DataType();
                        item.Index = input.ReadValueU32(endian);
                        item.Unknown2 = input.ReadValueU32(endian);
                        this.DataTypes.Add(item);
                    }
                }

                var totalFileCount = input.ReadValueU32(endian);
                var totalDirectoryCount = input.ReadValueU32(endian);

                this.DataOffsets.Clear();
                if (version >= 0x200)
                {
                    uint count = input.ReadValueU32(endian);
                    for (uint i = 0; i < count; i++)
                    {
                        this.DataOffsets.Add(input.ReadValueU32(endian));
                    }
                }
                else
                {
                    // don't know how to handle this situation
                    throw new InvalidOperationException();
                }

                if (version == 0x100)
                {
                    stringTableData.WriteFromStream(input, stringTableSize);
                    stringTableData.Position = 0;
                }

                var stringTableReader = new StringTableReader(stringTableData);

                using (var fileTableData = input.ReadToMemoryStream((totalDirectoryCount + totalFileCount) * 16))
                {
                    while (fileTableData.Position < fileTableData.Length)
                    {
                        var dir = new Wad.DirectoryEntry(null);
                        dir.Deserialize(fileTableData, endian, stringTableReader);
                        this.Directories.Add(dir);
                    }

                    if (this.TotalFileCount != totalFileCount ||
                        this.TotalDirectoryCount != totalDirectoryCount)
                    {
                        throw new InvalidOperationException();
                    }

                    if (fileTableData.Position != fileTableData.Length)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        private static readonly Encoding _WindowsEncoding = Encoding.GetEncoding(1252);

        private class StringTableReader : Wad.IStringTable
        {
            private readonly Stream _Input;

            public StringTableReader(MemoryStream input)
            {
                this._Input = input;
            }

            public string Get(uint index)
            {
                this._Input.Seek(index, SeekOrigin.Begin);
                return this._Input.ReadStringZ(_WindowsEncoding);
            }

            public uint Put(string value)
            {
                throw new NotSupportedException();
            }
        }

        private class StringTableWriter : Wad.IStringTable
        {
            public StringTableWriter(Stream output)
            {
                this._Output = output;
            }

            public static readonly Encoding WindowsEncoding = Encoding.GetEncoding(1252);

            private readonly Stream _Output;
            private readonly Dictionary<string, uint> _Offsets = new Dictionary<string, uint>();

            public string Get(uint index)
            {
                throw new NotSupportedException();
            }

            public uint Put(string value)
            {
                if (this._Offsets.ContainsKey(value) == true)
                {
                    return this._Offsets[value];
                }

                uint offset = (uint)this._Output.Position;
                this._Output.WriteStringZ(value, WindowsEncoding);
                this._Offsets.Add(value, offset);
                return offset;
            }
        }
    }
}
