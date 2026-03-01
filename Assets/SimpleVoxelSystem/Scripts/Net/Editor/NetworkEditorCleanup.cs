#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Unity.Netcode;

namespace SimpleVoxelSystem.Net.Editor
{
    [InitializeOnLoad]
    public static class NetworkEditorCleanup
    {
        static NetworkEditorCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownIfNeeded;
            EditorApplication.quitting += ShutdownIfNeeded;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                ShutdownIfNeeded();
            }
        }

        private static void ShutdownIfNeeded()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;

            if (nm.IsListening)
                nm.Shutdown();

            // Remove only runtime-created manager, not a scene-authored one.
            var go = nm.gameObject;
            if (go != null && go.name == "NetworkManagerRuntime")
                Object.DestroyImmediate(go);
        }
    }
}
#endif
