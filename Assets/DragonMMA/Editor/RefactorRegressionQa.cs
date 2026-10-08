using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    // A before/after characterization gate, not a new gameplay or recording system.
    public static class RefactorRegressionQa
    {
        public const string Root = "Artifacts/Refactor-20261008";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static void Record(string label)
        {
            var game = UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused || game == null ||
                game.DebugSession == null || string.IsNullOrEmpty(DragonSaveSystem.OverridePath))
                throw new InvalidOperationException("Paused graphics Play and isolated save required.");
            Directory.CreateDirectory(Root);
            RecordTimelines(label);
            RecordView(game, label);
        }

        private static void RecordTimelines(string label)
        {
            var output = new StringBuilder();
            var fields = typeof(DragonCombatChoreography.Frame).GetFields().OrderBy(f => f.Name).ToArray();
            int samples = 0;
            foreach (float duration in new[] { 10f, 20f, 30f })
                for (int n = 0; n <= duration * 30; n++)
                {
                    float t = n / 30f;
                    AppendFrame(output, $"baby/{duration}/{n}", BabyFirstBattle.Evaluate(t, duration), fields);
                    output.AppendLine(BabyFirstBattle.BeatLabel(t, duration));
                    samples++;
                }
            foreach (float t in new[] { -.1f, 1.7f, 3.8f, 5.5f, 5.66f, 5.9f, 6.2f, 6.25f, 6.6f, 6.95f, 7.8f, 9.2f, 11.6f, 13.7f, 14.5f, 18f, 20f, 21f })
            {
                AppendFrame(output, "boundary/" + Number(t), BabyFirstBattle.Evaluate(t), fields);
                output.AppendLine(Number(BabyFirstBattle.NextContact(t)));
                samples++;
            }
            for (int kind = 0; kind < 5; kind++)
                foreach (int mask in new[] { 0, 31 })
                {
                    var rewards = Enumerable.Range(0, 5).Select(i => (mask & (1 << i)) != 0).ToArray();
                    for (int n = 0; n <= 200; n++)
                    {
                        AppendFrame(output, $"species/{kind}/{mask}/{n}",
                            DragonCombatChoreography.Evaluate((DragonKind)kind, n / 10f, rewards), fields);
                        samples++;
                    }
                }
            File.WriteAllText(Root + "/" + label + "-timelines.txt", output.ToString());
            File.WriteAllText(Root + "/" + label + "-timeline-count.txt", samples.ToString(Invariant));
        }

        private static void AppendFrame(StringBuilder output, string key, DragonCombatChoreography.Frame frame, FieldInfo[] fields)
        {
            output.Append(key);
            foreach (var field in fields)
            {
                object value = field.GetValue(frame);
                output.Append('|').Append(field.Name).Append('=');
                if (value is float f) output.Append(Number(f));
                else if (value is Vector2 v) output.Append(Number(v.x)).Append(',').Append(Number(v.y));
                else output.Append(value);
            }
            output.AppendLine();
        }

        private static void RecordView(DragonMmaGame game, string label)
        {
            var view = game.DebugView;
            var animate = typeof(DragonMmaView).GetMethod("Animate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (animate == null) throw new InvalidOperationException("Presentation entry point missing.");
            var actors = view.GetComponentsInChildren<DragonActorView>(true);
            var fighter = actors.Single(a => a.name == "Hunter");
            var dragon = actors.Single(a => a.name == "Dragon");
            var bindings = new SerializedObject(view);
            var overlay = (DragonUiBindings)bindings.FindProperty("overlay").objectReferenceValue;
            var output = new StringBuilder();
            int samples = 0;
            var data = game.DebugData;
            var rules = game.DebugSession.Config;
            view.OpenPanel("");
            for (int kind = 0; kind < 5; kind++)
                foreach (int mask in new[] { 0, 31 })
                {
                    for (int i = 0; i < 5; i++) data.trainingRewardsClaimed[i] = (mask & (1 << i)) != 0;
                    data.currentEnemy = kind;
                    data.cart.Clear(); data.cart.Add(new DragonInstance((DragonKind)kind));
                    data.cheerBonus = 2;
                    data.lastWasExtortion = false;
                    for (int n = 0; n <= 200; n++)
                        Sample($"fight/{kind}/{mask}/{n}", HuntPhase.Fighting, n / 10f, rules.BattleSeconds);
                    foreach (bool extortion in new[] { false, true })
                    {
                        data.lastWasExtortion = extortion;
                        foreach (float p in new[] { 0f, .01f, .04f, .09f, .4f, .54f, .55f, .65f, .71f, .72f, .8f, .94f, .99f, 1f })
                            Sample($"result/{kind}/{mask}/{extortion}/{Number(p)}", HuntPhase.Result, p * rules.WinResultSeconds, rules.WinResultSeconds);
                    }
                    foreach (float t in new[] { 0f, .05f, .5f, .99f, 1f, 1.1f, 1.3f, 1.5f, 5f, 9f, 9.6f, 10f })
                        Sample($"recovery/{kind}/{mask}/{Number(t)}", HuntPhase.Recovery, t, rules.RecoverySeconds);
                    data.currentEnemy = -1;
                    foreach (float t in new[] { 0f, .5f, 1f, 1.5f })
                        Sample($"walk/{kind}/{mask}/{Number(t)}", HuntPhase.Walking, t, rules.WalkSeconds);
                }
            File.WriteAllText(Root + "/" + label + "-view.txt", output.ToString());
            File.WriteAllText(Root + "/" + label + "-view-count.txt", samples.ToString(Invariant));

            void Sample(string key, HuntPhase phase, float elapsed, float duration)
            {
                data.phase = phase; data.phaseRemaining = duration - elapsed;
                view.Refresh(true); view.RefreshLayout(640, 240);
                // Explicit clock holds actor/world motion constant across separate Editor runs.
                // Speed-line pulse color still uses Time.unscaledTime, and is excluded below.
                animate.Invoke(view, new object[] { 100f + elapsed, 0f });
                Canvas.ForceUpdateCanvases();
                output.Append(key);
                Actor(fighter); Actor(dragon);
                foreach (string name in new[] { "Technique", "Fighter name", "Dragon card" })
                    output.Append('|').Append(name).Append('=').Append(view.GetComponentsInChildren<Text>(true).Single(t => t.name == name).text);
                foreach (string path in new[] { "Hunt/Notification", "Hunt/PrisonCart/Passenger0", "Hunt/Contact sparks", "Hunt/Dragon name plate" })
                {
                    var rect = overlay.Get<RectTransform>(path);
                    output.Append('|').Append(path).Append('=').Append(rect.gameObject.activeSelf)
                        .Append(',').Append(Rounded(rect.anchoredPosition.x)).Append(',').Append(Rounded(rect.anchoredPosition.y));
                }
                output.AppendLine(); samples++;
            }
            void Actor(DragonActorView actor)
            {
                var rect = (RectTransform)actor.transform;
                var image = actor.Image;
                var visual = image.rectTransform;
                output.Append('|').Append(actor.name).Append('=').Append(actor.gameObject.activeSelf)
                    .Append(',').Append(image.sprite.name)
                    .Append(',').Append(Rounded(rect.anchoredPosition.x)).Append(',').Append(Rounded(rect.anchoredPosition.y))
                    .Append(',').Append(Rounded(rect.localEulerAngles.z))
                    .Append(',').Append(Rounded(visual.sizeDelta.x)).Append(',').Append(Rounded(visual.sizeDelta.y))
                    .Append(',').Append(Rounded(visual.localScale.x)).Append(',').Append(Rounded(visual.localScale.y))
                    .Append(',').Append(Rounded(visual.localEulerAngles.z))
                    .Append(',').Append(Rounded(image.color.r)).Append(',').Append(Rounded(image.color.g))
                    .Append(',').Append(Rounded(image.color.b)).Append(',').Append(Rounded(image.color.a));
            }
        }

        public static void Compare()
        {
            foreach (string category in new[] { "timelines", "view" })
            {
                var before = File.ReadAllLines(Root + "/before-" + category + ".txt");
                var after = File.ReadAllLines(Root + "/after-" + category + ".txt");
                int mismatch = Enumerable.Range(0, Math.Min(before.Length, after.Length)).Where(i => before[i] != after[i]).DefaultIfEmpty(-1).First();
                if (before.Length != after.Length || mismatch >= 0)
                {
                    File.WriteAllText(Root + "/comparison-failure.txt", category + " mismatch line=" + mismatch + "\n" +
                        (mismatch >= 0 ? before[mismatch] + "\n" + after[mismatch] : "line counts differ"));
                    throw new InvalidOperationException("Characterization regression in " + category + ", line " + mismatch);
                }
            }
            File.WriteAllText(Root + "/comparison.txt", "PASS: exact shared choreography fields + main actor/UI snapshots (geometry rounded to 0.001).\n" +
                "timeline samples=" + File.ReadAllText(Root + "/after-timeline-count.txt") + "\nview samples=" + File.ReadAllText(Root + "/after-view-count.txt") + "\n" +
                "Graphics Play, independently sampled isolated state; not a continuous gameplay/input/video check. Speed-line pulse color excluded.\n");
            Debug.Log("[Refactor QA] Before/after characterization PASS");
        }

        private static string Number(float value) => value.ToString("R", Invariant);
        private static string Rounded(float value) => value.ToString("F3", Invariant);
    }
}
