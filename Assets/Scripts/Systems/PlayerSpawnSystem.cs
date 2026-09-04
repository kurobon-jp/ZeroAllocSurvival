using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Definitions;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class PlayerSpawnSystem : CharacterSpawnSystem, IInitializable
    {
        private readonly CharacterDefinition _definition;
        private readonly bool _invincible;
        
        public PlayerSpawnSystem(World world, CharacterSlotRegistry slots, CharacterDefinition definition,
            CharacterVisualRegistry visuals, bool invincible) : base(world, slots, visuals)
        {
            _definition = definition;
            _invincible = invincible;
        }

        void IInitializable.Initialize()
        {
            var parameters = _definition.Parameters;
            using (World.BeginStructuralBatch())
            {
                var playerEntity = Spawn(Vector3.zero, false, _definition);
                if (_invincible) playerEntity.Add(default(Invincible));
                playerEntity.Add(new AutopilotMovement());
                playerEntity.Add(new PrimaryFireDirection { Value = Vector2.up });
                playerEntity.Add(new PlayerProgress { Level = 1, RequiredExperience = 5 });
                playerEntity.Add(new PlayerUpgradeLevels
                {
                    BaseMoveSpeed = parameters.moveSpeed,
                    BaseMaxHealth = parameters.health
                });
            }
        }
    }
}
