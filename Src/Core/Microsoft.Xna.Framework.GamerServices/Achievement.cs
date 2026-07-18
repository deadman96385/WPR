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
         * a picture. Report the absence instead and let the caller decide.
         */
        public Stream? GetPicture()
        {
            if (string.IsNullOrEmpty(_IconPath))
            {
                return null;
            }

            string path = Configuration.Current!.DataPath(_IconPath);
            if (!File.Exists(path))
            {
                return null;
            }

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}