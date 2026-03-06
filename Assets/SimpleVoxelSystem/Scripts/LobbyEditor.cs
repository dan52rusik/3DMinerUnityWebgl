using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SimpleVoxelSystem.Data;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Lobby area editor.
    /// Main script delegating work to sub-systems.
    /// </summary>
    public class LobbyEditor : MonoBehaviour
    {
        [Header("References")]
        public WellGenerator wellGenerator;
        public Camera editorCamera;

        [Header("Hotkeys")]
        public KeyCode toggleKey = KeyCode.F2;

        [Header("Runtime Editing")]
        public bool allowRuntimeEditing = true;
        public bool disableRuntimeEditingInPlayerBuild = true;
        public bool allowRuntimeEditingInEditorPlayMode = false;

        [Header("Distance")]
        public float placementRange = 200f;
        public LayerMask miningLayers = Physics.DefaultRaycastLayers;
        public float hoverSurfaceEpsilon = 0.01f;

        [Header("Auto Save")]
        public bool autoSaveLayout = true;
        public float autoSaveInterval = 1.5f;

        [Header("Mobile Input")]
        public float mobileDoubleTapWindow = 0.32f;
        public float mobileDoubleTapMaxDistance = 80f;

        [Header("Shared Lobby Sync")]
        public bool enableSharedLobbySync = false;
        public string sharedLobbyEndpoint = "";
        public string sharedLobbyRoomId = "global_lobby";
        public bool sharedLobbyVerboseLogs = false;
        public bool preferSharedSyncOverLocalSave = true;
        public bool logPersistenceModeOnStart = true;

        [Header("Local Lobby Persistence")]
        public bool persistLocalLobbyLayout = true;
        public bool clearSavedLobbyOnPlay = false;

        [Header("Baked Lobby Layout")]
        public bool useBakedLobbyLayout = true;
        public string bakedLobbyLayoutResourcePath = "LobbyBakedLayout";
        public string bakedShopZonesResourcePath = "LobbyBakedShopZones";

        [Header("Chunk Debug")]
        public bool showChunkDebug = true;
        public bool verboseLogs = false;

        public Color previewColorPlace = new Color(0.2f, 1f, 0.5f, 0.40f);
        public Color previewColorRemove = new Color(1f, 0.2f, 0.2f, 0.40f);
        public Color previewColorShop = new Color(0.3f, 0.6f, 1.0f, 0.45f);

        // --- Runtime State ---
        public bool IsEditMode { get; private set; }
        public EditorToolMode ToolMode { get; private set; } = EditorToolMode.Block;

        private BlockType selectedBlockType = BlockType.Stone;
        private VoxelIsland island;
        private GameObject previewCube;
        private Vector3Int? pendingPlacePos;
        private Vector3Int? pendingRemovePos;
        private Vector3? pendingShopWorldPos;
        private ShopZone hoveredZone;

        private LobbyPersistenceManager persistence;
        private LobbyEditorUIManager uiManager;
        private MobileTouchControls mobileControls;
        private LobbyRealtimeSync realtimeSync;

        private readonly HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();
        private float nextAutoSaveTime;
        private float lastMobileLookTapTime = -10f;
        private Vector2 lastMobileLookTapPos;
        private bool startupCleanupDone;
        private bool localPersistenceReasonLogged;

        private const int ChunkSize = 16;

        void Awake()
        {
            if (wellGenerator == null) wellGenerator = FindFirstObjectByType<WellGenerator>();
            mobileControls = MobileTouchControls.GetOrCreateIfNeeded();
            realtimeSync = GetComponent<LobbyRealtimeSync>() ?? gameObject.AddComponent<LobbyRealtimeSync>();

            realtimeSync.enableSync = enableSharedLobbySync;
            realtimeSync.endpointUrl = sharedLobbyEndpoint;
            realtimeSync.roomId = string.IsNullOrWhiteSpace(sharedLobbyRoomId) ? "global_lobby" : sharedLobbyRoomId;
            realtimeSync.verboseLogs = sharedLobbyVerboseLogs;

            if (wellGenerator != null) wellGenerator.OnFlatPlotReady += OnFlatPlotReady;

            if (IsRuntimeEditingEnabled())
            {
                var rootCanvas = FindFirstObjectByType<Canvas>();
                if (rootCanvas == null)
                {
                    var cGo = new GameObject("LobbyEditorCanvas");
                    rootCanvas = cGo.AddComponent<Canvas>();
                    rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    rootCanvas.sortingOrder = 4000; // Above mobile controls (3000)
                    rootCanvas.pixelPerfect = true;
                    cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    cGo.AddComponent<GraphicRaycaster>();
                }

                // Ensure EventSystem exists for clicking buttons
                if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    var esGo = new GameObject("EventSystem");
                    esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
                uiManager = new LobbyEditorUIManager(this, rootCanvas.transform);
                uiManager.BuildUI(ToggleEditMode, (bt) => selectedBlockType = bt, SetToolMode, SaveLayoutFull, ClearLobbyToBaseFloor);
            }
            LogPersistenceModeIfNeeded();
        }

        void Start()
        {
            if (wellGenerator != null) island = wellGenerator.GetComponent<VoxelIsland>();
            persistence = new LobbyPersistenceManager(island, verboseLogs);
        }

        void OnDestroy()
        {
            if (wellGenerator != null) wellGenerator.OnFlatPlotReady -= OnFlatPlotReady;
            FlushPendingSaves();
        }

        void OnApplicationPause(bool pause) { if (pause) FlushPendingSaves(); }
        void OnApplicationQuit() { FlushPendingSaves(); }

        void Update()
        {
            if (!IsRuntimeEditingEnabled()) return;

            if (IsToggleKeyDown()) ToggleEditMode();
            if (!IsEditMode) { HidePreview(); TryAutoSave(); return; }
            if (uiManager != null && uiManager.IsDialogOpen) { TryAutoSave(); return; }

            UpdateHover();
            HandleInput();
            TryAutoSave();

            if (showChunkDebug) DrawChunkDebug();
        }

        // --- Core Events ---

        private void OnFlatPlotReady()
        {
            if (wellGenerator == null || !wellGenerator.IsInLobbyMode) return;
            island = wellGenerator.GetComponent<VoxelIsland>();
            persistence = new LobbyPersistenceManager(island, verboseLogs);

            if (!startupCleanupDone)
            {
                startupCleanupDone = true;
                if (clearSavedLobbyOnPlay) persistence.ClearAllData(island);
            }

            if (ShouldUseLocalPersistence() && !clearSavedLobbyOnPlay)
            {
                LoadAndApplyLayout();
                LoadAndApplyShopZones();
            }
            else
            {
                ClearRuntimeShopZonesOnly();
                persistence.SaveShopZones(new ShopZoneSaveData());
                TryApplyBakedLayout();
                TryApplyBakedShopZones();
            }
        }

        public void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            mobileControls?.SetEditorModeVisible(IsEditMode);
            if (!IsEditMode)
            {
                HidePreview();
                uiManager?.CloseDialog();
                if (hoveredZone != null) { hoveredZone.SetDeleteHover(false); hoveredZone = null; }
            }
            foreach (var zone in FindObjectsByType<ShopZone>(FindObjectsSortMode.None))
                zone.SetEditorVisible(IsEditMode);
            uiManager?.RefreshUI(IsEditMode, ToolMode, selectedBlockType);
        }

        private void SetToolMode(EditorToolMode mode)
        {
            ToolMode = mode;
            uiManager?.RefreshUI(IsEditMode, ToolMode, selectedBlockType);
        }

        public LobbyLayoutSaveData CaptureCurrentLayoutForBake()
        {
            if (island == null) return null;
            LobbyLayoutSaveData data = new LobbyLayoutSaveData();
            for (int x = 0; x < island.TotalX; x++)
            for (int y = 0; y < island.TotalY; y++)
            for (int z = 0; z < island.TotalZ; z++)
            {
                if (island.TryGetBlockType(x, y, z, out BlockType bt))
                {
                    int id = (int)bt;
                    if (id > 0) data.entries.Add(new LobbyVoxelEntry { x = x, y = y, z = z, blockTypeId = id });
                }
            }
            return data;
        }

        public ShopZoneSaveData CaptureCurrentShopZonesForBake()
        {
            ShopZoneSaveData data = new ShopZoneSaveData();
            foreach (var z in FindObjectsByType<ShopZone>(FindObjectsSortMode.None))
                data.zones.Add(new ShopZoneEntry { worldX = z.transform.position.x, worldY = z.transform.position.y, worldZ = z.transform.position.z, sizeX = z.sizeX, sizeY = z.sizeY, sizeZ = z.sizeZ, zoneType = z.zoneType });
            return data;
        }

        // --- Logic extracted but simplified ---

        void UpdateHover()
        {
            pendingPlacePos = null; pendingRemovePos = null; pendingShopWorldPos = null;
            Camera cam = ResolveEditorCamera();
            if (cam == null) { HidePreview(); return; }

            if (EventSystem.current.IsPointerOverGameObject() && !(IsMobileControlsActive() && mobileControls.RemoveHeld))
            { HidePreview(); return; }

            Ray ray = cam.ScreenPointToRay(GetPointerPos());
            int layerMask = miningLayers & ~(1 << 2);

            if (!Physics.Raycast(ray, out RaycastHit hit, placementRange, layerMask, QueryTriggerInteraction.Ignore))
            { HidePreview(); return; }

            VoxelIsland hitIsland = hit.collider.GetComponentInParent<VoxelIsland>();
            if (hitIsland == null || hitIsland != island) { HidePreview(); return; }

            bool rmb = IsRightHeld();

            if (ToolMode != EditorToolMode.Block)
            {
                ShopZone newHovered = null;
                if (Physics.Raycast(ray, out RaycastHit trigHit, placementRange, miningLayers, QueryTriggerInteraction.Collide))
                    newHovered = trigHit.collider.GetComponentInParent<ShopZone>();

                if (newHovered != null) { HidePreview(); pendingShopWorldPos = null; }
                else
                {
                    Vector3 lp = island.transform.InverseTransformPoint(hit.point + hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x), py = -Mathf.FloorToInt(lp.y), pz = Mathf.FloorToInt(lp.z);
                    if (island.InBounds(px, py, pz))
                    {
                        pendingShopWorldPos = island.transform.TransformPoint(new Vector3(px + 0.5f, -py + 0.5f, pz + 0.5f));
                        ShowPreview(island, new Vector3(px, -py, pz), previewColorShop);
                    }
                    else HidePreview();
                }

                if (hoveredZone != newHovered)
                {
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(false);
                    hoveredZone = newHovered;
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(true);
                }
            }
            else
            {
                Vector3 lp = island.transform.InverseTransformPoint(hit.point + (rmb ? -hit.normal : hit.normal) * hoverSurfaceEpsilon);
                int px = Mathf.FloorToInt(lp.x), py = -Mathf.FloorToInt(lp.y), pz = Mathf.FloorToInt(lp.z);

                if (!rmb)
                {
                    if (island.InBounds(px, py, pz))
                    {
                        pendingPlacePos = new Vector3Int(px, py, pz);
                        Color bc = GetSelectedBlockPreviewColor();
                        ShowPreview(island, new Vector3(px, -py, pz), new Color(bc.r, bc.g, bc.b, 0.45f));
                    }
                    else HidePreview();
                }
                else
                {
                    if (island.IsSolid(px, py, pz))
                    {
                        pendingRemovePos = new Vector3Int(px, py, pz);
                        ShowPreview(island, new Vector3(px, -py, pz), previewColorRemove);
                    }
                    else HidePreview();
                }
            }
        }

        void HandleInput()
        {
            bool mobileLookDoubleTap = ConsumeMobileLookDoubleTap();

            if (ToolMode != EditorToolMode.Block)
            {
                if (IsRightJustPressed() && hoveredZone != null) DeleteShopZone(hoveredZone);
                else if ((IsLeftJustPressed() || mobileLookDoubleTap) && pendingShopWorldPos.HasValue)
                {
                    ShopZoneType zType = ShopZoneType.Mine;
                    if (ToolMode == EditorToolMode.PickaxeShop) zType = ShopZoneType.Pickaxe;
                    else if (ToolMode == EditorToolMode.SellPoint) zType = ShopZoneType.Sell;
                    else if (ToolMode == EditorToolMode.MinionShop) zType = ShopZoneType.Minion;

                    uiManager.OpenSizeDialog(pendingShopWorldPos.Value, zType, SpawnShopZoneByUI);
                }
            }
            else
            {
                bool place = IsLeftJustPressed() || (mobileLookDoubleTap && !mobileControls.RemoveHeld);
                bool remove = IsRightJustPressed() || (mobileLookDoubleTap && mobileControls.RemoveHeld);
                if (place && pendingPlacePos.HasValue) PlaceBlock(pendingPlacePos.Value);
                if (remove && pendingRemovePos.HasValue) RemoveBlock(pendingRemovePos.Value);
            }
        }

        // --- Extraction Proxies ---

        void SaveLayout()
        {
            if (!ShouldUseLocalPersistence() || island == null || dirtyChunks.Count == 0) return;
            persistence.EnsureDirectory();
            foreach (var cc in dirtyChunks)
            {
                List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
                int x0 = cc.x * ChunkSize, x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
                int z0 = cc.y * ChunkSize, z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);
                for (int x = x0; x < x1; x++)
                for (int y = 0; y < island.TotalY; y++)
                for (int z = z0; z < z1; z++)
                    if (island.TryGetBlockType(x, y, z, out BlockType bt))
                        entries.Add(new LobbyVoxelEntry { x = x, y = y, z = z, blockTypeId = (int)bt == 0 ? (int)BlockType.Dirt : (int)bt });
                persistence.SaveChunk(cc.x, cc.y, entries);
            }
            dirtyChunks.Clear();
        }

        public void SaveLayoutFull()
        {
            if (!ShouldUseLocalPersistence() || island == null) return;
            for (int cx = 0; cx < Mathf.CeilToInt((float)island.TotalX / ChunkSize); cx++)
                for (int cz = 0; cz < Mathf.CeilToInt((float)island.TotalZ / ChunkSize); cz++)
                    dirtyChunks.Add(new Vector2Int(cx, cz));
            SaveLayout();
            SaveShopZones();
        }

        public void LoadAndApplyLayout()
        {
            if (!ShouldUseLocalPersistence() || island == null) return;
            int loaded = 0;
            for (int cx = 0; cx < Mathf.CeilToInt((float)island.TotalX / ChunkSize); cx++)
            for (int cz = 0; cz < Mathf.CeilToInt((float)island.TotalZ / ChunkSize); cz++)
            {
                var data = persistence.LoadChunk(cx, cz);
                if (data == null) continue;
                for (int x = cx * ChunkSize; x < Mathf.Min((cx + 1) * ChunkSize, island.TotalX); x++)
                for (int y = 0; y < island.TotalY; y++)
                for (int z = cz * ChunkSize; z < Mathf.Min((cz + 1) * ChunkSize, island.TotalZ); z++)
                    island.RemoveVoxel(x, y, z, false);
                foreach (var e in data.entries)
                    island.SetVoxel(e.x, e.y, e.z, (BlockType)Mathf.Clamp(e.blockTypeId, 1, (int)BlockType.Grass));
                loaded++;
            }
            if (loaded > 0) island.RebuildMesh();
        }

        public void SaveShopZones()
        {
            ShopZoneSaveData data = new ShopZoneSaveData();
            foreach (var z in FindObjectsByType<ShopZone>(FindObjectsSortMode.None))
                data.zones.Add(new ShopZoneEntry { worldX = z.transform.position.x, worldY = z.transform.position.y, worldZ = z.transform.position.z, sizeX = z.sizeX, sizeY = z.sizeY, sizeZ = z.sizeZ, zoneType = z.zoneType });
            persistence.SaveShopZones(data);
        }

        public void LoadAndApplyShopZones()
        {
            ClearRuntimeShopZonesOnly();
            var data = persistence.LoadShopZones();
            if (data == null) return;
            foreach (var e in data.zones) SpawnShopZone(new Vector3(e.worldX, e.worldY, e.worldZ), e.sizeX, e.sizeY, e.sizeZ, e.zoneType);
        }

        // --- Helpers ---

        void SpawnShopZone(Vector3 pos, int sx, int sy, int sz, ShopZoneType type)
        {
            var go = new GameObject($"ShopZone_{type}");
            go.transform.position = pos;
            var zone = go.AddComponent<ShopZone>();
            zone.zoneType = type; zone.sizeX = sx; zone.sizeY = sy; zone.sizeZ = sz;
            zone.SetEditorVisible(IsEditMode);
        }

        void SpawnShopZoneByUI(Vector3 pos, int sx, int sy, int sz, ShopZoneType type)
        {
            SpawnShopZone(pos, sx, sy, sz, type);
            SaveShopZones();
        }

        void DeleteShopZone(ShopZone zone)
        {
            if (hoveredZone == zone) hoveredZone = null;
            Destroy(zone.gameObject);
            SaveShopZones();
        }

        void PlaceBlock(Vector3Int pos)
        {
            if (island == null || island.IsSolid(pos.x, pos.y, pos.z)) return;
            ApplyNetworkPlaceBlock(pos, selectedBlockType);
            realtimeSync?.NotifyLocalPlace(pos, selectedBlockType);
        }

        void RemoveBlock(Vector3Int pos)
        {
            if (island == null || !island.IsSolid(pos.x, pos.y, pos.z)) return;
            ApplyNetworkRemoveBlock(pos);
            realtimeSync?.NotifyLocalRemove(pos);
        }

        // --- Required Public API (For Sync) ---

        public void ApplyNetworkPlaceBlock(Vector3Int pos, BlockType type) { if (island == null) return; island.SetVoxel(pos.x, pos.y, pos.z, type, true); dirtyChunks.Add(new Vector2Int(pos.x / ChunkSize, pos.z / ChunkSize)); }
        public void ApplyNetworkRemoveBlock(Vector3Int pos) { if (island == null) return; island.RemoveVoxel(pos.x, pos.y, pos.z, true); dirtyChunks.Add(new Vector2Int(pos.x / ChunkSize, pos.z / ChunkSize)); }
        public void ApplyNetworkSpawnShopZone(Vector3 pos, int sx, int sy, int sz, ShopZoneType type) { SpawnShopZone(pos, sx, sy, sz, type); SaveShopZones(); }
        public void ApplyNetworkDeleteShopZone(Vector3 pos, ShopZoneType type) { foreach(var z in FindObjectsByType<ShopZone>(FindObjectsSortMode.None)) if(z.zoneType == type && (z.transform.position - pos).sqrMagnitude < 0.1f) { Destroy(z.gameObject); break; } SaveShopZones(); }

        // --- Boilerplate & Utilities ---

        void TryAutoSave() { if (!ShouldUseLocalPersistence() || !autoSaveLayout || island == null || dirtyChunks.Count == 0 || Time.unscaledTime < nextAutoSaveTime) return; SaveLayout(); nextAutoSaveTime = Time.unscaledTime + autoSaveInterval; }
        void FlushPendingSaves() { if (ShouldUseLocalPersistence()) { if (dirtyChunks.Count > 0) SaveLayout(); SaveShopZones(); PlayerPrefs.Save(); } }

        private bool IsToggleKeyDown() { return 
#if ENABLE_INPUT_SYSTEM
            Keyboard.current?[Key.F2].wasPressedThisFrame ?? false;
#else
            Input.GetKeyDown(toggleKey);
#endif
        }
        private bool IsLeftJustPressed() { return (mobileControls != null && mobileControls.IsActive) ? mobileControls.MinePressedThisFrame : 
#if ENABLE_INPUT_SYSTEM
            Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#else
            Input.GetMouseButtonDown(0);
#endif
        }
        private bool IsRightJustPressed() { return (mobileControls != null && mobileControls.IsActive) ? mobileControls.RemovePressedThisFrame : 
#if ENABLE_INPUT_SYSTEM
            Mouse.current?.rightButton.wasPressedThisFrame ?? false;
#else
            Input.GetMouseButtonDown(1);
#endif
        }
        private bool IsRightHeld() { return (mobileControls != null && mobileControls.IsActive) ? mobileControls.RemoveHeld : 
#if ENABLE_INPUT_SYSTEM
            Mouse.current?.rightButton.isPressed ?? false;
#else
            Input.GetMouseButton(1);
#endif
        }
        private Vector2 GetPointerPos() { if (mobileControls != null && mobileControls.IsActive) return mobileControls.AimScreenPosition; 
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.position.ReadValue() ?? Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
        private Camera ResolveEditorCamera() { if (editorCamera != null && editorCamera.isActiveAndEnabled) return editorCamera; GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) { Camera c = p.GetComponentInChildren<Camera>(true); if (c != null && c.isActiveAndEnabled) return editorCamera = c; } return editorCamera = Camera.main; }
        private bool IsMobileControlsActive() => mobileControls != null && mobileControls.IsActive;
        private bool ConsumeMobileLookDoubleTap() { if (!IsMobileControlsActive() || !mobileControls.LookTapPressedThisFrame) return false; float now = Time.unscaledTime; Vector2 pos = mobileControls.AimScreenPosition; bool isDoubleTap = now - lastMobileLookTapTime <= mobileDoubleTapWindow && Vector2.Distance(pos, lastMobileLookTapPos) <= mobileDoubleTapMaxDistance; lastMobileLookTapTime = now; lastMobileLookTapPos = pos; return isDoubleTap; }
        private bool ShouldUseLocalPersistence() { if (!persistLocalLobbyLayout) return false; if (preferSharedSyncOverLocalSave && realtimeSync != null && realtimeSync.UseAuthoritativeServerState) return false; return true; }
        private bool IsRuntimeEditingEnabled() { if (!allowRuntimeEditing) return false; if (Application.isEditor) return Application.isPlaying && allowRuntimeEditingInEditorPlayMode; return !disableRuntimeEditingInPlayerBuild; }
        private Color GetSelectedBlockPreviewColor() { for (int i = 0; i < 4; i++) if (new[] { BlockType.Dirt, BlockType.Stone, BlockType.Iron, BlockType.Gold }[i] == selectedBlockType) return new[] { new Color(0.55f, 0.27f, 0.07f), new Color(0.50f, 0.50f, 0.50f), new Color(0.65f, 0.44f, 0.40f), new Color(1.00f, 0.84f, 0.00f) }[i]; return previewColorPlace; }

        private void ClearRuntimeShopZonesOnly() { if (hoveredZone != null) hoveredZone.SetDeleteHover(false); hoveredZone = null; foreach (var z in FindObjectsByType<ShopZone>(FindObjectsSortMode.None)) Destroy(z.gameObject); }
        private void ClearLobbyToBaseFloor() { if (wellGenerator == null || !wellGenerator.IsInLobbyMode || island == null) return; int floorY = Mathf.Clamp(wellGenerator.LobbyFloorY, 0, island.TotalY - 1); for (int x = 0; x < island.TotalX; x++) for (int y = 0; y < island.TotalY; y++) for (int z = 0; z < island.TotalZ; z++) island.RemoveVoxel(x, y, z, false); for (int x = 0; x < island.TotalX; x++) for (int z = 0; z < island.TotalZ; z++) island.SetVoxel(x, floorY, z, BlockType.Dirt, false); island.RebuildMesh(); dirtyChunks.Clear(); persistence.ClearAllData(island); ClearRuntimeShopZonesOnly(); }

        private void ShowPreview(VoxelIsland targetIsland, Vector3 gridLocalOrigin, Color color) { EnsurePreview(); previewCube.SetActive(true); previewCube.transform.position = targetIsland.transform.TransformPoint(gridLocalOrigin + new Vector3(0.5f, 0.5f, 0.5f)); previewCube.transform.rotation = targetIsland.transform.rotation; previewCube.transform.localScale = targetIsland.transform.lossyScale; var mr = previewCube.GetComponent<MeshRenderer>(); if (mr != null) mr.material.color = color; }
        private void HidePreview() { if (previewCube != null) previewCube.SetActive(false); }
        private void EnsurePreview() { if (previewCube != null) return; previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube); previewCube.name = "LobbyEditorPreview"; previewCube.layer = 2; Destroy(previewCube.GetComponent<Collider>()); var mr = previewCube.GetComponent<MeshRenderer>(); Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); var mat = new Material(sh); mat.color = previewColorPlace; if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f); mat.renderQueue = 3000; mr.material = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

        private void TryApplyBakedLayout() { if (!useBakedLobbyLayout || island == null) return; TextAsset asset = Resources.Load<TextAsset>(bakedLobbyLayoutResourcePath); if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return; LobbyLayoutSaveData data = JsonUtility.FromJson<LobbyLayoutSaveData>(asset.text); if (data?.entries == null) return; for (int x = 0; x < island.TotalX; x++) for (int y = 0; y < island.TotalY; y++) for (int z = 0; z < island.TotalZ; z++) island.RemoveVoxel(x, y, z, false); foreach (var e in data.entries) if (island.InBounds(e.x, e.y, e.z)) island.SetVoxel(e.x, e.y, e.z, (BlockType)Mathf.Clamp(e.blockTypeId, 1, (int)BlockType.Grass), false); island.RebuildMesh(); }
        private void TryApplyBakedShopZones() { if (!useBakedLobbyLayout) return; TextAsset asset = Resources.Load<TextAsset>(bakedShopZonesResourcePath); if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return; ShopZoneSaveData data = JsonUtility.FromJson<ShopZoneSaveData>(asset.text); if (data?.zones == null) return; foreach (var e in data.zones) SpawnShopZone(new Vector3(e.worldX, e.worldY, e.worldZ), e.sizeX, e.sizeY, e.sizeZ, e.zoneType); }

        private void LogPersistenceModeIfNeeded() { if (!logPersistenceModeOnStart) return; string mode = (realtimeSync != null && realtimeSync.UseAuthoritativeServerState) ? "SharedSync" : (persistLocalLobbyLayout ? "LocalPersistence" : "None"); Debug.Log($"[LobbyEditor] Mode: {mode}"); }

        private void DrawChunkDebug() { if (island == null) return; float ly = -(wellGenerator.LobbyFloorY - 1) + 0.05f; Color gridColor = new Color(0f, 0.9f, 1f, 0.9f); for (int cx = 0; cx <= island.TotalX / ChunkSize; cx++) { int gx = Mathf.Min(cx * ChunkSize, island.TotalX); Debug.DrawLine(island.transform.TransformPoint(new Vector3(gx, ly, 0)), island.transform.TransformPoint(new Vector3(gx, ly, island.TotalZ)), gridColor); } for (int cz = 0; cz <= island.TotalZ / ChunkSize; cz++) { int gz = Mathf.Min(cz * ChunkSize, island.TotalZ); Debug.DrawLine(island.transform.TransformPoint(new Vector3(0, ly, gz)), island.transform.TransformPoint(new Vector3(island.TotalX, ly, gz)), gridColor); } }
    }
}
