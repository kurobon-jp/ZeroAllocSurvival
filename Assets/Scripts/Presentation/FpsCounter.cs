using System;
using LitheEcs;
using TMPro;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FpsCounter : MonoBehaviour
    {
        private const float UpdateInterval = .25f;
        private const int FpsPrefixLength = 5;
        private readonly char[] _buffer = new char[96];

        private float _elapsed;
        private int _frames;
        private long _alloc;
        private int _allocChangedFrame = -1;
        private EntityQuery<EnemyTag> _enemyQuery;
        private bool _hasEnemyQuery;

        [SerializeField] private TMP_Text _text;

        private void Awake()
        {
            _buffer[0] = 'F';
            _buffer[1] = 'P';
            _buffer[2] = 'S';
            _buffer[3] = ':';
            _buffer[4] = ' ';
            // Reserve enough TMP geometry during initialization so steady-state updates do not resize it.
            _buffer[5] = '0';
            _buffer[6] = '0';
            _buffer[7] = '0';
            _buffer[8] = '0';
            _buffer[9] = '0';
            _buffer[10] = '0';
            _buffer[11] = '\n';
            _buffer[12] = 'E';
            _buffer[13] = 'n';
            _buffer[14] = 'e';
            _buffer[15] = 'm';
            _buffer[16] = 'y';
            _buffer[17] = ':';
            _buffer[18] = ' ';
            _buffer[19] = '0';
            _buffer[20] = '0';
            _buffer[21] = '0';
            _buffer[22] = '0';
            _buffer[23] = '0';
            _buffer[24] = '0';
            var cursor = 25;
            _buffer[cursor++] = '\n';
            cursor = WriteLiteral("Alloc: 9999MB", cursor);
            _buffer[cursor++] = '\n';
            cursor = WriteLiteral("Alloc Frame: 9999999999", cursor);
            _text.SetCharArray(_buffer, 0, cursor);
            _text.ForceMeshUpdate();
        }

        public void Initialize(World world)
        {
            _enemyQuery = world.Query().With<EnemyTag>();
            _hasEnemyQuery = true;
        }

        public void AddAlloc(long alloc)
        {
            if (alloc == 0) return;
            _alloc += alloc;
            _allocChangedFrame = Time.frameCount;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < UpdateInterval) return;

            var fps = Mathf.Clamp(Mathf.RoundToInt(_frames / _elapsed), 0, 999999);
            var cursor = WritePositiveInteger(fps, FpsPrefixLength);
            _buffer[cursor++] = '\n';
            _buffer[cursor++] = 'E';
            _buffer[cursor++] = 'n';
            _buffer[cursor++] = 'e';
            _buffer[cursor++] = 'm';
            _buffer[cursor++] = 'y';
            _buffer[cursor++] = ':';
            _buffer[cursor++] = ' ';
            cursor = WritePositiveInteger(_hasEnemyQuery ? _enemyQuery.Count : 0, cursor);
            _buffer[cursor++] = '\n';
            cursor = WriteLiteral("Alloc: ", cursor);
            cursor = WriteByteSize(_alloc, cursor);
            _buffer[cursor++] = '\n';
            cursor = WriteLiteral("Alloc Frame: ", cursor);
            if (_allocChangedFrame >= 0)
                cursor = WritePositiveInteger(_allocChangedFrame, cursor);
            else
                _buffer[cursor++] = '-';

            _text.SetCharArray(_buffer, 0, cursor);
            _elapsed = 0f;
            _frames = 0;
        }

        private int WriteLiteral(string value, int offset)
        {
            for (var i = 0; i < value.Length; i++) _buffer[offset + i] = value[i];
            return offset + value.Length;
        }

        private int WritePositiveInteger(int value, int offset)
            => WritePositiveInteger((long)value, offset);

        private int WritePositiveInteger(long value, int offset)
        {
            if (value == 0)
            {
                _buffer[offset] = '0';
                return offset + 1;
            }

            var end = offset;
            for (var remaining = value; remaining > 0; remaining /= 10) end++;

            var cursor = end;
            while (value > 0)
            {
                _buffer[--cursor] = (char)('0' + value % 10);
                value /= 10;
            }

            return end;
        }

        private int WriteByteSize(long bytes, int offset)
        {
            bytes = Math.Max(0L, bytes);
            const long kilobyte = 1024L;
            const long megabyte = 1024L * 1024L;

            if (bytes < kilobyte)
            {
                offset = WritePositiveInteger(bytes, offset);
                return WriteLiteral("B", offset);
            }

            if (bytes < megabyte)
            {
                offset = WritePositiveInteger((bytes + kilobyte / 2) / kilobyte, offset);
                return WriteLiteral("KB", offset);
            }

            offset = WritePositiveInteger((bytes + megabyte / 2) / megabyte, offset);
            return WriteLiteral("MB", offset);
        }
    }
}
