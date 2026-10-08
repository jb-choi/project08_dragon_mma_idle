using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class BasicCombatTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void TenCycles_HaveOneContactEachAndNoRootTravel(int fps)
        {
            var gate=new FighterContactGate();int hits=0;
            for(int i=0;i<Mathf.CeilToInt(BasicCombatProfile.CycleSeconds*10*fps);i++)
            {
                float t=i/(float)fps;var f=DragonCombatChoreography.EvaluateBasicJab(t);
                Assert.That(f.FighterOffset,Is.EqualTo(Vector2.zero));Assert.That(f.DragonOffset,Is.EqualTo(Vector2.zero));
                Assert.That(f.FighterScale,Is.EqualTo(Vector2.one));Assert.That(f.DragonScale,Is.EqualTo(Vector2.one));
                if(f.ContactKey>=0&&gate.TryContact(t,f.ContactKey,f.ContactAt))hits++;
            }
            Assert.That(hits,Is.EqualTo(10));
        }
        [Test]
        public void LightHit_StartsAtImpact_NotBeforeOrAfterArbitraryDelay()
        {
            float t=BasicCombatProfile.IdleSeconds+BasicCombatProfile.JabContact;
            Assert.That(DragonCombatChoreography.EvaluateBasicJab(t-.001f).DragonPose,Is.EqualTo("fight_idle"));
            var impact=DragonCombatChoreography.EvaluateBasicJab(t+.00001f);
            Assert.That(impact.FighterPose,Is.EqualTo("jab"));Assert.That(impact.DragonPose,Is.EqualTo("light_hit"));
            Assert.That(impact.DragonSeconds,Is.EqualTo(0).Within(.0001));
            var after=DragonCombatChoreography.EvaluateBasicJab(t+.025f);
            Assert.That(after.DragonSeconds,Is.EqualTo(.025).Within(.0001));
            Assert.That(after.FighterSeconds,Is.EqualTo(BasicCombatProfile.JabContact).Within(.0001));
        }
        [TestCase("fighter_fight_idle",.75f)] [TestCase("fighter_jab",.55f)]
        [TestCase("dragon_0_fight_idle",1.5f)] [TestCase("dragon_0_light_hit",.30f)]
        [TestCase("dragon_0_heavy_hit",.64f)] [TestCase("dragon_0_threat",.50f)]
        public void SeparateClips_HavePersistentSpritesAndFixedGroundPivots(string name,float duration)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/DragonMMA/Animations/"+name+".anim");
            Assert.That(clip,Is.Not.Null);var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);Assert.That(keys.Last().time,Is.EqualTo(duration).Within(.001));
            var sprites=keys.Select(k=>(Sprite)k.value).ToArray();Assert.That(sprites.All(s=>s!=null),Is.True);
            Assert.That(sprites.All(s=>s.pivot.y==8),Is.True);Assert.That(sprites.Select(s=>s.rect.size).Distinct().Count(),Is.EqualTo(1));
            Assert.That(sprites.All(s=>s.name.StartsWith(name+"_")),Is.True,"No composite fighter/dragon atlas.");
            if(name=="fighter_jab")Assert.That(((Sprite)keys[3].value).name,Is.EqualTo("fighter_jab_3"));
        }
        [Test]
        public void AuthoredActors_KeepIndependentAnimatorsAndSharedContactProfile()
        {
            var hunter=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/Actors/Hunter.prefab").GetComponent<DragonActorView>();
            var dragon=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/Actors/Dragon.prefab").GetComponent<DragonActorView>();
            Assert.That(hunter.BasicCombat,Is.Not.Null);Assert.That(dragon.BasicCombat,Is.SameAs(hunter.BasicCombat));
            Assert.That(hunter.GetComponentInChildren<Animator>(),Is.Not.SameAs(dragon.GetComponentInChildren<Animator>()));
            Assert.That(hunter.BasicCombat.dragonHeadPixels.Length,Is.EqualTo(8));
        }
    }
}
