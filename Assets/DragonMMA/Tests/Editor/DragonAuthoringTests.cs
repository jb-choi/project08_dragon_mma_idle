using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DragonMMA.Tests
{
    public sealed class DragonAuthoringTests
    {
        private DragonGameConfig config;
        [SetUp] public void SetUp() => config = ScriptableObject.CreateInstance<DragonGameConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);
        private void Set(string key, float value)
        {
            var so = new SerializedObject(config); var p = so.FindProperty(key);
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private void Spec(string key, float value)
        {
            var so = new SerializedObject(config); var p = so.FindProperty("dragons").GetArrayElementAtIndex(0).FindPropertyRelative(key);
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        [Test] public void NewSave_UsesAuthoredStartingStateAndWalkDuration()
        {
            Set("startingCoins", 123); Set("startingPower", 4); Set("walkSeconds", 7);
            var session = new DragonGameSession(null, new System.Random(0), config);
            Assert.That(session.Data.coins, Is.EqualTo(123));
            Assert.That(session.Data.fighterPower, Is.EqualTo(4));
            Assert.That(session.Data.phaseRemaining, Is.EqualTo(7));
        }
        [Test] public void BattleAndCheer_UseAuthoredValues()
        {
            Set("battleSeconds", 6); Set("cheerPerInput", 1.5f); Set("maxCheerBonus", 7);
            var session = new DragonGameSession(null, null, config);
            session.DebugForceEncounter(DragonKind.Baby); session.Cheer(2);
            Assert.That(session.Data.phaseRemaining, Is.EqualTo(6));
            Assert.That(session.Data.cheerBonus, Is.EqualTo(3));
            session.Cheer(99); Assert.That(session.Data.cheerBonus, Is.EqualTo(7));
        }
        [Test] public void Training_UsesAuthoredCostDurationAndRewards()
        {
            Set("startingCoins", 37); Set("trainingUnlockCost", 37);
            Spec("trainingSeconds", 9); Spec("coreReward", 17); Spec("powerReward", 6);
            var session = new DragonGameSession(null, null, config);
            var dragon = new DragonInstance(DragonKind.Baby); session.Data.storage.Add(dragon);
            Assert.That(session.UnlockTraining(), Is.True); Assert.That(session.Data.coins, Is.Zero);
            Assert.That(session.AssignTraining(dragon.id, 100), Is.True);
            Assert.That(session.Data.training[0].endUtc, Is.EqualTo(109));
            session.ProcessTimers(109); Assert.That(session.Data.cores, Is.EqualTo(17));
            Assert.That(session.Data.fighterPower, Is.EqualTo(16));
            session.ProcessTimers(10000); Assert.That(session.Data.cores, Is.EqualTo(17));
        }
        [Test] public void Market_UsesAuthoredPriceAndDuration()
        {
            Spec("salePrice", 321); Spec("saleSeconds", 11);
            var session = new DragonGameSession(null, null, config);
            var dragon = new DragonInstance(DragonKind.Baby); session.Data.storage.Add(dragon);
            session.RegisterSale(dragon.id, 0, 100);
            Assert.That(session.Data.sales[0].endUtc, Is.EqualTo(111));
            session.ProcessTimers(111); Assert.That(session.Data.coins, Is.Zero);
            Assert.That(session.ClaimSale(0), Is.True); Assert.That(session.Data.coins, Is.EqualTo(321));
        }
        [Test] public void ConfiguredCartCapacity_IsNotRepairedBackToThree()
        {
            Set("cartCapacity", 8);
            var session = new DragonGameSession(null, null, config);
            for (int i = 0; i < 7; i++) session.Data.cart.Add(new DragonInstance(DragonKind.Baby));
            session.Data.EnsureDefaults(session.Config);
            Assert.That(session.Data.cart.Count, Is.EqualTo(7)); Assert.That(session.Data.storage, Is.Empty);
            session.DebugForceEncounter(DragonKind.Baby); session.Tick(20, 100);
            Assert.That(session.Data.cart.Count, Is.EqualTo(8));
            session.DebugForceEncounter(DragonKind.Baby); session.Tick(20, 100);
            Assert.That(session.Data.lastWasExtortion, Is.True);
        }
        [Test] public void SessionSnapshot_DoesNotMutateOrFollowAssetEdits()
        {
            var session = new DragonGameSession(null, null, config);
            session.GetSpec(DragonKind.Baby).salePrice = 999;
            Assert.That(config.GetSpec(DragonKind.Baby).salePrice, Is.EqualTo(40));
            Set("battleSeconds", 9);
            Assert.That(session.Config.BattleSeconds, Is.EqualTo(20));
            Assert.That(new DragonGameSession(null, null, config).Config.BattleSeconds, Is.EqualTo(9));
        }
        [Test] public void ExistingSaveProgress_IsNotResetByStartingSettings()
        {
            var data = new GameSaveData { coins = 345, fighterPower = 22 };
            Set("startingCoins", 900); Set("startingPower", 100);
            var session = new DragonGameSession(data, null, config);
            Assert.That(session.Data.coins, Is.EqualTo(345)); Assert.That(session.Data.fighterPower, Is.EqualTo(22));
        }
        [Test] public void AuthoredPrefab_AllBindingsAreUniqueAndInternal()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab");
            Assert.That(root, Is.Not.Null);
            var view = root.GetComponent<DragonMmaView>(); Assert.That(view, Is.Not.Null);
            foreach (var bindings in root.GetComponentsInChildren<DragonUiBindings>(true))
            {
                Assert.That(bindings.Elements.Select(e => e.key).Distinct().Count(), Is.EqualTo(bindings.Elements.Count), bindings.name);
                foreach (var entry in bindings.Elements)
                { Assert.That(entry.target, Is.Not.Null, entry.key); Assert.That(entry.target.transform.IsChildOf(root.transform), Is.True, entry.key); }
            }
            Assert.That(root.GetComponentsInChildren<DragonActorView>(true).Length, Is.EqualTo(4));
            foreach (string field in new[] { "overlay", "storage", "trainingLocked", "trainingEmpty", "trainingActive", "market", "skills", "settings", "quit", "hunter", "dragon", "helper", "trainee" })
                Assert.That(new SerializedObject(view).FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            foreach (var tx in root.GetComponentsInChildren<Text>(true))
            { Assert.That(tx.font, Is.Not.Null); Assert.That(tx.font.material, Is.Not.Null); Assert.That(AssetDatabase.GetAssetPath(tx.font), Does.EndWith(".ttf")); }
        }
        [Test] public void InitializationAndRefresh_PreserveAuthoredLayoutAndStyling_UpdatesStatusText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab");
            var root = Object.Instantiate(prefab); var host = new GameObject("Authoring test host");
            try
            {
                var view = root.GetComponent<DragonMmaView>(); var bindings = root.GetComponent<DragonUiBindings>();
                var button = bindings.Get<Image>("Hunt/Bottom toolbar/Camp");
                var position = button.rectTransform.anchoredPosition + new Vector2(19, 7);
                button.rectTransform.anchoredPosition = position; button.color = Color.magenta;
                var label = button.GetComponentInChildren<Text>(); label.text = "수정한 고정 문구";
                int childCount = root.GetComponentsInChildren<Transform>(true).Length;
                Set("cheerPerInput", 1.5f);
                view.Initialize(host.AddComponent<DragonMmaGame>(), new DragonGameSession(null, null, config), null);
                foreach (string page in new[] { "storage", "training", "market", "skills", "settings", "exit", "" }) view.OpenPanel(page);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
                Assert.That(button.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(button.color, Is.EqualTo(Color.magenta)); Assert.That(label.text, Is.EqualTo("거점 0 ↑"));
                Assert.That(bindings.Get<Text>("Hunt/Bottom toolbar/Input hint").text, Does.Contain("+1.5%p"));
                view.Dispose();
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(root); }
        }
        [Test] public void ActorMotion_IsRelativeToAuthoredPosition()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/Actors/Hunter.prefab"));
            try
            {
                var rect = root.GetComponent<RectTransform>(); var actor = root.GetComponent<DragonActorView>();
                rect.anchoredPosition = new Vector2(230, 45); rect.localEulerAngles = new Vector3(0, 0, 12);
                actor.Initialize(); actor.Offset(new Vector2(8, 9), 5);
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(238, 54)));
                Assert.That(rect.localEulerAngles.z, Is.EqualTo(12).Within(.01));
                Assert.That(actor.Image.rectTransform.localEulerAngles.z, Is.EqualTo(5).Within(.01));
                actor.Offset(Vector2.zero); Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(230, 45)));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void AnimationAssets_HavePersistentSpriteCurvesAndExpectedStates()
        {
            string[] paths = { "Hunter", "Dragon0", "Dragon1", "Dragon2", "Dragon3", "Dragon4" };
            foreach (string name in paths)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/DragonMMA/Animations/" + name + ".controller");
                Assert.That(controller, Is.Not.Null, name);
                Assert.That(controller.layers[0].stateMachine.states.Length, Is.EqualTo(name == "Hunter" ? FighterCombatTiming.Poses.Length + 3 : DragonAnimationTiming.Poses.Length + (name == "Dragon0" ? 5 : 0)));
                foreach (var clip in controller.animationClips)
                {
                    var curve = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                    Assert.That(curve.type, Is.EqualTo(typeof(Image)));
                    Assert.That(curve.path, Is.Empty);
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, curve);
                    string pose = clip.name.StartsWith("fighter_") ? clip.name.Substring(8) : clip.name.Substring(9);
                    if (pose == "fight_idle" || pose == "jab" || pose == "light_hit" || pose == "heavy_hit" || pose == "threat" || pose == "first_guard" || pose == "yield")
                    {
                        Assert.That(keys.Length, Is.EqualTo(pose == "fight_idle" ? 9 : 8));
                        foreach (var key in keys) Assert.That(key.value, Is.TypeOf<Sprite>());
                        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.EqualTo(pose == "fight_idle"));
                        continue; // Dedicated BasicCombatTests validate the new timing/contact contract.
                    }
                    bool loops = name == "Hunter" ? FighterCombatTiming.Loops(pose) : DragonAnimationTiming.Loops(pose);
                    Assert.That(keys.Length, Is.EqualTo(name == "Hunter" ? loops ? 9 : 8 : DragonAnimationTiming.KeyCount(pose)));
                    foreach (var key in keys) Assert.That(key.value, Is.TypeOf<Sprite>());
                    Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.EqualTo(loops));
                    if (name == "Hunter") Assert.That(keys.Last().time, Is.EqualTo(FighterCombatTiming.Duration(pose)).Within(.001));
                    else Assert.That(keys.Last().time, Is.EqualTo(DragonAnimationTiming.Duration((DragonKind)(name[6] - '0'), pose)).Within(.001));
                }
            }
        }
        [TestCase(30)] [TestCase(60)]
        public void FighterContacts_AreSingleAndFrameRateIndependent(int fps)
        {
            var gate = new FighterContactGate(); var rewards = new bool[5]; int contacts = 0;
            for (int i = 0; i < fps * 9.6f; i++)
            {
                float elapsed = i / (float)fps;
                int cycle = (int)(elapsed / FighterCombatTiming.CycleSeconds);
                float beat = elapsed - cycle * FighterCombatTiming.CycleSeconds;
                if (beat >= 2.1f) continue;
                bool defense = beat >= FighterCombatTiming.TurnSeconds;
                float contact = defense ? 1.575f : FighterCombatTiming.Contact(FighterCombatTiming.Attack(cycle, rewards));
                if (gate.TryContact(elapsed, cycle * 2 + (defense ? 1 : 0), cycle * 2.4f + contact)) contacts++;
            }
            Assert.That(contacts, Is.EqualTo(8));
        }
        [Test] public void FighterContactGate_ResetsOnNewBattleAndDoesNotReplayStaleSound()
        {
            var gate = new FighterContactGate();
            Assert.That(gate.TryContact(.23f, 0, .22f), Is.True);
            Assert.That(gate.TryContact(.24f, 0, .22f), Is.False);
            gate.Reset();
            Assert.That(gate.TryContact(.23f, 0, .22f), Is.True);
            Assert.That(gate.TryContact(8, 6, 7.4f), Is.False);
            Assert.That(gate.TryContact(.23f, 0, .22f), Is.True);
        }
        [Test] public void FighterHitStop_HoldsClockAndTravelThenRecovers()
        {
            foreach (string pose in new[] { "punch", "cross", "kick", "clinch", "takedown" })
            {
                float contact = FighterCombatTiming.Contact(pose);
                Assert.That(FighterCombatTiming.FrameTime(pose, 3), Is.EqualTo(contact));
                Assert.That(FighterCombatTiming.HeldTime(contact + .025f, contact), Is.EqualTo(contact));
                float expectedContactTravel = FighterCombatTiming.IsInPlaceStrike(pose) ? 0f : 1f;
                Assert.That(FighterCombatTiming.Travel(pose, contact), Is.EqualTo(expectedContactTravel));
                Assert.That(FighterCombatTiming.Travel(pose, FighterCombatTiming.Duration(pose)), Is.EqualTo(0).Within(.001));
                Assert.That(FighterCombatTiming.Loops(pose), Is.False);
            }
        }
        [Test] public void FighterSkillSelection_PreservesUnlockOrder()
        {
            var rewards = new bool[5];
            Assert.That(FighterCombatTiming.Attack(0, rewards), Is.EqualTo("punch"));
            Assert.That(FighterCombatTiming.Attack(1, rewards), Is.EqualTo("kick"));
            rewards[1] = true; Assert.That(FighterCombatTiming.Attack(0, rewards), Is.EqualTo("cross"));
            rewards[3] = true; Assert.That(FighterCombatTiming.Attack(2, rewards), Is.EqualTo("clinch"));
            rewards[4] = true; Assert.That(FighterCombatTiming.Attack(2, rewards), Is.EqualTo("takedown"));
        }
        [Test] public void FighterAssets_ArePointFilteredRegisteredAndColorKeySafe()
        {
            foreach (string pose in FighterCombatTiming.Poses)
            {
                string path = EditorTools.ArtSpriteSheetMigration.FighterSheetPath(pose);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(sprites.Count, Is.EqualTo(8), path);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(System.IO.File.ReadAllBytes(path));
                var sheetPixels = texture.GetPixels32();
                try
                {
                    for (int frame = 0; frame < 8; frame++)
                    {
                        string name = "fighter_" + pose + "_" + frame;
                        Assert.That(sprites.TryGetValue(name, out var sprite), Is.True, name);
                        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(192, 128)));
                        Assert.That(sprite.pivot, Is.EqualTo(new Vector2(96, 8)));
                        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(importer.mipmapEnabled, Is.False);
                        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                        int ox = Mathf.RoundToInt(sprite.rect.x), oy = Mathf.RoundToInt(sprite.rect.y);
                        for (int y = 0; y < 128; y++)
                        {
                            Assert.That(sheetPixels[(oy + y) * texture.width + ox].a, Is.Zero, name);
                            Assert.That(sheetPixels[(oy + y) * texture.width + ox + 191].a, Is.Zero, name);
                        }
                        for (int y = 0; y < 128; y++) for (int x = 0; x < 192; x++)
                        {
                            var p = sheetPixels[(oy + y) * texture.width + ox + x];
                            Assert.That(p.a == 0 || p.a == 255, Is.True, name);
                            Assert.That(p.a == 255 && p.r == 0 && p.g == 0 && p.b == 0, Is.False, name);
                        }
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }
    }
}
