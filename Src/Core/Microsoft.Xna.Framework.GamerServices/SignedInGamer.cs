using WPR.WindowsCompability;
using WPR.Common;

using Microsoft.EntityFrameworkCore;

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Xna.Framework.GamerServices
{
    public sealed class SignedInGamer : Gamer
    {
        private static bool FirstSignInSessionDone = false;
        private static bool SignInQueued = false;
        private static event EventHandler<SignedInEventArgs>? SignedInHandlers;

        private PlayerIndex _PlayerIndex;

        private GamerPrivileges _GamerPrivileges = new GamerPrivileges();

        private GamerPresence _GamerPresence = new GamerPresence();

        public event EventHandler<EventArgs> AvatarChanged;
        
        public static void Reset()
        {
            FirstSignInSessionDone = false;
            SignInQueued = false;
            SignedInHandlers = null;
            GamerServicesDispatcher.Reset();
        }

        public static event EventHandler<SignedInEventArgs> SignedIn
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                SignedInHandlers += value;
                if (FirstSignInSessionDone)
                {
                    GamerServicesDispatcher.Schedule(() =>
                        value.Invoke(null, new SignedInEventArgs(_SignedInGamers[0])));
                }
                else if (!SignInQueued)
                {
                    SignInQueued = true;
                    GamerServicesDispatcher.Schedule(DispatchSignedIn);
                }
            }
            remove
            {
                SignedInHandlers -= value;
            }
        }

        private static void DispatchSignedIn()
        {
            if (FirstSignInSessionDone)
            {
                return;
            }

            FirstSignInSessionDone = true;
            SignInQueued = false;
            SignedInHandlers?.Invoke(null, new SignedInEventArgs(_SignedInGamers[0]));
        }

        public static event EventHandler<SignedOutEventArgs> SignedOut;

        internal SignedInGamer()
        {
        }

        public IAsyncResult BeginGetAchievements(AsyncCallback? callback, Object? asyncState)
        {
            return Task.Run(async () =>
            {
                string productId = Application.Current.ProductId;

                List<Achievement> achievementStored = await AchievementContext.Current!.Achievements!
                    .Where(x => x.OwnProductId == productId)
                    .ToListAsync();

                /* The store holds only what this title has awarded. Live returned
                 * the whole set, and titles draw their achievements screen from
                 * it, so start from the definition and let the store supply the
                 * earned state. Without a definition the stored rows are still
                 * all there is to report.
                 */
                IReadOnlyList<AchievementDefinition> defined =
                    AchievementDefinitions.ForProduct(productId);

                AchievementCollection coll = new AchievementCollection();

                if (defined.Count > 0)
                {
                    foreach (AchievementDefinition definition in defined)
                    {
                        Achievement? earned = achievementStored.Find(stored =>
                            string.Equals(stored.Key, definition.Key, StringComparison.Ordinal));

                        coll.Add(earned ?? new Achievement
                        {
                            Key = definition.Key,
                            Name = definition.Name ?? definition.Key,
                            Description = definition.Description ?? string.Empty,
                            HowToEarn = definition.Description ?? string.Empty,
                            GamerScore = definition.GamerScore,
                            OwnProductId = productId,
                            DisplayBeforeEarned = true,
                            IsEarned = false,
                            _IconPath = string.Empty,
                        });
                    }
                }
                else
                {
                    foreach (Achievement achiQueried in achievementStored)
                    {
                        coll.Add(achiQueried);
                    }
                }

                var completeSource = new TaskCompletionSource<AchievementCollection>(asyncState);
                completeSource.SetResult(coll);

                if (callback != null)
                {
                    callback(completeSource.Task);
                }

                return coll;
            });
        }

        public AchievementCollection EndGetAchievements(IAsyncResult result)
        {
            Log.Error(LogCategory.GamerServices, "Result!");
            Task<AchievementCollection>? collectResult = result as Task<AchievementCollection>;
            return collectResult!.GetAwaiter().GetResult();
        }

        public AchievementCollection GetAchievements() => this.EndGetAchievements(this.BeginGetAchievements(null, null));

        public IAsyncResult BeginAwardAchievement(string achievementKey, AsyncCallback callback,
            object state)
        {
            return Task.Run(async () =>
            {
                List<Achievement> achievements = await AchievementContext.Current!.Achievements!
                    .Where(x => (x.OwnProductId == Application.Current.ProductId) && (x.Key == achievementKey))
                    .ToListAsync();

                if (achievements.Count > 1)
                {
                    Log.Warn(LogCategory.GamerServices, $"More then two achievements with key {achievementKey} exists!");
                }

                if (achievements.Count == 0)
                {
                    /* Nothing populates this table offline, so awarding used to
                     * find no row and silently do nothing: no record, no
                     * notification, and an achievement that could never be
                     * earned. The title has just told us the only thing that
                     * cannot be derived, its own key, so record the award from
                     * that. This works for any title because the key comes from
                     * the title rather than from a catalogue of its content.
                     *
                     * The rest is left blank rather than invented. Score,
                     * description, and artwork are properties of the achievement
                     * as published, and a wrong score reads as fact. The
                     * shipped mapping supplies a display name for the titles it
                     * covers; elsewhere the key is shown, which is at least
                     * true.
                     */
                    Achievement earned = new Achievement
                    {
                        Key = achievementKey,
                        OwnProductId = Application.Current.ProductId,
                        Name = ResolveAchievementName(achievementKey),
                        Description = string.Empty,
                        HowToEarn = string.Empty,
                        _IconPath = string.Empty,
                        DisplayBeforeEarned = true,
                        GamerScore = 0,
                        IsEarned = true,
                        // Earned against local data, never against a live service.
                        EarnedOnline = false,
                        EarnedDateTime = DateTime.Now
                    };

                    await AchievementContext.Current!.Achievements!.AddAsync(earned);
                    achievements.Add(earned);
                }
                else
                {
                    foreach (Achievement achievement in achievements)
                    {
                        if (achievement.IsEarned)
                        {
                            continue;
                        }

                        achievement.IsEarned = true;
                        achievement.EarnedOnline = false;
                        achievement.EarnedDateTime = DateTime.Now;
                    }
                }

                await AchievementContext.Current!.SaveChangesAsync();

                /* Announcing the award is cosmetic, and it is already saved above,
                 * so a host with nowhere to show it must not cost the award.
                 * NativeUI.NotificationManager is null by design on Linux and
                 * macOS, and on Windows until a host calls NativeUI.Initialize -
                 * which the evaluation harness never does. Dereferencing it
                 * unconditionally therefore faulted on every award under the
                 * harness, on a thread the title cannot see, which is exactly the
                 * shape that hides for a long time.
                 */
                try
                {
                    var notifier = NativeUI.NotificationManager;
                    if (notifier == null)
                    {
                        Log.Info(LogCategory.GamerServices,
                            "No notification host; award recorded without announcing it.");
                    }
                    else
                    {
                        Achievement announced = achievements[0];
                        var notification = new DesktopNotifications.Notification()
                        {
                            Title = Properties.Resources.AchievementUnlocked,
                            Body = announced.GamerScore > 0
                                ? $"{announced.GamerScore}G - {announced.Name}"
                                : announced.Name,
                            SoundUri = "AchievementUnlocked"
                        };

                        // Only point at artwork that is actually there.
                        if (!string.IsNullOrEmpty(announced._IconPath))
                        {
                            string iconPath = Configuration.Current!.DataPath(announced._IconPath);
                            if (File.Exists(iconPath))
                            {
                                notification.ImagePath = iconPath;
                            }
                        }

                        await notifier.ShowNotification(
                            notification, DateTime.Now + TimeSpan.FromDays(1));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(LogCategory.GamerServices, $"Fail to display Achievement notification with exception:\n {ex}");
                }

                if (callback != null)
                {
                    TaskCompletionSource source = new TaskCompletionSource(state);
                    source.SetResult();

                    callback(source.Task);
                }

                return Task.CompletedTask;
            });
        }

        /* The shipped mapping covers a handful of titles and is read from disk,
         * so neither a miss nor a failure to load it should cost the award. The
         * key is always shown when no better name is known.
         */
        private static string ResolveAchievementName(string achievementKey)
        {
            try
            {
                return Researcher.GetAchievementName(Application.Current.ProductId!, achievementKey)
                    ?? achievementKey;
            }
            catch (Exception ex)
            {
                Log.Error(LogCategory.GamerServices, $"Fail to resolve achievement name with exception:\n {ex}");
                return achievementKey;
            }
        }

        /* End waits for the operation to finish, as the pattern requires. This
         * did nothing, so AwardAchievement started the write and returned
         * immediately: a title that awarded an achievement and then exited could
         * lose it, and nothing downstream could observe the award either.
         */
        public void EndAwardAchievement(IAsyncResult result)
        {
            (result as Task)?.GetAwaiter().GetResult();
        }

        public void AwardAchievement(string achievementKey) => EndAwardAchievement(BeginAwardAchievement(achievementKey, null, null));

        /* Loads the shipped name mapping from disk, so build it once rather than
         * per award, but not before an award asks for it: constructing this
         * touches the data root, and a static initializer that reads
         * configuration would fault the whole type when it runs before the host
         * has set one.
         */
        private static TrueAchievements.GameToKey? _researcher;
        private static TrueAchievements.GameToKey Researcher =>
            _researcher ??= new TrueAchievements.GameToKey();

        private static readonly FriendCollection EmptyFriends = new FriendCollection();
        private static readonly GameDefaults DefaultGameDefaults = new GameDefaults();
        private static readonly AvatarDescription DefaultAvatar = AvatarDescription.CreateRandom();

        public FriendCollection GetFriends() => EmptyFriends;

        public bool IsFriend(Gamer gamer) => false;

        public AvatarDescription Avatar => DefaultAvatar;

        public GameDefaults GameDefaults => DefaultGameDefaults;

        public bool IsGuest => false;

        /* There is a signed-in gamer, but no Xbox Live session behind it: the
         * partner-token service is gone and every live call this facade offers
         * is served from local data. Claiming a live session invites titles onto
         * their online path, where the first live call then fails. Tiger Woods
         * does exactly that, moving itself to a live sign-in state and issuing a
         * leaderboard read that ends in its error state.
         *
         * Reporting a local sign-in is the honest answer and keeps those titles
         * on the offline path they already have.
         */
        public bool IsSignedInToLive
        {
            get
            {
                return false;
            }
        }

        public int PartySize => 0;

        public PlayerIndex PlayerIndex
        {
            get => _PlayerIndex;
            set => _PlayerIndex = value;
        }

        public GamerPresence Presence
        {
            get => _GamerPresence;
            set => _GamerPresence = value;
        }

        public GamerPrivileges Privileges
        {            
            get => _GamerPrivileges;
            set => _GamerPrivileges = value;
        }
    }
   
}
