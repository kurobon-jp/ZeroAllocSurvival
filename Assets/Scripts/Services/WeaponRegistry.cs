using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Definitions;

namespace ZeroAllocSurvival.Services
{
    internal sealed class WeaponRegistry
    {
        private readonly World _world;
        private readonly WeaponDefinition[] _definitions;
        private readonly int[] _poolIndices;
        private readonly int[] _impactPoolIndices;

        public int Count => _definitions.Length;

        public WeaponRegistry(World world, WeaponDefinition[] definitions, ProjectilePoolRegistry projectilePools)
        {
            _world = world;
            _definitions = definitions ?? System.Array.Empty<WeaponDefinition>();
            _poolIndices = new int[_definitions.Length];
            _impactPoolIndices = new int[_definitions.Length];
            for (var i = 0; i < _definitions.Length; i++)
            {
                var definition = _definitions[i];
                _poolIndices[i] = definition == null
                    ? -1
                    : projectilePools.Register(definition.BulletPrefab, definition.BulletPrewarmCount);
                _impactPoolIndices[i] = definition == null
                    ? -1
                    : projectilePools.Register(definition.ImpactPrefab, definition.ImpactPrewarmCount);
            }
        }

        public WeaponDefinition Definition(int index) =>
            index >= 0 && index < _definitions.Length ? _definitions[index] : null;

        public int IndexOf(WeaponDefinition definition)
        {
            for (var i = 0; i < _definitions.Length; i++)
                if (_definitions[i] == definition) return i;
            return -1;
        }

        public Entity Create(Entity owner, int definitionIndex, bool visible = false)
        {
            var definition = Definition(definitionIndex);
            if (definition == null) return default;
            var runtime = definition.CreateRuntime(
                _poolIndices[definitionIndex], _impactPoolIndices[definitionIndex]);
            runtime.DefinitionIndex = definitionIndex;
            if (owner.TryGet<PlayerUpgradeLevels>(out var upgrades))
            {
                runtime.Interval = Mathf.Max(.05f, runtime.BaseInterval * (1f - upgrades.WeaponInterval * .1f));
                runtime.Damage = runtime.BaseDamage * (1f + upgrades.AttackPower * .1f);
            }
            var weapon = _world.Spawn();
            weapon.Add(runtime);
            weapon.AddRelation<Owner>(owner);
            if (!visible || definition.WeaponPrefab == null) return weapon;

            var parent = owner.GetLink<Transform>();
            var view = Object.Instantiate(definition.WeaponPrefab, parent);
            view.transform.SetLocalPositionAndRotation(
                new Vector3(0f, 0f, VisualDepth.Weapon), Quaternion.identity);
            weapon.Add(Link.With(view), new WeaponViewState { BaseScale = view.transform.localScale });
            return weapon;
        }
    }
}
