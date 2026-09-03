using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ZeroAllocSurvival.Editor
{
    internal static class ProfilerDataTextExporter
    {
        private struct Aggregate
        {
            public int Count;
            public double TotalMilliseconds;
            public double MaximumMilliseconds;
            public long GcAllocatedBytes;
        }

        public static void ExportFromCommandLine()
        {
            var input = GetArgument("-profilerInput");
            var output = GetArgument("-profilerOutput");
            if (string.IsNullOrEmpty(input) || !File.Exists(input))
                throw new FileNotFoundException("Profiler input was not found.", input);
            if (string.IsNullOrEmpty(output)) output = Path.ChangeExtension(input, ".csv");
            Export(input, output);
        }

        [MenuItem("Tools/Zero Alloc Survival/Convert Profiler Data To CSV")]
        private static void ExportFromMenu()
        {
            var input = EditorUtility.OpenFilePanel("Select Unity Profiler capture", "ProfilerCaptures", "data");
            if (string.IsNullOrEmpty(input)) return;

            var output = EditorUtility.SaveFilePanel(
                "Save Profiler CSV", Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(input), "csv");
            if (string.IsNullOrEmpty(output)) return;

            Export(input, output);
            EditorUtility.RevealInFinder(output);
        }

        private static void Export(string input, string output)
        {
            if (!File.Exists(input)) throw new FileNotFoundException("Profiler input was not found.", input);

            ProfilerDriver.LoadProfile(input, false);
            var firstFrame = ProfilerDriver.firstFrameIndex;
            var lastFrame = ProfilerDriver.lastFrameIndex;
            if (firstFrame < 0 || lastFrame < firstFrame)
                throw new InvalidOperationException("The profiler capture contains no readable frames.");

            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var aggregates = new Dictionary<string, Aggregate>(4096, StringComparer.Ordinal);
            using var writer = new StreamWriter(output, false);
            writer.WriteLine("Frame,ThreadIndex,ThreadName,Marker,CallCount,TotalMilliseconds,MaximumMilliseconds,GCAllocatedBytes");

            try
            {
                for (var frame = firstFrame; frame <= lastFrame; frame++)
                {
                    EditorUtility.DisplayProgressBar("Exporting Profiler capture", $"Frame {frame}/{lastFrame}",
                        (frame - firstFrame) / (float)Math.Max(1, lastFrame - firstFrame));
                    WriteFrame(writer, aggregates, frame);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"Profiler CSV written: {output}");
        }

        private static void WriteFrame(StreamWriter writer, Dictionary<string, Aggregate> aggregates, int frame)
        {
            for (var threadIndex = 0;; threadIndex++)
            {
                using var frameData = ProfilerDriver.GetRawFrameDataView(frame, threadIndex);
                if (!frameData.valid) break;

                aggregates.Clear();
                for (var sampleIndex = 0; sampleIndex < frameData.sampleCount; sampleIndex++)
                {
                    var marker = frameData.GetSampleName(sampleIndex);
                    if (string.IsNullOrEmpty(marker)) continue;
                    aggregates.TryGetValue(marker, out var aggregate);
                    var duration = frameData.GetSampleTimeMs(sampleIndex);
                    aggregate.Count++;
                    aggregate.TotalMilliseconds += duration;
                    if (duration > aggregate.MaximumMilliseconds) aggregate.MaximumMilliseconds = duration;
                    if (marker == "GC.Alloc" && frameData.GetSampleMetadataCount(sampleIndex) > 0)
                        aggregate.GcAllocatedBytes += frameData.GetSampleMetadataAsLong(sampleIndex, 0);
                    aggregates[marker] = aggregate;
                }

                foreach (var pair in aggregates)
                {
                    var value = pair.Value;
                    WriteCsv(writer, frame.ToString(CultureInfo.InvariantCulture));
                    WriteCsv(writer, threadIndex.ToString(CultureInfo.InvariantCulture));
                    WriteCsv(writer, frameData.threadName);
                    WriteCsv(writer, pair.Key);
                    WriteCsv(writer, value.Count.ToString(CultureInfo.InvariantCulture));
                    WriteCsv(writer, value.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture));
                    WriteCsv(writer, value.MaximumMilliseconds.ToString("R", CultureInfo.InvariantCulture));
                    WriteCsv(writer, value.GcAllocatedBytes.ToString(CultureInfo.InvariantCulture), true);
                }
            }
        }

        private static string GetArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            return null;
        }

        private static void WriteCsv(TextWriter writer, string value, bool endLine = false)
        {
            writer.Write('"');
            writer.Write(value?.Replace("\"", "\"\"") ?? string.Empty);
            writer.Write('"');
            if (endLine) writer.WriteLine();
            else writer.Write(',');
        }
    }
}
