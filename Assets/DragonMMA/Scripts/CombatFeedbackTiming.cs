using UnityEngine;

namespace DragonMMA
{
    public enum CombatImpactKind { None, Weak, Heavy, Guard, Knockdown }
    public enum CombatActorState { Ready, Windup, Attack, Guard, Hit, Recover, Victory, Down }

    // Presentation-only timing shared by the game and Animation Lab.
    // A short anticipation cue makes the authored contact frame readable without
    // changing battle odds, duration, rewards, input windows or saved data.
    public static class CombatFeedbackTiming
    {
        public const float TelegraphSeconds = .12f;

        public struct Preset
        {
            public float HitStopSeconds, FlashSeconds, VfxScale, StageKickPixels, AudioScale;

            public Preset(float hitStopSeconds, float flashSeconds, float vfxScale, float stageKickPixels, float audioScale)
            {
                HitStopSeconds = hitStopSeconds;
                FlashSeconds = flashSeconds;
                VfxScale = vfxScale;
                StageKickPixels = stageKickPixels;
                AudioScale = audioScale;
            }
        }

        public static Preset Get(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Weak: return new Preset(.032f, .025f, .85f, 1.5f, .72f);
                case CombatImpactKind.Guard: return new Preset(.045f, .020f, .95f, 1.0f, .64f);
                case CombatImpactKind.Heavy: return new Preset(.075f, .050f, 1.35f, 3.0f, 1f);
                case CombatImpactKind.Knockdown: return new Preset(.105f, .080f, 1.65f, 5.0f, 1f);
                default: return new Preset(0, 0, 0, 0, 0);
            }
        }

        public static float Telegraph(float contactAge)
        {
            // Choreography timestamps are sums of several floats; normalize the
            // sub-frame rounding residue so the cue is guaranteed off at contact.
            if (contactAge < -TelegraphSeconds || contactAge >= -.0001f) return 0;
            return Mathf.SmoothStep(0, 1, 1 + contactAge / TelegraphSeconds);
        }

        public static float StageKick(float contactAge, CombatImpactKind kind)
        {
            var preset = Get(kind);
            const float visibleSeconds = .14f;
            if (preset.StageKickPixels <= 0 || contactAge < 0 || contactAge >= visibleSeconds) return 0;
            float decay = 1 - contactAge / visibleSeconds;
            return Mathf.Cos(contactAge * 115f) * decay * preset.StageKickPixels;
        }

        public static string Label(CombatActorState state)
        {
            switch (state)
            {
                case CombatActorState.Windup: return "준비";
                case CombatActorState.Attack: return "공격";
                case CombatActorState.Guard: return "방어";
                case CombatActorState.Hit: return "피격";
                case CombatActorState.Recover: return "회복";
                case CombatActorState.Victory: return "승리";
                case CombatActorState.Down: return "다운";
                default: return "대치";
            }
        }

        public static Color StateColor(CombatActorState state)
        {
            switch (state)
            {
                case CombatActorState.Attack: return new Color(.98f, .78f, .30f, 1);
                case CombatActorState.Guard: return new Color(.45f, .82f, 1f, 1);
                case CombatActorState.Hit:
                case CombatActorState.Down: return new Color(1f, .46f, .38f, 1);
                case CombatActorState.Victory: return new Color(.56f, 1f, .58f, 1);
                case CombatActorState.Windup: return new Color(1f, .88f, .48f, 1);
                case CombatActorState.Recover: return new Color(.72f, .82f, .86f, 1);
                default: return Color.white;
            }
        }
    }
}
