using System.Collections.Generic;
using UnityEngine;

namespace DragonMMA
{
    /// <summary>Stable name-based access to the packed art sheets in Resources.</summary>
    public static class DragonArtCatalog
    {
        public const string LegacyDragonSheetResource = "DragonMMA/Art/legacy_dragon_sheet";
        public const string EnvironmentSheetResource = "DragonMMA/Art/environment_sheet";

        public static string FighterSheetResource(string pose) => "DragonMMA/Art/fighter_" + pose + "_sheet";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;

            string resource = name.StartsWith("fighter_")
                ? FighterSheetResource(FighterPose(name))
                : name.StartsWith("dragon_")
                    ? LegacyDragonSheetResource
                    : EnvironmentSheetResource;
            foreach (var sprite in Resources.LoadAll<Sprite>(resource))
                Cache[sprite.name] = sprite;
            Cache.TryGetValue(name, out var result);
            return result;
        }

        private static string FighterPose(string name)
        {
            int start = "fighter_".Length;
            int end = name.LastIndexOf('_');
            return end > start ? name.Substring(start, end - start) : name.Substring(start);
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
