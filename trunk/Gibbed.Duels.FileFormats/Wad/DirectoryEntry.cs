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
using Gibbed.IO;

namespace Gibbed.Duels.FileFormats.Wad
{
    public class DirectoryEntry
    {
        public string Name;

        public DirectoryEntry ParentDirectory;
        public readonly List<DirectoryEntry> Directories = new List<DirectoryEntry>();
        public readonly List<FileEntry> Files = new List<FileEntry>();

        public IEnumerable<FileEntry> AllFiles
        {
            get { return this.Files.Concat(this.Directories.SelectMany(d => d.AllFiles)); }
        }

        public int TotalDirectoryCount
        {
            get { return this.Directories.Sum(d => d.TotalDirectoryCount) + this.Directories.Count; }
        }

        public int TotalFileCount
        {
            get { return this.Directories.Sum(d => d.TotalFileCount) + this.Files.Count; }
        }

        public DirectoryEntry(DirectoryEntry parentDirectory)
        {
            this.ParentDirectory = parentDirectory;
        }

        public void Serialize(Stream output, Endian endian, IStringTable stringTable)
        {
            output.WriteValueU32(stringTable.Put(this.Name), endian);
            output.WriteValueS32(this.Files.Count, endian);
            output.WriteValueS32(this.Directories.Count, endian);
            output.WriteValueS32(0, endian);

            foreach (var dir in this.Directories)
            {
                dir.Serialize(output, endian, stringTable);
            }

            foreach (var file in this.Files)
            {
                file.Serialize(output, endian, stringTable);
            }
        }

        public void Deserialize(Stream input, Endian endian, IStringTable stringTable)
        {
            var nameIndex = input.ReadValueU32(endian);
            this.Name = stringTable.Get(nameIndex);

            uint fileCount = input.ReadValueU32(endian);
            uint directoryCount = input.ReadValueU32(endian);
            uint unknown = input.ReadValueU32(endian);

            if (unknown != 0)
            {
                throw new InvalidOperationException();
            }

            this.Directories.Clear();
            for (uint i = 0; i < directoryCount; i++)
            {
                var dir = new DirectoryEntry(this);
                dir.Deserialize(input, endian, stringTable);
                this.Directories.Add(dir);
            }

            this.Files.Clear();
            for (uint i = 0; i < fileCount; i++)
            {
                var file = new FileEntry(this);
                file.Deserialize(input, endian, stringTable);
                this.Files.Add(file);
            }
        }

        public override string ToString()
        {
            if (this.ParentDirectory != null)
            {
                return Path.Combine(this.ParentDirectory.ToString(), this.Name);
            }

            return this.Name;
        }
    }
}
