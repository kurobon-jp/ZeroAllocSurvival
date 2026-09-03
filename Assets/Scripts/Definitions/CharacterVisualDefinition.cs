using System;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Definitions
{
    [Serializable]
    public struct AnimationClipRange
    {
        [Min(0)] public int startFrame;
        [Min(1)] public int frameCount;
        [Min(.01f)] public float fps;
    }

    [CreateAssetMenu(menuName = "Zero Alloc Survival/Character Visual", fileName = "CharacterVisual")]
    public sealed class CharacterVisualDefinition : ScriptableObject
    {
        [SerializeField] private Texture2D atlasTexture;
        [SerializeField] private Vector3 visualOffset;
        [SerializeField] private Vector3 visualScale = Vector3.one;
        [SerializeField, Min(1)] private int columns = 1;
        [SerializeField, Min(1)] private int rows = 1;
        [SerializeField, Min(1)] private int atlasColumns = 1;
        [SerializeField, Min(1)] private int atlasRows = 1;
        [SerializeField, Min(0)] private int atlasColumnOffset;
        [SerializeField, Min(0)] private int atlasRowOffset;
        [SerializeField] private AnimationClipRange idle;
        [SerializeField] private AnimationClipRange walk;
        [SerializeField] private AnimationClipRange dead;
        [SerializeField] private bool guaranteeEveryFrame;

        public Texture2D AtlasTexture => atlasTexture;
        public Vector3 VisualOffset => visualOffset;
        public Vector3 VisualScale => visualScale;
        public int AtlasColumns => Mathf.Max(1, atlasColumns);
        public int AtlasRows => Mathf.Max(1, atlasRows);

        internal CharacterAnimationState CreateAnimationState(float playbackSpeed, float phaseSeconds) => new()
        {
            Columns = (byte)Mathf.Clamp(columns, 1, byte.MaxValue),
            Rows = (byte)Mathf.Clamp(rows, 1, byte.MaxValue),
            AtlasColumns = (byte)Mathf.Clamp(atlasColumns, 1, byte.MaxValue),
            AtlasRows = (byte)Mathf.Clamp(atlasRows, 1, byte.MaxValue),
            AtlasColumnOffset = (byte)Mathf.Clamp(atlasColumnOffset, 0, byte.MaxValue),
            AtlasRowOffset = (byte)Mathf.Clamp(atlasRowOffset, 0, byte.MaxValue),
            IdleStart = (byte)Mathf.Clamp(idle.startFrame, 0, byte.MaxValue),
            IdleCount = (byte)Mathf.Clamp(idle.frameCount, 1, byte.MaxValue),
            WalkStart = (byte)Mathf.Clamp(walk.startFrame, 0, byte.MaxValue),
            WalkCount = (byte)Mathf.Clamp(walk.frameCount, 1, byte.MaxValue),
            DeadStart = (byte)Mathf.Clamp(dead.startFrame, 0, byte.MaxValue),
            DeadCount = (byte)Mathf.Clamp(dead.frameCount, 1, byte.MaxValue),
            IdleFps = Mathf.Max(.01f, idle.fps),
            WalkFps = Mathf.Max(.01f, walk.fps),
            DeadFps = Mathf.Max(.01f, dead.fps),
            PlaybackSpeed = Mathf.Max(.01f, playbackSpeed),
            LoopPhaseSeconds = Mathf.Max(0f, phaseSeconds),
            GuaranteeEveryFrame = guaranteeEveryFrame ? (byte)1 : (byte)0
        };
    }
}
