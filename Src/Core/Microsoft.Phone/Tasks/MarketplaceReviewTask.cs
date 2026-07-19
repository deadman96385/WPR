using WPR.Common;

namespace Microsoft.Phone.Tasks
{
    /* Deep-links to the current title's review page in the marketplace.
     *
     * There is no marketplace to open, so Show does nothing, which is the same
     * answer MarketplaceDetailTask gives. A title calls this from a menu it
     * drew itself and carries on either way; the phone left the title running
     * behind the marketplace app, so continuing is also what it expects.
     *
     * The type mattering at all has nothing to do with reviews. Farm Frenzy 2
     * references it from one branch of GuideHelper.Update that startup never
     * takes, but the JIT resolves every type a method body names before it runs
     * a single instruction of it, so the whole method failed with a
     * TypeLoadException on every frame - and GuideHelper.Update is the first
     * call in that title's Game.Update, ahead of both its current screen's
     * Update and base.Update. An unreachable reference to a two-member type
     * cost the title its screen updates and its entire component pass.
     */
    public class MarketplaceReviewTask
    {
        public void Show()
        {
            Log.Info(LogCategory.Common,
                "MarketplaceReviewTask.Show: no marketplace to review in.");
        }
    }
}
