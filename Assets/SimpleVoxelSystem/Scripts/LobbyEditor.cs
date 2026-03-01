using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SimpleVoxelSystem.Data;

using UnityEngine.EventSystems;
using Unity.Netcode;
using SimpleVoxelSystem.Net;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace SimpleVoxelSystem
{
    // â”€â”€â”€ Ð”Ð°Ð½Ð½Ñ‹Ðµ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ñ Ð²Ð¾ÐºÑÐµÐ»ÐµÐ¹ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€â”€ Ð”Ð°Ð½Ð½Ñ‹Ðµ Ð¾Ð´Ð½Ð¾Ð³Ð¾ Ñ‡Ð°Ð½ÐºÐ° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [System.Serializable]
    public class ChunkSaveData
    {
        public int chunkX, chunkZ;   // ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹ Ñ‡Ð°Ð½ÐºÐ° Ð² Ñ‡Ð°Ð½ÐºÐ¾Ð²Ð¾Ð¼ Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²Ðµ
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    // â”€â”€â”€ Ð”Ð°Ð½Ð½Ñ‹Ðµ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ñ Ð·Ð¾Ð½ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€â”€ Ð ÐµÐ¶Ð¸Ð¼ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public enum EditorToolMode { Block, Shop, PickaxeShop, SellPoint }

    // â”€â”€â”€ ÐžÑÐ½Ð¾Ð²Ð½Ð¾Ð¹ ÑÐºÑ€Ð¸Ð¿Ñ‚ Ñ€ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€Ð° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Ð ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€ Ð»Ð¾Ð±Ð±Ð¸-Ð¿Ð»Ð¾Ñ‰Ð°Ð´ÐºÐ¸.
    /// F2 â€” Ð²ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ/Ð²Ñ‹ÐºÐ»ÑŽÑ‡Ð¸Ñ‚ÑŒ.
    /// Ð’ Ñ€ÐµÐ¶Ð¸Ð¼Ðµ Block: Ð›ÐšÐœ ÑÑ‚Ð°Ð²Ð¸Ñ‚ Ð²Ð¾ÐºÑÐµÐ»ÑŒ, ÐŸÐšÐœ ÑƒÐ´Ð°Ð»ÑÐµÑ‚.
    /// Ð’ Ñ€ÐµÐ¶Ð¸Ð¼Ðµ Shop:  Ð›ÐšÐœ Ð¾Ñ‚ÐºÑ€Ñ‹Ð²Ð°ÐµÑ‚ Ð´Ð¸Ð°Ð»Ð¾Ð³ Ñ€Ð°Ð·Ð¼ÐµÑ€Ð° â†’ ÑÑ‚Ð°Ð²Ð¸Ñ‚ Ð½ÐµÐ²Ð¸Ð´Ð¸Ð¼Ñ‹Ð¹ Ñ‚Ñ€Ð¸Ð³Ð³ÐµÑ€-ÐºÑƒÐ±.
    /// </summary>
    public class LobbyEditor : MonoBehaviour
    {
        [Header("Ð¡ÑÑ‹Ð»ÐºÐ¸")]
        public WellGenerator wellGenerator;
        public Camera        editorCamera;

        [Header("Ð“Ð¾Ñ€ÑÑ‡Ð°Ñ ÐºÐ»Ð°Ð²Ð¸ÑˆÐ°")]
        public KeyCode toggleKey = KeyCode.F2;

        [Header("Ð”Ð°Ð»ÑŒÐ½Ð¾ÑÑ‚ÑŒ")]
        public float placementRange = 200f;
        public LayerMask miningLayers = Physics.DefaultRaycastLayers;
        [Tooltip("ÐœÐ°Ð»Ð¾Ðµ ÑÐ¼ÐµÑ‰ÐµÐ½Ð¸Ðµ Ð»ÑƒÑ‡Ð° Ð¿Ð¾ Ð½Ð¾Ñ€Ð¼Ð°Ð»Ð¸ Ð¿Ñ€Ð¸ Ð²Ñ‹Ð±Ð¾Ñ€Ðµ ÑÑ‡ÐµÐ¹ÐºÐ¸, Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð½Ðµ Ð¿ÐµÑ€ÐµÑÐºÐ°ÐºÐ¸Ð²Ð°Ñ‚ÑŒ Ð½Ð° ÑÐ¾ÑÐµÐ´Ð½Ð¸Ð¹ Ð±Ð»Ð¾Ðº.")]
        public float hoverSurfaceEpsilon = 0.01f;

        [Header("Ð”ÐµÐ±Ð°Ð³ Ñ‡Ð°Ð½ÐºÐ¾Ð²")]
        [Tooltip("ÐŸÐ¾ÐºÐ°Ð·Ñ‹Ð²Ð°Ñ‚ÑŒ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ñ‹ Ñ‡Ð°Ð½ÐºÐ¾Ð² 16Ã—16 Ð² Ñ€ÐµÐ¶Ð¸Ð¼Ðµ Ñ€ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€Ð°.")]
        public bool showChunkDebug = true;
        public bool verboseLogs = false;

        public Color previewColorPlace  = new Color(0.2f, 1f, 0.5f,  0.40f);
        public Color previewColorRemove = new Color(1f,   0.2f, 0.2f, 0.40f);
        public Color previewColorShop   = new Color(0.3f, 0.6f, 1.0f, 0.45f);

        // â”€â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public bool          IsEditMode   { get; private set; }
        public EditorToolMode ToolMode    { get; private set; } = EditorToolMode.Block;

        private BlockType   selectedBlockType = BlockType.Stone;
        private VoxelIsland island;
        private GameObject  previewCube;
        private Vector3Int? pendingPlacePos;
        private Vector3Int? pendingRemovePos;
        private Vector3?    pendingShopWorldPos;  // Ð¼Ð¸Ñ€Ð¾Ð²Ð°Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ñ Ð´Ð»Ñ shop
        private ShopZone    hoveredZone;          // Ð·Ð¾Ð½Ð° Ð¿Ð¾Ð´ ÐºÑƒÑ€ÑÐ¾Ñ€Ð¾Ð¼

        // â”€â”€â”€ Ð§Ð°Ð½ÐºÐ¾Ð²Ð¾Ðµ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ðµ Ð²Ð¾ÐºÑÐµÐ»ÐµÐ¹ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const int ChunkSize = 16;
        private static string ChunkDir =>
            Path.Combine(Application.persistentDataPath, "lobby_chunks");
        private readonly HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();

        private static Vector2Int VoxelToChunk(int x, int z)
            => new Vector2Int(x / ChunkSize, z / ChunkSize);
        private static string ChunkFilePath(int cx, int cz)
            => Path.Combine(ChunkDir, $"chunk_{cx}_{cz}.json");

        // â”€â”€â”€ Ð¡Ð¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ðµ Ð·Ð¾Ð½ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static string ShopSavePath =>
            Path.Combine(Application.persistentDataPath, "lobby_shopzones.json");
        private ShopZoneSaveData shopSaveData = new ShopZoneSaveData();
        private readonly List<ShopZone> spawnedZones = new List<ShopZone>();

        // â”€â”€â”€ UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Canvas     rootCanvas;
        private GameObject editorPanel;
        private Button     toggleBtn;
        private readonly List<Button> typeButtons = new List<Button>();
        private Button     shopToolBtn;
        private Button     pickaxeShopToolBtn;
        private Button     sellPointToolBtn;

        // Ð”Ð¸Ð°Ð»Ð¾Ð³ Ñ€Ð°Ð·Ð¼ÐµÑ€Ð° Ð·Ð¾Ð½Ñ‹
        private GameObject dialogPanel;
        private InputField  inputSizeX, inputSizeY, inputSizeZ;
        private bool        dialogOpen;

        private static readonly Color[] BtnColors =
        {
            new Color(0.55f, 0.27f, 0.07f),
            new Color(0.50f, 0.50f, 0.50f),
            new Color(0.65f, 0.44f, 0.40f),
            new Color(1.00f, 0.84f, 0.00f),
        };
        private static readonly BlockType[] BtnTypes =
        {
            BlockType.Dirt, BlockType.Stone, BlockType.Iron, BlockType.Gold
        };
        private static readonly string[] BtnLabels =
        {
            "Земля", "Камень", "Железо", "Золото"
        };

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Unity
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void Awake()
        {
            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (editorCamera == null)
                editorCamera = ResolveEditorCamera();
            if (wellGenerator != null)
                wellGenerator.OnFlatPlotReady += OnFlatPlotReady;
            BuildUI();
        }

        void Start()
        {
            if (wellGenerator != null)
                island = wellGenerator.GetComponent<VoxelIsland>();
        }

        void OnDestroy()
        {
            if (wellGenerator != null)
                wellGenerator.OnFlatPlotReady -= OnFlatPlotReady;
        }

        void Update()
        {
            if (IsToggleKeyDown()) ToggleEditMode();
            if (!IsEditMode) { HidePreview(); return; }
            if (dialogOpen) return; // Ð’Ð²Ð¾Ð´ Ð´Ð¸Ð°Ð»Ð¾Ð³Ð° Ð±Ð»Ð¾ÐºÐ¸Ñ€ÑƒÐµÑ‚ Ð²ÑÑ‘ Ð¾ÑÑ‚Ð°Ð»ÑŒÐ½Ð¾Ðµ

            UpdateHover();
            HandleInput();

            if (showChunkDebug) DrawChunkDebug();
        }

        private void OnFlatPlotReady()
        {
            // Ð–ÐµÑÑ‚ÐºÐ¾Ðµ ÑƒÑÐ»Ð¾Ð²Ð¸Ðµ: Ð»Ð¾Ð±Ð±Ð¸ Ð³Ñ€ÑƒÐ·Ð¸Ð¼ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐºÐ¾Ð³Ð´Ð° Ð¼Ñ‹ Ð² Ñ†ÐµÐ½Ñ‚Ñ€Ðµ Ð¼Ð¸Ñ€Ð° Ð¸ Ð² Ñ€ÐµÐ¶Ð¸Ð¼Ðµ Ð»Ð¾Ð±Ð±Ð¸
            if (wellGenerator == null || !wellGenerator.IsInLobbyMode) return;
            if (Vector3.SqrMagnitude(wellGenerator.transform.position) > 1.0f) return;

            island = wellGenerator.GetComponent<VoxelIsland>();
            LoadAndApplyLayout();
            LoadAndApplyShopZones();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Ð’Ð¸Ð·ÑƒÐ°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ Ñ‡Ð°Ð½ÐºÐ¾Ð² (Debug.DrawLine â€” Ð²Ð¸Ð´Ð½Ð¾ Ð² Scene view Ð²Ð¾ Ð²Ñ€ÐµÐ¼Ñ Play)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void DrawChunkDebug()
        {
            if (island == null || wellGenerator == null) return;

            int totalX = island.TotalX;
            int totalZ = island.TotalZ;

            // Y Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð¿Ð¾Ð»Ð° = localY = -(lobbyFloorY) + 1 (Ð½Ð° 1 Ð±Ð»Ð¾Ðº Ð²Ñ‹ÑˆÐµ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð¿Ð¾Ð»Ð°)
            float ly = -(wellGenerator.LobbyFloorY - 1) + 0.05f;

            int ccX = Mathf.CeilToInt((float)totalX / ChunkSize);
            int ccZ = Mathf.CeilToInt((float)totalZ / ChunkSize);

            // Ð“Ð¾Ð»ÑƒÐ±Ñ‹Ðµ Ð»Ð¸Ð½Ð¸Ð¸ â€” Ð²ÑÑ ÑÐµÑ‚ÐºÐ° Ñ‡Ð°Ð½ÐºÐ¾Ð²
            Color gridColor = new Color(0f, 0.9f, 1f, 0.9f); // Ñ†Ð¸Ð°Ð½

            // Ð›Ð¸Ð½Ð¸Ð¸ Ð¿Ð¾ X
            for (int cx = 0; cx <= ccX; cx++)
            {
                int gx = Mathf.Min(cx * ChunkSize, totalX);
                Vector3 p0 = island.transform.TransformPoint(new Vector3(gx, ly, 0));
                Vector3 p1 = island.transform.TransformPoint(new Vector3(gx, ly, totalZ));
                Debug.DrawLine(p0, p1, gridColor);
            }

            // Ð›Ð¸Ð½Ð¸Ð¸ Ð¿Ð¾ Z
            for (int cz = 0; cz <= ccZ; cz++)
            {
                int gz = Mathf.Min(cz * ChunkSize, totalZ);
                Vector3 p0 = island.transform.TransformPoint(new Vector3(0,      ly, gz));
                Vector3 p1 = island.transform.TransformPoint(new Vector3(totalX, ly, gz));
                Debug.DrawLine(p0, p1, gridColor);
            }

            // Ð–Ñ‘Ð»Ñ‚Ñ‹Ð¹/Ð¾Ñ€Ð°Ð½Ð¶ÐµÐ²Ñ‹Ð¹ â€” dirty-Ñ‡Ð°Ð½ÐºÐ¸ (Ð¸Ð·Ð¼ÐµÐ½Ñ‘Ð½Ñ‹, ÐµÑ‰Ñ‘ ÑƒÐ¶Ðµ Ð·Ð°Ð¿Ð¸ÑÐ°Ð½Ñ‹ Ð°Ð²Ñ‚Ð¾)
            foreach (var cc in dirtyChunks)
                DrawChunkRect(cc.x, cc.y, ly, new Color(1f, 0.75f, 0f, 1f));

            // Ð—ÐµÐ»Ñ‘Ð½Ñ‹Ð¹ â€” Ñ‡Ð°Ð½ÐºÐ¸ Ñ Ñ„Ð°Ð¹Ð»Ð¾Ð¼ Ð½Ð° Ð´Ð¸ÑÐºÐµ
            for (int cx = 0; cx < ccX; cx++)
            for (int cz = 0; cz < ccZ; cz++)
                if (dirtyChunks.Contains(new Vector2Int(cx, cz)) == false
                    && File.Exists(ChunkFilePath(cx, cz)))
                    DrawChunkRect(cx, cz, ly, new Color(0.2f, 1f, 0.3f, 1f));
        }

        /// <summary>
        /// Ð Ð¸ÑÑƒÐµÑ‚ Ñ€Ð°Ð¼ÐºÑƒ Ñ‡Ð°Ð½ÐºÐ° Ñ†Ð²ÐµÑ‚Ð½Ñ‹Ð¼Ð¸ Ð»Ð¸Ð½Ð¸ÑÐ¼Ð¸ + Ð´Ð¸Ð°Ð³Ð¾Ð½Ð°Ð»ÑŒ Ð´Ð»Ñ Ñ…Ð¾Ñ€Ð¾ÑˆÐµÐ¹ Ð²Ð¸Ð´Ð¸Ð¼Ð¾ÑÑ‚Ð¸.
        /// </summary>
        void DrawChunkRect(int cx, int cz, float localY, Color color)
        {
            if (island == null) return;
            int x0 = cx * ChunkSize,      x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
            int z0 = cz * ChunkSize,      z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);

            Vector3 a = island.transform.TransformPoint(new Vector3(x0, localY, z0));
            Vector3 b = island.transform.TransformPoint(new Vector3(x1, localY, z0));
            Vector3 c = island.transform.TransformPoint(new Vector3(x1, localY, z1));
            Vector3 d = island.transform.TransformPoint(new Vector3(x0, localY, z1));

            Debug.DrawLine(a, b, color);
            Debug.DrawLine(b, c, color);
            Debug.DrawLine(c, d, color);
            Debug.DrawLine(d, a, color);
            // Ð”Ð¸Ð°Ð³Ð¾Ð½Ð°Ð»Ð¸
            Debug.DrawLine(a, c, color * 0.7f);
            Debug.DrawLine(b, d, color * 0.7f);
        }

        void UpdateHover()
        {
            pendingPlacePos  = null;
            pendingRemovePos = null;
            pendingShopWorldPos = null;

            // ÐÐ°Ñ…Ð¾Ð´Ð¸Ð¼ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½ÑƒÑŽ Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½ÑƒÑŽ ÐºÐ°Ð¼ÐµÑ€Ñƒ Ð² Ð¼ÑƒÐ»ÑŒÑ‚Ð¸Ð¿Ð»ÐµÐµÑ€Ðµ.
            editorCamera = ResolveEditorCamera();
            if (editorCamera == null) { HidePreview(); return; }

            if (IsPointerOverUI()) { HidePreview(); return; }

            Vector2 pointerPos = GetPointerPos();
            Ray ray = editorCamera.ScreenPointToRay(pointerPos);
            
            // Ð˜Ð³Ð½Ð¾Ñ€Ð¸Ñ€ÑƒÐµÐ¼ Ð¿Ñ€ÐµÐ²ÑŒÑŽ (Layer 2)
            int layerMask = miningLayers & ~(1 << 2);

            if (!Physics.Raycast(ray, out RaycastHit hit, placementRange, layerMask, QueryTriggerInteraction.Ignore))
            { 
               HidePreview(); 
               return; 
            }

            VoxelIsland hitIsland = hit.collider.GetComponentInParent<VoxelIsland>();
            if (hitIsland == null) { HidePreview(); return; }
            
            // Ð•ÑÐ»Ð¸ Ð² Ñ€ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€Ðµ Ð½Ðµ Ð·Ð°ÐºÑ€ÐµÐ¿Ð»ÐµÐ½ ÐºÐ¾Ð½ÐºÑ€ÐµÑ‚Ð½Ñ‹Ð¹ Ð¾ÑÑ‚Ñ€Ð¾Ð², Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÐ¼ Ñ Ñ‚ÐµÐ¼, Ð² ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ð¹ Ð¿Ð¾Ð¿Ð°Ð»Ð¸
            var activeIsland = (island != null) ? island : hitIsland;
            if (hitIsland != activeIsland) { HidePreview(); return; }

            bool rmb = IsRightHeld();

            if (ToolMode == EditorToolMode.Shop || ToolMode == EditorToolMode.PickaxeShop || ToolMode == EditorToolMode.SellPoint)
            {
                // Ð’ Shop-Ñ€ÐµÐ¶Ð¸Ð¼Ðµ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÐ¼ Ð¸ÑÑ…Ð¾Ð´Ð½ÑƒÑŽ Ð¼Ð°ÑÐºÑƒ (Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð»Ð¾Ð²Ð¸Ñ‚ÑŒ ÐºÐ¾Ð»Ð»Ð°Ð¹Ð´ÐµÑ€Ñ‹ Ð·Ð¾Ð½)
                ShopZone newHovered = null;

                if (Physics.Raycast(ray, out RaycastHit trigHit, placementRange, miningLayers, QueryTriggerInteraction.Collide))
                {
                    newHovered = trigHit.collider.GetComponentInParent<ShopZone>();
                }

                if (newHovered != null)
                {
                    HidePreview();
                    pendingShopWorldPos = null;
                }
                else
                {
                    // ÐÐ°Ð²Ð¾Ð´Ð¸Ð¼ Ð½Ð° Ð¿Ð¾Ð» â€” Ð¿Ð¾ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÐ¼ Ð¿Ñ€ÐµÐ²ÑŒÑŽ Ð´Ð»Ñ Ð½Ð¾Ð²Ð¾Ð¹ Ð·Ð¾Ð½Ñ‹
                    Vector3 lp = activeIsland.transform.InverseTransformPoint(hit.point + hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x), py = -Mathf.FloorToInt(lp.y), pz = Mathf.FloorToInt(lp.z);
                    
                    if (activeIsland.InBounds(px, py, pz))
                    {
                        pendingShopWorldPos = activeIsland.transform.TransformPoint(new Vector3(px + 0.5f, -py + 0.5f, pz + 0.5f));
                        ShowPreview(activeIsland, new Vector3(px, -py, pz), previewColorShop);
                    }
                    else HidePreview();
                }

                // ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÐ¼ hover Ð·Ð¾Ð½Ñ‹
                if (hoveredZone != newHovered)
                {
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(false);
                    hoveredZone = newHovered;
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(true);
                }
            }
            else // Block editing mode
            {
                if (!rmb)
                {
                    // Ð›ÐšÐœ (place) - ÑÑ‚Ð°Ð²Ð¸Ð¼ Ð² ÑÑ‡ÐµÐ¹ÐºÑƒ Ð¿Ð¾Ð´ ÐºÑƒÑ€ÑÐ¾Ñ€Ð¾Ð¼/Ð¿Ð¾ Ð½Ð¾Ñ€Ð¼Ð°Ð»Ð¸ Ð±ÐµÐ· Ð¿Ñ€Ñ‹Ð¶ÐºÐ° Ñ‡ÐµÑ€ÐµÐ· Ð±Ð»Ð¾Ðº
                    Vector3 lp = activeIsland.transform.InverseTransformPoint(hit.point + hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x);
                    int py = -Mathf.FloorToInt(lp.y);
                    int pz = Mathf.FloorToInt(lp.z);

                    if (activeIsland.InBounds(px, py, pz))
                    {
                        pendingPlacePos = new Vector3Int(px, py, pz);
                        Color bc = BtnColors[(int)selectedBlockType];
                        ShowPreview(activeIsland, new Vector3(px, -py, pz),
                            new Color(bc.r, bc.g, bc.b, 0.45f));
                    }
                    else HidePreview();
                }
                else
                {
                    // ÐŸÐšÐœ (remove) - ÑƒÐ´Ð°Ð»ÑÐµÐ¼ ÑÐ°Ð¼Ñƒ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚ÑŒ
                    Vector3 lp = activeIsland.transform.InverseTransformPoint(hit.point - hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x);
                    int py = -Mathf.FloorToInt(lp.y);
                    int pz = Mathf.FloorToInt(lp.z);

                    if (activeIsland.IsSolid(px, py, pz))
                    {
                        pendingRemovePos = new Vector3Int(px, py, pz);
                        ShowPreview(activeIsland, new Vector3(px, -py, pz), previewColorRemove);
                        hoveredZone = null; // Ensure shop zone hover is cleared when in block mode
                    }
                    else HidePreview();
                }
            }
        }

        void HandleInput()
        {
            if (ToolMode == EditorToolMode.Shop || ToolMode == EditorToolMode.PickaxeShop || ToolMode == EditorToolMode.SellPoint)
            {
                if (IsRightJustPressed() && hoveredZone != null)
                    DeleteShopZone(hoveredZone);
                else if (IsLeftJustPressed() && pendingShopWorldPos.HasValue)
                {
                    ShopZoneType zoneType = ShopZoneType.Mine;
                    if (ToolMode == EditorToolMode.PickaxeShop) zoneType = ShopZoneType.Pickaxe;
                    else if (ToolMode == EditorToolMode.SellPoint) zoneType = ShopZoneType.Sell;
                    OpenSizeDialog(pendingShopWorldPos.Value, zoneType);
                }
            }
            else
            {
                if (IsLeftJustPressed()  && pendingPlacePos.HasValue)  PlaceBlock(pendingPlacePos.Value);
                if (IsRightJustPressed() && pendingRemovePos.HasValue) RemoveBlock(pendingRemovePos.Value);
            }
        }

        void DeleteShopZone(ShopZone zone)
        {
            if (TryRequestNetworkDeleteShopZone(zone))
            {
                hoveredZone = null;
                return;
            }

            int idx = spawnedZones.IndexOf(zone);
            if (idx >= 0)
            {
                spawnedZones.RemoveAt(idx);
                shopSaveData.zones.RemoveAt(idx);
                SaveShopZones();
            }
            hoveredZone = null;
            Destroy(zone.gameObject);
            if (verboseLogs) Debug.Log("[LobbyEditor] Ð—Ð¾Ð½Ð° Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð° ÑƒÐ´Ð°Ð»ÐµÐ½Ð°.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Ð Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ðµ Ð²Ð¾ÐºÑÐµÐ»ÐµÐ¹
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void PlaceBlock(Vector3Int pos)
        {
            if (island == null || island.IsSolid(pos.x, pos.y, pos.z)) return;
            island.SetVoxel(pos.x, pos.y, pos.z, selectedBlockType);
            island.RebuildMesh();
            dirtyChunks.Add(VoxelToChunk(pos.x, pos.z));
            SaveLayout();
        }

        void RemoveBlock(Vector3Int pos)
        {
            if (island == null || !island.IsSolid(pos.x, pos.y, pos.z)) return;
            island.RemoveVoxel(pos.x, pos.y, pos.z, true);
            dirtyChunks.Add(VoxelToChunk(pos.x, pos.z));
            SaveLayout();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Ð”Ð¸Ð°Ð»Ð¾Ð³ Ñ€Ð°Ð·Ð¼ÐµÑ€Ð° Shop-Ð·Ð¾Ð½Ñ‹
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void OpenSizeDialog(Vector3 worldPos, ShopZoneType type)
        {
            dialogOpen = true;
            HidePreview();

            if (dialogPanel != null) { Destroy(dialogPanel); }

            // Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ð¼ Ð¿Ð°Ð½ÐµÐ»ÑŒ Ð´Ð¸Ð°Ð»Ð¾Ð³Ð° Ð¿Ð¾ Ñ†ÐµÐ½Ñ‚Ñ€Ñƒ ÑÐºÑ€Ð°Ð½Ð°
            dialogPanel = MakePanel("ShopSizeDialog", rootCanvas.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(320f, 260f),
                new Color(0.08f, 0.08f, 0.14f, 0.97f));

            string title = "🛒 МАГАЗИН ШАХТ";
            if (type == ShopZoneType.Pickaxe) title = "⚒️ МАГАЗИН КИРОК";
            else if (type == ShopZoneType.Sell) title = "💰 ТОЧКА ПРОДАЖИ";
            // Ð—Ð°Ð³Ð¾Ð»Ð¾Ð²Ð¾Ðº
            MakeLabelOff(dialogPanel.transform, "DlgTitle",
                $"{title}\nВведите размер:", 14, TextAnchor.UpperCenter,
                new Vector2(10, -36), new Vector2(-10, 0), bold: true);

            // ÐŸÐ¾Ð»Ñ Ð²Ð²Ð¾Ð´Ð°
            float y = -95f;
            inputSizeX = MakeInputField(dialogPanel.transform, "InputX", "Ширина X (блоков):", ref y);
            inputSizeY = MakeInputField(dialogPanel.transform, "InputY", "Высота Y (блоков):", ref y);
            inputSizeZ = MakeInputField(dialogPanel.transform, "InputZ", "Длина  Z (блоков):", ref y);

            inputSizeX.text = "3";
            inputSizeY.text = "3";
            inputSizeZ.text = "3";

            // ÐšÐ½Ð¾Ð¿ÐºÐ¸ Ð¿Ð¾Ð´Ñ‚Ð²ÐµÑ€Ð´Ð¸Ñ‚ÑŒ / Ð¾Ñ‚Ð¼ÐµÐ½Ð°
            Button okBtn = MakeBtn(dialogPanel.transform, "OkBtn",
                "✅ Поставить зону",
                new Color(0.2f, 0.65f, 0.3f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-80f, 14f), new Vector2(148f, 36f));
            okBtn.onClick.AddListener(() => ConfirmShopPlace(worldPos, type));

            Button cancelBtn = MakeBtn(dialogPanel.transform, "CancelBtn",
                "✖ Отмена",
                new Color(0.6f, 0.2f, 0.2f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(80f, 14f), new Vector2(148f, 36f));
            cancelBtn.onClick.AddListener(CancelDialog);
        }

        void ConfirmShopPlace(Vector3 worldPos, ShopZoneType type)
        {
            int sx = Mathf.Max(1, ParseInt(inputSizeX?.text, 3));
            int sy = Mathf.Max(1, ParseInt(inputSizeY?.text, 3));
            int sz = Mathf.Max(1, ParseInt(inputSizeZ?.text, 3));

            if (TryRequestNetworkSpawnShopZone(worldPos, sx, sy, sz, type))
            {
                CloseDialog();
                return;
            }

            SpawnShopZone(worldPos, sx, sy, sz, type);

            shopSaveData.zones.Add(new ShopZoneEntry
            {
                worldX = worldPos.x, worldY = worldPos.y, worldZ = worldPos.z,
                sizeX = sx, sizeY = sy, sizeZ = sz,
                zoneType = type
            });
            SaveShopZones();
            CloseDialog();
        }

        void CancelDialog() => CloseDialog();

        void CloseDialog()
        {
            dialogOpen = false;
            if (dialogPanel != null) { Destroy(dialogPanel); dialogPanel = null; }
        }

        void SpawnShopZone(Vector3 worldPos, int sx, int sy, int sz, ShopZoneType type = ShopZoneType.Mine)
        {
            var go = new GameObject($"ShopZone_{spawnedZones.Count}");
            go.transform.position = worldPos;
            var zone = go.AddComponent<ShopZone>();
            zone.zoneType = type;
            zone.sizeX = sx;
            zone.sizeY = sy;
            zone.sizeZ = sz;
            spawnedZones.Add(zone);
            zone.SetEditorVisible(IsEditMode);
            if (verboseLogs) Debug.Log($"[LobbyEditor] Ð—Ð¾Ð½Ð° Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð° ({type}) Ð¿Ð¾ÑÑ‚Ð°Ð²Ð»ÐµÐ½Ð° {sx}x{sy}x{sz} @ {worldPos}");
        }

        public void ApplyNetworkSpawnShopZone(Vector3 worldPos, int sx, int sy, int sz, ShopZoneType type)
        {
            foreach (var z in spawnedZones)
            {
                if (z == null || z.zoneType != type) continue;
                if ((z.transform.position - worldPos).sqrMagnitude > 0.0001f) continue;
                if (z.sizeX == sx && z.sizeY == sy && z.sizeZ == sz) return;
            }

            SpawnShopZone(worldPos, sx, sy, sz, type);
            shopSaveData.zones.Add(new ShopZoneEntry
            {
                worldX = worldPos.x,
                worldY = worldPos.y,
                worldZ = worldPos.z,
                sizeX = sx,
                sizeY = sy,
                sizeZ = sz,
                zoneType = type
            });
            SaveShopZones();
        }

        public void ApplyNetworkDeleteShopZone(Vector3 worldPos, ShopZoneType type)
        {
            ShopZone target = FindClosestZone(worldPos, type);
            if (target == null) return;

            int idx = spawnedZones.IndexOf(target);
            if (idx >= 0)
            {
                spawnedZones.RemoveAt(idx);
                if (idx >= 0 && idx < shopSaveData.zones.Count)
                    shopSaveData.zones.RemoveAt(idx);
                SaveShopZones();
            }

            if (hoveredZone == target) hoveredZone = null;
            Destroy(target.gameObject);
        }

        private ShopZone FindClosestZone(Vector3 worldPos, ShopZoneType type)
        {
            ShopZone best = null;
            float bestSqr = float.MaxValue;
            const float maxDistSqr = 1.0f;

            foreach (var z in spawnedZones)
            {
                if (z == null || z.zoneType != type) continue;
                float sqr = (z.transform.position - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = z;
                }
            }

            if (bestSqr > maxDistSqr) return null;
            return best;
        }

        private bool TryRequestNetworkSpawnShopZone(Vector3 worldPos, int sx, int sy, int sz, ShopZoneType type)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return false;

            NetPlayerAvatar avatar = FindLocalNetworkAvatar();
            if (avatar == null)
                return false;

            avatar.RequestSpawnShopZoneServerRpc(worldPos, sx, sy, sz, (int)type);
            return true;
        }

        private bool TryRequestNetworkDeleteShopZone(ShopZone zone)
        {
            if (zone == null) return false;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return false;

            NetPlayerAvatar avatar = FindLocalNetworkAvatar();
            if (avatar == null)
                return false;

            avatar.RequestDeleteShopZoneServerRpc(zone.transform.position, (int)zone.zoneType);
            return true;
        }

        private NetPlayerAvatar FindLocalNetworkAvatar()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                var playerObj = nm.LocalClient?.PlayerObject;
                if (playerObj != null)
                {
                    var fromLocalClient = playerObj.GetComponent<NetPlayerAvatar>();
                    if (fromLocalClient != null && fromLocalClient.IsSpawned)
                        return fromLocalClient;
                }
            }

            var avatars = FindObjectsByType<NetPlayerAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var a in avatars)
            {
                if (a != null && a.IsSpawned && a.IsOwner)
                    return a;
            }
            return null;
        }

        static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out int v) ? v : def;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Ð§Ð°Ð½ÐºÐ¾Ð²Ð¾Ðµ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ðµ â€” Minecraft-ÑÑ‚Ð¸Ð»ÑŒ
        //  ÐšÐ°Ð¶Ð´Ñ‹Ð¹ Ñ‡Ð°Ð½Ðº 16Ã—16 Ñ…Ñ€Ð°Ð½Ð¸Ñ‚ÑÑ Ð² lobby_chunks/chunk_cx_cz.json.
        //  ÐŸÑ€Ð¸ Ð¸Ð·Ð¼ÐµÐ½ÐµÐ½Ð¸Ð¸ Ð±Ð»Ð¾ÐºÐ° Ð¿Ð¾Ð¼ÐµÑ‡Ð°ÐµÑ‚ÑÑ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐµÐ³Ð¾ Ñ‡Ð°Ð½Ðº (dirty).
        //  SaveLayout() Ð¿Ð¸ÑˆÐµÑ‚ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ dirty-Ñ‡Ð°Ð½ÐºÐ¸ Ð½Ð° Ð´Ð¸ÑÐº.
        //  LoadAndApplyLayout() Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ Ð²ÑÐµ Ñ„Ð°Ð¹Ð»Ñ‹ Ð¸ Ð¿ÐµÑ€ÐµÐºÑ€Ñ‹Ð²Ð°ÐµÑ‚ Ð¸Ð¼ÐµÐ½Ð½Ð¾ Ð¸Ñ… Ð¾Ð±Ð»Ð°ÑÑ‚ÑŒ;
        //  Ñ‡Ð°Ð½ÐºÐ¸ Ð±ÐµÐ· Ñ„Ð°Ð¹Ð»Ð° ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÑŽÑ‚ Ð±Ð°Ð·Ð¾Ð²Ñ‹Ð¹ Ð¿Ð¾Ð» Ð¸Ð· GenerateFlatPlot().         
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Ð¡Ð¾Ñ…Ñ€Ð°Ð½ÑÐµÑ‚ Ð½Ð° Ð´Ð¸ÑÐº Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¸Ð·Ð¼ÐµÐ½Ñ‘Ð½Ð½Ñ‹Ðµ (dirty) Ñ‡Ð°Ð½ÐºÐ¸.
        /// ÐŸÐ¾Ð¼ÐµÑ‡Ð°ÐµÑ‚ Ð²ÑÐµ Ñ‡Ð°Ð½ÐºÐ¸ ÐºÐ°Ðº Ñ‡Ð¸ÑÑ‚Ñ‹Ðµ Ð¿Ð¾ÑÐ»Ðµ Ð·Ð°Ð¿Ð¸ÑÐ¸.
        /// </summary>
        public void SaveLayout()
        {
            if (island == null || dirtyChunks.Count == 0) return;

            try { Directory.CreateDirectory(ChunkDir); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyEditor] ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ ÑÐ¾Ð·Ð´Ð°Ñ‚ÑŒ Ð¿Ð°Ð¿ÐºÑƒ Ñ‡Ð°Ð½ÐºÐ¾Ð²: {ex.Message}");
                return;
            }

            foreach (var cc in dirtyChunks)
                SaveChunk(cc.x, cc.y);

            if (verboseLogs) Debug.Log($"[LobbyEditor] Ð¡Ð±Ñ€Ð¾ÑˆÐµÐ½Ð¾ {dirtyChunks.Count} Ñ‡Ð°Ð½Ðº(Ð¾Ð²) Ð½Ð° Ð´Ð¸ÑÐº.");
            dirtyChunks.Clear();
        }

        /// <summary>
        /// ÐŸÑ€Ð¸Ð½ÑƒÐ´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÑ‚ Ð’Ð¡Ð• Ñ‡Ð°Ð½ÐºÐ¸ Ð¾ÑÑ‚Ñ€Ð¾Ð²Ð° (Ð½Ð°Ð¿Ñ€Ð¸Ð¼ÐµÑ€, Ð¿Ð¾ ÐºÐ½Ð¾Ð¿ÐºÐµ Â«Ð¡Ð¾Ñ…Ñ€Ð°Ð½Ð¸Ñ‚ÑŒÂ»).
        /// </summary>
        public void SaveLayoutFull()
        {
            if (island == null) return;
            // ÐŸÐ¾Ð¼ÐµÑ‡Ð°ÐµÐ¼ Ð²ÑÐµ Ñ‡Ð°Ð½ÐºÐ¸ Ð³Ñ€ÑÐ·Ð½Ñ‹Ð¼Ð¸
            int chunkCountX = Mathf.CeilToInt((float)island.TotalX / ChunkSize);
            int chunkCountZ = Mathf.CeilToInt((float)island.TotalZ / ChunkSize);
            for (int cx = 0; cx < chunkCountX; cx++)
            for (int cz = 0; cz < chunkCountZ; cz++)
                dirtyChunks.Add(new Vector2Int(cx, cz));
            SaveLayout();
        }

        private void SaveChunk(int cx, int cz)
        {
            var data = new ChunkSaveData { chunkX = cx, chunkZ = cz };

            int x0 = cx * ChunkSize, x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
            int z0 = cz * ChunkSize, z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);

            for (int x = x0; x < x1; x++)
            for (int y = 0; y < island.TotalY; y++)
            for (int z = z0; z < z1; z++)
            {
                if (island.TryGetBlockType(x, y, z, out BlockType bt))
                {
                    int id = (int)bt;
                    // Ð’ ÑÑ‚Ð°Ñ€Ñ‹Ñ… Ñ‡Ð°Ð½ÐºÐ°Ñ… "Ð·ÐµÐ¼Ð»Ñ" Ð¼Ð¾Ð³Ð»Ð° Ð±Ñ‹Ñ‚ÑŒ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð° ÐºÐ°Ðº 0. ÐŸÐ¸ÑˆÐµÐ¼ 1 (Dirt), Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð¸Ð·Ð±ÐµÐ¶Ð°Ñ‚ÑŒ Air.
                    if (id == 0) id = (int)BlockType.Dirt;
                    data.entries.Add(new LobbyVoxelEntry { x = x, y = y, z = z, blockTypeId = id });
                }
            }

            try { File.WriteAllText(ChunkFilePath(cx, cz), JsonUtility.ToJson(data, true)); }
            catch (System.Exception ex)
            { Debug.LogError($"[LobbyEditor] ÐžÑˆÐ¸Ð±ÐºÐ° Ð·Ð°Ð¿Ð¸ÑÐ¸ Ñ‡Ð°Ð½ÐºÐ° {cx},{cz}: {ex.Message}"); }
        }

        /// <summary>
        /// Ð—Ð°Ð³Ñ€ÑƒÐ¶Ð°ÐµÑ‚ Ð²ÑÐµ ÑÐ¾Ñ…Ñ€Ð°Ð½Ñ‘Ð½Ð½Ñ‹Ðµ Ñ‡Ð°Ð½ÐºÐ¸ Ð¸ Ð½Ð°ÐºÐ»Ð°Ð´Ñ‹Ð²Ð°ÐµÑ‚ Ð¸Ñ… Ð¿Ð¾Ð²ÐµÑ€Ñ… Ð±Ð°Ð·Ð¾Ð²Ð¾Ð³Ð¾ Ð¿Ð¾Ð»Ð°.
        /// Ð§Ð°Ð½ÐºÐ¸ Ð±ÐµÐ· Ñ„Ð°Ð¹Ð»Ð° ÐÐ• Ñ‚Ñ€Ð¾Ð³Ð°ÑŽÑ‚ÑÑ â€” Ð±Ð°Ð·Ð¾Ð²Ñ‹Ð¹ Ð¿Ð¾Ð» Ð¸Ð· GenerateFlatPlot Ð¾ÑÑ‚Ð°Ñ‘Ñ‚ÑÑ.
        /// </summary>
        public void LoadAndApplyLayout()
        {
            if (island == null) return;
            if (!Directory.Exists(ChunkDir)) return;

            string[] files;
            try { files = Directory.GetFiles(ChunkDir, "chunk_*.json"); }
            catch { return; }

            if (files.Length == 0) return;

            int loaded = 0;
            foreach (string file in files)
            {
                ChunkSaveData data;
                try { data = JsonUtility.FromJson<ChunkSaveData>(File.ReadAllText(file)); }
                catch { continue; }
                if (data == null) continue;

                // ÐžÑ‡Ð¸Ñ‰Ð°ÐµÐ¼ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¾Ð±Ð»Ð°ÑÑ‚ÑŒ ÑÑ‚Ð¾Ð³Ð¾ Ñ‡Ð°Ð½ÐºÐ°
                int x0 = data.chunkX * ChunkSize, x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
                int z0 = data.chunkZ * ChunkSize, z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);

                for (int x = x0; x < x1; x++)
                for (int y = 0; y < island.TotalY; y++)
                for (int z = z0; z < z1; z++)
                    island.RemoveVoxel(x, y, z, false);

                // Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÐ¼ Ð¸Ð· Ñ„Ð°Ð¹Ð»Ð°
                if (data.entries != null)
                    foreach (var e in data.entries)
                    {
                        int raw = e.blockTypeId;
                        // Ð¡Ð¾Ð²Ð¼ÐµÑÑ‚Ð¸Ð¼Ð¾ÑÑ‚ÑŒ ÑÐ¾ ÑÑ‚Ð°Ñ€Ñ‹Ð¼Ð¸ ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸ÑÐ¼Ð¸ Ð»Ð¾Ð±Ð±Ð¸: 0 Ñ‚Ñ€Ð°ÐºÑ‚ÑƒÐµÐ¼ ÐºÐ°Ðº Dirt.
                        if (raw <= 0) raw = (int)BlockType.Dirt;
                        if (raw > (int)BlockType.Grass) raw = (int)BlockType.Dirt;
                        island.SetVoxel(e.x, e.y, e.z, (BlockType)raw);
                    }

                loaded++;
            }

            if (loaded > 0)
            {
                island.RebuildMesh();
                if (verboseLogs) Debug.Log($"[LobbyEditor] Ð—Ð°Ð³Ñ€ÑƒÐ¶ÐµÐ½Ð¾ {loaded} Ñ‡Ð°Ð½Ðº(Ð¾Ð²).");
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Save / Load Ð·Ð¾Ð½ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð°
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void SaveShopZones()
        {
            try { File.WriteAllText(ShopSavePath, JsonUtility.ToJson(shopSaveData, true)); }
            catch (System.Exception ex) { Debug.LogError($"[LobbyEditor] Ð¡Ð¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ðµ Ð·Ð¾Ð½: {ex.Message}"); }
        }

        public void LoadAndApplyShopZones()
        {
            // Ð£Ð´Ð°Ð»ÑÐµÐ¼ ÑÑ‚Ð°Ñ€Ñ‹Ðµ
            foreach (var z in spawnedZones) if (z != null) Destroy(z.gameObject);
            spawnedZones.Clear();

            if (!File.Exists(ShopSavePath)) { shopSaveData = new ShopZoneSaveData(); return; }
            try { shopSaveData = JsonUtility.FromJson<ShopZoneSaveData>(File.ReadAllText(ShopSavePath)) ?? new ShopZoneSaveData(); }
            catch { shopSaveData = new ShopZoneSaveData(); return; }

            foreach (var e in shopSaveData.zones)
                SpawnShopZone(new Vector3(e.worldX, e.worldY, e.worldZ), e.sizeX, e.sizeY, e.sizeZ, e.zoneType);

            if (verboseLogs) Debug.Log($"[LobbyEditor] Ð—Ð°Ð³Ñ€ÑƒÐ¶ÐµÐ½Ð¾ {spawnedZones.Count} Ð·Ð¾Ð½ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð°.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Preview cube
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void ShowPreview(VoxelIsland targetIsland, Vector3 gridLocalOrigin, Color color)
        {
            EnsurePreview();
            previewCube.SetActive(true);

            // world position. gridLocalOrigin.y - Ð¸Ð½Ð²ÐµÑ€Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½, Ð¿Ð¾ÑÑ‚Ð¾Ð¼Ñƒ (x, y, z)
            Vector3 worldPos = targetIsland.transform.TransformPoint(gridLocalOrigin + new Vector3(0.5f, 0.5f, 0.5f));
            previewCube.transform.position = worldPos;
            previewCube.transform.rotation = targetIsland.transform.rotation;
            previewCube.transform.localScale = targetIsland.transform.lossyScale;

            var mr = previewCube.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = color;
        }

        void HidePreview() { if (previewCube != null) previewCube.SetActive(false); }

        void EnsurePreview()
        {
            if (previewCube != null) return;
            previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewCube.name = "LobbyEditorPreview";
            previewCube.layer = 2; // Ignore Raycast
            Destroy(previewCube.GetComponent<Collider>());
            var mr = previewCube.GetComponent<MeshRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            mat.color = previewColorPlace;
            if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface",  1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Toggle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            if (!IsEditMode)
            {
                HidePreview();
                CloseDialog();
                // Ð¡Ð±Ñ€Ð°ÑÑ‹Ð²Ð°ÐµÐ¼ hover-ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ
                if (hoveredZone != null) { hoveredZone.SetDeleteHover(false); hoveredZone = null; }
            }
            RefreshZoneVisibility();
            RefreshUI();
        }

        void RefreshZoneVisibility()
        {
            foreach (var zone in spawnedZones)
                if (zone != null) zone.SetEditorVisible(IsEditMode);
        }

        void SetToolMode(EditorToolMode mode)
        {
            ToolMode = mode;
            RefreshUI();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // UI
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void BuildUI()
        {
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                var cGo = new GameObject("LobbyEditorCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            // ÐšÐ½Ð¾Ð¿ÐºÐ°-Ñ‚Ð¾Ð³Ð³Ð»
            toggleBtn = MakeBtn(rootCanvas.transform, "LobbyEditToggle",
                "✏️ Редактор [F2]",
                new Color(0.25f, 0.65f, 0.25f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -110f), new Vector2(168f, 36f));
            toggleBtn.onClick.AddListener(ToggleEditMode);

            // ÐŸÐ°Ð½ÐµÐ»ÑŒ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð¾Ð² (Ð²Ñ‹ÑÐ¾Ñ‚Ð° 450 â€” ÑƒÑ‡Ð¸Ñ‚Ñ‹Ð²Ð°ÐµÑ‚ Ð¸ Shop-ÐºÐ½Ð¾Ð¿ÐºÑƒ)
            editorPanel = MakePanel("LobbyEditorPanel", rootCanvas.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(168f, 500f),
                new Color(0.07f, 0.07f, 0.11f, 0.93f));

            MakeLabelOff(editorPanel.transform, "EdTitle",
                "✏️ РЕДАКТОР ЛОББИ", 13, TextAnchor.UpperCenter,
                new Vector2(4, -30), new Vector2(-4, 0), bold: true);
            MakeLabelOff(editorPanel.transform, "EdHint",
                "ЛКМ — поставить\nПКМ — удалить",
                11, TextAnchor.UpperCenter,
                new Vector2(4, -54), new Vector2(-4, -32));

            // Ð‘Ð»Ð¾Ñ‡Ð½Ñ‹Ðµ Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ñ‹
            for (int i = 0; i < BtnTypes.Length; i++)
            {
                int idx = i;
                Color c = BtnColors[i];
                Button btn = MakeBtn(editorPanel.transform, $"BType_{i}",
                    BtnLabels[i],
                    new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -(86f + i * 46f)), new Vector2(148f, 38f));
                btn.onClick.AddListener(() =>
                {
                    selectedBlockType = BtnTypes[idx];
                    SetToolMode(EditorToolMode.Block);
                });
                typeButtons.Add(btn);
            }

            // ÐšÐ½Ð¾Ð¿ÐºÐ° Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð° Â«ÐœÐ°Ð³Ð°Ð·Ð¸Ð½ Ð¨Ð°Ñ…Ñ‚Â»
            shopToolBtn = MakeBtn(editorPanel.transform, "ShopTool",
                "🛒 Зона шахт",
                new Color(0.15f, 0.35f, 0.80f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + BtnTypes.Length * 46f)), new Vector2(148f, 38f));
            shopToolBtn.onClick.AddListener(() => SetToolMode(EditorToolMode.Shop));

            // ÐšÐ½Ð¾Ð¿ÐºÐ° Ð¸Ð½ÑÑ‚Ñ€ÑƒÐ¼ÐµÐ½Ñ‚Ð° Â«ÐœÐ°Ð³Ð°Ð·Ð¸Ð½ ÐšÐ¸Ñ€Ð¾ÐºÂ»
            pickaxeShopToolBtn = MakeBtn(editorPanel.transform, "PickaxeShopTool",
                "⚒️ Зона кирок",
                new Color(0.25f, 0.45f, 0.25f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + (BtnTypes.Length + 1) * 46f)), new Vector2(148f, 38f));
            pickaxeShopToolBtn.onClick.AddListener(() => SetToolMode(EditorToolMode.PickaxeShop));
            // Кнопка инструмента «Точка продажи»
            sellPointToolBtn = MakeBtn(editorPanel.transform, "SellPointTool",
                "💰 Точка продажи",
                new Color(0.60f, 0.45f, 0.12f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + (BtnTypes.Length + 2) * 46f)), new Vector2(148f, 38f));
            sellPointToolBtn.onClick.AddListener(() => SetToolMode(EditorToolMode.SellPoint));

            // Ð¡Ð¾Ñ…Ñ€Ð°Ð½Ð¸Ñ‚ÑŒ
            Button saveBtn = MakeBtn(editorPanel.transform, "ManualSaveBtn",
                "💾 Сохранить",
                new Color(0.2f, 0.45f, 0.9f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(148f, 38f));
            saveBtn.onClick.AddListener(() => { SaveLayoutFull(); SaveShopZones(); });

            editorPanel.SetActive(false);
        }

        void RefreshUI()
        {
            if (editorPanel != null) editorPanel.SetActive(IsEditMode);
            if (toggleBtn != null)
            {
                var img = toggleBtn.GetComponent<Image>();
                if (img != null)
                    img.color = IsEditMode ? new Color(0.9f, 0.55f, 0.1f, 1f)
                                           : new Color(0.25f, 0.65f, 0.25f, 1f);
            }
            // Ð’Ñ‹Ð´ÐµÐ»ÑÐµÐ¼ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð±Ð»Ð¾Ðº
            for (int i = 0; i < typeButtons.Count && i < BtnTypes.Length; i++)
            {
                if (typeButtons[i] == null) continue;
                bool sel = ToolMode == EditorToolMode.Block && BtnTypes[i] == selectedBlockType;
                var img = typeButtons[i].GetComponent<Image>();
                Color c = BtnColors[i];
                if (img != null) img.color = sel ? Color.white : new Color(c.r*0.65f, c.g*0.65f, c.b*0.65f, 1f);
                var txt = typeButtons[i].GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }
            // Ð’Ñ‹Ð´ÐµÐ»ÑÐµÐ¼ ÐºÐ½Ð¾Ð¿ÐºÑƒ Shop
            if (shopToolBtn != null)
            {
                bool sel = ToolMode == EditorToolMode.Shop;
                var img = shopToolBtn.GetComponent<Image>();
                if (img != null) img.color = sel ? Color.white : new Color(0.15f, 0.35f, 0.80f, 1f);
                var txt = shopToolBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }

            // Ð’Ñ‹Ð´ÐµÐ»ÑÐµÐ¼ ÐºÐ½Ð¾Ð¿ÐºÑƒ PickaxeShop
            if (pickaxeShopToolBtn != null)
            {
                bool sel = ToolMode == EditorToolMode.PickaxeShop;
                var img = pickaxeShopToolBtn.GetComponent<Image>();
                if (img != null) img.color = sel ? Color.white : new Color(0.25f, 0.45f, 0.25f, 1f);
                var txt = pickaxeShopToolBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }

            // Выделяем кнопку SellPoint
            if (sellPointToolBtn != null)
            {
                bool sel = ToolMode == EditorToolMode.SellPoint;
                var img = sellPointToolBtn.GetComponent<Image>();
                if (img != null) img.color = sel ? Color.white : new Color(0.60f, 0.45f, 0.12f, 1f);
                var txt = sellPointToolBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Input
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            // Ð‘Ð¾Ð»ÐµÐµ Ð½Ð°Ð´ÐµÐ¶Ð½Ð°Ñ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð´Ð»Ñ Ñ€ÐµÐ´Ð°ÐºÑ‚Ð¾Ñ€Ð° Ð¸ WebGL
            return EventSystem.current.IsPointerOverGameObject();
        }

        Camera ResolveEditorCamera()
        {
            if (editorCamera != null && editorCamera.isActiveAndEnabled)
                return editorCamera;

            // Ð’ Ð¼ÑƒÐ»ÑŒÑ‚Ð¸Ð¿Ð»ÐµÐµÑ€Ðµ Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½Ñ‹Ð¹ Ð°Ð²Ð°Ñ‚Ð°Ñ€ Ð¿Ð¾Ð¼ÐµÑ‡Ð°ÐµÑ‚ÑÑ Ñ‚ÐµÐ³Ð¾Ð¼ Player.
            GameObject localPlayer = GameObject.FindGameObjectWithTag("Player");
            if (localPlayer != null)
            {
                Camera localCam = localPlayer.GetComponentInChildren<Camera>(true);
                if (localCam != null && localCam.isActiveAndEnabled)
                {
                    editorCamera = localCam;
                    return editorCamera;
                }
            }

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
            {
                editorCamera = Camera.main;
                return editorCamera;
            }

            Camera[] allCameras = Camera.allCameras;
            for (int i = 0; i < allCameras.Length; i++)
            {
                Camera cam = allCameras[i];
                if (cam != null && cam.isActiveAndEnabled)
                {
                    editorCamera = cam;
                    return editorCamera;
                }
            }

            editorCamera = null;
            return null;
        }

        Vector2 GetPointerPos()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            // Ð”Ð»Ñ Ð»ÑƒÑ‡Ð° Ð¿Ñ€ÐµÐ´Ð¿Ð¾Ñ‡Ð¸Ñ‚Ð°ÐµÐ¼ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹ legacy Input, ÐºÐ¾Ð³Ð´Ð° Ð¾Ð½ Ð´Ð¾ÑÑ‚ÑƒÐ¿ÐµÐ½.
            Vector2 legacyPos = Input.mousePosition;
            if (legacyPos.x >= 0f && legacyPos.x <= Screen.width &&
                legacyPos.y >= 0f && legacyPos.y <= Screen.height)
                return legacyPos;
#endif

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Vector2.zero;
        }

        bool IsToggleKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?[Key.F2].wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleKey);
#else
            return false;
#endif
        }

        bool IsLeftJustPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        bool IsRightJustPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.rightButton.wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        bool IsRightHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.rightButton.isPressed ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // UI factories
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        static Font _font;
        static Font GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        static GameObject MakePanel(string name, Transform parent,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        static Button MakeBtn(Transform parent, string name, string label, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = color;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var tGo = new GameObject("Label");
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = tGo.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white; txt.text = label;
            return btn;
        }

        static Text MakeLabelOff(Transform parent, string name, string text,
            int fontSize, TextAnchor align,
            Vector2 offsetMin, Vector2 offsetMax, bool bold = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var txt = go.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = fontSize;
            txt.alignment = align; txt.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            txt.text = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        // Ð¡Ñ‚Ñ€Ð¾ÐºÐ° Â«ÐœÐµÑ‚ÐºÐ° + InputFieldÂ»
        static InputField MakeInputField(Transform parent, string name, string label, ref float offsetY)
        {
            // ÐœÐµÑ‚ÐºÐ°
            var lGo = new GameObject(name + "_Label");
            lGo.transform.SetParent(parent, false);
            var lrt = lGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 1f); lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, offsetY);
            lrt.sizeDelta = new Vector2(280f, 22f);
            var lt = lGo.AddComponent<Text>();
            lt.font = GetFont(); lt.fontSize = 12;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            lt.text = label;

            offsetY -= 26f;

            // Input background
            var iGo = new GameObject(name);
            iGo.transform.SetParent(parent, false);
            var irt = iGo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, offsetY);
            irt.sizeDelta = new Vector2(280f, 32f);
            var bg = iGo.AddComponent<Image>(); bg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            // Text child
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(iGo.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 2); trt.offsetMax = new Vector2(-6, -2);
            var txt = tGo.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white; txt.supportRichText = false;

            var field = iGo.AddComponent<InputField>();
            field.textComponent = txt;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.targetGraphic = bg;

            offsetY -= 36f;
            return field;
        }
    }
}



