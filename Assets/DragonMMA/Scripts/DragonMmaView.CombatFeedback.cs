using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA
{
    public sealed partial class DragonMmaView
    {
        private void AnimateContactFeedback(HuntPresentation.Frame frame, Vector3 basicContactPoint, float time,
            bool downImpact, CombatImpactKind impactKind, float contactAge)
        {
            bool basicJab = frame.BasicJab, hit = frame.Actors.Hit, defending = frame.Actors.Defending;
            float telegraph = frame.Actors.Telegraph;
            bool contactVisual = hit || downImpact;
            var feedback = CombatFeedbackTiming.Get(impactKind);
            impact.gameObject.SetActive(!basicJab && (contactVisual || time < cheerUntil));
            if (hit || telegraph > 0 || downImpact)
            {
                float effect = hit ? (contactAge < .025f ? .72f : contactAge < .055f ? 1.12f : .90f) : 1;
                if (downImpact) effect = Mathf.Lerp(1.35f, .55f, Mathf.Clamp01(contactAge / .18f));
                bool targetDragon = downImpact ? knockdownOnDragon : !defending;
                RectTransform target = targetDragon ? dragon.Image.rectTransform : hunter.Image.rectTransform;
                Rect targetRect = target.rect;
                Vector2 contactPoint = new Vector2(
                    targetRect.xMin + targetRect.width * (targetDragon ? .30f : .70f),
                    targetRect.yMin + targetRect.height * (downImpact ? .30f : .58f));
                Vector3 point = target.TransformPoint(contactPoint);
                if (basicJab) point = basicContactPoint;
                Color cueColor = impactKind == CombatImpactKind.Guard ? new Color(.50f, .82f, 1, 1) :
                    impactKind == CombatImpactKind.Weak ? new Color(1f, .91f, .50f, 1) :
                    impactKind == CombatImpactKind.Heavy ? new Color(1f, .46f, .16f, 1) :
                    impactKind == CombatImpactKind.Knockdown ? new Color(1f, .28f, .18f, 1) : impactColor;
                if (contactVisual)
                {
                    Sprite sprite = ImpactSprite(impactKind);
                    if (sprite != null) impact.sprite = sprite;
                    // Place the effect center on the receiving actor, not an approximated glove coordinate.
                    impact.rectTransform.position = point - impact.rectTransform.TransformVector(impact.rectTransform.rect.center);
                    impact.rectTransform.localScale = impactScale * effect * Mathf.Max(.01f, feedback.VfxScale) * (basicJab ? .35f : 1);
                    impact.rectTransform.localRotation = Quaternion.Euler(0, 0,
                        impactKind == CombatImpactKind.Heavy ? -8 : impactKind == CombatImpactKind.Guard ? 10 : 0);
                    impact.color = Color.white;
                }
                if (basicJab) HideImpactLayers();
                else AnimateImpactLayers(point, target, cueColor, contactAge, telegraph, hit || downImpact, impactKind, feedback.VfxScale);
            }
            else
            {
                impact.rectTransform.anchoredPosition = impactPosition; impact.rectTransform.localScale = impactScale;
                impact.rectTransform.localRotation = Quaternion.identity; impact.color = impactColor;
                HideImpactLayers();
            }
        }
        private Vector2 SnapToScreenPixel(Vector2 motion)
        {
            float scale = Mathf.Max(.1f, scaler.scaleFactor);
            return new Vector2(Mathf.Round(motion.x * scale) / scale, Mathf.Round(motion.y * scale) / scale);
        }
        private void AnimateImpactLayers(Vector3 point, RectTransform target, Color color, float age, float telegraph,
            bool contact, CombatImpactKind kind, float strength)
        {
            bool heavy = kind == CombatImpactKind.Heavy || kind == CombatImpactKind.Knockdown;
            float life = Mathf.Clamp01(Mathf.Max(0, age) / (heavy ? .34f : .22f));
            if (impactRing != null) impactRing.sprite = kind == CombatImpactKind.Guard ? guardImpactSprite :
                kind == CombatImpactKind.Weak ? weakImpactSprite : heavyImpactSprite;
            if (impactEcho != null) impactEcho.sprite = heavyImpactSprite;
            if (impactDust != null) impactDust.sprite = knockdownImpactSprite;
            SetEffect(impactRing, contact && age >= 0 && life < 1, point, Color.white,
                Mathf.Lerp(.65f, 1.55f, life) * Mathf.Max(.8f, strength), (1 - life) * .72f);
            SetEffect(impactEcho, contact && heavy && age >= .025f && life < 1, point,
                Color.white, Mathf.Lerp(.82f, 1.72f, life), (1 - life) * .42f);
            Vector3 groundPoint = target.TransformPoint(new Vector2(target.rect.center.x, target.rect.yMin + target.rect.height * .08f));
            SetEffect(impactDust, contact && heavy && age >= 0 && age < .42f, groundPoint,
                Color.white, new Vector2(Mathf.Lerp(.7f, 1.7f, life), Mathf.Lerp(.6f, .95f, life)), 1 - life);

            if (speedLines != null)
                for (int i = 0; i < speedLines.Length; i++)
                {
                    Image line = speedLines[i];
                    if (line == null) continue;
                    bool show = telegraph > .16f || (contact && age >= 0 && age < .11f);
                    line.gameObject.SetActive(show);
                    if (!show) continue;
                    float pulse = .48f + .52f * Mathf.Sin((Time.unscaledTime * 18f) + i * 1.7f);
                    Color lineColor = heavy ? new Color(1f, .49f, .20f, .42f + pulse * .42f) :
                        new Color(.55f, .90f, 1f, .34f + pulse * .34f);
                    line.color = lineColor;
                    line.rectTransform.localScale = new Vector3(Mathf.Lerp(.75f, 1.28f, telegraph), 1, 1);
                }
        }
        private Sprite ImpactSprite(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Guard: return guardImpactSprite != null ? guardImpactSprite : impact.sprite;
                case CombatImpactKind.Heavy: return heavyImpactSprite != null ? heavyImpactSprite : impact.sprite;
                case CombatImpactKind.Knockdown: return knockdownImpactSprite != null ? knockdownImpactSprite : impact.sprite;
                default: return weakImpactSprite != null ? weakImpactSprite : impact.sprite;
            }
        }
        private static void SetEffect(Image image, bool visible, Vector3 point, Color color, float scale, float alpha) =>
            SetEffect(image, visible, point, color, Vector2.one * scale, alpha);
        private static void SetEffect(Image image, bool visible, Vector3 point, Color color, Vector2 scale, float alpha)
        {
            if (image == null) return;
            image.gameObject.SetActive(visible);
            if (!visible) return;
            image.rectTransform.position = point - image.rectTransform.TransformVector(image.rectTransform.rect.center);
            image.rectTransform.localScale = new Vector3(scale.x, scale.y, 1);
            color.a *= Mathf.Clamp01(alpha);
            image.color = color;
        }
        private void HideImpactLayers()
        {
            if (impactRing != null) impactRing.gameObject.SetActive(false);
            if (impactEcho != null) impactEcho.gameObject.SetActive(false);
            if (impactDust != null) impactDust.gameObject.SetActive(false);
            if (speedLines != null)
                foreach (var line in speedLines) if (line != null) line.gameObject.SetActive(false);
        }
        private AudioClip SoundFor(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Guard: return guardSound;
                case CombatImpactKind.Heavy: return heavySound;
                case CombatImpactKind.Knockdown: return knockdownSound;
                default: return jabSound;
            }
        }
        private void PlayContact(AudioClip clip, float volumeScale = 1)
        {
            if (Application.isPlaying && combatAudio != null && combatAudio.isActiveAndEnabled && clip != null && !AudioListener.pause)
                combatAudio.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }
        private void UpdateCombatStateLabels(bool walking, CombatActorState fighterState, CombatActorState dragonState)
        {
            if (fighterStateText == null || dragonStateText == null) return;
            if (walking)
            {
                fighterStateText.color = fighterLabelColor;
                dragonStateText.color = dragonLabelColor;
                return;
            }
            fighterStateText.text = "파이터 · " + CombatFeedbackTiming.Label(fighterState);
            fighterStateText.color = CombatFeedbackTiming.StateColor(fighterState);
            if (Data.currentEnemy >= 0)
            {
                dragonStateText.text = game.GetSpec((DragonKind)Data.currentEnemy).displayName + " · " + CombatFeedbackTiming.Label(dragonState);
                dragonStateText.color = CombatFeedbackTiming.StateColor(dragonState);
            }
        }
        private void StopContactAudio()
        {
            if (Application.isPlaying && combatAudio != null && combatAudio.isPlaying) combatAudio.Stop();
        }
    }
}
