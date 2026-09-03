using System;
using System.Collections.Generic;
using ZeroAllocSurvival.Definitions;

namespace ZeroAllocSurvival.Services
{
    internal sealed class CharacterVisualRegistry
    {
        internal readonly struct Entry
        {
            public readonly CharacterVisualDefinition Visual;

            public Entry(CharacterVisualDefinition visual)
            {
                Visual = visual;
            }
        }

        private readonly Dictionary<CharacterVisualDefinition, int> _ids = new();
        private readonly List<Entry> _entries = new();

        public int Count => _entries.Count;
        public Entry this[int index] => _entries[index];

        public int Register(CharacterVisualDefinition visual)
        {
            if (visual == null || visual.AtlasTexture == null)
                throw new InvalidOperationException("A registered character visual requires an atlas texture.");
            if (_ids.TryGetValue(visual, out var id)) return id;
            id = _entries.Count;
            _ids.Add(visual, id);
            _entries.Add(new Entry(visual));
            return id;
        }

        public int IdOf(CharacterVisualDefinition visual)
        {
            if (visual != null && _ids.TryGetValue(visual, out var id)) return id;
            throw new InvalidOperationException("Character visual was not registered by the composition root.");
        }
    }
}
