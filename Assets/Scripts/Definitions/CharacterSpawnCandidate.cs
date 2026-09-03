using System;
using UnityEngine;

namespace ZeroAllocSurvival.Definitions
{
    [Serializable]
    public struct CharacterSpawnCandidate
    {
        public CharacterDefinition definition;
        [Min(0f)] public float weight;
    }
}
