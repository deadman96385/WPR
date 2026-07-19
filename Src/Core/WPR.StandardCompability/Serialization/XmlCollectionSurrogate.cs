using System.Collections.Generic;

namespace WPR.StandardCompability.Serialization
{
    /* XmlSerializer cannot map an array whose element type is a generic
     * collection. Given a member of type List<T>[] it throws a bare
     * NullReferenceException out of its own code generator, with no frame of its
     * own on the stack, so the fault appears to belong to whichever title method
     * called Serialize. The phone's serializer handled the shape, and titles
     * saved games through it.
     *
     * A jagged T[][] maps correctly, as do List<T> and List<List<T>>, so the
     * patcher hides the offending member and exposes one of these in its place.
     * These two conversions are what that generated property calls; keeping them
     * here rather than emitting the loops as IL keeps the rewrite to a handful
     * of instructions.
     */
    public static class XmlCollectionSurrogate
    {
        public static T[][]? ToJagged<T>(List<T>[]? value)
        {
            if (value == null)
            {
                return null;
            }

            var jagged = new T[value.Length][];
            for (int index = 0; index < value.Length; index++)
            {
                jagged[index] = value[index] == null
                    ? System.Array.Empty<T>()
                    : value[index].ToArray();
            }

            return jagged;
        }

        public static List<T>[]? FromJagged<T>(T[][]? value)
        {
            if (value == null)
            {
                return null;
            }

            var lists = new List<T>[value.Length];
            for (int index = 0; index < value.Length; index++)
            {
                lists[index] = value[index] == null
                    ? new List<T>()
                    : new List<T>(value[index]);
            }

            return lists;
        }
    }
}
