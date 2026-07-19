using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Xna.Framework.GamerServices
{
    internal sealed class AchievementDefinition
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("gamerScore")]
        public int GamerScore { get; set; }
    }

    internal sealed class AchievementProduct
    {
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;

        [JsonPropertyName("achievements")]
        public List<AchievementDefinition> Achievements { get; set; } = new();
    }

    internal sealed class AchievementDefinitionFile
    {
        [JsonPropertyName("products")]
        public List<AchievementProduct> Products { get; set; } = new();
    }

    /* The set of achievements a title's Live definition would have returned.
     *
     * The store behind AchievementContext only ever holds what a title has
     * awarded, so on a fresh install it is empty. That is not what the phone
     * did: Live returned the whole set, earned and unearned, and titles built
     * their achievements screen straight from it. Plants vs. Zombies looks each
     * of its eighteen achievements up by key and dereferences the result with no
     * null check, so an empty set threw a NullReferenceException out of
     * AchievementsWidget.Draw between a BeginFrame and its End, which left the
     * sprite batch begun and blanked the whole selector screen behind it.
     *
     * Keys come out of the title's own assembly rather than being invented, and
     * fields the emulator has no source for are left unset rather than guessed.
     */
    internal static class AchievementDefinitions
    {
        private const string DefinitionsResource = "Assets.achievement-definitions.json";
        private const string PlaceholderResource = "Assets.achievement-placeholder.png";

        private static readonly Lazy<Dictionary<string, List<AchievementDefinition>>> Products =
            new(LoadProducts, isThreadSafe: true);

        public static IReadOnlyList<AchievementDefinition> ForProduct(string? productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return Array.Empty<AchievementDefinition>();
            }

            return Products.Value.TryGetValue(productId, out List<AchievementDefinition>? found)
                ? found
                : Array.Empty<AchievementDefinition>();
        }

        /* Titles hand the picture straight to Texture2D.FromStream while building
         * their list, so a null takes the whole pass down and the list stays
         * empty - the same failure the missing definitions caused. Stand in for
         * artwork the emulator does not have instead of reporting its absence to
         * a caller that has nowhere to put the answer.
         */
        public static Stream? OpenPlaceholderPicture() => OpenResource(PlaceholderResource);

        private static Dictionary<string, List<AchievementDefinition>> LoadProducts()
        {
            var products = new Dictionary<string, List<AchievementDefinition>>(
                StringComparer.OrdinalIgnoreCase);

            using Stream? stream = OpenResource(DefinitionsResource);
            if (stream is null)
            {
                return products;
            }

            AchievementDefinitionFile? file =
                JsonSerializer.Deserialize<AchievementDefinitionFile>(stream);
            if (file is null)
            {
                return products;
            }

            foreach (AchievementProduct product in file.Products)
            {
                if (!string.IsNullOrEmpty(product.ProductId))
                {
                    products[product.ProductId] = product.Achievements;
                }
            }

            return products;
        }

        private static Stream? OpenResource(string suffix)
        {
            Assembly assembly = typeof(AchievementDefinitions).Assembly;

            // Matched by suffix so the manifest prefix stays an implementation
            // detail of how the project names embedded resources.
            string? name = assembly.GetManifestResourceNames()
                .FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));

            return name is null ? null : assembly.GetManifestResourceStream(name);
        }
    }
}
