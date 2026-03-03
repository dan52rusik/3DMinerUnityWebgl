using UnityEditor;
using UnityEngine;

namespace SimpleVoxelSystem.Editor
{
    [CustomEditor(typeof(WellGenerator))]
    public class WellGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Reset Player Progress: sets Money/XP to 0, Level to 1, removes private island and current mine, and rewrites saved progress.",
                MessageType.Warning);

            GUI.backgroundColor = new Color(0.8f, 0.25f, 0.25f, 1f);
            if (GUILayout.Button("Reset Player Progress (New Player)", GUILayout.Height(30f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Player Progress",
                    "This will reset player progress to a new player state (0 progress, no private island). Continue?",
                    "Reset",
                    "Cancel"))
                {
                    ExecuteReset((WellGenerator)target);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private static void ExecuteReset(WellGenerator wellGenerator)
        {
            if (Application.isPlaying)
            {
                PlayerProgressPersistence persistence = Object.FindFirstObjectByType<PlayerProgressPersistence>();
                if (persistence != null)
                    persistence.ResetProgressToNewPlayer();
                else
                {
                    wellGenerator.ResetPlayerWorldForNewProgress();
                    PlayerProgressPersistence.ResetStoredProgressToNewPlayer();
                }

                Debug.Log("[WellGeneratorEditor] Player progress reset in Play Mode.");
                return;
            }

            PlayerProgressPersistence.ResetStoredProgressToNewPlayer();
            Debug.Log("[WellGeneratorEditor] Saved player progress reset (Edit Mode). Run Play Mode to apply world-state reset in scene.");
        }
    }
}
