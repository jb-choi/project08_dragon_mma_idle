using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DragonMMA.EditorTools;

namespace DragonMMA.Tests
{
    public sealed class AnimationQualityTests
    {
        [TestCase("punch")] [TestCase("cross")] [TestCase("kick")]
        public void FighterStrikes_UseAuthoredReachWithoutRootSliding(string pose)
        {
            for (int i = 0; i <= 32; i++)
                Assert.That(FighterCombatTiming.Travel(pose, FighterCombatTiming.Duration(pose) * i / 32f),
                    Is.EqualTo(0).Within(.001f), pose + " must stay planted at authored combat range");
        }

        [TestCase("clinch")] [TestCase("takedown")]
        public void GrapplingApproach_PlantsBeforeContactAndDoesNotSlideIntoHit(string pose)
        {
            float contact = FighterCombatTiming.Contact(pose);
            float planted = FighterCombatTiming.Travel(pose, contact - AnimationMotionQuality.ApproachPlantLeadSeconds);
            float nearContact = FighterCombatTiming.Travel(pose, contact - .02f);
            float atContact = FighterCombatTiming.Travel(pose, contact);
            Assert.That(planted, Is.EqualTo(1).Within(.001f));
            Assert.That(nearContact, Is.EqualTo(atContact).Within(.001f), "Last 20 ms must be a planted strike, not root sliding.");
        }

        [Test]
        public void FighterApproach_IsContinuousAndReturnsToAuthoredRoot()
        {
            foreach (string pose in new[] { "clinch", "takedown" })
            {
                float previous = FighterCombatTiming.Travel(pose, 0);
                for (int i = 1; i <= 240; i++)
                {
                    float time = FighterCombatTiming.Duration(pose) * i / 240f;
                    float current = FighterCombatTiming.Travel(pose, time);
                    Assert.That(Mathf.Abs(current - previous), Is.LessThan(.08f), pose + " discontinuity at " + time);
                    previous = current;
                }
                Assert.That(previous, Is.EqualTo(0).Within(.001f));
            }
        }

        [Test]
        public void WalkPresentation_IsFootAnchoredBoundedAndLoopSeamless()
        {
            float duration = FighterCombatTiming.Duration("walk");
            var first = AnimationMotionQuality.FighterWalk(0, duration);
            var last = AnimationMotionQuality.FighterWalk(duration, duration);
            Assert.That(first.Scale, Is.EqualTo(last.Scale));
            Assert.That(first.Rotation, Is.EqualTo(last.Rotation).Within(.001f));
            for (int i = 0; i <= 64; i++)
            {
                var pose = AnimationMotionQuality.FighterWalk(duration * i / 64f, duration);
                Assert.That(pose.Scale, Is.EqualTo(Vector2.one));
                Assert.That(pose.Rotation, Is.EqualTo(0));
            }
        }

        [Test]
        public void WalkSheet_HasEightDistinctFramesWithTwoAirborneBeats()
        {
            string path = FighterWalkQualityUpgrade.SheetPath;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            Assert.That(sprites.Length, Is.EqualTo(8));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
            try
            {
                Color32[] pixels = texture.GetPixels32();
                int[] hashes = new int[8];
                var bodyCenters = new Vector2[8];
                var headCenters = new Vector2[8];
                var bounds = new RectInt[8];
                for (int frame = 0; frame < 8; frame++)
                {
                    int floor = 128;
                    int minX = 192, maxX = 0, maxY = 0;
                    float bodyX = 0, bodyY = 0, headX = 0, headY = 0; int bodyCount = 0, headCount = 0;
                    unchecked
                    {
                        int hash = 17;
                        for (int y = 0; y < 128; y++) for (int x = 0; x < 192; x++)
                        {
                            Color32 pixel = pixels[y * texture.width + frame * 192 + x];
                            if (pixel.a > 0)
                            {
                                floor = Mathf.Min(floor, y); bodyX += x; bodyY += y; bodyCount++;
                                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                                if (y > 88) { headX += x; headY += y; headCount++; }
                            }
                            hash = hash * 31 + pixel.a;
                        }
                        hashes[frame] = hash;
                    }
                    Assert.That(floor, Is.EqualTo(AnimationMotionQuality.WalkFloorPixel(frame)),
                        $"frame {frame} must be {AnimationMotionQuality.WalkBeat(frame)}");
                    bodyCenters[frame] = new Vector2(bodyX / bodyCount, bodyY / bodyCount);
                    headCenters[frame] = new Vector2(headX / headCount, headY / headCount);
                    bounds[frame] = new RectInt(minX, floor, maxX - minX + 1, maxY - floor + 1);
                }
                Assert.That(hashes.Distinct().Count(), Is.EqualTo(8), "Walk animation must not contain duplicate silhouettes.");
                for (int frame = 0; frame < 8; frame++)
                {
                    int next = (frame + 1) % 8;
                    Assert.That(Vector2.Distance(bodyCenters[frame], bodyCenters[next]), Is.LessThanOrEqualTo(10), $"body pop {frame}->{next}");
                    Assert.That(Vector2.Distance(headCenters[frame], headCenters[next]), Is.LessThanOrEqualTo(9), $"head pop {frame}->{next}");
                    Assert.That(Mathf.Abs(bounds[frame].height - bounds[next].height), Is.LessThanOrEqualTo(10), $"height pop {frame}->{next}");
                    Assert.That(Mathf.Abs(bounds[frame].width - bounds[next].width), Is.LessThanOrEqualTo(14), $"width pop {frame}->{next}");
                }
                Assert.That(bounds[2].y, Is.EqualTo(AnimationMotionQuality.WalkAirborneFloorPixel));
                Assert.That(bounds[6].y, Is.EqualTo(AnimationMotionQuality.WalkAirborneFloorPixel));
                Assert.That(bodyCenters.Max(center => center.y) - bodyCenters.Min(center => center.y), Is.InRange(4, 14),
                    "MMA approach needs a visible but restrained center-of-mass bounce.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void WalkGroundTravel_PausesOnLandingAndCatchesUpDuringFlight()
        {
            float duration = FighterCombatTiming.Duration("walk");
            const int samples = 4096;
            float distance = 0, dt = duration / samples;
            for (int i = 0; i < samples; i++)
                distance += AnimationMotionQuality.RecommendedWalkGroundSpeed *
                    AnimationMotionQuality.WalkSpeedMultiplier((i + .5f) * dt, duration) * dt;
            Assert.That(distance, Is.EqualTo(AnimationMotionQuality.RecommendedWalkGroundSpeed * duration).Within(.02f),
                "Bounce timing must not change average hunt speed.");
            Assert.That(AnimationMotionQuality.WalkSpeedMultiplier(duration * 3 / 8f, duration), Is.LessThanOrEqualTo(.2f));
            Assert.That(AnimationMotionQuality.WalkSpeedMultiplier(duration * 7 / 8f, duration), Is.LessThanOrEqualTo(.2f));
            Assert.That(AnimationMotionQuality.WalkSpeedMultiplier(duration * 2 / 8f, duration), Is.GreaterThanOrEqualTo(2f));
            Assert.That(AnimationMotionQuality.WalkSpeedMultiplier(duration * 6 / 8f, duration), Is.GreaterThanOrEqualTo(2f));
        }

        [Test]
        public void WalkBounceSheet_PassesMechanicsValidator()
        {
            Assert.That(FighterWalkQualityUpgrade.ValidateBounceMechanics(), Does.StartWith("Bounce QA PASS"));
        }

        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void GameplayWalkSpeed_MatchesAuthoredStride(string prefab)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefab + ".prefab");
            var serialized = new SerializedObject(root.GetComponent<DragonMmaView>());
            Assert.That(serialized.FindProperty("scrollSpeed").floatValue,
                Is.EqualTo(AnimationMotionQuality.RecommendedWalkGroundSpeed).Within(.001f));
        }

        [Test]
        public void PunchAndKickChoreography_StayAtOneCombatRoot()
        {
            var rewards = new bool[5];
            float cycleStart = 0;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                string pose = DragonCombatChoreography.Counter(DragonKind.Baby, cycle, rewards);
                float counter = cycleStart + DragonCombatChoreography.CounterStart(DragonKind.Baby, cycle);
                float expected = AnimationMotionQuality.FighterCombatStanceOffset;
                foreach (float local in new[] { 0f, FighterCombatTiming.Contact(pose) * .5f, FighterCombatTiming.Contact(pose), FighterCombatTiming.Duration(pose) })
                {
                    var frame = DragonCombatChoreography.Evaluate(DragonKind.Baby, counter + local, rewards);
                    Assert.That(frame.FighterOffset.x, Is.EqualTo(expected).Within(.001f), pose + " root moved during strike");
                    Assert.That(frame.FighterTravel, Is.EqualTo(0).Within(.001f));
                }
                cycleStart += DragonCombatChoreography.CycleSeconds(DragonKind.Baby, cycle);
            }
        }

        [Test]
        public void ImpactSprites_AreDistinctColoredAuthoredAssets()
        {
            string[] paths =
            {
                CombatAnimationPolishUpgrade.WeakPath, CombatAnimationPolishUpgrade.HeavyPath,
                CombatAnimationPolishUpgrade.GuardPath, CombatAnimationPolishUpgrade.KnockdownPath
            };
            var hashes = new System.Collections.Generic.HashSet<int>();
            foreach (string path in paths)
            {
                Assert.That(File.Exists(path), Is.True, path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                try
                {
                    int hash = 17, colored = 0, opaque = 0;
                    foreach (Color32 pixel in texture.GetPixels32())
                    {
                        if (pixel.a < 16) continue;
                        opaque++;
                        if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b)) > 18) colored++;
                        unchecked { hash = hash * 31 + pixel.r * 7 + pixel.g * 5 + pixel.b * 3 + pixel.a; }
                    }
                    Assert.That(opaque, Is.GreaterThan(500), path + " has too little readable shape");
                    Assert.That(colored, Is.GreaterThan(opaque / 8), path + " regressed to a monochrome placeholder");
                    Assert.That(hashes.Add(hash), Is.True, path + " duplicates another effect");
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
    }
}
