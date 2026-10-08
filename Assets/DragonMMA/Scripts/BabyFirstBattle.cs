using UnityEngine;

namespace DragonMMA
{
    // A beginner encounter, not a damage timeline. The session alone resolves wins/losses.
    // Reuse the approved jab/contact choreography; vary the context, not its artwork.
    public static class BabyFirstBattle
    {
        public const float Duration = 20f;
        private static readonly float[] JabStarts = { 1.5f, 3.6f, 7.6f, 9f, 13.5f, 14.3f, 17.8f };
        private static readonly float[] ThreatStarts = { 5.5f, 11.6f };
        private const float JabPlaybackSeconds = BasicCombatProfile.JabSeconds + BasicCombatProfile.HitHold;
        private const float ThreatSeconds = 1.1f, GuardEnd = 1.45f;
        private const float GuardRiseSeconds = .16f, GuardHoldUntil = .75f, GuardRecoverSeconds = .45f;
        private const float GuardRaisedPose = .15f, GuardEndPose = .45f, GuardRecoverPoseSpan = .30f;
        private const float ThreatRiseSeconds = .4f, ThreatHoldUntil = .7f;
        private const float ThreatRaisedPose = .27f, ThreatRecoverPoseSpan = .23f;

        public static string BeatLabel(float elapsed, float duration = Duration)
        {
            float t = ReferenceTime(elapsed, duration);
            if (t < 5.5f) return "첫 교전 · 거리와 반응 확인";
            if (t < 7.6f) return "첫 위협 · 가드를 유지한다";
            if (t < 11.6f) return "빈틈 · 잽으로 다시 확인";
            if (t < 13.5f) return "같은 신호 · 침착하게 기다린다";
            if (t < 16f) return "짧은 연계 · 잽 두 번 후 회수";
            return "마지막 교환 · 자세를 지킨다";
        }

        public static float NextContact(float elapsed)
        {
            foreach (float start in JabStarts)
                if (elapsed < start + BasicCombatProfile.JabContact - .0001f)
                    return start + BasicCombatProfile.JabContact;
            return Duration;
        }

        public static DragonCombatChoreography.Frame Evaluate(float elapsed, float duration = Duration)
        {
            float t = ReferenceTime(elapsed, duration);
            float scale = Mathf.Max(.01f, duration) / Duration;
            var f = DragonCombatChoreography.EvaluateBasicJab(0);
            float idleStart = 0;
            if (TryJab(t, scale, ref f, ref idleStart) || TryThreat(t, ref f, ref idleStart)) return f;
            f.FighterSeconds = t - idleStart;
            f.DragonSeconds = t - idleStart;
            f.Cue = t < 1.5f ? "서로 살핀다 · 가볍게 호흡" : t >= 18.4f ? "팔 회수 · 결착 전까지 가드 유지" : "가드 복귀 · 다음 움직임을 읽는다";
            return f;
        }


        private static bool TryJab(float t, float scale, ref DragonCombatChoreography.Frame f, ref float idleStart)
        {
            for (int i = 0; i < JabStarts.Length; i++)
            {
                float local = t - JabStarts[i];
                float end = JabPlaybackSeconds;
                if (local >= 0 && local <= end)
                {
                    f = DragonCombatChoreography.EvaluateBasicJab(BasicCombatProfile.IdleSeconds + local);
                    f.ContactKey = i;
                    f.ContactAt = (JabStarts[i] + BasicCombatProfile.JabContact) * scale;
                    f.ContactAge *= scale;
                    f.CycleIndex = i;
                    f.CycleStart = JabStarts[i] * scale;
                    f.Cue = i < 2 ? "거리 확인 · 단발 잽" : i < 4 ? "위협이 끝났다 · 짧게 반격" : i < 6 ? "잽 · 잽 · 가드 복귀" : "마지막 잽 · 무리하지 않는다";
                    return true;
                }
                if (local > end) idleStart = Mathf.Max(idleStart, JabStarts[i] + end);
            }
            return false;
        }

        private static bool TryThreat(float t, ref DragonCombatChoreography.Frame f, ref float idleStart)
        {
            for (int i = 0; i < ThreatStarts.Length; i++)
            {
                float local = t - ThreatStarts[i];
                if (local >= 0 && local < GuardEnd)
                {
                    // Threat is a readable feint, not a fake contact/damage event.
                    // Rise -> briefly hold the open mouth -> settle, then wait before the jab.
                    f.Defending = true; f.Guarded = true;
                    f.FighterPose = "first_guard";
                    f.FighterSeconds = GuardPoseTime(local);
                    f.FighterState = CombatActorState.Guard;
                    if (local < ThreatSeconds)
                    {
                        f.DragonPose = "threat";
                        f.DragonSeconds = ThreatPoseTime(local);
                        f.DragonState = local < ThreatHoldUntil ? CombatActorState.Windup : CombatActorState.Recover;
                    }
                    else f.DragonSeconds = local - ThreatSeconds;
                    f.Cue = local < ThreatSeconds ? "새끼용의 허세 · 공격을 멈추고 가드" : "앞발이 내려온다 · 반격을 서두르지 않는다";
                    return true;
                }
                if (local >= GuardEnd) idleStart = Mathf.Max(idleStart, ThreatStarts[i] + GuardEnd);
            }
            return false;
        }

        private static float GuardPoseTime(float local)
        {
            if (local < GuardRiseSeconds) return local * (GuardRaisedPose / GuardRiseSeconds);
            if (local < GuardHoldUntil) return GuardRaisedPose;
            return Mathf.Min(GuardEndPose, GuardRaisedPose + (local - GuardHoldUntil) * (GuardRecoverPoseSpan / GuardRecoverSeconds));
        }

        private static float ThreatPoseTime(float local)
        {
            if (local < ThreatRiseSeconds) return local * (ThreatRaisedPose / ThreatRiseSeconds);
            if (local < ThreatHoldUntil) return ThreatRaisedPose;
            return ThreatRaisedPose + (local - ThreatHoldUntil) * (ThreatRecoverPoseSpan / ThreatRiseSeconds);
        }

        private static float ReferenceTime(float elapsed, float duration) =>
            Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration)) * Duration;
    }
}
