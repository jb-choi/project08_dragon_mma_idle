using UnityEngine;

namespace DragonMMA
{
    // Presentation only. Never changes encounter duration, odds, rewards or save data.
    public static class DragonAnimationTiming
    {
        public const float FrontAnchorPixels = 52;
        public static readonly string[] Poses = { "idle", "walk", "windup", "attack", "hurt", "defeat" };
        private static readonly float[] Windups = { .30f, .48f, .52f, .65f, .78f };
        private static readonly float[] Attacks = { .60f, .72f, .80f, .90f, 1.02f };
        private static readonly float[] Contacts = { .20f, .28f, .34f, .40f, .46f };
        public static bool Loops(string pose) => pose == "idle" || pose == "walk";
        public static int KeyCount(string pose) => Loops(pose) || pose == "windup" || pose == "attack" ? 7 : 6;
        public static int SpriteFrame(string pose, int key) => key < 6 ? key : Loops(pose) ? 0 : 5;
        public static float Windup(DragonKind kind) => Windups[Mathf.Clamp((int)kind, 0, 4)];
        public static float Contact(DragonKind kind) => Contacts[Mathf.Clamp((int)kind, 0, 4)];
        // Giant's extended foreclaw is source column 3; the other attacks peak in column 4.
        public static int ContactFrame(DragonKind kind) => kind == DragonKind.Giant ? 2 : 3;
        public static float Duration(DragonKind kind, string pose)
        {
            switch (pose)
            {
                case "idle": return 1.20f;
                case "walk": return .72f;
                case "windup": return Windup(kind);
                case "attack": return Attacks[Mathf.Clamp((int)kind, 0, 4)];
                case "hurt": return .40f;
                case "defeat": return .80f;
                default: return 1.20f;
            }
        }
        public static float FrameTime(DragonKind kind, string pose, int frame)
        {
            float duration = Duration(kind, pose);
            if (frame == 6) return duration; // Explicit end hold; clip length stays authoritative for sampling.
            if (Loops(pose)) return duration * frame / 6f;
            if (pose == "windup") return (duration - .06f) * frame / 5f;
            if (pose == "attack")
            {
                int peak = ContactFrame(kind);
                return frame <= peak ? Contact(kind) * frame / peak :
                    Mathf.Lerp(Contact(kind), duration - .07f, (frame - peak) / (float)(5 - peak));
            }
            if (pose == "hurt") return frame <= 3 ? .12f * frame / 3f : Mathf.Lerp(.12f, duration, (frame - 3) / 2f);
            return duration * frame / 5f;
        }
    }
}
