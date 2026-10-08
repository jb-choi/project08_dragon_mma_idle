using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA
{
    public sealed class DragonActorView : MonoBehaviour
    {
        public enum ActorRole { Hunter, Dragon }
        [Header("Authored visual · edit child Visual in the prefab")]
        [SerializeField] private Image spriteImage;
        [SerializeField] private Animator animator;
        [SerializeField] private ActorRole role;
        [SerializeField] private RuntimeAnimatorController hunterController;
        [SerializeField] private RuntimeAnimatorController[] dragonControllers;
        [SerializeField] private Sprite[] dragonPreviews;
        [SerializeField] private float[] dragonPixelScales;
        [SerializeField] private BasicCombatProfile basicCombatProfile;
        private RectTransform rect;
        private Vector2 authoredPosition;
        private Quaternion authoredRotation;
        private Vector3 authoredRootScale;
        private Quaternion visualRotation;
        private Vector3 visualScale;
        private Color authoredColor;
        private string currentPose;
        private int currentKind = -1;
        private Sprite sizedSprite;
        private readonly Dictionary<string, float> fighterLengths = new Dictionary<string, float>();
        public Image Image => spriteImage;
        public Vector2 AuthoredPosition => authoredPosition;
        public BasicCombatProfile BasicCombat => basicCombatProfile;
        public Vector3 SpritePixelWorld(Vector2 pixel)
        {
            var sprite = spriteImage.sprite;
            var visual = spriteImage.rectTransform;
            return visual.TransformPoint(new Vector2(
                visual.rect.xMin + pixel.x / sprite.rect.width * visual.rect.width,
                visual.rect.yMin + pixel.y / sprite.rect.height * visual.rect.height));
        }
        public void SetCombatPixelScale(float multiplier)
        {
            var visual = spriteImage.rectTransform;
            visual.sizeDelta *= multiplier;
            visual.anchoredPosition = new Vector2(visual.sizeDelta.x * visual.pivot.x - DragonAnimationTiming.FrontAnchorPixels, 0);
        }
        public void Initialize()
        {
            rect = (RectTransform)transform;
            authoredPosition = rect.anchoredPosition;
            authoredRotation = rect.localRotation;
            authoredRootScale = rect.localScale;
            visualRotation = spriteImage.rectTransform.localRotation;
            visualScale = spriteImage.rectTransform.localScale;
            authoredColor = spriteImage.color;
            animator.speed = 1;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (role == ActorRole.Hunter) animator.runtimeAnimatorController = hunterController;
            fighterLengths.Clear();
            if (role == ActorRole.Hunter && hunterController != null)
                foreach (var clip in hunterController.animationClips)
                    if (clip.name.StartsWith("fighter_")) fighterLengths[clip.name.Substring(8)] = clip.length;
            currentPose = null; currentKind = -1; sizedSprite = null;
        }
        public void SetDragonKind(DragonKind kind)
        {
            int index = (int)kind;
            if (role != ActorRole.Dragon || currentKind == index) return;
            currentKind = index; currentPose = null;
            if (dragonControllers != null && index < dragonControllers.Length)
                animator.runtimeAnimatorController = dragonControllers[index];
            if (dragonPreviews != null && index < dragonPreviews.Length) spriteImage.sprite = dragonPreviews[index];
            fighterLengths.Clear();
            if (animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    string prefix = "dragon_" + index + "_";
                    if (clip.name.StartsWith(prefix)) fighterLengths[clip.name.Substring(prefix.Length)] = clip.length;
                }
            ApplyDragonGeometry();
        }
        public void Play(string pose)
        {
            if (!animator.isActiveAndEnabled) return;
            animator.speed = 1;
            if (currentPose == pose || animator.runtimeAnimatorController == null) return;
            currentPose = pose; animator.Play(pose, 0, 0);
        }
        public void SampleFighter(string pose, float seconds)
        {
            if (role != ActorRole.Hunter || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            float duration = fighterLengths.TryGetValue(pose, out float length) ? length : FighterCombatTiming.Duration(pose);
            float normalized = FighterCombatTiming.Loops(pose) || pose == "fight_idle" ? Mathf.Repeat(Mathf.Max(0, seconds), duration) / duration : Mathf.Clamp01(seconds / duration);
            animator.speed = 0;
            currentPose = pose;
            animator.Play(pose, 0, normalized);
            animator.Update(0); // Sprite, root motion and contact share the same presentation clock.
        }
        public void SetPresentationPaused(bool paused) => animator.speed = paused ? 0 : 1;
        public void DragonFacingAndScale(bool right, float scale = 1)
        {
            if (role != ActorRole.Dragon) return;
            spriteImage.rectTransform.localScale = new Vector3(visualScale.x * (right ? -1 : 1) * scale, visualScale.y * scale, visualScale.z);
        }
        public Vector2 OffsetToVisualWorldCenter(Vector3 target)
        {
            Offset(Vector2.zero);
            Sprite sprite = spriteImage.sprite;
            if (sprite == null || currentKind < 0 || dragonPixelScales == null || currentKind >= dragonPixelScales.Length) return Vector2.zero;
            Vector3 center = spriteImage.rectTransform.TransformPoint(sprite.bounds.center * sprite.pixelsPerUnit * dragonPixelScales[currentKind]);
            return rect.parent.InverseTransformVector(target - center);
        }
        public void SampleDragon(string pose, float seconds)
        {
            if (role != ActorRole.Dragon || currentKind < 0 || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            float duration = fighterLengths.TryGetValue(pose, out float length) ? length : DragonAnimationTiming.Duration((DragonKind)currentKind, pose);
            float normalized = DragonAnimationTiming.Loops(pose) || pose == "fight_idle" ? Mathf.Repeat(Mathf.Max(0, seconds), duration) / duration : Mathf.Clamp01(seconds / duration);
            animator.speed = 0; currentPose = pose;
            animator.Play(pose, 0, normalized); animator.Update(0);
            ApplyDragonGeometry();
        }
        private void ApplyDragonGeometry()
        {
            Sprite sprite = spriteImage.sprite;
            if (sprite == null || sprite == sizedSprite || dragonPixelScales == null || currentKind >= dragonPixelScales.Length) return;
            sizedSprite = sprite;
            float scale = dragonPixelScales[currentKind];
            var visual = spriteImage.rectTransform;
            visual.anchorMin = visual.anchorMax = new Vector2(.5f, 0);
            visual.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            visual.sizeDelta = (Vector2)sprite.bounds.size * sprite.pixelsPerUnit * scale;
            // Keep the opponent's front contact line registered even when the web
            // prefab uses a smaller pixel scale than the desktop-authored sprite pivot.
            visual.anchoredPosition = new Vector2(sprite.pivot.x * scale - DragonAnimationTiming.FrontAnchorPixels, 0);
        }
        public void HitFlash(float amount) => HitFlash(amount, new Color(1, .52f, .42f, authoredColor.a));
        public void HitFlash(float amount, Color color) => spriteImage.color = Color.Lerp(authoredColor,
            new Color(color.r, color.g, color.b, authoredColor.a), Mathf.Clamp01(amount));
        public void PresentationScale(Vector2 multiplier)
        {
            rect.localScale = new Vector3(authoredRootScale.x * multiplier.x, authoredRootScale.y * multiplier.y, authoredRootScale.z);
        }
        public void Offset(Vector2 motion, float rotation = 0)
        {
            rect.anchoredPosition = authoredPosition + motion;
            rect.localRotation = role == ActorRole.Hunter ? authoredRotation : authoredRotation * Quaternion.Euler(0, 0, rotation);
            if (role == ActorRole.Hunter) spriteImage.rectTransform.localRotation = visualRotation * Quaternion.Euler(0, 0, rotation);
        }
#if UNITY_EDITOR
        public void ConfigureDragonAppearanceEditor(Sprite[] previews, float[] pixelScales, Material material)
        {
            if (role != ActorRole.Dragon) return;
            dragonPreviews = previews; dragonPixelScales = pixelScales;
            spriteImage.material = material; spriteImage.useSpriteMesh = true; spriteImage.preserveAspect = false;
            currentKind = 0; spriteImage.sprite = previews[0]; sizedSprite = null; ApplyDragonGeometry(); currentKind = -1;
        }
        public void ConfigureEditor(Image image, Animator visualAnimator, ActorRole actorRole,
            RuntimeAnimatorController hunter, RuntimeAnimatorController[] dragons, Sprite[] previews)
        {
            spriteImage = image; animator = visualAnimator; role = actorRole;
            hunterController = hunter; dragonControllers = dragons; dragonPreviews = previews;
        }
#endif
    }
}

