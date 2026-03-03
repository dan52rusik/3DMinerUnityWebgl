using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class LobbyPersistenceManager
    {
        private const int ChunkSize = 16;
        private static string ChunkDir => Path.Combine(Application.persistentDataPath, "lobby_chunks");
        private static string ChunkFilePath(int cx, int cz) => Path.Combine(ChunkDir, $"chunk_{cx}_{cz}.json");
        private static string ChunkPrefsKey(int cx, int cz) => $"lobby_chunk_v2_{cx}_{cz}";
        private static string ShopSavePath => Path.Combine(Application.persistentDataPath, "lobby_shopzones.json");
        private const string ShopSavePrefsKey = "lobby_shopzones_v2_json";

        private readonly VoxelIsland island;
        private readonly bool verboseLogs;

        public LobbyPersistenceManager(VoxelIsland island, bool verboseLogs)
        {
            this.island = island;
            this.verboseLogs = verboseLogs;
        }

        public void SaveChunk(int cx, int cz, List<LobbyVoxelEntry> entries)
        {
            var data = new ChunkSaveData { chunkX = cx, chunkZ = cz, entries = entries };
            string json = JsonUtility.ToJson(data, true);
            try
            {
                PlayerPrefs.SetString(ChunkPrefsKey(cx, cz), json);
                File.WriteAllText(ChunkFilePath(cx, cz), json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyPersistence] Ошибка записи чанка {cx},{cz}: {ex.Message}");
            }
        }

        public ChunkSaveData LoadChunk(int cx, int cz)
        {
            string json = null;
            if (PlayerPrefs.HasKey(ChunkPrefsKey(cx, cz)))
                json = PlayerPrefs.GetString(ChunkPrefsKey(cx, cz), string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                string file = ChunkFilePath(cx, cz);
                if (File.Exists(file))
                    json = File.ReadAllText(file);
#endif
            }

            if (string.IsNullOrWhiteSpace(json)) return null;

            try { return JsonUtility.FromJson<ChunkSaveData>(json); }
            catch { return null; }
        }

        public void SaveShopZones(ShopZoneSaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            try
            {
                PlayerPrefs.SetString(ShopSavePrefsKey, json);
                PlayerPrefs.Save();
                File.WriteAllText(ShopSavePath, json);
            }
            catch (System.Exception ex) { Debug.LogError($"[LobbyPersistence] Сохранение зон: {ex.Message}"); }
        }

        public ShopZoneSaveData LoadShopZones()
        {
            string json = null;
            if (PlayerPrefs.HasKey(ShopSavePrefsKey))
                json = PlayerPrefs.GetString(ShopSavePrefsKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (File.Exists(ShopSavePath))
                    json = File.ReadAllText(ShopSavePath);
#endif
            }

            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<ShopZoneSaveData>(json); }
            catch { return null; }
        }

        public void ClearAllData(VoxelIsland island)
        {
            if (island != null)
            {
                int chunkCountX = Mathf.CeilToInt((float)island.TotalX / ChunkSize);
                int chunkCountZ = Mathf.CeilToInt((float)island.TotalZ / ChunkSize);
                for (int cx = 0; cx < chunkCountX; cx++)
                for (int cz = 0; cz < chunkCountZ; cz++)
                    PlayerPrefs.DeleteKey(ChunkPrefsKey(cx, cz));
            }

            PlayerPrefs.DeleteKey(ShopSavePrefsKey);
            PlayerPrefs.Save();

#if !UNITY_WEBGL || UNITY_EDITOR
            try { if (Directory.Exists(ChunkDir)) Directory.Delete(ChunkDir, true); } catch { }
            try { if (File.Exists(ShopSavePath)) File.Delete(ShopSavePath); } catch { }
#endif
        }
        
        public void EnsureDirectory()
        {
            try { Directory.CreateDirectory(ChunkDir); } catch { }
        }
    }
}
