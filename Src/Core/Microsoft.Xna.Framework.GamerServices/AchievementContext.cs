using Microsoft.EntityFrameworkCore;
using WPR.Common;

using System.IO;

namespace Microsoft.Xna.Framework.GamerServices
{
    public class AchievementContext : DbContext
    {
        public DbSet<Achievement>? Achievements { get; set; }
        private static AchievementContext? _Current;

        private const string DatabasePath = "Database/achievements.db";
        private const string DatabaseFolder = "Database/";

        public static AchievementContext Current
        {
            get
            {
                if (_Current == null)
                {
                    _Current = new AchievementContext();
                }

                return _Current;
            }
        }

        /* Mirrors ApplicationContext: the cached instance binds to whichever data
         * root was configured when it was first asked for, so a test that swaps
         * roots has to drop it. */
        internal static void ResetForTests()
        {
            _Current?.Dispose();
            _Current = null;
        }

        public AchievementContext()
        {
            Directory.CreateDirectory(Configuration.Current.DataPath(DatabaseFolder));
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={Configuration.Current.DataPath(DatabasePath)}");
    }
}
