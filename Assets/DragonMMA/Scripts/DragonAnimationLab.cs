using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DragonMMA
{
    // Presentation-only sandbox. Never creates a game session or reads/writes a save.
    public sealed class DragonAnimationLab : MonoBehaviour
    {
        public enum PreviewMode { Clips, Combat, Practice, Assets, BasicJab }
        public static readonly float[] PlaybackSpeeds = { .25f, .5f, 1f, 2f };
        private static readonly string[] BasicFighterPoses = { "fight_idle", "jab" };
        private static readonly string[] BasicDragonPoses = { "fight_idle", "light_hit", "heavy_hit", "threat" };
        [SerializeField] private DragonActorView fighter, dragon;
        [SerializeField] private Canvas canvas;
        [SerializeField] private AnimationLabAssetPreview assetPreview;
        [SerializeField] private Text stageCaption;
        [SerializeField] private Text status, playLabel, loopLabel, unlockLabel;
        [SerializeField] private Slider timeline;
        [SerializeField] private Button[] modeButtons, speciesButtons, fighterButtons, dragonButtons, speedButtons;
        [SerializeField] private Button playButton, restartButton, previousButton, nextButton, contactButton, loopButton, unlockButton;
        [SerializeField] private PreviewMode mode = PreviewMode.Combat;
        [SerializeField] private DragonKind kind = DragonKind.Baby;
        [SerializeField] private bool playing = true, loop = true, allSkills;
        [SerializeField] private float speed = 1;
        private readonly List<KeyValuePair<Button, UnityAction>> listeners = new List<KeyValuePair<Button, UnityAction>>();
        private readonly bool[] rewards = new bool[5];
        private readonly CombatSpritePreloader preloader = new CombatSpritePreloader();
        private string fighterPose = "idle", dragonPose = "idle", cue = "";
        private float elapsed, duration = 1, fighterClipDuration = 1, dragonClipDuration = 1;
        private bool initialized;
        public PreviewMode Mode => mode;
        public DragonKind Kind => kind;
        public float Elapsed => elapsed;
        public float Duration => duration;
        public float Speed => speed;
        public bool IsPlaying => playing;
        public bool Loop => loop;
        public bool AllSkills => allSkills;
        public DragonActorView Fighter => fighter;
        public DragonActorView Dragon => dragon;
        public AnimationLabAssetPreview AssetPreview => assetPreview;

        private void Awake()
        {
            if (fighter == null || dragon == null || canvas == null)
            {
                Debug.LogError("[Animation Lab] Missing authored actor/canvas references.", this);
                enabled = false; return;
            }
            preloader.Preload();
            fighter.Initialize(); dragon.Initialize(); dragon.SetDragonKind(kind);
            if (assetPreview != null) assetPreview.Initialize();
            if (mode == PreviewMode.Assets && (assetPreview == null || !assetPreview.IsReady)) mode = PreviewMode.Combat;
            for (int i = 0; i < rewards.Length; i++) rewards[i] = allSkills;
            initialized = true; UpdateDuration(); Render();
        }
        private void OnEnable()
        {
            BindArray(modeButtons, i => SetMode((PreviewMode)i));
            BindArray(speciesButtons, i => SetSpecies((DragonKind)i));
            BindArray(fighterButtons, i => SelectFighterPose(i < FighterCombatTiming.Poses.Length ? FighterCombatTiming.Poses[i] : BasicFighterPoses[i-FighterCombatTiming.Poses.Length]));
            BindArray(dragonButtons, i => SelectDragonPose(i < DragonAnimationTiming.Poses.Length ? DragonAnimationTiming.Poses[i] : BasicDragonPoses[i-DragonAnimationTiming.Poses.Length]));
            BindArray(speedButtons, i => SetSpeed(PlaybackSpeeds[i]));
            Bind(playButton, () => SetPlaying(!playing)); Bind(restartButton, Restart);
            Bind(previousButton, () => Step(-1)); Bind(nextButton, () => Step(1));
            Bind(contactButton, JumpToContact); Bind(loopButton, () => SetLoop(!loop));
            Bind(unlockButton, () => SetAllSkills(!allSkills));
            if (timeline != null) timeline.onValueChanged.AddListener(OnSeek);
            if (assetPreview != null) assetPreview.SelectionChanged += Restart;
        }
        private void OnDisable()
        {
            foreach (var entry in listeners) if (entry.Key != null) entry.Key.onClick.RemoveListener(entry.Value);
            listeners.Clear();
            if (timeline != null) timeline.onValueChanged.RemoveListener(OnSeek);
            if (assetPreview != null) assetPreview.SelectionChanged -= Restart;
        }
        private void OnDestroy() => preloader.Clear();
        private void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(action); listeners.Add(new KeyValuePair<Button, UnityAction>(button, action));
        }
        private void BindArray(Button[] buttons, Action<int> action)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++) { int index = i; Bind(buttons[i], () => action(index)); }
        }
        private void Update() => Advance(Time.unscaledDeltaTime);
        public void Advance(float delta)
        {
            if (!initialized) return;
            if (playing)
            {
                elapsed += Mathf.Max(0, delta) * speed;
                if (!loop && elapsed >= duration) { elapsed = duration; playing = false; }
            }
            Render();
        }
        public void SetMode(PreviewMode value)
        {
            if (!Enum.IsDefined(typeof(PreviewMode), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == PreviewMode.Assets && (assetPreview == null || !assetPreview.IsReady)) { Debug.LogWarning("[Animation Lab] Register valid preview subjects before using Asset mode.", this); return; }
            mode = value; Restart();
        }
        public void SetSpecies(DragonKind value)
        {
            if ((int)value < 0 || (int)value > 4) throw new ArgumentOutOfRangeException(nameof(value));
            kind = value; if (initialized) dragon.SetDragonKind(kind); Restart();
        }
        public void SelectFighterPose(string pose)
        {
            if (Array.IndexOf(FighterCombatTiming.Poses, pose) < 0 && Array.IndexOf(BasicFighterPoses, pose) < 0) throw new ArgumentException("Unknown fighter pose.", nameof(pose));
            if (Array.IndexOf(BasicFighterPoses, pose) >= 0) { kind=DragonKind.Baby; if(initialized)dragon.SetDragonKind(kind);dragonPose="fight_idle"; }
            fighterPose = pose; mode = PreviewMode.Clips; Restart();
        }
        public void SelectDragonPose(string pose)
        {
            if (Array.IndexOf(DragonAnimationTiming.Poses, pose) < 0 && Array.IndexOf(BasicDragonPoses, pose) < 0) throw new ArgumentException("Unknown dragon pose.", nameof(pose));
            if (Array.IndexOf(BasicDragonPoses, pose) >= 0) { kind=DragonKind.Baby; if(initialized)dragon.SetDragonKind(kind);fighterPose="fight_idle"; }
            dragonPose = pose; mode = PreviewMode.Clips; Restart();
        }
        public void SetSpeed(float value) { speed = Mathf.Clamp(value, .05f, 4); Render(); }
        public void SetPlaying(bool value)
        {
            if (value && !loop && elapsed >= duration) elapsed = 0;
            playing = value; Render();
        }
        public void SetLoop(bool value) { loop = value; if (!loop) elapsed = LocalTime(); Render(); }
        public void SetAllSkills(bool value)
        {
            allSkills = value; for (int i = 0; i < rewards.Length; i++) rewards[i] = value;
            Render();
        }
        public void Restart() { elapsed = 0; UpdateDuration(); Render(); }
        public void Seek(float seconds) { playing = false; elapsed = loop ? Mathf.Max(0, seconds) : Mathf.Clamp(seconds, 0, duration); Render(); }
        public void Step(int frames) => Seek(elapsed + frames / (mode == PreviewMode.Assets && assetPreview != null ? assetPreview.FrameRate : 60f));
        private void OnSeek(float normalized)
        {
            float cycle = mode == PreviewMode.Combat && loop ? Mathf.Floor(elapsed / duration) * duration : 0;
            Seek(cycle + normalized * duration);
        }
        private float LocalTime() => !loop ? Mathf.Clamp(elapsed, 0, duration) : elapsed < duration ? elapsed : Mathf.Repeat(elapsed, duration);
        public void JumpToContact()
        {
            if (mode == PreviewMode.Assets) { Seek(0); return; }
            if (mode == PreviewMode.Combat && kind == DragonKind.Baby && fighter.BasicCombat != null)
            {
                float local = LocalTime();
                float next = BabyFirstBattle.NextContact(local);
                Seek(Mathf.Floor(elapsed / duration) * duration + next);
            }
            else if (mode == PreviewMode.BasicJab)
            {
                Seek(Mathf.Floor(elapsed / BasicCombatProfile.CycleSeconds) * BasicCombatProfile.CycleSeconds +
                    BasicCombatProfile.IdleSeconds + BasicCombatProfile.JabContact);
            }
            else if (mode == PreviewMode.Combat)
            {
                float local = LocalTime();
                if (!loop && local >= duration - .0001f) local = 0;
                Seek(DragonCombatChoreography.NextContact(kind, local, rewards));
            }
            else if (mode == PreviewMode.Practice) Seek(DragonTrainingChoreography.ContactTime(kind, 0));
            else Seek(dragonPose == "attack" ? DragonAnimationTiming.Contact(kind) : FighterCombatTiming.Contact(fighterPose));
        }
        private void UpdateDuration()
        {
            if (mode == PreviewMode.Assets && assetPreview != null) { duration = assetPreview.Duration; return; }
            if (mode == PreviewMode.Clips)
            {
                fighterClipDuration = ClipLength(fighter, "fighter_" + fighterPose, FighterCombatTiming.Duration(fighterPose));
                dragonClipDuration = ClipLength(dragon, "dragon_" + (int)kind + "_" + dragonPose, DragonAnimationTiming.Duration(kind, dragonPose));
            }
            duration = mode == PreviewMode.BasicJab ? BasicCombatProfile.CycleSeconds :
                mode == PreviewMode.Combat && kind == DragonKind.Baby && fighter.BasicCombat != null ? BabyFirstBattle.Duration :
                mode == PreviewMode.Combat ? DragonCombatChoreography.SequenceSeconds(kind) :
                mode == PreviewMode.Practice ? DragonTrainingChoreography.SuperCycleSeconds(kind) :
                Mathf.Max(fighterClipDuration, dragonClipDuration);
        }
        private static float ClipLength(DragonActorView actor, string clipName, float fallback)
        {
            if (actor == null) return fallback;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips) if (clip.name == clipName) return clip.length;
            return fallback;
        }
        private Vector2 Snap(Vector2 offset)
        {
            // Stage magnification is included so foot contact remains on a screen pixel.
            float scale = Mathf.Max(.01f, canvas.scaleFactor * Mathf.Abs(fighter.transform.parent.lossyScale.x / canvas.transform.lossyScale.x));
            return new Vector2(Mathf.Round(offset.x * scale) / scale, Mathf.Round(offset.y * scale) / scale);
        }
        private void Render()
        {
            if (!initialized) return;
            bool assets = mode == PreviewMode.Assets && assetPreview != null;
            fighter.transform.parent.gameObject.SetActive(!assets);
            if (assetPreview != null) assetPreview.SetVisible(assets);
            if (stageCaption != null) stageCaption.text = assets ? "PREVIEW   /   UI 애니메이션   /   배율·위치·반전 조정" : "PREVIEW   /   2× 확대   /   본게임 접촉 간격 유지";
            if (contactButton != null) contactButton.interactable = !assets;
            if (unlockButton != null) unlockButton.gameObject.SetActive(!assets);
            if (assets)
            {
                assetPreview.Sample(elapsed, loop);
                cue = $"자유 애니메이션 · {assetPreview.FrameRate:0.##}fps · 배치는 프리뷰 전용";
                UpdateReadout(assetPreview.Description);
                return;
            }
            string hp = fighterPose, dp = dragonPose;
            float ht = LocalTime(), dt = ht; Vector2 h = Vector2.zero, d = Vector2.zero;
            Vector2 hs = Vector2.one, ds = Vector2.one;
            float hr = 0, dr = 0, hf = 0, df = 0;
            dragon.DragonFacingAndScale(false);
            if (mode == PreviewMode.Clips && kind == DragonKind.Baby && fighter.BasicCombat != null &&
                (Array.IndexOf(BasicFighterPoses,fighterPose)>=0 || Array.IndexOf(BasicDragonPoses,dragonPose)>=0))
            {
                fighter.BasicCombat.ApplyPair(fighter,dragon,new DragonCombatChoreography.Frame{FighterPose=fighterPose,DragonPose=dragonPose,FighterSeconds=LocalTime(),DragonSeconds=LocalTime(),FighterScale=Vector2.one,DragonScale=Vector2.one});
                cue="독립 클립 · 머리/목 및 회수 비교";UpdateReadout(fighterPose+" ↔ "+dragonPose);return;
            }
            if (mode == PreviewMode.BasicJab || (mode == PreviewMode.Combat && kind == DragonKind.Baby && fighter.BasicCombat != null))
            {
                dragon.SetDragonKind(DragonKind.Baby);
                var f = mode == PreviewMode.BasicJab ? DragonCombatChoreography.EvaluateBasicJab(elapsed) : BabyFirstBattle.Evaluate(LocalTime());
                fighter.BasicCombat.ApplyPair(fighter, dragon, f);
                cue = (mode == PreviewMode.Combat ? BabyFirstBattle.BeatLabel(LocalTime()) + " / " : "") + f.Cue + (f.Held ? " / CONTACT HOLD" : "");
                UpdateReadout("Baby | " + f.FighterPose + " ↔ " + f.DragonPose);
                return;
            }
            if (mode == PreviewMode.Combat)
            {
                // For non-loop playback hold just before the next cycle's first frame.
                float sample = !loop && elapsed >= duration ? Mathf.Max(0, duration - .0001f) : elapsed;
                var f = DragonCombatChoreography.Evaluate(kind, sample, rewards);
                hp = f.FighterPose; dp = f.DragonPose; ht = f.FighterSeconds; dt = f.DragonSeconds;
                h = f.FighterOffset + f.StageOffset; d = f.DragonOffset + f.StageOffset; hr = f.FighterRotation; dr = f.DragonRotation;
                hs = f.FighterScale; ds = f.DragonScale;
                float travel = hp == "cross" ? rewards[2] ? 102 : 88 : hp == "takedown" ? 96 : hp == "clinch" ? 88 : hp == "kick" ? 80 : 68;
                h.x += f.FighterTravel * travel;
                float flash = CombatFeedbackTiming.Get(f.Impact).FlashSeconds;
                hf = f.Defending && hp == "hurt" && f.ContactAge >= 0 && f.ContactAge < flash ? .6f : 0;
                df = !f.Defending && dp == "hurt" && f.ContactAge >= 0 && f.ContactAge < flash ? .45f : 0;
                cue = $"{f.Pattern} · {CombatFeedbackTiming.Label(f.FighterState)} ↔ {CombatFeedbackTiming.Label(f.DragonState)} · {f.Cue}" +
                    (f.Telegraph > 0 ? " / 타격 예고" : "") + (f.Held ? " / HIT STOP" : "");
            }
            else if (mode == PreviewMode.Practice)
            {
                float sample = !loop && elapsed >= duration ? Mathf.Max(0, duration - .0001f) : elapsed;
                var practice = DragonTrainingChoreography.Evaluate(kind, sample);
                hp = "idle"; ht = DragonTrainingChoreography.IdleSampleSeconds(practice.RoundElapsed, practice.Round + 1);
                dp = practice.Pose; dt = practice.PoseSeconds;
                cue = $"수련 {practice.Round + 1}/3 · {practice.Cue}";
            }
            else
            {
                // Native looping clips keep their own period; the longer partner must not reset them mid-loop.
                if (FighterCombatTiming.Loops(hp)) ht = loop ? elapsed : Mathf.Min(elapsed, fighterClipDuration - .0001f);
                if (DragonAnimationTiming.Loops(dp)) dt = loop ? elapsed : Mathf.Min(elapsed, dragonClipDuration - .0001f);
                cue = "클립 원본 확인 · 이동 연출 없음";
            }
            fighter.SampleFighter(hp, ht); fighter.Offset(Snap(h), hr); fighter.PresentationScale(hs); fighter.HitFlash(hf);
            dragon.SampleDragon(dp, dt); dragon.Offset(Snap(d), dr); dragon.PresentationScale(ds); dragon.HitFlash(df);
            UpdateReadout($"{kind}  |  {hp} ↔ {dp}");
        }
        private void UpdateReadout(string description)
        {
            if (timeline != null) timeline.SetValueWithoutNotify(LocalTime() / duration);
            if (status != null) status.text = $"{description}\n{LocalTime():0.000} / {duration:0.000}s  |  {speed:0.##}×\n{cue}";
            if (playLabel != null) playLabel.text = playing ? "일시정지" : "재생";
            if (loopLabel != null) loopLabel.text = loop ? "반복 ON" : "반복 OFF";
            if (unlockLabel != null) unlockLabel.text = allSkills ? "기술: 전체 해금" : "기술: 기본";
            Highlight(modeButtons, (int)mode); Highlight(speciesButtons, (int)kind);
            Highlight(fighterButtons, mode == PreviewMode.Clips ? Array.IndexOf(FighterCombatTiming.Poses, fighterPose) : -1);
            Highlight(dragonButtons, mode == PreviewMode.Clips ? Array.IndexOf(DragonAnimationTiming.Poses, dragonPose) : -1);
            Highlight(speedButtons, Array.IndexOf(PlaybackSpeeds, speed));
        }
        private static void Highlight(Button[] buttons, int selected)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null && buttons[i].targetGraphic != null)
                buttons[i].targetGraphic.color = i == selected ? new Color(.16f, .52f, .46f) : new Color(.16f, .21f, .28f);
        }
#if UNITY_EDITOR
        public void ConfigureAssetPreviewEditor(AnimationLabAssetPreview preview, Button[] modes)
        {
            assetPreview = preview; modeButtons = modes;
            stageCaption = transform.Find("Stage Caption").GetComponent<Text>();
        }
        public void ConfigureEditor(DragonActorView hunter, DragonActorView opponent, Canvas root, Text readout, Slider scrub,
            Button[] modes, Button[] species, Button[] fighters, Button[] dragons, Button[] speeds, Button[] transport)
        {
            fighter = hunter; dragon = opponent; canvas = root; status = readout; timeline = scrub;
            modeButtons = modes; speciesButtons = species; fighterButtons = fighters; dragonButtons = dragons; speedButtons = speeds;
            playButton = transport[0]; restartButton = transport[1]; previousButton = transport[2]; nextButton = transport[3];
            contactButton = transport[4]; loopButton = transport[5]; unlockButton = transport[6];
            playLabel = playButton.GetComponentInChildren<Text>(); loopLabel = loopButton.GetComponentInChildren<Text>(); unlockLabel = unlockButton.GetComponentInChildren<Text>();
        }
#endif
    }
}
