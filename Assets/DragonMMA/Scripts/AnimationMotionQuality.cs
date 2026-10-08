using UnityEngine;

namespace DragonMMA
{
    // Shared presentation curves. These values never change combat rules or save data.
    public static class AnimationMotionQuality
    {
        public const float ApproachPlantLeadSeconds = .065f;
        public const float ContactHoldSeconds = .06f;
        public const float RecommendedWalkGroundSpeed = 18f;
        public const float FighterCombatStanceOffset = 98f;
        public const int WalkFrameCount = 8;
        public const int WalkGroundFloorPixel = 8;
        public const int WalkAirborneFloorPixel = 12;

        // A compact MMA bounce step: land/compress, push, float, land, then repeat.
        // The mean is exactly 1 so gameplay ground speed stays unchanged while the
        // ground nearly stops beneath a planted foot and advances during push/flight.
        private static readonly float[] WalkSpeedMultipliers =
            { .15f, 1.65f, 2.05f, .15f, .15f, 1.65f, 2.05f, .15f };

        public struct WalkPose
        {
            public Vector2 Scale;
            public float Rotation;
        }

        // Brings the root to its contact position before the contact drawing appears,
        // then keeps it planted through contact instead of sliding under the strike.
        public static float AttackTravel(float seconds, float contact, float duration, float anticipation = -.035f)
        {
            seconds = Mathf.Max(0, seconds);
            contact = Mathf.Max(.01f, contact);
            duration = Mathf.Max(contact + .01f, duration);
            float windupEnd = Mathf.Min(.08f, contact * .35f);
            float plantAt = Mathf.Max(windupEnd + .01f, contact - ApproachPlantLeadSeconds);
            if (seconds < windupEnd)
                return Mathf.Lerp(0, anticipation, Mathf.SmoothStep(0, 1, seconds / windupEnd));
            if (seconds < plantAt)
            {
                float p = Mathf.InverseLerp(windupEnd, plantAt, seconds);
                return Mathf.Lerp(anticipation, 1, Mathf.SmoothStep(0, 1, p));
            }
            if (seconds <= contact + ContactHoldSeconds) return 1;
            float recovery = Mathf.InverseLerp(contact + ContactHoldSeconds, duration, seconds);
            return 1 - Mathf.SmoothStep(0, 1, recovery);
        }

        // The authored sheet already contains the complete weight transfer. Applying
        // another scale/rotation wobble here visibly deforms the walk.
        public static WalkPose FighterWalk(float seconds, float duration)
        {
            return new WalkPose
            {
                Scale = Vector2.one,
                Rotation = 0
            };
        }

        public static bool WalkFrameIsAirborne(int frame) => PositiveFrame(frame) == 2 || PositiveFrame(frame) == 6;

        public static bool WalkFrameIsLanding(int frame)
        {
            int normalized = PositiveFrame(frame);
            return normalized == 0 || normalized == 3 || normalized == 4 || normalized == 7;
        }

        public static int WalkFloorPixel(int frame) =>
            WalkFrameIsAirborne(frame) ? WalkAirborneFloorPixel : WalkGroundFloorPixel;

        public static string WalkBeat(int frame)
        {
            switch (PositiveFrame(frame))
            {
                case 0: return "left landing compression";
                case 1: return "left push-off";
                case 2: return "first airborne";
                case 3: return "right toe landing";
                case 4: return "right landing compression";
                case 5: return "right push-off";
                case 6: return "second airborne";
                default: return "left toe landing";
            }
        }

        public static float WalkSpeedMultiplier(float seconds, float duration)
        {
            duration = Mathf.Max(.01f, duration);
            float frame = Mathf.Repeat(seconds, duration) / duration * WalkFrameCount;
            int from = Mathf.FloorToInt(frame) % WalkFrameCount;
            int to = (from + 1) % WalkFrameCount;
            float blend = Mathf.SmoothStep(0, 1, frame - Mathf.Floor(frame));
            return Mathf.Lerp(WalkSpeedMultipliers[from], WalkSpeedMultipliers[to], blend);
        }

        private static int PositiveFrame(int frame)
        {
            int normalized = frame % WalkFrameCount;
            return normalized < 0 ? normalized + WalkFrameCount : normalized;
        }
    }
}
