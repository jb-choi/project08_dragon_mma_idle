using UnityEngine;

namespace DragonMMA
{
    // A read-only projection of session state. It chooses presentation, never wins or rewards.
    // Unity actors, audio, contact gates and cart world coordinates remain owned by the view.
    public static class HuntPresentation
    {
        public struct Frame
        {
            public DragonCombatChoreography.Frame Actors;
            public Vector2 FighterMotion, DragonMotion;
            public bool Walking, BasicJab, BabyResolution, RunAway, CaptureTransferred;
            public float BattleElapsed, TransportScale, CarryProgress;
            public string Beat, Action;
            public bool UsesContactProfile => BasicJab || BabyResolution;
        }

        public static Frame Evaluate(GameSaveData data, DragonRuntimeConfig rules, bool hasContactProfile,
            float time, float jabTravel, float powerPunchTravel, float upgradedPunchTravel)
        {
            bool fighting = data.phase == HuntPhase.Fighting;
            bool walking = data.phase == HuntPhase.Walking;
            bool baby = data.currentEnemy == (int)DragonKind.Baby && hasContactProfile;
            var frame = new Frame
            {
                Walking = walking,
                BasicJab = fighting && baby,
                BabyResolution = baby && (data.phase == HuntPhase.Result || data.phase == HuntPhase.Recovery),
                BattleElapsed = Mathf.Max(0, rules.BattleSeconds - data.phaseRemaining),
                TransportScale = 1,
                Beat = "이동 중",
                Action = "자동 사냥 중",
                Actors = new DragonCombatChoreography.Frame
                {
                    FighterPose = walking ? "walk" : "idle", DragonPose = "idle",
                    FighterSeconds = time, DragonSeconds = time,
                    FighterScale = Vector2.one, DragonScale = Vector2.one,
                    ContactAge = -1, ContactKey = -1,
                    FighterState = CombatActorState.Ready, DragonState = CombatActorState.Ready
                }
            };

            if (fighting) EvaluateFight(ref frame, data, rules, jabTravel, powerPunchTravel, upgradedPunchTravel);
            else if (walking)
            {
                var walk = AnimationMotionQuality.FighterWalk(time, FighterCombatTiming.Duration("walk"));
                frame.Actors.FighterScale = walk.Scale;
                frame.Actors.FighterRotation = walk.Rotation;
            }
            else if (data.phase == HuntPhase.Result) EvaluateResult(ref frame, data, rules);
            else if (data.phase == HuntPhase.Recovery) EvaluateRecovery(ref frame, data, rules);

            if (frame.BabyResolution) ApplyBabyContinuity(ref frame, data, time);
            return frame;
        }

        private static void EvaluateFight(ref Frame frame, GameSaveData data, DragonRuntimeConfig rules,
            float jabTravel, float powerPunchTravel, float upgradedPunchTravel)
        {
            var actors = frame.BasicJab ? BabyFirstBattle.Evaluate(frame.BattleElapsed, rules.BattleSeconds) :
                DragonCombatChoreography.Evaluate((DragonKind)Mathf.Clamp(data.currentEnemy, 0, 4),
                    frame.BattleElapsed, data.trainingRewardsClaimed);
            frame.Actors = actors;
            frame.FighterMotion = actors.FighterOffset + actors.StageOffset;
            frame.DragonMotion = actors.DragonOffset + actors.StageOffset;
            string pose = actors.FighterPose;
            float travel = pose == "cross" ? data.trainingRewardsClaimed[2] ? upgradedPunchTravel : powerPunchTravel :
                pose == "takedown" ? jabTravel + 28 : pose == "clinch" ? jabTravel + 20 : pose == "kick" ? jabTravel + 12 : jabTravel;
            frame.FighterMotion.x += actors.FighterTravel * travel;
            frame.Beat = frame.BasicJab ? BabyFirstBattle.BeatLabel(frame.BattleElapsed, rules.BattleSeconds) :
                actors.Pattern == DragonCombatChoreography.ExchangePattern.Power ? "강공 · 무게를 버텨라" :
                actors.Pattern == DragonCombatChoreography.ExchangePattern.Rush ? "속공 · 박자를 읽어라" : "탐색 · 거리를 재라";
            frame.Action = frame.BasicJab ? actors.Cue : Technique(pose, actors.Cue, data, rules);
            frame.Action += $"   응원 +{data.cheerBonus:0.0}%p";
        }

        private static string Technique(string pose, string cue, GameSaveData data, DragonRuntimeConfig rules)
        {
            switch (pose)
            {
                case "takedown": return rules.GetSpec(DragonKind.Giant).skillName;
                case "clinch": return rules.GetSpec(DragonKind.Stonehorn).skillName;
                case "cross": return rules.GetSpec(data.trainingRewardsClaimed[2] ? DragonKind.Logtail : DragonKind.Headbutt).skillName;
                case "kick": return "킥 반격";
                case "punch": case "jab": return "왼손 잽";
                default: return cue;
            }
        }

        private static void EvaluateResult(ref Frame frame, GameSaveData data, DragonRuntimeConfig rules)
        {
            frame.Beat = "결착 · 포획 완료";
            frame.Actors.FighterPose = "victory"; frame.Actors.DragonPose = "defeat";
            frame.Actors.FighterState = CombatActorState.Victory; frame.Actors.DragonState = CombatActorState.Down;
            float elapsed = rules.WinResultSeconds - data.phaseRemaining;
            frame.Actors.FighterSeconds = elapsed; frame.Actors.DragonSeconds = elapsed;
            float progress = 1 - Mathf.Clamp01(data.phaseRemaining / rules.WinResultSeconds);
            if (data.lastWasExtortion)
            {
                if (progress >= .55f && progress < .72f)
                    frame.Actors.DragonSeconds = Mathf.Lerp(.80f, 0, (progress - .55f) / .17f);
                if (progress >= .72f)
                {
                    frame.Actors.DragonPose = "walk"; frame.Actors.DragonSeconds = elapsed;
                    frame.RunAway = true; frame.DragonMotion.x = (progress - .72f) * 2200;
                }
            }
            else
            {
                frame.CarryProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .55f) / .45f));
                frame.TransportScale = Mathf.Lerp(1, .35f, frame.CarryProgress);
                frame.CaptureTransferred = progress >= .94f;
            }
        }

        private static void EvaluateRecovery(ref Frame frame, GameSaveData data, DragonRuntimeConfig rules)
        {
            frame.Beat = "열세 · 자세를 회복하라";
            float elapsed = rules.RecoverySeconds - data.phaseRemaining;
            float riseAt = Mathf.Max(FighterCombatTiming.Duration("knockdown"),
                rules.RecoverySeconds - FighterCombatTiming.Duration("recovery"));
            bool down = elapsed < riseAt;
            frame.Actors.FighterPose = down ? "knockdown" : "recovery";
            frame.Actors.FighterState = down ? CombatActorState.Down : CombatActorState.Recover;
            frame.Actors.DragonState = CombatActorState.Victory;
            frame.Actors.FighterSeconds = down ? elapsed : elapsed - riseAt;
            float progress = Mathf.Clamp01(elapsed / .7f);
            float returnToFeet = down ? 1 : 1 - Mathf.Clamp01(frame.Actors.FighterSeconds / FighterCombatTiming.Duration("recovery"));
            frame.FighterMotion.x = -progress * 22 * returnToFeet;
            frame.DragonMotion.x = Mathf.Max(0, rules.RecoverySeconds - 1 - data.phaseRemaining) * 200;
            if (elapsed > 1)
            {
                frame.Actors.DragonPose = "walk"; frame.Actors.DragonSeconds = elapsed - 1;
                frame.RunAway = true;
            }
        }

        private static void ApplyBabyContinuity(ref Frame frame, GameSaveData data, float time)
        {
            if (data.phase == HuntPhase.Result)
            {
                frame.Actors.FighterPose = "fight_idle";
                frame.Actors.DragonPose = data.lastWasExtortion && frame.RunAway ? "fight_idle" : "yield";
            }
            else
            {
                frame.Actors.DragonPose = "fight_idle"; frame.Actors.DragonSeconds = time;
                frame.DragonMotion = Vector2.zero; frame.RunAway = false;
            }
            frame.Actors.FighterOffset = frame.FighterMotion;
            frame.Actors.DragonOffset = frame.DragonMotion;
        }

        public static float BabyRecoveryOpacity(float elapsed) => 1 - Mathf.Clamp01((elapsed - 1.1f) / .4f);
    }
}
