using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZeroAllocSurvival.Services
{
    /// <summary>Prevents Core RP from creating its per-frame runtime debug updater.</summary>
    internal static class RenderingDebugRuntimeDisabler
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly FieldInfo EnableRuntimeUiField = typeof(DebugManager).GetField(
            "m_EnableRuntimeUI", BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Disable()
        {
            // DebugManager.enableRuntimeUI cannot be used here. Core RP 17.5 calls
            // DisableRuntime(), which dereferences an uncreated persistent UI and throws.
            // Setting the backing field before DebugUpdater.RuntimeInit (AfterSceneLoad)
            // prevents the updater and its Update loop from being created at all.
            EnableRuntimeUiField?.SetValue(DebugManager.instance, false);
        }
#endif
    }
}
