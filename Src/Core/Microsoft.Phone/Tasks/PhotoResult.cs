using System;
using System.IO;

namespace Microsoft.Phone.Tasks
{
    public class PhotoResult : TaskEventArgs
    {
        public PhotoResult(TaskResult taskResult)
            : base(taskResult)
        {
        }

        // Glow Artisan reads ChosenPhoto in its Completed handler. The chooser
        // never completes on the desktop, so the handler does not run and this
        // stays null rather than being handed an empty stream that would read
        // as a chosen photo of nothing.
        public Stream? ChosenPhoto { get; internal set; }

        public string? OriginalFileName { get; internal set; }
    }
}
