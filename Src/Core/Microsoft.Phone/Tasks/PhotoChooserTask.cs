using System;

namespace Microsoft.Phone.Tasks
{
    // Glow Artisan builds one of these in OnActivated, which runs from
    // Game.set_IsActive before the loop starts, so the type missing took the
    // whole activation path with it. Show() is ChooserBase's offline no-op:
    // the picker is phone-shell UI and stays pending rather than completing.
    public class PhotoChooserTask : ChooserBase<PhotoResult>
    {
        public PhotoChooserTask()
        {
        }

        public bool ShowCamera { get; set; }

        public int PixelHeight { get; set; }

        public int PixelWidth { get; set; }
    }
}
