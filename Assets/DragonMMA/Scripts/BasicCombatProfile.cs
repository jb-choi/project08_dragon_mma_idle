using UnityEngine;

namespace DragonMMA
{
    [CreateAssetMenu(menuName = "Dragon MMA/Basic Combat Contact Profile")]
    public sealed class BasicCombatProfile : ScriptableObject
    {
        // Pixel coordinates are measured from the registered frame's bottom-left.
        public Vector2 fighterImpactPixel;
        public Vector2[] dragonHeadPixels = new Vector2[8];
        public const float IdleSeconds = 1.5f, JabContact = .20f, JabSeconds = .55f;
        public const float HitHold = 2f / 60f, LightHitSeconds = .30f;
        public const float CycleSeconds = IdleSeconds + JabSeconds + HitHold + .15f;
        public static readonly float[] JabFrameTimes = { 0, 4f/60, 8f/60, JabContact, 15f/60, 19f/60, 25f/60, JabSeconds };
        public static readonly float[] LightHitFrameTimes = { 0, 1f/60, 3f/60, 5f/60, 8f/60, 11f/60, 14f/60, LightHitSeconds };

        public Vector3 ApplyPair(DragonActorView fighter, DragonActorView dragon, DragonCombatChoreography.Frame frame)
        {
            fighter.PresentationScale(Vector2.one); dragon.PresentationScale(Vector2.one);
            fighter.HitFlash(0); dragon.HitFlash(0);
            fighter.SampleFighter(frame.FighterPose, frame.FighterSeconds);
            dragon.DragonFacingAndScale(false); dragon.SampleDragon(frame.DragonPose, frame.DragonSeconds);
            fighter.Offset(Vector2.zero); dragon.Offset(Vector2.zero);
            // Match anatomical contact height while retaining both authored ground anchors.
            Vector3 glove = fighter.SpritePixelWorld(fighterImpactPixel);
            Vector3 floor = dragon.SpritePixelWorld(new Vector2(0, 8));
            Vector3 head = dragon.SpritePixelWorld(dragonHeadPixels[0]);
            float ratio = (glove.y - floor.y) / Mathf.Max(.01f, head.y - floor.y);
            dragon.SetCombatPixelScale(ratio);
            head = dragon.SpritePixelWorld(dragonHeadPixels[0]);
            Vector2 alignment = fighter.transform.parent.InverseTransformVector(head - glove);
            alignment.y = 0;
            fighter.Offset(alignment + frame.FighterOffset); dragon.Offset(frame.DragonOffset);
            int index = 0;
            if (frame.DragonPose == "light_hit")
                for (int i = 1; i < LightHitFrameTimes.Length; i++) if (frame.DragonSeconds >= LightHitFrameTimes[i]) index = i;
            return dragon.SpritePixelWorld(dragonHeadPixels[index]);
        }
    }
}
