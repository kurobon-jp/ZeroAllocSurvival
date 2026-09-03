using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class CharacterAnimationSystem : QueryActionSystem<CharacterAnimationState, BatchVisual>
    {
        private const float StartWalkingSpeedSquared = .04f;
        private const float StopWalkingSpeedSquared = .0025f;

        public CharacterAnimationSystem(World world) : base(world)
        {
        }

        protected override void ForEach(in Entity entity, ref CharacterAnimationState animation,
            ref BatchVisual batchVisual)
        {
            var clip = CharacterAnimationClip.Idle;
            var horizontalVelocity = 0f;
            var directControl = false;
            if (entity.Has<Dead>())
                clip = CharacterAnimationClip.Dead;
            else if (entity.TryGet<CrowdAgent>(out var agent))
            {
                directControl = agent.DirectControl != 0;
                var isPlayer = entity.Has<PlayerTag>();
                // Autopilot can keep a movement request while avoidance or targeting leaves the
                // character physically stationary. In that case the visual must remain Idle.
                // Direct input still considers DesiredVelocity so Walk reacts without a frame of lag.
                // The player must also remain Idle when crowd correction moves it without any
                // movement intent; CurrentVelocity contains that external push velocity.
                var movementSquared = isPlayer && agent.DesiredVelocity.sqrMagnitude <= StopWalkingSpeedSquared
                    ? 0f
                    : directControl
                        ? Mathf.Max(agent.CurrentVelocity.sqrMagnitude, agent.DesiredVelocity.sqrMagnitude)
                        : agent.CurrentVelocity.sqrMagnitude;
                if (animation.IsWalking != 0)
                {
                    if (movementSquared <= StopWalkingSpeedSquared) animation.IsWalking = 0;
                }
                else if (movementSquared >= StartWalkingSpeedSquared)
                {
                    animation.IsWalking = 1;
                }

                if (animation.IsWalking != 0)
                    clip = CharacterAnimationClip.Walk;
                if (isPlayer && entity.TryGet<PrimaryFireDirection>(out var aimDirection))
                    horizontalVelocity = aimDirection.Value.x;
                else
                    horizontalVelocity = agent.FacingVelocity.x;
            }

            if (Mathf.Abs(horizontalVelocity) > .05f)
            {
                var facingLeft = horizontalVelocity < 0f ? (byte)1 : (byte)0;
                if (animation.FacingInitialized == 0 || animation.AppliedFacingLeft != facingLeft)
                {
                    animation.AppliedFacingLeft = facingLeft;
                    animation.FacingInitialized = 1;
                }
            }

            if (animation.Initialized != 0 && animation.AppliedClip == clip)
            {
                AdvanceManualFrame(ref animation, clip);
                return;
            }

            animation.Initialized = 1;
            animation.AppliedClip = clip;
            animation.CurrentFrame = 0;
            animation.FrameAccumulator = 0f;
        }

        private void AdvanceManualFrame(ref CharacterAnimationState animation, CharacterAnimationClip clip)
        {
            var playbackSpeed = animation.PlaybackSpeed > 0f ? animation.PlaybackSpeed : 1f;
            var fps = clip switch
            {
                CharacterAnimationClip.Idle => animation.IdleFps,
                CharacterAnimationClip.Walk => animation.WalkFps,
                _ => animation.DeadFps
            } * playbackSpeed;
            var frameCount = clip switch
            {
                CharacterAnimationClip.Idle => animation.IdleCount,
                CharacterAnimationClip.Walk => animation.WalkCount,
                _ => animation.DeadCount
            };
            var interval = 1f / Mathf.Max(.01f, fps);
            animation.FrameAccumulator += DeltaTime;
            if (animation.FrameAccumulator < interval) return;

            // Advance at most once per rendered frame. Excess time is retained, but bounded,
            // so a slow frame cannot skip a visual frame.
            animation.FrameAccumulator = Mathf.Min(animation.FrameAccumulator - interval, interval);
            if (clip == CharacterAnimationClip.Dead)
                animation.CurrentFrame = (byte)Mathf.Min(animation.CurrentFrame + 1, frameCount - 1);
            else
                animation.CurrentFrame = (byte)((animation.CurrentFrame + 1) % frameCount);
        }
    }
}
