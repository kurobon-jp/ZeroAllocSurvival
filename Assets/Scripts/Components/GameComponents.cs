using LitheEcs;
using UnityEngine;
using EntityId = LitheEcs.EntityId;

namespace ZeroAllocSurvival.Components
{
    internal struct PlayerTag : ISingleton
    {
    }

    internal struct EnemyTag
    {
    }

    /// <summary>Prevents health loss and knockback while preserving hit feedback.</summary>
    internal struct Invincible
    {
    }

    public struct GamePause
    {
    }

    public struct LevelUp
    {
        internal UpgradeChoice First;
        internal UpgradeChoice Second;
        internal UpgradeChoice Third;
        internal byte ChoiceCount;
        internal byte ChoicesGenerated;
    }

    /// <summary>Selects the registered BatchRendererGroup presentation for a character.</summary>
    internal struct BatchVisual
    {
        public int BatchId;
        public float Depth;
        public byte SortByY;
    }

    internal struct CharacterState
    {
        public float Health;
        public float MaxHealth;
        public float AttackPower;
        public float MoveSpeed;
        public Vector2 KnockbackVelocity;
    }

    internal struct AutopilotMovement
    {
        public Vector3 Direction;
    }

    /// <summary>Last non-zero movement direction used by the primary weapon.</summary>
    internal struct PrimaryFireDirection
    {
        public Vector2 Value;
    }

    internal struct ExperienceReward
    {
        public int Value;
    }

    internal struct ExperienceDrop
    {
        public int Value;
        public Vector3 Position;
        public float Speed;
    }

    internal struct ExperienceCollected
    {
        public int Value;
    }

    internal struct PlayerProgress
    {
        public int Level;
        public int Experience;
        public int RequiredExperience;
    }

    internal enum UpgradeKind : byte
    {
        UnlockWeapon,
        WeaponLevel,
        PlayerStat
    }

    internal enum PlayerStatKind : byte
    {
        MoveSpeed,
        WeaponInterval,
        MaxHealth,
        AttackPower,
        ExperienceGain
    }

    internal struct UpgradeChoice
    {
        public UpgradeKind Kind;
        public int Index;
        public byte NextLevel;
    }

    internal struct UpgradeSelected
    {
        public UpgradeChoice Choice;
    }

    internal struct PlayerUpgradeLevels
    {
        public float BaseMoveSpeed;
        public float BaseMaxHealth;
        public byte MoveSpeed;
        public byte WeaponInterval;
        public byte MaxHealth;
        public byte AttackPower;
        public byte ExperienceGain;
    }

    /// <summary> ECS-side mirror of the physics pose, separate from the interpolated render Transform. </summary>
    internal struct PhysicsPosition
    {
        public Vector3 Value;
    }

    /// <summary>Classification used when inserting a character into CharacterSpatialHash.</summary>
    internal struct SpatialGroup
    {
        public int Value;
    }

    internal struct CollisionRadius
    {
        public float Value;
    }

    internal struct CharacterSlot
    {
        public int Value;
    }

    /// <summary>Shared LocalAvoidance agent state for both the player and enemies.</summary>
    internal struct CrowdAgent
    {
        public EntityId EntityId;
        public Vector2 DesiredVelocity;
        /// <summary>Movement intent used for presentation, excluding knockback and avoidance corrections.</summary>
        public Vector2 FacingVelocity;
        public Vector2 CurrentVelocity;
        public float Radius;
        public float Mass;
        public float AvoidanceWeight;
        public float CorrectionVelocityWeight;
        public float MaximumCorrectionSpeed;
        public byte AvoidancePriority;
        public uint Layer;
        public uint CollisionMask;
        public uint ContactEventMask;
        public byte RequiresImmediateApply;
        public byte DirectControl;
        public byte StableContactResolution;
    }

    internal struct Target
    {
    }

    public struct Owner
    {
    }

    /// <summary>Emitted once when two entities begin touching.</summary>
    public struct ContactEntered
    {
        public Entity Source;
        public Entity Other;
    }

    /// <summary>Emitted once when two entities stop touching.</summary>
    public struct ContactExited
    {
        public Entity Other;
    }

    /// <summary>Periodic damage applied while this enemy remains in contact with the target.</summary>
    internal struct ContactDamage
    {
        public Entity Target;
        public float Interval;
        public float Timer;
    }

    /// <summary>Per-character contact attack configuration copied from CharacterParameters.</summary>
    internal struct ContactAttack
    {
        public float Interval;
    }

    internal struct Damage
    {
        public float Value;
        public Entity Target;
        public Vector2 Knockback;
    }

    internal struct Dead
    {
        public float RemainingFade;
        public float FadeDuration;
        public byte Initialized;
    }

    internal enum CharacterAnimationClip : byte
    {
        Idle,
        Walk,
        Dead
    }

    internal struct CharacterAnimationState
    {
        public byte Columns;
        public byte Rows;
        public byte AtlasColumns;
        public byte AtlasRows;
        public byte AtlasColumnOffset;
        public byte AtlasRowOffset;
        public byte IdleStart;
        public byte IdleCount;
        public byte WalkStart;
        public byte WalkCount;
        public byte DeadStart;
        public byte DeadCount;
        public float IdleFps;
        public float WalkFps;
        public float DeadFps;
        public float PlaybackSpeed;
        public float LoopPhaseSeconds;
        public float FrameAccumulator;
        public byte CurrentFrame;
        public byte GuaranteeEveryFrame;
        public CharacterAnimationClip AppliedClip;
        public byte AppliedFacingLeft;
        public byte FacingInitialized;
        public byte IsWalking;
        public byte Initialized;
    }

    internal struct CharacterVisualFeedback
    {
        public float HitFlashRemaining;
        public float AppliedEmission;
        public float AppliedFade;
        public byte HasAppliedEffect;
    }
    
    // Simulation positions remain on Z = 0; rendering systems alone apply these depths.
    // The camera looks along +Z, therefore smaller values appear in front.
    internal static class VisualDepth
    {
        public const float Projectile = -.5f;
        public const float Weapon = -.45f;
        public const float Player = -.4f;
        public const float Enemy = -.3f;
        public const float Experience = -.2f;
        public const float Explosion = -.1f;
    }
}
