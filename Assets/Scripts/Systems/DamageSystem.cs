using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Presentation;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class DamageSystem : QueryActionSystem<Damage>
    {
        private readonly HitscanResolutionQueue _hitscanQueue;
        private readonly GaugePresenter _hpGauge;
        private readonly bool _logHitscanDiagnostics;
        
        public DamageSystem(World world, HitscanResolutionQueue hitscanQueue, GaugePresenter hpGauge,
            bool logHitscanDiagnostics) : base(world)
        {
            _hitscanQueue = hitscanQueue;
            _hpGauge = hpGauge;
            _logHitscanDiagnostics = logHitscanDiagnostics;
        }

        protected override bool OnPreTick()
        {
            ResolveHitscans();
            return true;
        }

        protected override void OnPostTick()
        {
            CommandBuffer.Playback();
        }

        protected override void ForEach(in Entity entity, ref Damage damage)
        {
            ApplyDamage(in damage);
            CommandBuffer.Despawn(entity);
        }

        private void ApplyDamage(in Damage damage)
        {
            if (!damage.Target.TryGetRef<CharacterState>(out var state) || state.Value.Health <= 0f)
                return;

            if (damage.Target.Has<Invincible>())
            {
                TriggerHitFlash(damage.Target);
                return;
            }

            state.Value.Health -= damage.Value;
            if (damage.Target.Has<PlayerTag>())
                _hpGauge.SetProgress(state.Value.Health, state.Value.MaxHealth);

            state.Value.KnockbackVelocity = AccumulateKnockback(
                state.Value.KnockbackVelocity, damage.Knockback);
            TriggerHitFlash(damage.Target);
            if (state.Value.Health <= 0)
                CommandBuffer.AddComponent<Dead>(damage.Target);
        }

        private void ResolveHitscans()
        {
            for (var pelletIndex = 0; pelletIndex < _hitscanQueue.PelletCount; pelletIndex++)
            {
                ref readonly var pellet = ref _hitscanQueue.GetPellet(pelletIndex);
                var remainingHits = pellet.Penetration;
                var skippedDead = 0;
                var appliedHits = 0;
                var kills = 0;
                var end = pellet.CandidateStart + pellet.CandidateCount;
                for (var candidateIndex = pellet.CandidateStart;
                     candidateIndex < end && remainingHits > 0;
                     candidateIndex++)
                {
                    var targetId = _hitscanQueue.GetCandidate(candidateIndex);
                    if (!World.TryGetEntity(targetId, out var target) ||
                        !target.TryGetRef<CharacterState>(out var state))
                        continue;
                    if (state.Value.Health <= 0f)
                    {
                        skippedDead++;
                        continue;
                    }

                    state.Value.Health -= pellet.Damage;
                    TriggerHitFlash(target);
                    var targetPosition = target.Get<PhysicsPosition>().Value;
                    var knockbackDirection = targetPosition - pellet.Origin;
                    knockbackDirection.z = 0f;
                    if (knockbackDirection.sqrMagnitude > .0001f)
                        knockbackDirection.Normalize();
                    else
                        knockbackDirection = pellet.Direction;
                    state.Value.KnockbackVelocity = AccumulateKnockback(state.Value.KnockbackVelocity,
                        new Vector2(knockbackDirection.x, knockbackDirection.y) * pellet.Knockback);
                    remainingHits--;
                    appliedHits++;
                    if (state.Value.Health <= 0f)
                    {
                        kills++;
                        CommandBuffer.AddComponent<Dead>(target);
                    }
                }

                if (_logHitscanDiagnostics)
                    Debug.Log($"[Hitscan.Resolve] shot={pellet.ShotId} pellet={pellet.PelletIndex} " +
                              $"candidates={pellet.CandidateCount} skippedDead={skippedDead} " +
                              $"hits={appliedHits} kills={kills} unusedPenetration={remainingHits}");
            }

            _hitscanQueue.Clear();
        }

        private static void TriggerHitFlash(Entity target)
        {
            if (target.TryGetRef<CharacterVisualFeedback>(out var feedback))
                feedback.Value.HitFlashRemaining = CharacterVisualTiming.HitFlashDuration;
        }

        private static Vector2 AccumulateKnockback(Vector2 current, Vector2 incoming)
        {
            var maximumSpeed = Mathf.Max(current.magnitude, incoming.magnitude);
            return Vector2.ClampMagnitude(current + incoming, maximumSpeed);
        }
    }

    internal sealed class CharacterVisualFeedbackSystem : QueryActionSystem<CharacterVisualFeedback>
    {
        internal CharacterVisualFeedbackSystem(World world) : base(world)
        {
        }

        protected override Query<CharacterVisualFeedback> CreateQuery()
        {
            return base.CreateQuery().With<BatchVisual>();
        }

        protected override void ForEach(in Entity entity, ref CharacterVisualFeedback feedback)
        {
            feedback.HitFlashRemaining = Mathf.Max(0f, feedback.HitFlashRemaining - DeltaTime);
            var emission = feedback.HitFlashRemaining / CharacterVisualTiming.HitFlashDuration;
            var fade = 1f;
            var isFading = entity.TryGet<Dead>(out var dead) && dead.Initialized != 0;
            if (isFading)
                fade = Mathf.Clamp01(dead.RemainingFade / Mathf.Max(.01f, dead.FadeDuration));

            feedback.AppliedEmission = emission;
            feedback.AppliedFade = fade;
            feedback.HasAppliedEffect = emission > 0f || isFading ? (byte)1 : (byte)0;
        }
    }
}
