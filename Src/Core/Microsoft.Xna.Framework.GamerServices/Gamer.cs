using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using System.Linq;

using WPR.Common;

namespace Microsoft.Xna.Framework.GamerServices
{
    public abstract class Gamer : IDisposable
    {
        internal static SignedInGamerCollection _SignedInGamers;

        private LeaderboardWriter _LeaderboardWriter;
        private String _GamerTag;

        internal Gamer()
        {
            _LeaderboardWriter = new LeaderboardWriter();
            _GamerTag = Configuration.Current?.GamerTag ?? "HarryDirk";
        }

        static Gamer()
        {
            _SignedInGamers = new SignedInGamerCollection(new List<SignedInGamer>{
                new SignedInGamer() { PlayerIndex = PlayerIndex.One }
            });
        }

        public IAsyncResult BeginGetProfile(AsyncCallback callback, object asyncState)
        {
            return Task.Run(async () =>
            {
                GamerProfile profile = new GamerProfile();
                var earnedIte = AchievementContext.Current.Achievements!.Where(a => a.IsEarned);

                profile.TotalAchievements = await earnedIte.CountAsync();
                profile.GamerScore = await earnedIte.SumAsync(a => a.GamerScore);
                profile.GamerZone = GamerZone.Underground;
                profile.Region = System.Globalization.RegionInfo.CurrentRegion;
                profile.Reputation = 100.0f;
                profile.Motto = "";

                if (callback != null)
                {
                    TaskCompletionSource<GamerProfile> source = new TaskCompletionSource<GamerProfile>(asyncState);
                    source.SetResult(profile);

                    callback(source.Task);
                }

                return profile;
            });
        }

        public GamerProfile EndGetProfile(IAsyncResult result)
        {
            return (result as Task<GamerProfile>)!.GetAwaiter().GetResult();
        }

        public GamerProfile GetProfile() => EndGetProfile(BeginGetProfile(null, null));

        public static IAsyncResult BeginGetFromGamertag(
            string gamertag,
            AsyncCallback callback,
            object asyncState)
        {
            var gamer = new SignedInGamer { Gamertag = gamertag };
            var source = new TaskCompletionSource<Gamer>(asyncState,
                TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetResult(gamer);
            callback?.Invoke(source.Task);
            return source.Task;
        }

        public static Gamer EndGetFromGamertag(IAsyncResult result) =>
            ((Task<Gamer>)result).GetAwaiter().GetResult();

        public static Gamer GetFromGamertag(string gamertag) =>
            EndGetFromGamertag(BeginGetFromGamertag(
                gamertag, callback: null!, asyncState: null!));

        public override string ToString() => Gamertag;

        /* There is no partner token without a Live session, so this fails
         * offline and that answer is truthful. What it must not do is fail on
         * the caller's stack.
         *
         * The phone reached a Live service over the network here, so a title's
         * completion callback could not possibly have run before Begin
         * returned. Invoking it inline puts it on the caller's stack instead,
         * and because the operation always fails offline, the failure threw
         * back out through Begin into whatever called it.
         *
         * That is not a cosmetic difference. Fusion Sentient asks for a token
         * from its SignedIn handler, and its callback calls EndGetPartnerToken
         * with no guard, so the throw unwound out of GamerSignedInCallback
         * before its next statement - GetFriendsList - could run, then out of
         * the SignedIn multicast, abandoning any handler queued behind it, and
         * on into GamerServicesDispatcher.Update, discarding the rest of the
         * pending queue. One unavailable service silently cost the title an
         * unrelated call it always made on the phone.
         */
        public static IAsyncResult BeginGetPartnerToken(
          string audienceUri,
          AsyncCallback callback,
          object asyncState)
        {
            var source = new TaskCompletionSource<string>(asyncState,
                TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetException(new InvalidOperationException(
                "The partner-token service is unavailable while running offline."));
            CompleteOffCallerStack(source.Task, callback);
            return source.Task;
        }

        /* Raise a completion where the phone raised it - on a pool thread,
         * after Begin has returned - and contain what the callback throws.
         *
         * Containment is required rather than tidy. By the time a callback
         * runs there is no caller left to receive an exception, and an
         * unhandled one on a pool thread ends the process. A title that throws
         * inside its own completion handler is still a real finding, so it is
         * recorded instead of dropped; it goes to the log rather than to
         * standard error because it is not an exception the game loop
         * swallowed, and reporting it as one would misattribute it.
         */
        private static void CompleteOffCallerStack<T>(
            Task<T> task,
            AsyncCallback? callback)
        {
            if (callback == null)
            {
                /* The synchronous wrapper observes the fault itself through
                 * End. Observe it here too, so a caller that abandons the
                 * operation does not leave a faulted task unobserved.
                 */
                task.ContinueWith(
                    static completed => { _ = completed.Exception; },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return;
            }

            task.ContinueWith(
                completed =>
                {
                    try
                    {
                        callback(completed);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(LogCategory.GamerServices,
                            "Title callback threw while completing an " +
                            $"asynchronous Gamer operation:\n{ex}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        public static string EndGetPartnerToken(IAsyncResult result) =>
            ((Task<string>)result).GetAwaiter().GetResult();

        public static string GetPartnerToken(string audienceUri) =>
            EndGetPartnerToken(BeginGetPartnerToken(
                audienceUri, callback: null!, asyncState: null!));

        public string Gamertag
        {
            get => _GamerTag;
            set => _GamerTag = value;
        }

        public string DisplayName => _GamerTag;

        public void Dispose()
        {
            IsDisposed = true;
        }

        public bool IsDisposed
        {
            get;
            set;
        }

        public static SignedInGamerCollection SignedInGamers
        {
            get
            {
                return _SignedInGamers;
            }
        }

        public object Tag
        {
            get;
            set;
        }

        public LeaderboardWriter LeaderboardWriter
        {
            get => _LeaderboardWriter;
        }
    }
}
