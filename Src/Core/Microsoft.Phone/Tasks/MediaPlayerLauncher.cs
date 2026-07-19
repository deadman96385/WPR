using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Phone.Tasks
{
    public class MediaPlayerLauncher
    {
        /* Launching the media player put another application in front of the
         * title, so the title saw Deactivated and then Activated when the user
         * came back out of it. Titles depend on that far more than on the
         * video: Oregon Trail plays its logo through here, and its engine
         * never queues anything to draw until it has been deactivated once -
         * with Show() as a silent no-op it drew a black frame forever.
         *
         * The host supplies the round trip, because nothing in this assembly
         * can see the running game. There is no media player to run, so the
         * honest length of the visit is as close to nothing as the loop can
         * express.
         */
        public static Action<TimeSpan>? ForegroundLaunch { get; set; }

        public MediaLocationType Location { get; set; }

        public Uri Media { get; set; }

        public MediaPlaybackControls Controls { get; set; }

        public MediaPlayerOrientation Orientation { get; set; }

        public void Show()
        {
            ForegroundLaunch?.Invoke(TimeSpan.FromMilliseconds(250));
        }
    }
}
