using UnityEngine;

namespace DragonMMA
{
    // Pure shared-clock choreography: both actors and the impact use the same elapsed time.
    // Three deterministic rhythms vary pose, distance and beat without changing combat rules.
    public static class DragonCombatChoreography
    {
        public enum ExchangePattern { Probe, Rush, Power }

        private struct Rhythm
        {
            public float Read, WindupScale, Between, Counter, Rest;
            public Rhythm(float read, float windupScale, float between, float counter, float rest)
            { Read = read; WindupScale = windupScale; Between = between; Counter = counter; Rest = rest; }
        }

        private static readonly float[] Lunges = { 12, 14, 16, 20, 24 };
        private static readonly string[] Threats = { "새끼용 · 짧은 도약", "박치기용 · 뿔 돌진", "통나무꼬리용 · 꼬리 휘두르기", "돌뿔용 · 중량 밀치기", "숲 거구룡 · 앞발 강타" };

        public struct Frame
        {
            public string FighterPose, DragonPose, Cue;
            public float FighterSeconds, DragonSeconds, FighterTravel, FighterRotation, DragonRotation;
            public Vector2 FighterOffset, DragonOffset, StageOffset, FighterScale, DragonScale;
            public float ContactAge, ContactAt, Telegraph, HitStopSeconds, CycleStart;
            public int ContactKey, CycleIndex;
            public bool Defending, Guarded, Powered, Hit, Held;
            public ExchangePattern Pattern;
            public CombatImpactKind Impact;
            public CombatActorState FighterState, DragonState;
        }

        private static bool Unlocked(bool[] rewards, int index) => rewards != null && rewards.Length > index && rewards[index];
        private static Rhythm RhythmFor(int cycle)
        {
            switch (Pattern(cycle))
            {
                case ExchangePattern.Rush: return new Rhythm(.24f, 1f, .12f, 1.04f, .30f);
                case ExchangePattern.Power: return new Rhythm(.34f, 1.12f, .16f, 1.16f, .28f);
                default: return new Rhythm(.16f, .84f, .08f, .98f, .24f);
            }
        }

        public static ExchangePattern Pattern(int cycle) => (ExchangePattern)(Mathf.Abs(cycle) % 3);
        public static float EnemyStart(DragonKind kind) => EnemyStart(kind, 0);
        public static float EnemyStart(DragonKind kind, int cycle)
        {
            var rhythm = RhythmFor(cycle);
            return rhythm.Read + DragonAnimationTiming.Windup(kind) * rhythm.WindupScale;
        }
        public static CombatImpactKind EnemyImpact(int cycle)
        {
            switch (Pattern(cycle))
            {
                case ExchangePattern.Rush: return CombatImpactKind.Weak;
                case ExchangePattern.Power: return CombatImpactKind.Heavy;
                default: return CombatImpactKind.Guard;
            }
        }
        public static float CounterStart(DragonKind kind) => CounterStart(kind, 0);
        public static float CounterStart(DragonKind kind, int cycle)
        {
            var rhythm = RhythmFor(cycle);
            float hold = CombatFeedbackTiming.Get(EnemyImpact(cycle)).HitStopSeconds;
            return EnemyStart(kind, cycle) + DragonAnimationTiming.Duration(kind, "attack") + hold + rhythm.Between;
        }
        public static float CycleSeconds(DragonKind kind) => CycleSeconds(kind, 0);
        public static float CycleSeconds(DragonKind kind, int cycle)
        {
            var rhythm = RhythmFor(cycle);
            return CounterStart(kind, cycle) + rhythm.Counter + rhythm.Rest;
        }
        public static float SequenceSeconds(DragonKind kind) => CycleSeconds(kind, 0) + CycleSeconds(kind, 1) + CycleSeconds(kind, 2);

        public static string Counter(DragonKind kind, int cycle, bool[] rewards)
        {
            if (kind == DragonKind.Giant && cycle % 3 == 2 && Unlocked(rewards, 4)) return "takedown";
            if ((int)kind >= (int)DragonKind.Stonehorn && cycle % 3 == 2 && Unlocked(rewards, 3)) return "clinch";
            if (kind == DragonKind.Baby) return cycle % 2 == 0 ? "punch" : "kick";
            if (kind == DragonKind.Logtail && cycle % 2 == 0) return "kick";
            if (cycle % 2 == 1) return "kick";
            return Unlocked(rewards, 1) ? "cross" : "punch";
        }

        public static void Locate(DragonKind kind, float elapsed, out int cycle, out float cycleStart, out float beat)
        {
            elapsed = Mathf.Max(0, elapsed);
            float sequence = SequenceSeconds(kind);
            int group = Mathf.FloorToInt(elapsed / sequence);
            cycle = group * 3;
            cycleStart = group * sequence;
            beat = elapsed - cycleStart;
            for (int variant = 0; variant < 2; variant++)
            {
                float length = CycleSeconds(kind, cycle);
                if (beat < length) return;
                beat -= length;
                cycleStart += length;
                cycle++;
            }
        }

        public static float NextContact(DragonKind kind, float elapsed, bool[] rewards)
        {
            Locate(kind, elapsed, out int cycle, out float start, out _);
            float enemy = start + EnemyStart(kind, cycle) + DragonAnimationTiming.Contact(kind);
            string counterPose = Counter(kind, cycle, rewards);
            float counter = start + CounterStart(kind, cycle) + FighterCombatTiming.Contact(counterPose);
            if (elapsed < enemy - .0001f) return enemy;
            if (elapsed < counter - .0001f) return counter;
            start += CycleSeconds(kind, cycle);
            cycle++;
            return start + EnemyStart(kind, cycle) + DragonAnimationTiming.Contact(kind);
        }

        public static Frame Evaluate(DragonKind kind, float elapsed, bool[] rewards)
        {
            Locate(kind, elapsed, out int cycle, out float cycleStart, out float beat);
            var rhythm = RhythmFor(cycle);
            var pattern = Pattern(cycle);
            var f = new Frame
            {
                FighterPose = "idle", DragonPose = "idle", FighterSeconds = beat, DragonSeconds = beat,
                ContactAge = -1, ContactKey = -1, Cue = "거리 확인 · 가드 대기", CycleIndex = cycle,
                CycleStart = cycleStart, Pattern = pattern, FighterState = CombatActorState.Ready, DragonState = CombatActorState.Ready,
                FighterOffset = new Vector2(AnimationMotionQuality.FighterCombatStanceOffset, 0),
                FighterScale = Vector2.one, DragonScale = Vector2.one
            };
            float enemyStart = EnemyStart(kind, cycle), counterStart = CounterStart(kind, cycle);
            float windupStart = rhythm.Read;
            if (beat >= windupStart && beat < enemyStart)
            {
                f.DragonPose = "windup";
                f.DragonSeconds = (beat - windupStart) / rhythm.WindupScale;
                f.DragonState = CombatActorState.Windup;
                float anticipation = Mathf.Clamp01((beat - windupStart) / Mathf.Max(.01f, enemyStart - windupStart));
                float coil = Mathf.Sin(anticipation * Mathf.PI * .5f);
                f.DragonScale = new Vector2(1 + coil * .07f, 1 - coil * .10f);
                f.DragonOffset.y = -coil * 5;
                f.Cue = Threats[Mathf.Clamp((int)kind, 0, 4)] + (pattern == ExchangePattern.Power ? " · 강공 준비" : pattern == ExchangePattern.Rush ? " · 박자 변화" : " 준비");
                return f;
            }
            if (beat >= enemyStart && beat < counterStart)
            {
                float local = beat - enemyStart, contact = DragonAnimationTiming.Contact(kind);
                f.Impact = EnemyImpact(cycle);
                var preset = CombatFeedbackTiming.Get(f.Impact);
                float held = FighterCombatTiming.HeldTime(local, contact, preset.HitStopSeconds);
                f.DragonPose = local < DragonAnimationTiming.Duration(kind, "attack") + preset.HitStopSeconds ? "attack" : "idle";
                f.DragonSeconds = held; f.Defending = true; f.Guarded = pattern != ExchangePattern.Power;
                f.ContactAge = local - contact; f.ContactAt = cycleStart + enemyStart + contact; f.ContactKey = cycle * 2;
                f.Telegraph = CombatFeedbackTiming.Telegraph(f.ContactAge); f.HitStopSeconds = preset.HitStopSeconds;
                f.Held = f.ContactAge >= 0 && f.ContactAge < preset.HitStopSeconds;
                float strike = Travel(held, contact, DragonAnimationTiming.Duration(kind, "attack"));
                f.DragonOffset.x = -strike * Lunges[Mathf.Clamp((int)kind, 0, 4)];
                if (kind == DragonKind.Baby) f.DragonOffset.y = Mathf.Sin(strike * Mathf.PI) * 12;
                if (kind == DragonKind.Logtail) f.DragonRotation = strike * -2;
                if (local >= contact - .12f && f.ContactAge < .40f)
                {
                    f.FighterPose = f.Guarded ? "block" : "hurt";
                    f.FighterSeconds = FighterCombatTiming.HeldTime(local - contact + .12f, .12f, preset.HitStopSeconds);
                    f.FighterState = f.Guarded ? CombatActorState.Guard : CombatActorState.Hit;
                }
                float reaction = Reaction(f.ContactAge, .28f, preset.HitStopSeconds);
                float emphasis = pattern == ExchangePattern.Power ? 1.35f : pattern == ExchangePattern.Rush ? .85f : 1f;
                float contactWeight = f.ContactAge < 0 ? f.Telegraph * .55f : f.Held ? 1 : reaction * .55f;
                f.DragonScale = new Vector2(1 + contactWeight * .10f * emphasis, 1 - contactWeight * .08f * emphasis);
                f.FighterScale = f.Guarded
                    ? new Vector2(1 - contactWeight * .06f, 1 + contactWeight * .08f)
                    : new Vector2(1 + contactWeight * .12f, 1 - contactWeight * .14f);
                f.FighterOffset.x = -reaction * (pattern == ExchangePattern.Rush ? 8 : (int)kind >= (int)DragonKind.Stonehorn ? 20 : 13);
                if (pattern == ExchangePattern.Rush) f.FighterOffset.y = reaction * 5;
                f.FighterRotation = f.Guarded ? pattern == ExchangePattern.Rush ? -2 * reaction : 0 : reaction * 3;
                f.StageOffset = new Vector2(-CombatFeedbackTiming.StageKick(f.ContactAge, f.Impact), 0);
                f.Hit = f.ContactAge >= 0 && f.ContactAge < Mathf.Max(.09f, preset.FlashSeconds);
                f.DragonState = CombatActorState.Attack;
                f.Cue = pattern == ExchangePattern.Rush ? "흘리기 · 반격 각도 확보" : f.FighterPose == "block" ? "가드 · 반격 준비" : f.FighterPose == "hurt" ? "강타 피격 · 자세 회복" : Threats[(int)kind];
                return f;
            }
            if (beat >= counterStart && beat < counterStart + rhythm.Counter)
            {
                float local = beat - counterStart;
                string pose = Counter(kind, cycle, rewards);
                float contact = FighterCombatTiming.Contact(pose);
                f.Impact = pose == "punch" ? CombatImpactKind.Weak : CombatImpactKind.Heavy;
                var preset = CombatFeedbackTiming.Get(f.Impact);
                f.FighterPose = pose; f.FighterSeconds = FighterCombatTiming.HeldTime(local, contact, preset.HitStopSeconds);
                f.FighterTravel = FighterCombatTiming.Travel(pose, f.FighterSeconds);
                f.Powered = pose != "punch"; f.ContactAge = local - contact; f.HitStopSeconds = preset.HitStopSeconds;
                f.Telegraph = CombatFeedbackTiming.Telegraph(f.ContactAge);
                f.ContactAt = cycleStart + counterStart + contact; f.ContactKey = cycle * 2 + 1;
                f.Held = f.ContactAge >= 0 && f.ContactAge < preset.HitStopSeconds;
                float reaction = Reaction(f.ContactAge, .32f, preset.HitStopSeconds);
                float emphasis = f.Impact == CombatImpactKind.Heavy ? 1.25f : .82f;
                float contactWeight = f.ContactAge < 0 ? f.Telegraph * .55f : f.Held ? 1 : reaction * .55f;
                f.FighterScale = new Vector2(1 + contactWeight * .11f * emphasis, 1 - contactWeight * .08f * emphasis);
                f.DragonScale = new Vector2(1 + contactWeight * .13f * emphasis, 1 - contactWeight * .16f * emphasis);
                if (f.ContactAge >= 0 && f.ContactAge < .40f)
                {
                    f.DragonPose = "hurt";
                    f.DragonSeconds = .12f + FighterCombatTiming.HeldTime(f.ContactAge, 0, preset.HitStopSeconds);
                    f.DragonState = f.ContactAge < preset.FlashSeconds + .12f ? CombatActorState.Hit : CombatActorState.Recover;
                }
                f.DragonOffset.x = reaction * (pose == "punch" ? 10 : 16);
                if (pose == "clinch") { f.DragonOffset.x -= f.FighterTravel * 14; f.FighterRotation = -f.FighterTravel * 4; }
                if (pose == "takedown") { f.DragonOffset.y = reaction * 7; f.DragonRotation = reaction * 4; }
                f.StageOffset = new Vector2(CombatFeedbackTiming.StageKick(f.ContactAge, f.Impact), 0);
                f.Hit = f.ContactAge >= 0 && f.ContactAge < Mathf.Max(.09f, preset.FlashSeconds);
                f.FighterState = CombatActorState.Attack;
                f.Cue = pose;
                if (f.FighterSeconds > FighterCombatTiming.Duration(pose) + .07f)
                {
                    f.FighterPose = "idle"; f.FighterSeconds = local; f.Cue = "가드 복귀";
                    f.FighterState = CombatActorState.Recover;
                }
            }
            return f;
        }

        // First-pair quality loop. It uses the same Frame contract and shared clock
        // as the existing species choreography, with no gameplay damage events.
        public static Frame EvaluateBasicJab(float elapsed)
        {
            elapsed = Mathf.Max(0, elapsed);
            int cycle = Mathf.FloorToInt(elapsed / BasicCombatProfile.CycleSeconds);
            float start = cycle * BasicCombatProfile.CycleSeconds;
            float beat = elapsed - start;
            float local = beat - BasicCombatProfile.IdleSeconds;
            var f = new Frame
            {
                FighterPose = "fight_idle", DragonPose = "fight_idle",
                FighterSeconds = beat < BasicCombatProfile.IdleSeconds ? beat : 0,
                DragonSeconds = beat < BasicCombatProfile.IdleSeconds ? beat : 0,
                FighterScale = Vector2.one, DragonScale = Vector2.one,
                FighterState = CombatActorState.Ready, DragonState = CombatActorState.Ready,
                ContactAge = -1, ContactKey = -1, CycleStart = start, CycleIndex = cycle,
                Pattern = ExchangePattern.Probe, Cue = "가드 · 앞발과 뒷발 체중 이동"
            };
            if (local < 0 || local > BasicCombatProfile.JabSeconds + BasicCombatProfile.HitHold) return f;
            f.FighterPose = "jab";
            f.FighterSeconds = FighterCombatTiming.HeldTime(local, BasicCombatProfile.JabContact, BasicCombatProfile.HitHold);
            f.FighterState = local < BasicCombatProfile.JabContact ? CombatActorState.Windup : CombatActorState.Attack;
            f.ContactAge = local - BasicCombatProfile.JabContact;
            f.ContactAt = start + BasicCombatProfile.IdleSeconds + BasicCombatProfile.JabContact;
            f.ContactKey = cycle;
            f.Impact = CombatImpactKind.Weak; f.HitStopSeconds = BasicCombatProfile.HitHold;
            f.Held = f.ContactAge >= 0 && f.ContactAge < BasicCombatProfile.HitHold;
            f.Hit = f.ContactAge >= 0 && f.ContactAge < .07f;
            if (f.ContactAge >= 0 && f.ContactAge < BasicCombatProfile.LightHitSeconds)
            {
                f.DragonPose = "light_hit";
                f.DragonSeconds = f.ContactAge; // Recoil starts at impact; only the glove holds briefly.
                f.DragonState = f.ContactAge < .12f ? CombatActorState.Hit : CombatActorState.Recover;
            }
            f.Cue = f.ContactAge < 0 ? "앞발 체중 · 왼 어깨 · 왼손 잽" : f.ContactAge < .12f ? "주먹 ↔ 머리 접촉" : "팔 회수 · 가드 복귀";
            if (f.FighterSeconds > .30f) f.FighterState = CombatActorState.Recover;
            return f;
        }

        private static float Reaction(float age, float recovery, float hitStopSeconds) =>
            age < 0 ? 0 : 1 - Mathf.SmoothStep(0, 1, Mathf.Max(0, age - hitStopSeconds) / recovery);

        private static float Travel(float seconds, float contact, float duration)
            => AnimationMotionQuality.AttackTravel(seconds, contact, duration, -.04f);
    }
}
