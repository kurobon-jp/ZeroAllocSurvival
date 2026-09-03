using System;
using System.IO;
using System.Text;
using LitheEcs;
using UnityEngine;
using ZeroAllocSurvival.Components;

namespace ZeroAllocSurvival.Services
{
    internal sealed class PerformanceLogService : IDisposable
    {
        private const string Header =
            "elapsed_seconds,enemies,fps,alloc_bytes_per_frame,alloc_bytes_total,frames\n";

        private readonly EntityQuery<EnemyTag> _enemyQuery;
        private readonly float _interval;

        private FileStream _stream;
        private byte[] _writeBuffer;
        private float _intervalElapsed;
        private double _totalElapsed;
        private int _frameCount;
        private long _allocatedBytes;

        public PerformanceLogService(World world, float interval)
        {
            _enemyQuery = world.Query().With<EnemyTag>();
            _interval = Mathf.Max(.1f, interval);
        }

        public void Begin()
        {
            var path = Path.Combine(Application.persistentDataPath, "performance.csv");
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096,
                FileOptions.SequentialScan);

            var header = Encoding.UTF8.GetBytes(Header);
            _stream.Write(header, 0, header.Length);

            WriteEnvironmentFile();
            _writeBuffer = new byte[160];
        }

        public void RecordFrame(float unscaledDeltaTime, long allocatedBytes)
        {
            _intervalElapsed += unscaledDeltaTime;
            _totalElapsed += unscaledDeltaTime;
            _frameCount++;
            _allocatedBytes += allocatedBytes > 0L ? allocatedBytes : 0L;
            if (_intervalElapsed < _interval) return;

            var cursor = 0;
            cursor = WriteFixed(_totalElapsed, 2, cursor);
            _writeBuffer[cursor++] = (byte)',';
            cursor = WriteUnsigned((ulong)_enemyQuery.Count, cursor);
            _writeBuffer[cursor++] = (byte)',';
            var framesPerSecond = _intervalElapsed > 0f ? _frameCount / _intervalElapsed : 0f;
            cursor = WriteFixed(framesPerSecond, 1, cursor);
            _writeBuffer[cursor++] = (byte)',';
            var bytesPerFrame = _frameCount > 0 ? _allocatedBytes / _frameCount : 0L;
            cursor = WriteUnsigned((ulong)bytesPerFrame, cursor);
            _writeBuffer[cursor++] = (byte)',';
            cursor = WriteUnsigned((ulong)_allocatedBytes, cursor);
            _writeBuffer[cursor++] = (byte)',';
            cursor = WriteUnsigned((ulong)_frameCount, cursor);
            _writeBuffer[cursor++] = (byte)'\n';
            _stream.Write(_writeBuffer, 0, cursor);
            _stream.Flush();

            _intervalElapsed = 0f;
            _frameCount = 0;
            _allocatedBytes = 0L;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }

        private int WriteFixed(double value, int decimalPlaces, int offset)
        {
            var scale = decimalPlaces == 1 ? 10UL : 100UL;
            var scaled = (ulong)(Math.Max(0d, value) * scale + .5d);
            offset = WriteUnsigned(scaled / scale, offset);
            _writeBuffer[offset++] = (byte)'.';
            var fraction = scaled % scale;
            if (decimalPlaces == 2 && fraction < 10UL)
                _writeBuffer[offset++] = (byte)'0';
            return WriteUnsigned(fraction, offset);
        }

        private int WriteUnsigned(ulong value, int offset)
        {
            if (value == 0UL)
            {
                _writeBuffer[offset] = (byte)'0';
                return offset + 1;
            }

            var end = offset;
            for (var remaining = value; remaining > 0UL; remaining /= 10UL) end++;
            var cursor = end;
            while (value > 0UL)
            {
                _writeBuffer[--cursor] = (byte)('0' + value % 10UL);
                value /= 10UL;
            }

            return end;
        }

        private static void WriteEnvironmentFile()
        {
            var environment =
                "key,value\n" +
                $"unity,{Escape(Application.unityVersion)}\n" +
                $"platform,{Escape(Application.platform.ToString())}\n" +
                $"editor,{Application.isEditor}\n" +
                $"development,{Debug.isDebugBuild}\n" +
                $"os,{Escape(SystemInfo.operatingSystem)}\n" +
                $"cpu,{Escape(SystemInfo.processorType)}\n" +
                $"cpu_cores,{SystemInfo.processorCount}\n" +
                $"memory_mb,{SystemInfo.systemMemorySize}\n" +
                $"gpu,{Escape(SystemInfo.graphicsDeviceName)}\n" +
                $"graphics_api,{Escape(SystemInfo.graphicsDeviceType.ToString())}\n";
            var path = Path.Combine(Application.persistentDataPath, "performance-environment.csv");
            var bytes = Encoding.UTF8.GetBytes(environment);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                stream.Write(bytes, 0, bytes.Length);
        }

        private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
