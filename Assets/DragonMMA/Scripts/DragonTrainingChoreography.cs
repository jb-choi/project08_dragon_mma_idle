using UnityEngine;

namespace DragonMMA
{
    // Three deterministic practice rhythms keep the training loop from repeating
    // the exact same pause/attack cadence while remaining previewable and testable.
    public static class DragonTrainingChoreography
    {
        private static readonly float[] Ready = { .58f, .82f, .46f };
        private static readonly float[] Recover = { .72f, .54f, .92f };
        private static readonly float[] IdleRates = { .92f, 1.04f, .86f };
        private static readonly string[] Cues = { "짧은 호흡 뒤 공격", "무게를 옮겨 공격", "길게 회복 후 공격" };

        public struct Frame
        {
            public string Pose, Cue;
            public float PoseSeconds, RoundElapsed;
            public int Round;
        }

        public static float ReadySeconds(int round) => Ready[Mathf.Abs(round) % Ready.Length];
        public static float RecoverSeconds(int round) => Recover[Mathf.Abs(round) % Recover.Length];
        public static float RoundSeconds(DragonKind kind, int round) =>
            ReadySeconds(round) + DragonAnimationTiming.Windup(kind) + DragonAnimationTiming.Duration(kind, "attack") + RecoverSeconds(round);
        public static float SuperCycleSeconds(DragonKind kind) => RoundSeconds(kind, 0) + RoundSeconds(kind, 1) + RoundSeconds(kind, 2);
        public static float ContactTime(DragonKind kind, int round)
        {
            round = Mathf.Clamp(round, 0, 2);
            float time = 0;
            for (int i = 0; i < round; i++) time += RoundSeconds(kind, i);
            return time + ReadySeconds(round) + DragonAnimationTiming.Windup(kind) + DragonAnimationTiming.Contact(kind);
        }
        public static float IdleSampleSeconds(float seconds, int round) => Mathf.Max(0, seconds) * IdleRates[Mathf.Abs(round) % IdleRates.Length];

        public static Frame Evaluate(DragonKind kind, float elapsed)
        {
            float local = Mathf.Repeat(Mathf.Max(0, elapsed), SuperCycleSeconds(kind));
            for (int round = 0; round < 3; round++)
            {
                float roundSeconds = RoundSeconds(kind, round);
                if (local >= roundSeconds) { local -= roundSeconds; continue; }
                float ready = ReadySeconds(round), windup = DragonAnimationTiming.Windup(kind);
                float attack = DragonAnimationTiming.Duration(kind, "attack");
                var frame = new Frame { Round = round, RoundElapsed = local, Pose = "idle", Cue = Cues[round] };
                if (local < ready) frame.PoseSeconds = IdleSampleSeconds(local, round);
                else if (local < ready + windup) { frame.Pose = "windup"; frame.PoseSeconds = local - ready; }
                else if (local < ready + windup + attack) { frame.Pose = "attack"; frame.PoseSeconds = local - ready - windup; }
                else frame.PoseSeconds = IdleSampleSeconds(local - ready - windup - attack, round);
                return frame;
            }
            return new Frame { Pose = "idle", Cue = Cues[0] };
        }
    }
}
