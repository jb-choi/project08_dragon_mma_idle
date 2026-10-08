using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace DragonMMA
{
    // View-owned references: load the small animation frames before the first Tick,
    // not when an Animator first applies a victory/hurt/recovery sprite.
    public sealed class CombatSpritePreloader
    {
        private static readonly ProfilerMarker PreloadMarker = new ProfilerMarker("DragonMMA.CombatSpritePreload");
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(276);
        private readonly HashSet<Texture2D> textures = new HashSet<Texture2D>();
        public IReadOnlyCollection<Sprite> Sprites => sprites.Values;
        public int TextureCount => textures.Count;

        public void Preload()
        {
            using (PreloadMarker.Auto())
            {
                foreach (string pose in FighterCombatTiming.Poses)
                    foreach (var sprite in Resources.LoadAll<Sprite>(DragonArtCatalog.FighterSheetResource(pose)))
                        Retain(sprite);

                foreach (string pose in new[] { "fight_idle", "jab", "first_guard" })
                    foreach (var sprite in Resources.LoadAll<Sprite>(DragonArtCatalog.FighterSheetResource(pose))) Retain(sprite);
                foreach (string pose in new[] { "fight_idle", "light_hit", "heavy_hit", "threat", "yield" })
                    foreach (var sprite in Resources.LoadAll<Sprite>("DragonMMA/DragonSheets/dragon_0_" + pose)) Retain(sprite);

                // Five exact sheets, 36 frames each. No per-Tick loads or broad Resources scan.
                for (int kind = 0; kind <= (int)DragonKind.Giant; kind++)
                    foreach (var sprite in Resources.LoadAll<Sprite>("DragonMMA/DragonSheets/dragon_" + kind))
                        if (!sprite.name.EndsWith("_portrait")) Retain(sprite);
            }
        }

        private void Retain(Sprite sprite)
        {
            if (sprites.ContainsKey(sprite.name)) return;
            Texture2D texture = sprite.texture;
            if (texture == null)
            {
                Debug.LogWarning("[Dragon MMA] Missing combat texture: " + sprite.name);
                return;
            }
            sprites.Add(sprite.name, sprite);
            textures.Add(texture);
        }

        public void Clear()
        {
            sprites.Clear();
            textures.Clear();
            // Do not unload shared assets while another actor/view may still use them.
        }
    }
}
