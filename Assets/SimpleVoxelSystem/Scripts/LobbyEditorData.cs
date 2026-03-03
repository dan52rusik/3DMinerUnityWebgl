using System.Collections.Generic;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    [System.Serializable]
    public class LobbyVoxelEntry
    {
        public int x, y, z;
        public int blockTypeId;
    }

    [System.Serializable]
    public class LobbyLayoutSaveData
    {
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    [System.Serializable]
    public class ChunkSaveData
    {
        public int chunkX, chunkZ;   // координаты чанка в чанковом пространстве
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    [System.Serializable]
    public class ShopZoneEntry
    {
        public float worldX, worldY, worldZ;
        public int   sizeX, sizeY, sizeZ;
        public ShopZoneType zoneType = ShopZoneType.Mine;
    }

    [System.Serializable]
    public class ShopZoneSaveData
    {
        public List<ShopZoneEntry> zones = new List<ShopZoneEntry>();
    }

    public enum EditorToolMode { Block, Shop, PickaxeShop, SellPoint }
}
