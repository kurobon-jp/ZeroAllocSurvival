using System;
using UnityEngine;

namespace ZeroAllocSurvival.Definitions
{
    [Serializable]
    public struct CharacterParameters
    {
        [Min(1f)] public float health;
        [Min(0f)] public float attackPower;
        [Min(0.01f)] public float contactDamageInterval;
        [Min(0f)] public float moveSpeed;
        [Min(0.01f)] public float collisionRadius;
    }
}

