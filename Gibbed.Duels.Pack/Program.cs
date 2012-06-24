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
using System.Globalization;
using System.IO;
using System.Linq;
using Gibbed.Duels.FileFormats;
using Gibbed.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using NDesk.Options;
using Wad = Gibbed.Duels.FileFormats.Wad;

namespace Gibbed.Duels.Pack
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private class MyFileEntry : Wad.FileEntry
        {
            public MyFileEntry(Wad.DirectoryEntry directory)
                : base(directory)
            {
            }

            public string FilePath;
        }

        private static Wad.DirectoryEntry GetOrCreateDirectory(WadFile wad, string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar);

            Wad.DirectoryEntry root;

            root = wad.Directories.SingleOrDefault(d => d.Name == parts[0]);
            if (root == null)
            {
                root = new Wad.DirectoryEntry(null)
                {
                    Name = parts[0],
                };
                wad.Directories.Add(root);
            }

            Wad.DirectoryEntry current = root;
            foreach (string part in parts.Skip(1))
            {
                var child = current.Directories.SingleOrDefault(
                    d => d.Name == part);

                if (child == null)
                {
                    child = new Wad.DirectoryEntry(current)
                    {
                        Name = part,
                    };
                    current.Directories.Add(child);
                }

                current = child;
            }

            return current;
        }

        public static void Main(string[] args)
        {
            ushort wadVersion = 0x202;
            bool compressFiles = false;
            bool uppercaseFileNames = false;
            bool verbose = false;
            bool showHelp = false;
            string headerFileName = "@header.xml";

            var options = new OptionSet()
            {
                {
                    "c|compress",
                    "overwrite files",
                    v => compressFiles = v != null
                    },
                {
                    "u|uppercase",
                    "uppsercase file names",
                    v => uppercaseFileNames = v != null
                    },
                {
                    "wh|wad-header=",
                    "specify WAD header file name (default is @header.xml)",
                    v => headerFileName = v
                    },
                {
                    "wv|wad-version=",
                    "specify WAD version (default is 0x201)",
                    v =>
                    {
                        if (v != null)
                        {
                            if (v.StartsWith("0x") == false)
                            {
                                wadVersion = ushort.Parse(v);
                            }
                            else
                            {
                                wadVersion = ushort.Parse(v.Substring(2), NumberStyles.AllowHexSpecifier);
                            }
                        }
                    }
                    },
                {
                    "v|verbose",
                    "show verbose messages",
                    v => verbose = v != null
                    },
                {
                    "h|help",
                    "show this message and exit",
                    v => showHelp = v != null
                    },
            };

            List<string> extra;

            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (extra.Count < 1 || extra.Count > 2 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_dir [output_wad]", GetExecutableName());
                Console.WriteLine("Pack a Duels of the Planewalkers WAD.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (wadVersion != 0x201 &&
                wadVersion != 0x202)
            {
                Console.WriteLine("Warning: unexpected WAD version specified.");
            }

            var paths = new SortedDictionary<string, string>();

            var inputPath = Path.GetFullPath(extra[0]);
            var outputPath = extra.Count > 1 ? extra[1] : Path.ChangeExtension(inputPath, ".wad");

            Wad.ArchiveFlags flags = Wad.ArchiveFlags.None;

            flags |= Wad.ArchiveFlags.Unknown6Observed;
            flags |= Wad.ArchiveFlags.HasDataTypes;

            if (compressFiles == true)
            {
                flags |= Wad.ArchiveFlags.HasCompressedFiles;
            }

            var wad = new WadFile()
            {
                Version = wadVersion,
                Flags = flags,
            };

            string headerPath = headerFileName;
            if (Path.IsPathRooted(headerPath) == false)
            {
                headerPath = Path.Combine(inputPath, headerPath);
            }

            if (wad.Version >= 0x202)
            {
                if (File.Exists(headerPath) == false)
                {
                    Console.WriteLine("Could not find read header file '{0}'!", headerPath);
                    return;
                }

                wad.HeaderXml = File.ReadAllBytes(headerPath);
            }

            Console.WriteLine("Collecting files...");

            foreach (var path in Directory.GetFiles(inputPath, "*", SearchOption.AllDirectories))
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath == headerPath)
                {
                    continue;
                }

                var partPath = fullPath.Substring(inputPath.Length + 1);

                if (uppercaseFileNames == true)
                {
                    partPath = partPath.ToUpperInvariant();
                }

                if (paths.ContainsKey(partPath) == true)
                {
                    // warning?
                    continue;
                }

                paths[partPath] = fullPath;
            }

            var files = new List<MyFileEntry>();
            foreach (var kvp in paths)
            {
                string fileName = Path.GetFileName(kvp.Key);
                var directoryName = Path.GetDirectoryName(kvp.Key);
                if (directoryName == null)
                {
                    throw new InvalidOperationException();
                }

                var dir = GetOrCreateDirectory(wad, directoryName);

                var file = new MyFileEntry(dir);

                file.FilePath = kvp.Value;
                file.Name = fileName;
                file.Size = 0;
                file.Unknown0C = 0;

                file.OffsetIndex = wad.DataOffsets.Count;
                file.OffsetCount = 1;
                wad.DataOffsets.Add(0);

                dir.Files.Add(file);
                files.Add(file);
            }

            if (verbose == true)
            {
                Console.WriteLine("Collected {0} files.", files.Count);
            }

            using (var output = File.Create(outputPath))
            {
                Console.WriteLine("Writing stub header...");
                wad.Serialize(output);

                Console.WriteLine("Writing file data...");
                foreach (var entry in files)
                {
                    if (verbose == true)
                    {
                        Console.WriteLine(">> {0}", entry);
                    }

                    wad.DataOffsets[entry.OffsetIndex] = (uint)output.Position;

                    using (var input = File.OpenRead(entry.FilePath))
                    {
                        if (compressFiles == false)
                        {
                            entry.Size = (uint)input.Length;
                            output.WriteFromStream(input, input.Length);
                        }
                        else
                        {
                            using (var temp = new MemoryStream())
                            {
                                var zlib = new DeflaterOutputStream(temp, new Deflater(Deflater.BEST_COMPRESSION));
                                zlib.WriteFromStream(input, input.Length);
                                zlib.Finish();
                                temp.Flush();
                                temp.Position = 0;

                                if (temp.Length < input.Length)
                                {
                                    entry.Size = (uint)(4 + temp.Length);
                                    output.WriteValueU32((uint)input.Length);
                                    output.WriteFromStream(temp, temp.Length);
                                }
                                else
                                {
                                    input.Seek(0, SeekOrigin.Begin);
                                    entry.Size = (uint)(4 + input.Length);
                                    output.WriteValueU32(0xFFFFFFFFu);
                                    output.WriteFromStream(input, input.Length);
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("Writing header...");
                output.Seek(0, SeekOrigin.Begin);
                wad.Serialize(output);

                Console.WriteLine("Done!");
            }
        }
    }
}
