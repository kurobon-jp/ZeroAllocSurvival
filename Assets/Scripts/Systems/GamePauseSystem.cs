using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Systems
{
    public class GamePauseSystem : BaseSystem, ITickable
    {
        public GamePauseSystem(World world) : base(world)
        {
        }

        public void Tick(float deltaTime)
        {
            Time.timeScale = World.Query().With<GamePause>().Count > 0 ? 0f : 1f;
        }
    }
}