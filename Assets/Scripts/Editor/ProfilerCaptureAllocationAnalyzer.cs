#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace ZeroAllocSurvival.Editor
{
    internal static class ProfilerCaptureAllocationAnalyzer
    {
        private const string MenuPath = "Tools/Profiler/Analyze GC Alloc After First Frame";

        [MenuItem(MenuPath)]
        private static void Analyze()
        {
            var initialDirectory = Path.Combine(Application.dataPath, "..", "ProfilerCaptures");
            var capturePath = EditorUtility.OpenFilePanel(
                "Select Unity Profiler capture", initialDirectory, "data");
            if (string.IsNullOrEmpty(capturePath)) return;

            ProfilerDriver.LoadProfile(capturePath, false);
            EditorApplication.delayCall += () => AnalyzeLoadedCapture(capturePath);
        }

        private static void AnalyzeLoadedCapture(string capturePath)
        {
            var firstFrame = ProfilerDriver.firstFrameIndex;
            var lastFrame = ProfilerDriver.lastFrameIndex;
            if (firstFrame < 0 || lastFrame < firstFrame)
            {
                Debug.LogError($"Profiler capture could not be loaded: {capturePath}");
                return;
            }

            long totalBytes = 0;
            var framesWithAllocations = 0;
            var report = new StringBuilder(512);
            report.AppendLine($"Profiler capture: {capturePath}");
            report.AppendLine($"Captured frames: {firstFrame}..{lastFrame}");
            report.AppendLine($"Excluded first frame: {firstFrame}");

            for (var frame = firstFrame + 1; frame <= lastFrame; frame++)
            {
                long frameBytes = 0;
                for (var thread = 0;; thread++)
                {
                    using var view = CreateHierarchyView(frame, thread);
                    if (!view.valid) break;

                    var rootId = view.GetRootItemID();
                    if (rootId >= 0)
                        frameBytes += (long)view.GetItemColumnDataAsDouble(
                            rootId, HierarchyFrameDataView.columnGcMemory);
                }

                if (frameBytes <= 0) continue;
                framesWithAllocations++;
                totalBytes += frameBytes;
                report.AppendLine($"Frame {frame}: {frameBytes} B");
            }

            report.AppendLine();
            report.AppendLine(framesWithAllocations == 0
                ? "Result: No GC allocations after the first captured frame."
                : $"Result: {totalBytes} B allocated across {framesWithAllocations} frame(s) after the first captured frame.");

            Debug.Log(report.ToString());
        }

        private static HierarchyFrameDataView CreateHierarchyView(int frame, int thread)
        {
            return (HierarchyFrameDataView)Activator.CreateInstance(
                typeof(HierarchyFrameDataView),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    frame,
                    thread,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnGcMemory,
                    false
                },
                null);
        }
    }
}
#endif
