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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Gibbed.Duels.FileFormats;
using Gibbed.Squish;
using NDesk.Options;
using Tdx = Gibbed.Duels.FileFormats.Tdx;

namespace Gibbed.Duels.TdxConvert
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public static void Main(string[] args)
        {
            bool showHelp = false;
            bool saveExtra = false;

            var options = new OptionSet()
            {
                {
                    "e|extra",
                    "save extra data if present when converting from TDX",
                    v => saveExtra = v != null
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

            if (extra.Count < 1 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_texture*", GetExecutableName());
                Console.WriteLine("Convert a texture to / from Duels of the Planewalkers TDX format.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            foreach (string inputPath in extra)
            {
                Console.WriteLine(inputPath);

                var extension = Path.GetExtension(inputPath);
                if (extension != null)
                {
                    extension = extension.ToLowerInvariant();
                }

                // convert from tdx
                if (extension == ".tdx")
                {
                    Stream input = File.OpenRead(inputPath);
                    var tdx = new TdxFile();
                    tdx.Deserialize(input);
                    input.Close();

                    if (saveExtra == true && tdx.ExtraData != null)
                    {
                        using (var output = File.Create(Path.ChangeExtension(inputPath, "_extra.bin")))
                        {
                            output.Write(tdx.ExtraData, 0, tdx.ExtraData.Length);
                        }
                    }

                    if (tdx.Format == Tdx.D3DFormat.DXT1 ||
                        tdx.Format == Tdx.D3DFormat.DXT3 ||
                        tdx.Format == Tdx.D3DFormat.DXT5)
                    {
                        Native.Flags flags = 0;

                        if (tdx.Format == Tdx.D3DFormat.DXT1)
                        {
                            flags |= Native.Flags.DXT1;
                        }
                        else if (tdx.Format == Tdx.D3DFormat.DXT3)
                        {
                            flags |= Native.Flags.DXT3;
                        }
                        else if (tdx.Format == Tdx.D3DFormat.DXT5)
                        {
                            flags |= Native.Flags.DXT5;
                        }

                        var decompressed = Native.DecompressImage(
                            tdx.Mipmaps[0].Data,
                            tdx.Mipmaps[0].Width,
                            tdx.Mipmaps[0].Height,
                            flags);

                        var bitmap = MakeBitmapFromDXT(
                            tdx.Mipmaps[0].Width,
                            tdx.Mipmaps[0].Height,
                            decompressed,
                            true);

                        bitmap.Save(
                            Path.ChangeExtension(inputPath, ".png"),
                            ImageFormat.Png);
                    }
                    else if (tdx.Format == Tdx.D3DFormat.A8R8G8B8)
                    {
                        var bitmap = MakeBitmapFromA8R8G8B8(
                            tdx.Mipmaps[0].Width,
                            tdx.Mipmaps[0].Height,
                            tdx.Mipmaps[0].Data);

                        bitmap.Save(
                            Path.ChangeExtension(inputPath, ".png"),
                            ImageFormat.Png);
                    }
                    else if (tdx.Format == Tdx.D3DFormat.A4R4G4B4)
                    {
                        var bitmap = MakeBitmapFromA4R4G4B4(
                            tdx.Mipmaps[0].Width,
                            tdx.Mipmaps[0].Height,
                            tdx.Mipmaps[0].Data);

                        bitmap.Save(
                            Path.ChangeExtension(inputPath, ".png"),
                            ImageFormat.Png);
                    }
                    else if (tdx.Format == Tdx.D3DFormat.X8R8G8B8)
                    {
                        var bitmap = MakeBitmapFromX8R8G8B8(
                            tdx.Mipmaps[0].Width,
                            tdx.Mipmaps[0].Height,
                            tdx.Mipmaps[0].Data);

                        bitmap.Save(
                            Path.ChangeExtension(inputPath, ".png"),
                            ImageFormat.Png);
                    }
                    else
                    {
                        throw new NotSupportedException("unsupported format " + tdx.Format.ToString());
                    }
                }
                    // convert to tdx
                else
                {
                    // just a crappy convert to A8R8G8B8 for now...

                    using (var bitmap = new Bitmap(inputPath))
                    {
                        var tdx = new TdxFile();
                        tdx.Width = (ushort)bitmap.Width;
                        tdx.Height = (ushort)bitmap.Height;
                        tdx.Flags = 0;
                        tdx.Format = Tdx.D3DFormat.A8R8G8B8;

                        var mip = new Tdx.Mipmap();
                        mip.Width = tdx.Width;
                        mip.Height = tdx.Height;

                        int stride = mip.Width * 4;

                        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                        var buffer = new byte[mip.Height * stride];

                        var bitmapData = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        IntPtr scan = bitmapData.Scan0;
                        for (int y = 0, o = 0; y < mip.Height; y++, o += stride)
                        {
                            Marshal.Copy(scan, buffer, o, stride);
                            scan += bitmapData.Stride;
                        }
                        bitmap.UnlockBits(bitmapData);

                        mip.Data = buffer;

                        tdx.Mipmaps.Add(mip);

                        using (var output = File.Create(Path.ChangeExtension(inputPath, ".TDX")))
                        {
                            tdx.Serialize(output);
                        }
                    }
                }
            }
        }

        private static Bitmap MakeBitmapFromDXT(uint width, uint height, byte[] buffer, bool keepAlpha)
        {
            Bitmap bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);

            for (uint i = 0; i < width * height * 4; i += 4)
            {
                // flip red and blue
                byte r = buffer[i + 0];
                buffer[i + 0] = buffer[i + 2];
                buffer[i + 2] = r;
            }

            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(buffer, 0, data.Scan0, (int)(width * height * 4));
            bitmap.UnlockBits(data);
            return bitmap;
        }

        private static Bitmap MakeBitmapFromA8R8G8B8(uint width, uint height, byte[] buffer)
        {
            Bitmap bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(buffer, 0, data.Scan0, (int)(width * height * 4));
            bitmap.UnlockBits(data);
            return bitmap;
        }

        private static Bitmap MakeBitmapFromA4R4G4B4(uint width, uint height, byte[] buffer)
        {
            Bitmap bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);

            byte[] newbuffer = new byte[width * height * 4];

            for (uint i = 0, j = 0; i < width * height * 2; i += 2, j += 4)
            {
                newbuffer[j + 0] = (byte)(((buffer[i + 0] >> 0) & 0x0F) * 0x11); // A
                newbuffer[j + 1] = (byte)(((buffer[i + 0] >> 4) & 0x0F) * 0x11); // R
                newbuffer[j + 2] = (byte)(((buffer[i + 1] >> 0) & 0x0F) * 0x11); // G
                newbuffer[j + 3] = (byte)(((buffer[i + 1] >> 4) & 0x0F) * 0x11); // B
            }

            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(newbuffer, 0, data.Scan0, (int)(width * height * 4));
            bitmap.UnlockBits(data);
            return bitmap;
        }

        private static Bitmap MakeBitmapFromX8R8G8B8(uint width, uint height, byte[] buffer)
        {
            Bitmap bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppRgb);
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(buffer, 0, data.Scan0, (int)(width * height * 4));
            bitmap.UnlockBits(data);
            return bitmap;
        }
    }
}
