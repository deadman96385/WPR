using System;
using System.Runtime.Serialization;

namespace Microsoft.Xna.Framework.GamerServices
{
    // Nothing here throws this, but the type still has to exist. Glow Artisan's
    // PHGLib.XboxLive.XboxLiveManager.Initialize catches it alongside
    // GameUpdateRequiredException and InvalidOperationException, and a catch
    // clause is resolved when the method is prepared rather than when it runs.
    // A missing type therefore takes out the whole method before its first
    // instruction, so Initialized was never set and the GamerServicesComponent
    // was never added - which is what left LeaderboardUI.GetFriendToBeat
    // indexing an empty collection several screens later.
    [Serializable]
    public class NetworkException : Exception
    {
        public NetworkException()
        {
        }

        public NetworkException(string message)
            : base(message)
        {
        }

        protected NetworkException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public NetworkException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
