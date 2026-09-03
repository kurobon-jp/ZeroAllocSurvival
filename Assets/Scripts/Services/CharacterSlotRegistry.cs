using System;
using System.Collections.Generic;

namespace ZeroAllocSurvival.Services
{
    /// <summary>Allocates stable array slots for characters participating in crowd simulation.</summary>
    public sealed class CharacterSlotRegistry
    {
        private readonly Stack<int> _freeSlots;
        private readonly bool[] _allocated;
        private int _nextSlot;

        public CharacterSlotRegistry(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _freeSlots = new Stack<int>(capacity);
            _allocated = new bool[capacity];
        }

        public int Allocate()
        {
            var slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : _nextSlot++;
            if ((uint)slot >= (uint)_allocated.Length)
                throw new InvalidOperationException("Character slot capacity was exceeded.");
            _allocated[slot] = true;
            return slot;
        }

        public void Release(int slot)
        {
            if ((uint)slot >= (uint)_allocated.Length || !_allocated[slot]) return;
            _allocated[slot] = false;
            _freeSlots.Push(slot);
        }
    }
}
