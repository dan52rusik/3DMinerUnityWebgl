using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleVoxelSystem.Editor
{
    [CustomEditor(typeof(SimpleVoxelSystem.LobbyEditor))]
    public class LobbyEditorBakeEditor : UnityEditor.Editor
    {
        private const string DefaultResourceFile = "Assets/SimpleVoxelSystem/Resources/LobbyBakedLayout.json";
        private const string DefaultShopZonesFile = "Assets/SimpleVoxelSystem/Resources/LobbyBakedShopZones.json";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Bake current runtime lobby to Resources JSON. This becomes the static layout for builds without server sync.",
                MessageType.Info);

            if (GUILayout.Button("Bake Current Lobby To Resources JSON", GUILayout.Height(28f)))
            {
                BakeLayout((SimpleVoxelSystem.LobbyEditor)target);
            }
        }

        private static void BakeLayout(SimpleVoxelSystem.LobbyEditor lobbyEditor)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LobbyEditorBake] Enter Play Mode, build/edit lobby, then bake.");
                return;
            }

            SimpleVoxelSystem.LobbyLayoutSaveData data = lobbyEditor.CaptureCurrentLayoutForBake();
            if (data == null || data.entries == null || data.entries.Count == 0)
            {
                Debug.LogWarning("[LobbyEditorBake] Nothing to bake (layout is empty or island is unavailable).");
                return;
            }

            string json = JsonUtility.ToJson(data, true);
            string resourcePath = lobbyEditor.bakedLobbyLayoutResourcePath;
            if (string.IsNullOrWhiteSpace(resourcePath))
                resourcePath = "LobbyBakedLayout";

            string filePath = DefaultResourceFile;
            string normalized = resourcePath.Replace('\\', '/').Trim();
            if (normalized != "LobbyBakedLayout")
                filePath = $"Assets/SimpleVoxelSystem/Resources/{normalized}.json";

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, json);

            SimpleVoxelSystem.ShopZoneSaveData zones = lobbyEditor.CaptureCurrentShopZonesForBake();
            string zonesJson = JsonUtility.ToJson(zones ?? new SimpleVoxelSystem.ShopZoneSaveData(), true);
            string zonesPath = DefaultShopZonesFile;
            string zonesResourcePath = lobbyEditor.bakedShopZonesResourcePath;
            if (!string.IsNullOrWhiteSpace(zonesResourcePath))
            {
                string normalizedZones = zonesResourcePath.Replace('\\', '/').Trim();
                if (normalizedZones != "LobbyBakedShopZones")
                    zonesPath = $"Assets/SimpleVoxelSystem/Resources/{normalizedZones}.json";
            }

            string zonesDir = Path.GetDirectoryName(zonesPath);
            if (!string.IsNullOrWhiteSpace(zonesDir))
                Directory.CreateDirectory(zonesDir);

            File.WriteAllText(zonesPath, zonesJson);
            AssetDatabase.Refresh();
            int zonesCount = zones != null && zones.zones != null ? zones.zones.Count : 0;
            Debug.Log($"[LobbyEditorBake] Saved baked lobby: {filePath} ({data.entries.Count} voxels), {zonesPath} ({zonesCount} zones)");
        }
    }
}
