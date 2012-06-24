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
using Gibbed.Duels.FileFormats;
using Gibbed.IO;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using NDesk.Options;
using Wad = Gibbed.Duels.FileFormats.Wad;

namespace Gibbed.Duels.Unpack
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static string MakePath(Wad.FileEntry entry)
        {
            string name = entry.Name;

            var parent = entry.Directory;
            while (parent != null)
            {
                name = Path.Combine(parent.Name, name);
                parent = parent.ParentDirectory;
            }

            return name;
        }

        public static void Main(string[] args)
        {
            bool overwriteFiles = false;
            bool lowercaseFileNames = false;
            bool verbose = false;
            bool showHelp = false;

            var options = new OptionSet()
            {
                {
                    "o|overwrite",
                    "overwrite files",
                    v => overwriteFiles = v != null
                    },
                {
                    "l|lowercase",
                    "lowercase file names (so they are not all annoyingly uppercase!)",
                    v => lowercaseFileNames = v != null
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
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_wad [output_path]", GetExecutableName());
                Console.WriteLine("Unpack a Duels of the Planewalkers WAD.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var inputPath = Path.GetFullPath(extra[0]);
            var outputPath = extra.Count > 1 ? extra[1] : Path.ChangeExtension(inputPath, null) + "_unpacked";

            using (var input = File.OpenRead(inputPath))
            {
                ushort checkMagic, checkVersion;
                string checkReason;
                if (WadFile.IsBadHeader(input, out checkMagic, out checkVersion, out checkReason) == true)
                {
                    Console.WriteLine("Error: {0} (magic = 0x{1:X4}, version = 0x{2:X4})",
                                      checkReason,
                                      checkMagic,
                                      checkVersion);
                    return;
                }
                input.Seek(0, SeekOrigin.Begin);

                var wad = new WadFile();

                Console.WriteLine("Reading header...");
                wad.Deserialize(input);

                if ((wad.Flags & ~Wad.ArchiveFlags.ValidFlags) != Wad.ArchiveFlags.None)
                {
                    Console.WriteLine("Warning: unknown archive flags present! (things may blow up past this point)");
                    Console.WriteLine("  {0}", wad.Flags);
                }

                Console.WriteLine("Writing files...");

                var hasCompressedFiles = (wad.Flags & Wad.ArchiveFlags.HasCompressedFiles) ==
                                         Wad.ArchiveFlags.HasCompressedFiles;

                if (wad.Version >= 0x202)
                {
                    Directory.CreateDirectory(outputPath);
                    var headerPath = Path.Combine(outputPath, "@header.xml");
                    using (var output = File.Create(headerPath))
                    {
                        output.WriteBytes(wad.HeaderXml);
                    }
                }

                foreach (var entry in wad.AllFiles)
                {
                    var entryName = MakePath(entry);

                    if (lowercaseFileNames == true)
                    {
                        entryName = entryName.ToLowerInvariant();
                    }

                    var entryPath = Path.Combine(outputPath, entryName);

                    if (overwriteFiles == false &&
                        File.Exists(entryPath) == true)
                    {
                        continue;
                    }

                    if (verbose == true)
                    {
                        Console.WriteLine(">> {0}", entryName);
                    }

                    var entryDirectory = Path.GetDirectoryName(entryPath);
                    if (entryDirectory != null)
                    {
                        Directory.CreateDirectory(entryDirectory);
                    }

                    input.Seek(wad.DataOffsets[entry.OffsetIndex], SeekOrigin.Begin);

                    using (var output = File.Create(entryPath))
                    {
                        if (hasCompressedFiles == false)
                        {
                            output.WriteFromStream(input, entry.Size);
                        }
                        else
                        {
                            int length = input.ReadValueS32(wad.Endian);

                            if (length == -1)
                            {
                                // no compression
                                output.WriteFromStream(input, entry.Size - 4);
                            }
                            else
                            {
                                using (var temp = input.ReadToMemoryStream(entry.Size - 4))
                                {
                                    var zlib = new InflaterInputStream(temp);
                                    output.WriteFromStream(zlib, length);
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("Done!");
            }
        }
    }
}
