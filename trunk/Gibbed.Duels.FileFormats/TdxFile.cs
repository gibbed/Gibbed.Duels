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
using Gibbed.IO;

namespace Gibbed.Duels.FileFormats
{
    public class TdxFile
    {
        public ushort Width;
        public ushort Height;
        public Tdx.HeaderFlags Flags;
        public Tdx.D3DFormat Format;
        public byte[] ExtraData;
        public readonly List<Tdx.Mipmap> Mipmaps = new List<Tdx.Mipmap>();

        private static int GetPixelByteSize(Tdx.D3DFormat format)
        {
            switch (format)
            {
                case Tdx.D3DFormat.A8:
                case Tdx.D3DFormat.P8:
                case Tdx.D3DFormat.L8:
                case Tdx.D3DFormat.A4L4:
                case Tdx.D3DFormat.S8_LOCKABLE:
                case Tdx.D3DFormat.VERTEXDATA:
                {
                    return 1;
                }

                case Tdx.D3DFormat.R5G6B5:
                case Tdx.D3DFormat.X1R5G5B5:
                case Tdx.D3DFormat.A1R5G5B5:
                case Tdx.D3DFormat.A4R4G4B4:
                case Tdx.D3DFormat.R3G3B2:
                case Tdx.D3DFormat.A8R3G3B2:
                case Tdx.D3DFormat.X4R4G4B4:
                case Tdx.D3DFormat.A8P8:
                case Tdx.D3DFormat.A8L8:
                case Tdx.D3DFormat.V8U8:
                case Tdx.D3DFormat.L6V5U5:
                case Tdx.D3DFormat.D16_LOCKABLE:
                case Tdx.D3DFormat.D15S1:
                case Tdx.D3DFormat.D16:
                case Tdx.D3DFormat.INDEX16:
                case Tdx.D3DFormat.L16:
                {
                    return 2;
                }

                case Tdx.D3DFormat.R8G8B8:
                {
                    return 3;
                }

                case Tdx.D3DFormat.A8R8G8B8:
                case Tdx.D3DFormat.X8R8G8B8:
                case Tdx.D3DFormat.A2B10G10R10:
                case Tdx.D3DFormat.G16R16:
                case Tdx.D3DFormat.X8L8V8U8:
                case Tdx.D3DFormat.Q8W8V8U8:
                case Tdx.D3DFormat.V16U16:
                case Tdx.D3DFormat.A2W10V10U10:
                case Tdx.D3DFormat.D32:
                case Tdx.D3DFormat.D24S8:
                case Tdx.D3DFormat.D24X8:
                case Tdx.D3DFormat.D24X4S4:
                case Tdx.D3DFormat.INDEX32:
                case Tdx.D3DFormat.A8B8G8R8:
                case Tdx.D3DFormat.X8B8G8R8:
                case Tdx.D3DFormat.A2R10G10B10:
                case Tdx.D3DFormat.D32F_LOCKABLE:
                case Tdx.D3DFormat.D24FS8:
                case Tdx.D3DFormat.D32_LOCKABLE:
                {
                    return 4;
                }

                case Tdx.D3DFormat.A16B16G16R16:
                {
                    return 8;
                }

                case Tdx.D3DFormat.G8R8_G8B8:
                case Tdx.D3DFormat.R8G8_B8G8:
                {
                    return 1;
                }

                case Tdx.D3DFormat.YUY2:
                case Tdx.D3DFormat.UYVY:
                {
                    return 2;
                }
            }

            throw new ArgumentException("unhandled " + format.ToString());
        }

        private int GetMipSize(Tdx.D3DFormat format, ushort width, ushort height)
        {
            width = Math.Max((ushort)4, width);
            height = Math.Max((ushort)4, height);

            if (format == Tdx.D3DFormat.DXT1)
            {
                return
                    (((width + 3) / 4) *
                     ((height + 3) / 4)) * 8;
            }

            if (format == Tdx.D3DFormat.DXT2 ||
                format == Tdx.D3DFormat.DXT3 ||
                format == Tdx.D3DFormat.DXT4 ||
                format == Tdx.D3DFormat.DXT5)
            {
                return
                    (((width + 3) / 4) *
                     ((height + 3) / 4)) * 16;
            }

            return width * height * GetPixelByteSize(format);
        }


        public void Serialize(Stream output)
        {
            output.WriteValueU16(512);
            output.WriteValueU16(this.Width);
            output.WriteValueU16(this.Height);
            output.WriteValueU16((ushort)this.Mipmaps.Count);
            output.WriteValueEnum<Tdx.HeaderFlags>(this.Flags);
            output.WriteValueEnum<Tdx.D3DFormat>(this.Format);

            if ((this.Flags & Tdx.HeaderFlags.HasExtraData) == Tdx.HeaderFlags.HasExtraData)
            {
                output.WriteValueS32(this.ExtraData.Length);
                output.Write(this.ExtraData, 0, this.ExtraData.Length);
            }

            if ((this.Flags & Tdx.HeaderFlags.Unknown12) == Tdx.HeaderFlags.Unknown12)
            {
                // ref#1
                throw new FormatException();
            }
            else
            {
                if ((this.Flags & Tdx.HeaderFlags.Unknown0) == Tdx.HeaderFlags.Unknown0)
                {
                    // ref#2
                    throw new FormatException();
                }
                if ((this.Flags & Tdx.HeaderFlags.Unknown1) == Tdx.HeaderFlags.Unknown1)
                {
                    // ref#3
                    throw new FormatException();
                }

                if (this.Width > 2048 ||
                    this.Height > 2048)
                {
                    // ref#4
                    throw new FormatException();
                }

                foreach (var mip in this.Mipmaps)
                {
                    output.Write(mip.Data, 0, mip.Data.Length);
                }
            }
        }

        public void Deserialize(Stream input)
        {
            ushort magic = input.ReadValueU16();
            if (magic != 512)
            {
                throw new FormatException();
            }

            this.Width = input.ReadValueU16();
            this.Height = input.ReadValueU16();
            ushort mipCount = input.ReadValueU16();
            this.Flags = input.ReadValueEnum<Tdx.HeaderFlags>();
            this.Format = input.ReadValueEnum<Tdx.D3DFormat>();

            if ((this.Flags & Tdx.HeaderFlags.HasExtraData) == Tdx.HeaderFlags.HasExtraData)
            {
                uint length = input.ReadValueU32();
                this.ExtraData = new byte[length];
                input.Read(this.ExtraData, 0, this.ExtraData.Length);
            }

            if ((this.Flags & Tdx.HeaderFlags.Unknown12) == Tdx.HeaderFlags.Unknown12)
            {
                // ref#5
                throw new FormatException();
            }
            else
            {
                if ((this.Flags & Tdx.HeaderFlags.Unknown0) == Tdx.HeaderFlags.Unknown0)
                {
                    // ref#6
                    throw new FormatException();
                }
                if ((this.Flags & Tdx.HeaderFlags.Unknown1) == Tdx.HeaderFlags.Unknown1)
                {
                    // ref#7
                    throw new FormatException();
                }

                if (this.Width > 2048 ||
                    this.Height > 2048)
                {
                    // ref#8
                    throw new FormatException();
                }

                this.Mipmaps.Clear();
                for (ushort i = 0; i < mipCount; i++)
                {
                    var mip = new Tdx.Mipmap();
                    mip.Width = (ushort)Math.Max(1, this.Width >> i);
                    mip.Height = (ushort)Math.Max(1, this.Height >> i);
                    mip.Data = new byte[Math.Max(1, GetMipSize(this.Format, mip.Width, mip.Height))];
                    input.Read(mip.Data, 0, mip.Data.Length);
                    this.Mipmaps.Add(mip);
                }
            }

            if (input.Position != input.Length)
            {
                throw new FormatException();
            }
        }
    }
}
