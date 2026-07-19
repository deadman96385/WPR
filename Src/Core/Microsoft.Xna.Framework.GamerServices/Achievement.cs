using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System;

using WPR.Common;

namespace Microsoft.Xna.Framework.GamerServices
{
    public class Achievement
    {
        public Achievement()
        {
        }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key, Column(Order = 0)]
        public int Id { get; set; }

        public string _IconPath { get; set; }

        public string Description { get; set; }

        public bool DisplayBeforeEarned { get; set; }

        public DateTime EarnedDateTime { get; set; }

        public bool EarnedOnline { get; set; }

        public int GamerScore { get; set; }

        public string HowToEarn { get; set; }

        public bool IsEarned { get; set; }

        public string Key { get; set; }

        public string Name { get; set; }

        public string OwnProductId { get; set; }

        /* An achievement earned offline is recorded from the key the title
         * awarded and has no artwork behind it, and a stored icon can go missing
         * with the data root. Titles enumerate achievements and call this while
         * building their list, so throwing here takes down that whole pass over
         * a picture.
         *
         * Reporting the absence was the earlier contract, and the caller has
         * nowhere to put that answer: Plants vs. Zombies passes the result
         * straight to Texture2D.FromStream inside AchievementItem's constructor,
         * so a null throws out of the first item and leaves its list empty,
         * which is the same blank selector screen a missing definition caused.
         * Stand in for the artwork instead, and keep the absence visible by
         * standing in with something plainly not the title's own icon.
         */
        public Stream? GetPicture()
        {
            if (string.IsNullOrEmpty(_IconPath))
            {
                return AchievementDefinitions.OpenPlaceholderPicture();
            }

            string path = Configuration.Current!.DataPath(_IconPath);
            if (!File.Exists(path))
            {
                return AchievementDefinitions.OpenPlaceholderPicture();
            }

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}