using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA
{
    // Shared visual assets, with a separate placement root so clips cannot overwrite preview adjustments.
    public sealed class AnimationLabAssetPreview : MonoBehaviour
    {
        [Serializable]
        public sealed class Subject
        {
            public string label;
            public RectTransform placement;
            public GameObject animationTarget;
            public AnimationClip[] clips;
            [Range(.25f, 3f)] public float scale = 1;
            public Vector2 offset;
            public bool flip;
        }

        [SerializeField] private Subject[] subjects = Array.Empty<Subject>();
        [SerializeField] private GameObject stage, controls, combatControls;
        [SerializeField] private Dropdown subjectDropdown, clipDropdown;
        [SerializeField] private Slider scaleSlider, xSlider, ySlider;
        [SerializeField] private Toggle flipToggle;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text adjustmentLabel;
        private int subjectIndex, clipIndex;
        private bool initialized;
        private float scale = 1;
        private Vector2 offset;
        private bool flip;
        private VisualState[] states;
        public event Action SelectionChanged;
        public Subject[] Subjects => subjects;
        public bool IsReady => initialized;
        public AnimationClip SelectedClip => subjects.Length == 0 || subjects[subjectIndex].clips == null || subjects[subjectIndex].clips.Length == 0
            ? null : subjects[subjectIndex].clips[clipIndex];
        public GameObject SelectedTarget => subjects.Length == 0 ? null : subjects[subjectIndex].animationTarget;
        public float Duration => SelectedClip == null ? 1 : Mathf.Max(.001f, SelectedClip.length);
        public float FrameRate => SelectedClip == null ? 60 : Mathf.Max(1, SelectedClip.frameRate);
        public string Description => subjects.Length == 0 ? "등록된 대상 없음" : subjects[subjectIndex].label + " / " + (SelectedClip == null ? "클립 없음" : SelectedClip.name);

        public void Initialize()
        {
            if (initialized) return;
            if (subjects == null || subjects.Length == 0 || Array.Exists(subjects, s => s == null || s.animationTarget == null || s.placement == null || s.clips == null || s.clips.Length == 0 || Array.Exists(s.clips, c => c == null)))
            {
                Debug.LogError("[Animation Lab] Each preview subject needs a placement, animation target and non-empty clip list. Fix the Animation Lab Inspector.", this);
                return;
            }
            states = new VisualState[subjects.Length];
            for (int i = 0; i < subjects.Length; i++)
            {
                states[i] = new VisualState(subjects[i].animationTarget);
                states[i].FreezeAnimators();
            }
            initialized = true;
            subjectDropdown.ClearOptions();
            var labels = new List<string>();
            foreach (var subject in subjects) labels.Add(subject.label);
            subjectDropdown.AddOptions(labels);
            SelectSubject(0);
        }

        private void OnEnable()
        {
            if (subjectDropdown != null) subjectDropdown.onValueChanged.AddListener(SelectSubject);
            if (clipDropdown != null) clipDropdown.onValueChanged.AddListener(SelectClip);
            if (scaleSlider != null) scaleSlider.onValueChanged.AddListener(SetScale);
            if (xSlider != null) xSlider.onValueChanged.AddListener(SetX);
            if (ySlider != null) ySlider.onValueChanged.AddListener(SetY);
            if (flipToggle != null) flipToggle.onValueChanged.AddListener(SetFlip);
            if (resetButton != null) resetButton.onClick.AddListener(ResetAdjustments);
            if (initialized) foreach (var state in states) state.FreezeAnimators();
        }
        private void OnDisable()
        {
            if (subjectDropdown != null) subjectDropdown.onValueChanged.RemoveListener(SelectSubject);
            if (clipDropdown != null) clipDropdown.onValueChanged.RemoveListener(SelectClip);
            if (scaleSlider != null) scaleSlider.onValueChanged.RemoveListener(SetScale);
            if (xSlider != null) xSlider.onValueChanged.RemoveListener(SetX);
            if (ySlider != null) ySlider.onValueChanged.RemoveListener(SetY);
            if (flipToggle != null) flipToggle.onValueChanged.RemoveListener(SetFlip);
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetAdjustments);
            if (initialized) foreach (var state in states) state.Restore(true);
        }
        public void SetVisible(bool visible)
        {
            if (stage != null) stage.SetActive(visible);
            if (controls != null) controls.SetActive(visible);
            if (combatControls != null) combatControls.SetActive(!visible);
        }
        public void SelectSubject(int index)
        {
            if (!initialized || subjects.Length == 0) return;
            subjectIndex = Mathf.Clamp(index, 0, subjects.Length - 1);
            for (int i = 0; i < subjects.Length; i++) subjects[i].placement.gameObject.SetActive(i == subjectIndex);
            subjectDropdown.SetValueWithoutNotify(subjectIndex);
            clipDropdown.ClearOptions();
            var labels = new List<string>();
            foreach (var clip in subjects[subjectIndex].clips) labels.Add(clip == null ? "Missing clip" : clip.name);
            clipDropdown.AddOptions(labels);
            clipIndex = 0; clipDropdown.SetValueWithoutNotify(0);
            ResetAdjustments(); SelectionChanged?.Invoke();
        }
        public void SelectClip(int index)
        {
            if (!initialized || subjects.Length == 0 || subjects[subjectIndex].clips.Length == 0) return;
            clipIndex = Mathf.Clamp(index, 0, subjects[subjectIndex].clips.Length - 1);
            clipDropdown.SetValueWithoutNotify(clipIndex);
            states[subjectIndex].Restore(false);
            SelectionChanged?.Invoke();
        }
        public void Sample(float seconds, bool loop)
        {
            if (!initialized || SelectedClip == null) return;
            states[subjectIndex].Restore(false);
            float time = loop ? Mathf.Repeat(Mathf.Max(0, seconds), Duration) : Mathf.Clamp(seconds, 0, Duration);
            SelectedClip.SampleAnimation(SelectedTarget, time);
            ApplyAdjustments();
        }
        public void SetScale(float value) { scale = Mathf.Clamp(value, .25f, 3); ApplyAdjustments(); }
        public void SetX(float value) { offset.x = value; ApplyAdjustments(); }
        public void SetY(float value) { offset.y = value; ApplyAdjustments(); }
        public void SetFlip(bool value) { flip = value; ApplyAdjustments(); }
        public void ResetAdjustments()
        {
            if (subjects.Length == 0) return;
            var subject = subjects[subjectIndex]; scale = subject.scale; offset = subject.offset; flip = subject.flip;
            scaleSlider.SetValueWithoutNotify(scale); xSlider.SetValueWithoutNotify(offset.x); ySlider.SetValueWithoutNotify(offset.y);
            flipToggle.SetIsOnWithoutNotify(flip); ApplyAdjustments();
        }
        private void ApplyAdjustments()
        {
            if (!initialized || subjects.Length == 0) return;
            var placement = subjects[subjectIndex].placement;
            placement.anchoredPosition = offset;
            placement.localScale = new Vector3(scale * (flip ? -1 : 1), scale, 1);
            if (adjustmentLabel != null) adjustmentLabel.text = $"배율 {scale:0.00}×   X {offset.x:0}   Y {offset.y:0}   · 프리뷰 전용";
        }

        private sealed class VisualState
        {
            private readonly Transform[] transforms;
            private readonly Vector3[] positions, scales;
            private readonly Quaternion[] rotations;
            private readonly RectTransform[] rects;
            private readonly Vector2[] anchorsMin, anchorsMax, pivots, sizes;
            private readonly Vector3[] anchoredPositions;
            private readonly Image[] images;
            private readonly Sprite[] sprites;
            private readonly Color[] colors;
            private readonly float[] fills;
            private readonly Animator[] animators;
            private readonly bool[] animatorEnabled;
            public VisualState(GameObject target)
            {
                transforms = target.GetComponentsInChildren<Transform>(true);
                positions = new Vector3[transforms.Length]; scales = new Vector3[transforms.Length]; rotations = new Quaternion[transforms.Length];
                for (int i = 0; i < transforms.Length; i++) { positions[i] = transforms[i].localPosition; scales[i] = transforms[i].localScale; rotations[i] = transforms[i].localRotation; }
                rects = target.GetComponentsInChildren<RectTransform>(true);
                anchorsMin = new Vector2[rects.Length]; anchorsMax = new Vector2[rects.Length]; pivots = new Vector2[rects.Length]; sizes = new Vector2[rects.Length]; anchoredPositions = new Vector3[rects.Length];
                for (int i = 0; i < rects.Length; i++) { anchorsMin[i] = rects[i].anchorMin; anchorsMax[i] = rects[i].anchorMax; pivots[i] = rects[i].pivot; sizes[i] = rects[i].sizeDelta; anchoredPositions[i] = rects[i].anchoredPosition3D; }
                images = target.GetComponentsInChildren<Image>(true); sprites = new Sprite[images.Length]; colors = new Color[images.Length];
                fills = new float[images.Length];
                for (int i = 0; i < images.Length; i++) { sprites[i] = images[i].sprite; colors[i] = images[i].color; fills[i] = images[i].fillAmount; }
                animators = target.GetComponentsInChildren<Animator>(true); animatorEnabled = new bool[animators.Length];
                for (int i = 0; i < animators.Length; i++) animatorEnabled[i] = animators[i].enabled;
            }
            public void FreezeAnimators() { foreach (var animator in animators) if (animator != null) animator.enabled = false; }
            public void Restore(bool restoreAnimators)
            {
                for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) { transforms[i].localPosition = positions[i]; transforms[i].localScale = scales[i]; transforms[i].localRotation = rotations[i]; }
                for (int i = 0; i < rects.Length; i++) if (rects[i] != null) { rects[i].anchorMin = anchorsMin[i]; rects[i].anchorMax = anchorsMax[i]; rects[i].pivot = pivots[i]; rects[i].sizeDelta = sizes[i]; rects[i].anchoredPosition3D = anchoredPositions[i]; }
                for (int i = 0; i < images.Length; i++) if (images[i] != null) { images[i].sprite = sprites[i]; images[i].color = colors[i]; images[i].fillAmount = fills[i]; }
                if (restoreAnimators) for (int i = 0; i < animators.Length; i++) if (animators[i] != null) animators[i].enabled = animatorEnabled[i];
            }
        }
#if UNITY_EDITOR
        public void ConfigureEditor(GameObject previewStage, GameObject previewControls, GameObject actorControls,
            Dropdown targets, Dropdown clips, Slider zoom, Slider x, Slider y, Toggle mirror, Button reset, Text readout)
        {
            stage = previewStage; controls = previewControls; combatControls = actorControls;
            subjectDropdown = targets; clipDropdown = clips; scaleSlider = zoom; xSlider = x; ySlider = y;
            flipToggle = mirror; resetButton = reset; adjustmentLabel = readout;
        }
        public void AddSubjectEditor(Subject subject)
        {
            Array.Resize(ref subjects, subjects.Length + 1); subjects[subjects.Length - 1] = subject;
            ApplyAuthoredLayoutEditor();
        }
        public Transform StageEditor => stage == null ? null : stage.transform;
        public void ApplyAuthoredLayoutEditor()
        {
            for (int i = 0; i < subjects.Length; i++)
            {
                var subject = subjects[i]; if (subject.placement == null) continue;
                subject.placement.anchoredPosition = subject.offset;
                subject.placement.localScale = new Vector3(subject.scale * (subject.flip ? -1 : 1), subject.scale, 1);
                subject.placement.gameObject.SetActive(i == 0);
            }
        }
#endif
    }
}
