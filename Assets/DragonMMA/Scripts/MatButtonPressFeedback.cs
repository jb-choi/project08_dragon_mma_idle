using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragonMMA
{
    /// <summary>Deforms only the authored skin and label; the button's hitbox never moves.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class MatButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISubmitHandler
    {
        [SerializeField] private RectTransform plate;
        [SerializeField] private Text label;
        [SerializeField, Min(0)] private float pressDepth = 2.5f;
        [SerializeField, Min(.01f)] private float pressSeconds = .075f;
        [SerializeField, Min(.01f)] private float releaseSeconds = .16f;
        private Button button;
        private Vector3 plateScale, labelScale;
        private Vector2 platePosition, labelPosition;
        private Color labelColor;
        private bool initialized, pointerHeld, pointerInside, animating, wasInteractable;
        private float amount, from, target, elapsed, submitRemaining;

        public RectTransform Plate => plate;
        public Text Label => label;

        private void Awake() => Initialize();
        private void OnEnable()
        {
            Initialize();
            UpdateLabelState();
        }

        private void Initialize()
        {
            if (initialized || plate == null || label == null) return;
            button = GetComponent<Button>();
            plateScale = plate.localScale;
            labelScale = label.rectTransform.localScale;
            platePosition = plate.anchoredPosition;
            labelPosition = label.rectTransform.anchoredPosition;
            labelColor = label.color;
            initialized = true;
            wasInteractable = button.IsInteractable();
        }

        private bool CanPress()
        {
            Initialize();
            return initialized && isActiveAndEnabled && button.IsActive() && button.IsInteractable();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !CanPress()) return;
            pointerHeld = pointerInside = true;
            submitRemaining = 0;
            TransitionTo(1);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pointerHeld = false;
            if (initialized) TransitionTo(0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (pointerHeld && CanPress()) TransitionTo(1);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (pointerHeld && initialized) TransitionTo(0);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!CanPress()) return;
            // Button handles the actual click. This component must not invoke onClick again.
            submitRemaining = pressSeconds + .04f;
            TransitionTo(1);
        }

        private void LateUpdate() => AdvanceFeedback(Time.unscaledDeltaTime);

        /// <summary>Unscaled clock, also used by deterministic editor tests.</summary>
        public void AdvanceFeedback(float deltaTime)
        {
            Initialize();
            if (!initialized) return;
            bool interactable = button.IsInteractable();
            if (interactable != wasInteractable)
            {
                wasInteractable = interactable;
                UpdateLabelState();
                if (!interactable)
                {
                    pointerHeld = false;
                    submitRemaining = 0;
                    TransitionTo(0);
                }
            }
            deltaTime = Mathf.Max(0, deltaTime);
            if (submitRemaining > 0)
            {
                submitRemaining -= deltaTime;
                if (submitRemaining <= 0 && !(pointerHeld && pointerInside)) TransitionTo(0);
            }
            if (!animating) return;
            elapsed += deltaTime;
            float duration = target > .5f ? pressSeconds : releaseSeconds;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
            float easing = target > .5f ? PressEase(t) : ReleaseEase(t);
            amount = Mathf.LerpUnclamped(from, target, easing);
            ApplyPose(amount);
            if (t >= 1) animating = false;
        }

        private void TransitionTo(float value)
        {
            if (animating && Mathf.Approximately(target, value)) return;
            if (!animating && Mathf.Approximately(amount, value)) return;
            from = amount;
            target = value;
            elapsed = 0;
            animating = true;
        }

        private void ApplyPose(float value)
        {
            plate.localScale = Vector3.Scale(plateScale, new Vector3(1 - .025f * value, 1 - .10f * value, 1));
            plate.anchoredPosition = platePosition + Vector2.down * (pressDepth * value);
            label.rectTransform.localScale = Vector3.Scale(labelScale, new Vector3(1 - .018f * value, 1 - .045f * value, 1));
            label.rectTransform.anchoredPosition = labelPosition + Vector2.down * (pressDepth * value);
        }

        private void UpdateLabelState()
        {
            if (!initialized) return;
            var color = labelColor;
            if (!button.IsInteractable()) color.a *= .48f;
            label.color = color;
        }

        private void OnDisable()
        {
            pointerHeld = pointerInside = animating = false;
            submitRemaining = amount = elapsed = 0;
            if (initialized) ApplyPose(0);
        }

        public static float PressEase(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - Mathf.Pow(1 - t, 3);
        }

        public static float ReleaseEase(float t)
        {
            t = Mathf.Clamp01(t);
            float x = t - 1;
            const float spring = .8f;
            return 1 + (spring + 1) * x * x * x + spring * x * x;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(RectTransform authoredPlate, Text authoredLabel, float depth)
        {
            plate = authoredPlate;
            label = authoredLabel;
            pressDepth = depth;
            initialized = false;
        }
#endif
    }
}
