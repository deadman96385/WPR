using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using WPR.Common;

namespace Microsoft.Xna.Framework.GamerServices
{
    // IDisposable is part of the shape, not an implementation detail. Glow
    // Artisan reads its friend-to-beat board inside a using, and the compiler's
    // finally calls IDisposable.Dispose through the interface; without the
    // interface on the type that dispatch throws EntryPointNotFoundException.
    // That throw lands on the thread pool thread QueueCompletion runs the
    // callback on, where nothing catches it, so it takes the process down
    // rather than being swallowed the way a fault inside the game loop is.
    public class LeaderboardReader : IDisposable
    {
        private const int OfflineCompletionDelayMilliseconds = 10;
        private ReadOnlyCollection<LeaderboardEntry>? _Entries;
        public ReadOnlyCollection<LeaderboardEntry>? Entries => this._Entries;

        public LeaderboardIdentity LeaderboardIdentity { get; private set; }

        public int PageStart { get; private set; }

        public LeaderboardReader()
            : this(default, 0)
        {
        }

        private LeaderboardReader(LeaderboardIdentity leaderboardIdentity, int pageStart)
        {
            _Entries = new ReadOnlyCollection<LeaderboardEntry>(new List<LeaderboardEntry>());
            LeaderboardIdentity = leaderboardIdentity;
            PageStart = pageStart;
        }

        public IAsyncResult BeginPageDown(AsyncCallback callback, object asyncState)
        {
            return CompletePage(callback, asyncState);
        }
        public IAsyncResult BeginPageUp(AsyncCallback callback, object asyncState)
        {
            return CompletePage(callback, asyncState);
        }
        public static IAsyncResult BeginRead(LeaderboardIdentity leaderb,
            int pageStart, int pageSize, AsyncCallback callback, object asyncState)
        {
            return CompleteRead(leaderb, pageStart, callback, asyncState);
        }

        public static LeaderboardReader Read(
            LeaderboardIdentity leaderboardId, int pageStart, int pageSize)
        {
            return EndRead(BeginRead(
                leaderboardId, pageStart, pageSize, callback: null!, asyncState: null!));
        }

        public static IAsyncResult BeginRead(
          LeaderboardIdentity leaderboardId,
          Gamer pivotGamer,
          int pageSize,
          AsyncCallback callback,
          object asyncState)
        {
            return CompleteRead(leaderboardId, 0, callback, asyncState);
        }

        public static LeaderboardReader Read(
            LeaderboardIdentity leaderboardId, Gamer pivotGamer, int pageSize)
        {
            return EndRead(BeginRead(
                leaderboardId, pivotGamer, pageSize, callback: null!, asyncState: null!));
        }

        public static IAsyncResult BeginRead(
          LeaderboardIdentity leaderboardId,
          IEnumerable<Gamer> gamers,
          Gamer pivotGamer,
          int pageSize,
          AsyncCallback callback,
          object asyncState)
        {
            return CompleteRead(leaderboardId, 0, callback, asyncState);
        }

        public static LeaderboardReader Read(
            LeaderboardIdentity leaderboardId,
            IEnumerable<Gamer> gamers,
            Gamer pivotGamer,
            int pageSize)
        {
            return EndRead(BeginRead(
                leaderboardId, gamers, pivotGamer, pageSize,
                callback: null!, asyncState: null!));
        }

        private static IAsyncResult CompleteRead(
            LeaderboardIdentity leaderboardIdentity,
            int pageStart,
            AsyncCallback? callback,
            object? asyncState)
        {
            var source = new TaskCompletionSource<LeaderboardReader>(asyncState,
                TaskCreationOptions.RunContinuationsAsynchronously);
            QueueCompletion(source, new LeaderboardReader(leaderboardIdentity, pageStart), callback);
            return source.Task;
        }

        public static LeaderboardReader EndRead(IAsyncResult result)
        {
            return ((Task<LeaderboardReader>)result).GetAwaiter().GetResult();
        }

        private IAsyncResult CompletePage(AsyncCallback? callback, object? asyncState)
        {
            var source = new TaskCompletionSource<LeaderboardReader>(asyncState,
                TaskCreationOptions.RunContinuationsAsynchronously);
            QueueCompletion(source, this, callback);
            return source.Task;
        }

        private static void QueueCompletion(
            TaskCompletionSource<LeaderboardReader> source,
            LeaderboardReader reader,
            AsyncCallback? callback)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // Live leaderboard operations cannot complete before Begin* returns. A short
                // deferral preserves that ordering for titles which prepare callback state next.
                Thread.Sleep(OfflineCompletionDelayMilliseconds);
                source.SetResult(reader);
                callback?.Invoke(source.Task);
            });
        }

        public void EndPageDown(IAsyncResult result) =>
            ((Task<LeaderboardReader>)result).GetAwaiter().GetResult();

        public void EndPageUp(IAsyncResult result) =>
            ((Task<LeaderboardReader>)result).GetAwaiter().GetResult();

        public void PageDown()
        {
            // The offline reader has no additional pages. Preserve the synchronous XNA ABI
            // without fabricating entries or invoking an asynchronous callback.
        }

        public void PageUp()
        {
            // The offline reader always starts at its only empty page.
        }

        //public IAsyncResult TotalLeaderboardSize()
        //{
        //    return StubUtils.ForeverTask;
        //}


        public Int32 TotalLeaderboardSize => _Entries?.Count ?? 0;

        public bool CanPageDown => false;

        public bool CanPageUp => false;

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            // There is nothing offline to release. Recording the state keeps
            // IsDisposed honest for a title that checks it, and disposing twice
            // stays harmless, which is what the using in the read callback and
            // the one in the page callbacks between them require.
            IsDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
