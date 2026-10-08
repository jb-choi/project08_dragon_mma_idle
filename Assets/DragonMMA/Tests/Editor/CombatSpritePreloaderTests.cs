using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class CombatSpritePreloaderTests
    {
        [Test]
        public void Preload_CoversOnlyCombatSpritesAndTheirTextures()
        {
            var preloader = new CombatSpritePreloader();
            try
            {
                preloader.Preload();
                Assert.That(preloader.Sprites.Count, Is.EqualTo(340));
                Assert.That(preloader.TextureCount, Is.EqualTo(25));
                Assert.That(preloader.Sprites.Count(s => s.name.StartsWith("fighter_")), Is.EqualTo(120));
                Assert.That(preloader.Sprites.Count(s => s.name.StartsWith("dragon_")), Is.EqualTo(220));
                Assert.That(preloader.Sprites.All(s => s.texture != null), Is.True);
                Assert.That(preloader.Sprites.Any(s => s.name.Contains("reference")), Is.False);
            }
            finally { preloader.Clear(); }
        }

        [Test]
        public void RepeatedPreload_RetainsTheSameReferencesWithoutDuplicates()
        {
            var preloader = new CombatSpritePreloader();
            try
            {
                preloader.Preload();
                var first = new HashSet<Sprite>(preloader.Sprites);
                preloader.Preload();
                Assert.That(preloader.Sprites.Count, Is.EqualTo(first.Count));
                Assert.That(first.SetEquals(preloader.Sprites), Is.True);
                Assert.That(preloader.TextureCount, Is.EqualTo(25));
            }
            finally { preloader.Clear(); }
        }

        [Test]
        public void Clear_ReleasesOwnedReferencesAndAllowsAnotherInitialization()
        {
            var preloader = new CombatSpritePreloader();
            try
            {
                preloader.Preload();
                preloader.Clear();
                Assert.That(preloader.Sprites, Is.Empty);
                Assert.That(preloader.TextureCount, Is.Zero);
                preloader.Preload();
                Assert.That(preloader.Sprites.Count, Is.EqualTo(340));
            }
            finally { preloader.Clear(); }
        }

        [Test]
        public void Preload_CoversEverySpriteActuallyReferencedByAuthoredClips()
        {
            var preloader = new CombatSpritePreloader();
            try
            {
                preloader.Preload();
                var loaded = new HashSet<Sprite>(preloader.Sprites);
                var referenced = new HashSet<Sprite>();
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/DragonMMA/Animations" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Assets/DragonMMA/Animations/Environment/")) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                            if (key.value is Sprite sprite) referenced.Add(sprite);
                }
                Assert.That(referenced.Count, Is.EqualTo(340));
                Assert.That(referenced.All(loaded.Contains), Is.True, "A controller uses a sprite that is not preloaded.");
            }
            finally { preloader.Clear(); }
        }
    }
}
