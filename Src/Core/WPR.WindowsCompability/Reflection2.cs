using System;
using System.Reflection;

namespace WPR.WindowsCompability
{
    // The phone reports a reflective read with no target as ArgumentNullException,
    // because Silverlight's reflection has no TargetException to throw. .NET
    // reports TargetException, which derives from ApplicationException and so
    // matches none of the handlers a phone title would have written.
    //
    // Titles depend on the difference. Glow Artisan's save writer walks its own
    // static fields, recurses into every complex one, and reads each member of
    // that field's value - whether or not the value is null - under a single
    // catch (ArgumentNullException). Its first run has committedBoard null,
    // because neither the static constructor nor ClearData assigns it, so on the
    // phone the first member read threw, was caught, and the loop carried on.
    // Here it left the constructor instead and the title never started.
    //
    // The patcher rewrites the game's callvirt to FieldInfo.GetValue into a call
    // to the static shim below. Receiver and argument are already on the stack in
    // that order, so the substitution is stack-neutral.
    public static class Reflection2
    {
        public static object? GetFieldValue(FieldInfo field, object? obj)
        {
            if (field is null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            try
            {
                return field.GetValue(obj);
            }
            catch (TargetException targetFailure)
            {
                // Keep the original text so a trace still names the real cause;
                // ArgumentNullException has no constructor taking an inner
                // exception alongside a parameter name.
                throw new ArgumentNullException("obj", targetFailure.Message);
            }
        }

        // The assembly a name is resolved against is the second place the phone
        // and .NET disagree. The phone resolved an unqualified BCL name through
        // an assembly-scoped lookup; .NET searches only the assembly asked, so
        // "System.String" comes back null from a game's own assembly.
        //
        // Glow Artisan's save reader is built on that. Storage2.ReadArrayType
        // recovers an array's element type with
        // GetExecutingAssembly().GetType(name) after stripping "[]", then hands
        // the result straight to Array.CreateInstance. Its own types resolve, so
        // Storage v1 - whose only arrays are LevelProgress[] and
        // GlowUserPuzzle[] - reads correctly and still does. Storage2 added
        // String[] menuStack and Single[] m_sliders, and both of those resolve
        // to null here and throw ArgumentNullException out of a constructor that
        // has no handler.
        //
        // That the phone resolved them is settled by the title's own code rather
        // than inference: ReadArrayType has an explicit branch comparing the
        // resolved type's FullName to "System.String", which cannot be reached
        // unless the lookup returned a type, and menuStack is read on every
        // launch of a shipped build.
        private static readonly Assembly CoreLibrary = typeof(object).Assembly;

        public static Type? GetAssemblyType(Assembly assembly, string typeName)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            // Ask the game's own assembly first, so nothing that resolves today
            // changes; the core library is consulted only where .NET would have
            // returned null.
            return assembly.GetType(typeName) ?? CoreLibrary.GetType(typeName);
        }

        public static object? CreateAssemblyInstance(Assembly assembly, string typeName)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            object? created = assembly.CreateInstance(typeName);
            if (created is not null)
            {
                return created;
            }

            // Same fallback, same order. Assembly.CreateInstance is GetType
            // followed by a default construction, so mirror both halves rather
            // than only the lookup: ReadArrayType boxes a fresh element per slot
            // and reads into it, which is how Single[] gets its eight values.
            Type? core = CoreLibrary.GetType(typeName);
            return core is null ? null : Activator.CreateInstance(core);
        }
    }
}
