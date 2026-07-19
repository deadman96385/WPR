using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace System.Data.Linq
{
    /* Reads the rows out of a phone local-database file.
     *
     * This is not an implementation of SQL Server Compact. It reads the one
     * thing a title needs - the rows of its own table - out of the on-disk
     * layout those files use, and it is deliberately strict: anything it cannot
     * account for is skipped rather than guessed at, because a reader that
     * invents rows is worse than one that finds none.
     *
     * The layout, established by reading the seven databases Trivial Pursuit
     * ships and checking every reading against something independent:
     *
     *   The file is a sequence of 4096-byte pages. A data page opens with a
     *   28-byte header whose byte 20 is the number of row slots on the page,
     *   and closes with that many 4-byte slot entries. Row data therefore
     *   occupies [28, 4096 - 4 * slots). Each slot entry ends in 0x02, which is
     *   the cross-check that a page really is a data page and that the slot
     *   count was read from the right byte.
     *
     *   Rows are laid out continuously across those regions and freely span a
     *   page boundary, so the regions are concatenated and parsed as one
     *   stream. That is not a detail worth skipping: a row of Trivial Pursuit's
     *   answers regularly runs off the end of one page and resumes 28 bytes
     *   into the next, and reading pages independently silently truncates it
     *   mid-string.
     *
     *   A row is a column count, then 0x80, then each non-string column as a
     *   4-byte little-endian value, then 0x00, then for each string column a
     *   0x80 and a one-byte offset giving that string's end within the row's
     *   character block, then 0x00, then the block itself. A string column
     *   whose marker is absent by the time the 0x00 arrives is null. Text is
     *   single-byte: no database here uses a byte in 0x80-0x9F, so Latin-1
     *   decodes all six locales exactly, accents included.
     */
    internal static class LocalDatabaseReader
    {
        private const int PageSize = 4096;
        private const int PageHeaderSize = 28;
        private const int SlotEntrySize = 4;
        private const byte SlotMarker = 0x02;
        private const byte Present = 0x80;

        internal static List<object?[]> ReadRows(string path, Type[] columnTypes)
        {
            byte[] file = File.ReadAllBytes(path);
            byte[] stream = ConcatenateDataRegions(file);
            return ParseRows(stream, columnTypes);
        }

        private static byte[] ConcatenateDataRegions(byte[] file)
        {
            var data = new List<byte>(file.Length);
            for (int start = 0; start + PageSize <= file.Length; start += PageSize)
            {
                int slots = file[start + 20];
                if (slots == 0)
                {
                    continue;
                }

                int dataEnd = PageSize - (SlotEntrySize * slots);
                if (dataEnd <= PageHeaderSize)
                {
                    continue;
                }

                // Every slot entry ends in the same marker. A page where that
                // does not hold is not a data page, whatever byte 20 said.
                bool looksLikeDataPage = true;
                for (int slot = 0; slot < slots - 1; slot++)
                {
                    if (file[start + dataEnd + 3 + (SlotEntrySize * slot)] != SlotMarker)
                    {
                        looksLikeDataPage = false;
                        break;
                    }
                }

                if (!looksLikeDataPage)
                {
                    continue;
                }

                for (int offset = PageHeaderSize; offset < dataEnd; offset++)
                {
                    data.Add(file[start + offset]);
                }
            }

            return data.ToArray();
        }

        private static List<object?[]> ParseRows(byte[] s, Type[] columnTypes)
        {
            int fixedColumns = 0;
            int stringColumns = 0;
            foreach (Type type in columnTypes)
            {
                if (type == typeof(string))
                {
                    stringColumns++;
                }
                else
                {
                    fixedColumns++;
                }
            }

            var rows = new List<object?[]>();
            int o = 0;
            while (o + 16 < s.Length)
            {
                if (BitConverter.ToUInt32(s, o) != (uint)columnTypes.Length || s[o + 4] != Present)
                {
                    o++;
                    continue;
                }

                int p = o + 5;
                var values = new object?[columnTypes.Length];

                // Fixed-width columns, in declaration order.
                bool malformed = false;
                for (int column = 0; column < columnTypes.Length && !malformed; column++)
                {
                    if (columnTypes[column] == typeof(string))
                    {
                        continue;
                    }

                    if (p + 4 > s.Length)
                    {
                        malformed = true;
                        break;
                    }

                    values[column] = unchecked((int)BitConverter.ToUInt32(s, p));
                    p += 4;
                }

                if (malformed || p >= s.Length || s[p] != 0)
                {
                    o++;
                    continue;
                }

                p++;

                // Offsets into the character block, one per present string.
                var ends = new List<int>();
                while (p + 1 < s.Length && s[p] == Present && ends.Count < stringColumns)
                {
                    ends.Add(s[p + 1]);
                    p += 2;
                }

                if (p >= s.Length || s[p] != 0 || ends.Count == 0)
                {
                    o++;
                    continue;
                }

                p++;

                if (!Ascending(ends) || ends[0] == 0 || p + ends[ends.Count - 1] > s.Length)
                {
                    o++;
                    continue;
                }

                int blockLength = ends[ends.Count - 1];
                if (!IsText(s, p, blockLength))
                {
                    o++;
                    continue;
                }

                int taken = 0;
                int previousEnd = 0;
                for (int column = 0; column < columnTypes.Length; column++)
                {
                    if (columnTypes[column] != typeof(string))
                    {
                        continue;
                    }

                    if (taken >= ends.Count)
                    {
                        values[column] = null;
                        continue;
                    }

                    int end = ends[taken];
                    values[column] = Encoding.Latin1.GetString(s, p + previousEnd, end - previousEnd);
                    previousEnd = end;
                    taken++;
                }

                rows.Add(values);
                o = p + blockLength;
            }

            return rows;
        }

        private static bool Ascending(List<int> values)
        {
            for (int index = 1; index < values.Count; index++)
            {
                if (values[index] < values[index - 1])
                {
                    return false;
                }
            }

            return true;
        }

        /* Free space holds whatever was written there before, so a run of bytes
         * that parses as a row is not yet a row. Text is what separates a live
         * row from a stale one: a control byte inside the character block means
         * the offsets were read against something that is no longer a record.
         */
        private static bool IsText(byte[] s, int start, int length)
        {
            for (int index = start; index < start + length; index++)
            {
                if (s[index] < 0x20)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
