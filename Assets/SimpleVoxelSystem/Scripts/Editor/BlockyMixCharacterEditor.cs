using UnityEditor;
using UnityEngine;

namespace SimpleVoxelSystem.Editor
{
    [CustomEditor(typeof(BlockyMixCharacter))]
    public class BlockyMixCharacterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            BlockyMixCharacter c = (BlockyMixCharacter)target;

            GUI.backgroundColor = new Color(0.22f, 0.62f, 0.95f, 1f);
            if (GUILayout.Button("Rebuild Mix Character", GUILayout.Height(30f)))
            {
                Undo.RecordObject(c.gameObject, "Rebuild Mix Character");
                c.Rebuild();
                EditorUtility.SetDirty(c.gameObject);
            }

            GUI.backgroundColor = new Color(0.85f, 0.35f, 0.30f, 1f);
            if (GUILayout.Button("Clear Mix Visual", GUILayout.Height(24f)))
            {
                Undo.RecordObject(c.gameObject, "Clear Mix Character");
                c.ClearVisual();
                EditorUtility.SetDirty(c.gameObject);
            }
            GUI.backgroundColor = Color.white;
        }
    }

    public static class BlockyMixCharacterMenu
    {
        [MenuItem("Tools/SimpleVoxelSystem/Create Mix Character On Selected Player")]
        private static void CreateOnSelectedPlayer()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Mix Character", "Select player GameObject first.", "OK");
                return;
            }

            if (go.GetComponent<BlockyMixCharacter>() == null)
                go.AddComponent<BlockyMixCharacter>();

            BlockyMixCharacter c = go.GetComponent<BlockyMixCharacter>();
            c.Rebuild();
            EditorUtility.SetDirty(go);
            Debug.Log("[BlockyMixCharacter] Mix visual generated on: " + go.name);
        }
    }
}
