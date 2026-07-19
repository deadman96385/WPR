using System;
using System.Windows;
using System.Threading.Tasks;

namespace WPR.WindowsCompability
{
    public static class MessageBox
    {
        public static Func<string, string, MessageBoxButton, Task<MessageBoxResult>>? ShowSimpleImpl;

        public static MessageBoxResult Show(string title, string caption, MessageBoxButton buttons)
        {
            return ShowSimpleImpl?.Invoke(title, caption, buttons).GetAwaiter().GetResult()
                ?? MessageBoxResult.OK;
        }

        /* The phone's one-argument overload: an OK-only prompt with no caption.
         * A title that only wants to tell the user something calls this one, and
         * it is worth having for a reason beyond completeness - Trivial Pursuit
         * calls it from a menu handler that runs during Update, so the missing
         * overload did not fail once. It threw on every frame, the click that
         * chose a save slot never completed, and the menu sat there looking
         * frozen with no fault of its own on screen.
         */
        public static MessageBoxResult Show(string title)
        {
            return Show(title, string.Empty, MessageBoxButton.OK);
        }
    }

}
