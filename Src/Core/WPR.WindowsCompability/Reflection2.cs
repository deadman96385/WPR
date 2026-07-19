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
    }
}
