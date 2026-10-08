using UnityEngine;

namespace DragonMMA
{
    public sealed partial class DragonMmaView
    {
        private void Animate(float time, float dt)
        {
            var frame = HuntPresentation.Evaluate(Data, Rules, hunter.BasicCombat != null,
                time, jabTravel, powerPunchTravel, upgradedPunchTravel);
            AnimateHunt(frame, time);
            AnimateWorld(frame, time, dt);
            AnimateTraining(time);
        }

        private void SynchronizeCombatPhase(HuntPresentation.Frame frame, float time)
        {
            bool fighting = Data.phase == HuntPhase.Fighting;
            bool resolvedFromFight = previousPresentationPhase == HuntPhase.Fighting && !fighting;
            if (previousPresentationPhase != Data.phase || previousPresentationEnemy != Data.currentEnemy ||
                (fighting && previousBattleElapsed >= 0 && frame.BattleElapsed < previousBattleElapsed))
            {
                contactGate.Reset(); StopContactAudio();
                if (resolvedFromFight && (Data.phase == HuntPhase.Result || Data.phase == HuntPhase.Recovery))
                {
                    knockdownUntil = frame.BabyResolution ? 0 : time + .18f;
                    knockdownOnDragon = Data.phase == HuntPhase.Result;
                    if (!frame.BabyResolution) PlayContact(knockdownSound, CombatFeedbackTiming.Get(CombatImpactKind.Knockdown).AudioScale);
                }
                previousPresentationPhase = Data.phase; previousPresentationEnemy = Data.currentEnemy;
            }
            previousBattleElapsed = fighting ? frame.BattleElapsed : -1;
        }

        private void AnimateHunt(HuntPresentation.Frame frame, float time)
        {
            var actors = frame.Actors;
            SynchronizeCombatPhase(frame, time);
            notification.anchoredPosition = frame.BabyResolution ? new Vector2(24, notificationPosition.y) : notificationPosition;
            if (Data.phase == HuntPhase.Fighting && actors.ContactKey >= 0 &&
                contactGate.TryContact(frame.BattleElapsed, actors.ContactKey, actors.ContactAt))
                PlayContact(SoundFor(actors.Impact), CombatFeedbackTiming.Get(actors.Impact).AudioScale);

            if (Data.phase == HuntPhase.Result && !Data.lastWasExtortion)
            {
                int newest = Data.cart.Count - 1;
                if (newest >= 0 && newest < cargo.Length) cargo[newest].gameObject.SetActive(frame.CaptureTransferred);
            }

            bool downImpact = time < knockdownUntil;
            CombatImpactKind impactKind = downImpact ? CombatImpactKind.Knockdown : actors.Impact;
            float contactAge = downImpact ? .18f - (knockdownUntil - time) : actors.ContactAge;
            var feedback = CombatFeedbackTiming.Get(impactKind);
            UpdateCombatStage(frame, impactKind);

            hunter.PresentationScale(actors.FighterScale);
            hunter.SampleFighter(actors.FighterPose, actors.FighterSeconds);
            hunter.Offset(SnapToScreenPixel(frame.FighterMotion), actors.FighterRotation);
            hunter.HitFlash((actors.Defending && actors.FighterPose == "hurt" && contactAge >= 0 && contactAge < feedback.FlashSeconds) ||
                (downImpact && !knockdownOnDragon && contactAge < feedback.FlashSeconds) ? .78f : 0,
                actors.Defending ? new Color(.48f, .88f, 1f, 1) : new Color(1f, .43f, .28f, 1));

            dragon.gameObject.SetActive(!frame.Walking && Data.currentEnemy >= 0 && !frame.CaptureTransferred);
            overlay.Show("Hunt/Dragon name plate", !frame.Walking && Data.currentEnemy >= 0 && Data.phase != HuntPhase.Recovery);
            if (Data.currentEnemy >= 0)
            {
                dragon.SetDragonKind((DragonKind)Data.currentEnemy);
                dragon.PresentationScale(actors.DragonScale);
                dragon.DragonFacingAndScale(frame.RunAway, frame.TransportScale);
                dragon.SampleDragon(actors.DragonPose, actors.DragonSeconds);
                dragon.Offset(SnapToScreenPixel(CaptureMotion(frame)), actors.DragonRotation);
                dragon.HitFlash((!actors.Defending && actors.DragonPose == "hurt" && contactAge >= 0 && contactAge < feedback.FlashSeconds) ||
                    (downImpact && knockdownOnDragon && contactAge < feedback.FlashSeconds) ? .72f : 0,
                    impactKind == CombatImpactKind.Knockdown ? new Color(1f, .24f, .12f, 1) : new Color(1f, .78f, .30f, 1));
            }

            Vector3 basicContactPoint = frame.UsesContactProfile ? hunter.BasicCombat.ApplyPair(hunter, dragon, actors) : Vector3.zero;
            if (frame.BabyResolution)
            {
                // The profile recalibrates actor geometry; re-apply transport using that geometry.
                dragon.DragonFacingAndScale(frame.RunAway, frame.TransportScale);
                if (CanCarry(frame)) dragon.Offset(SnapToScreenPixel(CaptureMotion(frame)), actors.DragonRotation);
                if (Data.phase == HuntPhase.Recovery)
                {
                    Color tint = dragon.Image.color;
                    tint.a = HuntPresentation.BabyRecoveryOpacity(Rules.RecoverySeconds - Data.phaseRemaining);
                    dragon.Image.color = tint;
                }
            }
            AnimateContactFeedback(frame, basicContactPoint, time, downImpact, impactKind, contactAge);
            UpdateCombatStateLabels(frame.Walking, actors.FighterState, actors.DragonState);
            Set(overlay, "Hunt/Battle status/Technique", frame.Action);
        }

        private void UpdateCombatStage(HuntPresentation.Frame frame, CombatImpactKind impactKind)
        {
            bool visible = !frame.Walking && Data.currentEnemy >= 0;
            if (combatFocusObjects != null)
                foreach (var focusObject in combatFocusObjects) if (focusObject != null) focusObject.SetActive(visible);
            if (combatBeatLabel == null) return;
            combatBeatLabel.text = frame.Beat;
            combatBeatLabel.color = impactKind == CombatImpactKind.Heavy || impactKind == CombatImpactKind.Knockdown
                ? new Color(1f, .64f, .28f, 1) : new Color(.74f, .94f, 1f, 1);
        }

        private bool CanCarry(HuntPresentation.Frame frame) => Data.phase == HuntPhase.Result &&
            !Data.lastWasExtortion && frame.CarryProgress > 0 && Data.cart.Count > 0;

        private Vector2 CaptureMotion(HuntPresentation.Frame frame)
        {
            if (!CanCarry(frame)) return frame.DragonMotion;
            int newest = Mathf.Min(Data.cart.Count - 1, cargo.Length - 1);
            // Aim at the passenger center, not the cart root's bottom corner.
            Vector3 target = cargo[newest].rectTransform.TransformPoint(cargo[newest].rectTransform.rect.center);
            Vector2 motion = dragon.OffsetToVisualWorldCenter(target) * frame.CarryProgress;
            motion.y += Mathf.Sin(frame.CarryProgress * Mathf.PI) * 50;
            return motion;
        }

        private void AnimateWorld(HuntPresentation.Frame frame, float time, float dt)
        {
            if (frame.Walking)
            {
                float gaitSpeed = AnimationMotionQuality.WalkSpeedMultiplier(frame.Actors.FighterSeconds, FighterCombatTiming.Duration("walk"));
                scroll = Mathf.Repeat(scroll + dt * scrollSpeed * gaitSpeed, Mathf.Max(1, terrainTileWidth));
            }
            for (int i = 0; i < ground.Length; i++) ground[i].anchoredPosition = groundPositions[i] + Vector2.left * Mathf.Round(scroll);
            cartRoot.anchoredPosition = cartPosition + Vector2.up * (frame.Walking ? Mathf.Sin(time * 10) * 1.5f : 0);
            if (returnTime > 0)
            {
                returnTime -= dt;
                departure.anchoredPosition = departurePosition + Vector2.left * ((1 - returnTime / Mathf.Max(.1f, cartReturnSeconds)) * 800);
                helper.Play("walk");
                if (returnTime <= 0) departure.gameObject.SetActive(false);
            }
            for (int i = 0; i < roamers.Length; i++)
                roamers[i].rectTransform.anchoredPosition = roamerPositions[i] + new Vector2(Mathf.Sin(time * .6f + i) * 12, Mathf.Sin(time * 3 + i) * 2);
        }

        private void AnimateTraining(float time)
        {
            if (!trainingActive.gameObject.activeInHierarchy || Data.training[0].IsEmpty) return;
            var practice = DragonTrainingChoreography.Evaluate(Data.training[0].Kind, time * .75f);
            trainee.DragonFacingAndScale(false);
            trainee.SampleDragon(practice.Pose, practice.PoseSeconds);
            trainee.Offset(Vector2.zero);
        }
    }
}
