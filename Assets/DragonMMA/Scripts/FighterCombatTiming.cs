using UnityEngine;

namespace DragonMMA
{
    // Presentation only: does not change battle duration, odds, rewards or saves.
    public static class FighterCombatTiming
    {
        public const float CycleSeconds = 2.4f, TurnSeconds = 1.05f, HitStopSeconds = .05f;
        public static readonly string[] Poses = { "idle", "walk", "punch", "cross", "kick", "hurt", "block", "clinch", "takedown", "knockdown", "recovery", "victory" };

        public static bool Loops(string pose) => pose == "idle" || pose == "walk";
        public static float Duration(string pose)
        {
            switch (pose)
            {
                case "idle": return .96f;
                case "walk": return .72f;
                case "punch": return .62f;
                case "cross": return .76f;
                case "kick": return .88f;
                case "block": return .36f;
                case "hurt": return .40f;
                case "clinch": return .90f;
                case "takedown": return .96f;
                case "knockdown": return .72f;
                case "recovery": return .96f;
                case "victory": return .88f;
                default: return .96f;
            }
        }
        public static float Contact(string pose)
        {
            switch (pose)
            {
                case "punch": return .22f;
                case "cross": return .28f;
                case "kick": return .36f;
                case "clinch": return .40f;
                case "takedown": return .44f;
                default: return .12f;
            }
        }
        public static float FrameTime(string pose, int frame)
        {
            float duration = Duration(pose);
            if (Loops(pose)) return frame * duration / 8;
            if (pose == "block" || pose == "hurt") return frame <= 3 ? frame * .04f : .12f + (duration - .12f) * (frame - 3) / 4;
            if (pose == "punch" || pose == "cross" || pose == "kick" || pose == "clinch" || pose == "takedown")
            {
                float contact = Contact(pose);
                switch (frame)
                {
                    case 0: return 0;
                    case 1: return contact * .45f;
                    case 2: return contact - .055f;
                    case 3: return contact;
                    case 4: return contact + .06f;
                    case 5: return contact + .15f;
                    case 6: return duration - .09f;
                    default: return duration;
                }
            }
            return frame * duration / 7;
        }
        public static string Attack(int cycle, bool[] rewards)
        {
            bool special = cycle % 3 == 2;
            if (special && rewards[4]) return "takedown";
            if (special && rewards[3]) return "clinch";
            return cycle % 2 == 1 ? "kick" : rewards[1] ? "cross" : "punch";
        }
        public static float HeldTime(float time, float contact) => HeldTime(time, contact, HitStopSeconds);
        public static float HeldTime(float time, float contact, float hitStopSeconds) =>
            time <= contact ? time : time < contact + hitStopSeconds ? contact : time - hitStopSeconds;
        public static bool IsInPlaceStrike(string pose) =>
            pose == "punch" || pose == "cross" || pose == "kick";
        public static float Travel(string pose, float time)
            => IsInPlaceStrike(pose) ? 0 : AnimationMotionQuality.AttackTravel(time, Contact(pose), Duration(pose));
    }

    // Emits a contact once even if the render frame skips over its exact timestamp.
    public sealed class FighterContactGate
    {
        private int lastKey = -1;
        private float lastElapsed = -1;
        public void Reset() { lastKey = -1; lastElapsed = -1; }
        public bool TryContact(float elapsed, int key, float contactAt)
        {
            if (elapsed + .001f < lastElapsed) Reset();
            lastElapsed = elapsed;
            if (elapsed < contactAt || lastKey == key) return false;
            lastKey = key;
            // Do not replay stale sound when restoring a save or skipping several turns.
            return elapsed - contactAt <= .12f;
        }
    }
}
