using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace WPR
{
    /* A phone title localises its name by pointing the manifest at a string in a
     * resource-only DLL it ships, so Title reads "@AppResLib.dll,-100" rather
     * than a name. Twenty-two titles in this library do it, and left unresolved
     * every one of them installs, lists, and opens a window under that same
     * literal string - indistinguishable from each other.
     *
     * The window title is not only cosmetic: the evaluation driver finds a
     * running game by it, and a matcher that could not tell two titles apart has
     * hidden a live window here before.
     *
     * Read rather than loaded. LoadLibraryEx would do this on Windows, but these
     * are ARM phone binaries and this assembly also targets Android, and nothing
     * about reading a string table needs the host to be able to load the image.
     */
    internal static class LocalizedResourceName
    {
        private static readonly Regex Reference =
            new(@"^@([^,]+),\s*-(\d+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private const int ResourceDirectoryIndex = 2;
        private const int StringResourceType = 6;

        /// <summary>
        /// The manifest title, with a resource reference replaced by the name it
        /// points at. Anything unrecognised or unreadable comes back unchanged:
        /// an obviously unresolved reference beats a confidently wrong name.
        /// </summary>
        public static string Resolve(string? title, ZipArchive archive)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return title ?? string.Empty;
            }

            Match match = Reference.Match(title.Trim());
            if (!match.Success || archive is null)
            {
                return title;
            }

            string wanted = match.Groups[1].Value.Trim();
            if (!int.TryParse(match.Groups[2].Value, out int stringId))
            {
                return title;
            }

            try
            {
                ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(candidate =>
                    string.Equals(candidate.FullName, wanted, StringComparison.OrdinalIgnoreCase) ||
                    candidate.FullName.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    return title;
                }

                byte[] image;
                using (Stream stream = entry.Open())
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    image = buffer.ToArray();
                }

                string? resolved = ReadString(image, stringId);
                return string.IsNullOrWhiteSpace(resolved) ? title : resolved!.Trim();
            }
            catch (Exception exception) when (
                exception is IOException or InvalidDataException or
                NotSupportedException or ObjectDisposedException)
            {
                return title;
            }
        }

        /* Win32 keeps strings in blocks of sixteen, so the resource to ask for is
         * id/16 + 1 and the wanted string is id%16 entries into it. Each entry is
         * a length word followed by that many UTF-16 code units and no
         * terminator, so an empty slot is a zero length rather than a gap.
         */
        private static string? ReadString(byte[] image, int stringId)
        {
            try
            {
                if (image.Length < 0x40 || image[0] != (byte)'M' || image[1] != (byte)'Z')
                {
                    return null;
                }

                int pe = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
                if (pe < 0 || pe + 24 > image.Length ||
                    image[pe] != (byte)'P' || image[pe + 1] != (byte)'E' ||
                    image[pe + 2] != 0 || image[pe + 3] != 0)
                {
                    return null;
                }

                int optional = pe + 24;
                ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(optional));
                int directories = magic switch
                {
                    0x10B => optional + 96,   // PE32
                    0x20B => optional + 112,  // PE32+
                    _ => -1,
                };
                if (directories < 0)
                {
                    return null;
                }

                int resourcesRva = BinaryPrimitives.ReadInt32LittleEndian(
                    image.AsSpan(directories + ResourceDirectoryIndex * 8));
                if (resourcesRva == 0)
                {
                    return null;
                }

                int root = OffsetOf(image, pe, resourcesRva);
                if (root < 0)
                {
                    return null;
                }

                int block = Math.DivRem(stringId, 16, out int position) + 1;

                // Level 1 is the resource type, level 2 the block, level 3 the
                // language - and any language will do for a name nobody
                // localises past the neutral entry here.
                int node = root;
                foreach (int? wanted in new int?[] { StringResourceType, block, null })
                {
                    if (!TryFindEntry(image, node, wanted, out uint offset))
                    {
                        return null;
                    }
                    // The high bit marks a subdirectory rather than a leaf; the
                    // rest is the offset from the resource root either way.
                    node = root + (int)(offset & 0x7FFFFFFF);
                    if (node < 0 || node + 16 > image.Length)
                    {
                        return null;
                    }
                }

                int dataRva = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(node));
                int size = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(node + 4));
                int start = OffsetOf(image, pe, dataRva);
                if (start < 0 || size < 0 || start + size > image.Length)
                {
                    return null;
                }

                int cursor = start;
                int end = start + size;
                for (int index = 0; index < 16; index++)
                {
                    if (cursor + 2 > end)
                    {
                        return null;
                    }

                    int length = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(cursor));
                    cursor += 2;
                    if (index == position)
                    {
                        return cursor + length * 2 > end
                            ? null
                            : System.Text.Encoding.Unicode.GetString(image, cursor, length * 2);
                    }
                    cursor += length * 2;
                }

                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        /* An entry of a resource directory, by id or the first there is.
         *
         * The offset comes back through an out parameter and unsigned on
         * purpose. A subdirectory entry sets the high bit, so read as a signed
         * int it is negative - and a -1 "not found" sentinel then swallows every
         * real subdirectory, which is exactly what made this resolve nothing at
         * first.
         */
        private static bool TryFindEntry(byte[] image, int directory, int? wanted, out uint offset)
        {
            offset = 0;
            if (directory < 0 || directory + 16 > image.Length)
            {
                return false;
            }

            int named = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(directory + 12));
            int ids = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(directory + 14));

            for (int index = 0; index < named + ids; index++)
            {
                int at = directory + 16 + index * 8;
                if (at + 8 > image.Length)
                {
                    return false;
                }

                uint identifier = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at));
                uint candidate = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(at + 4));

                // A named entry's high bit points at a string, not an id.
                if ((identifier & 0x80000000) != 0)
                {
                    continue;
                }

                if (wanted is null || identifier == (uint)wanted.Value)
                {
                    offset = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>File offset of an RVA, through whichever section contains it.</summary>
        private static int OffsetOf(byte[] image, int pe, int rva)
        {
            int sections = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 6));
            int table = pe + 24 + BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 20));

            for (int index = 0; index < sections; index++)
            {
                int at = table + index * 40;
                if (at + 40 > image.Length)
                {
                    return -1;
                }

                int virtualSize = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(at + 8));
                int virtualAddress = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(at + 12));
                int rawSize = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(at + 16));
                int rawPointer = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(at + 20));

                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                {
                    return rawPointer + (rva - virtualAddress);
                }
            }

            return -1;
        }
    }
}
