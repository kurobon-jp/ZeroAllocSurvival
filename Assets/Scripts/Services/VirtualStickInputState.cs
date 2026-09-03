using UnityEngine;

namespace ZeroAllocSurvival.Services
{
    /// <summary>Shared input state. Presentation writes it; gameplay systems only read it.</summary>
    internal sealed class VirtualStickInputState
    {
        public Vector2 Direction { get; private set; }
        public bool IsActive { get; private set; }
        public bool HasBeenUsed { get; private set; }

        public void Set(Vector2 direction)
        {
            Direction = Vector2.ClampMagnitude(direction, 1f);
            IsActive = true;
            HasBeenUsed = true;
        }

        public void Release()
        {
            Direction = Vector2.zero;
            IsActive = false;
        }
    }
}
