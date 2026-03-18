using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SimpleVoxelSystem.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class PlayerBuildingSystem : MonoBehaviour
    {
        [Serializable]
        public class SavedBlockState
        {
            public int x;
            public int y;
            public int z;
            public int blockTypeId;
        }

        public static PlayerBuildingSystem Instance { get; private set; }
        public static bool IsBuildModeActiveGlobal => Instance != null && Instance.IsBuildMode;

        [Header("Input")]
        public KeyCode toggleBuildModeKey = KeyCode.Tab;
        public float placementRange = 100f;
        public LayerMask placementLayers = Physics.DefaultRaycastLayers;
        public float hoverSurfaceEpsilon = 0.01f;

        [Header("Visuals")]
        public Color previewPlaceColor = new Color(0.2f, 1f, 0.5f, 0.45f);

        public bool IsBuildMode { get; private set; }

        private static readonly BlockType[] BuildableBlocks =
        {
            BlockType.Dirt,
            BlockType.Stone,
            BlockType.Iron,
            BlockType.Gold
        };

        private readonly Dictionary<Vector3Int, BlockType> builtBlocks = new Dictionary<Vector3Int, BlockType>();
        private readonly Dictionary<BlockType, Button> blockButtons = new Dictionary<BlockType, Button>();
        private readonly Dictionary<BlockType, Text> blockButtonLabels = new Dictionary<BlockType, Text>();

        private WellGenerator wellGenerator;
        private PlayerPickaxe playerPickaxe;
        private MobileTouchControls mobileControls;
        private Camera buildCamera;

        private GameObject panel;
        private GameObject controlsPanel;
        private Button modeButton;
        private Text modeButtonLabel;
        private Button inventoryButton;
        private Text inventoryButtonLabel;
        private Text titleLabel;
        private Text statusLabel;
        private GameObject statusPanel;

        private GameObject previewCube;
        private Vector3Int? pendingPlacePos;
        private BlockType selectedBlockType = BlockType.Dirt;
        private float statusHideAt;
        private bool isInventoryOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<PlayerBuildingSystem>() != null)
                return;

            GameObject go = new GameObject("PlayerBuildingSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerBuildingSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveReferences();
            BuildUI();
            Loc.OnLanguageChanged += RefreshUITexts;
            PlayerPickaxe.OnInventoryChanged += RefreshBlockButtons;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Loc.OnLanguageChanged -= RefreshUITexts;
            PlayerPickaxe.OnInventoryChanged -= RefreshBlockButtons;
        }

        private void Update()
        {
            ResolveReferences();
            RefreshVisibility();

            if (Time.unscaledTime > statusHideAt && statusLabel != null)
            {
                statusLabel.text = string.Empty;
                if (statusPanel != null)
                    statusPanel.SetActive(false);
            }

            if (IsTogglePressed())
                ToggleBuildMode();

            if (IsInventoryTogglePressed())
                ToggleInventoryPanel();

            if (!IsBuildMode)
            {
                HidePreview();
                return;
            }

            if (!CanBuildOnCurrentIsland())
            {
                SetBuildMode(false);
                ShowStatus(Loc.T("build_island_only"));
                return;
            }

            if (OnboardingTutorial.IsGameplayInputBlocked || GameUIWindow.IsAnyWindowActive())
            {
                HidePreview();
                return;
            }

            UpdateHover();
            HandleInput();
        }

        public List<SavedBlockState> CapturePlacedBlocks()
        {
            List<SavedBlockState> save = new List<SavedBlockState>(builtBlocks.Count);
            foreach (KeyValuePair<Vector3Int, BlockType> kv in builtBlocks)
            {
                save.Add(new SavedBlockState
                {
                    x = kv.Key.x,
                    y = kv.Key.y,
                    z = kv.Key.z,
                    blockTypeId = (int)kv.Value
                });
            }
            return save;
        }

        public void RestorePlacedBlocks(List<SavedBlockState> savedBlocks)
        {
            builtBlocks.Clear();

            if (wellGenerator == null || wellGenerator.PrivateIsland == null || savedBlocks == null)
                return;

            VoxelIsland island = wellGenerator.PrivateIsland;
            foreach (SavedBlockState state in savedBlocks)
            {
                if (state == null)
                    continue;

                Vector3Int pos = new Vector3Int(state.x, state.y, state.z);
                if (!IsBuildGridAllowed(island, pos))
                    continue;

                BlockType type = (BlockType)Mathf.Clamp(state.blockTypeId, (int)BlockType.Dirt, (int)BlockType.Gold);
                island.SetVoxel(pos.x, pos.y, pos.z, type, false);
                builtBlocks[pos] = type;
            }

            island.RebuildMesh();
            RefreshBlockButtons();
        }

        private void ResolveReferences()
        {
            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();

            if (playerPickaxe == null)
                playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();

            if (mobileControls == null)
                mobileControls = MobileTouchControls.GetOrCreateIfNeeded();

            if (buildCamera == null || !buildCamera.isActiveAndEnabled)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Camera playerCam = player.GetComponentInChildren<Camera>(true);
                    if (playerCam != null && playerCam.isActiveAndEnabled)
                        buildCamera = playerCam;
                }

                if (buildCamera == null)
                    buildCamera = Camera.main;
            }
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("PlayerBuildCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3050;
            canvas.pixelPerfect = true;
            canvasGo.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 1f;

            controlsPanel = RuntimeUIFactory.MakePanel("BuildControlsPanel", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -290f), new Vector2(184f, 84f), new Color(0f, 0f, 0f, 0f));
            panel = RuntimeUIFactory.MakePanel("BuildInventoryPanel", canvas.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(540f, 136f), new Color(0.06f, 0.08f, 0.12f, 0.9f));
            statusPanel = RuntimeUIFactory.MakePanel("BuildStatusPanel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 154f), new Vector2(460f, 36f), new Color(0.06f, 0.08f, 0.12f, 0.82f));

            titleLabel = RuntimeUIFactory.MakeLabelFixed(panel.transform, "Title", string.Empty,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(504f, 24f),
                14, TextAnchor.MiddleCenter, bold: true);
            titleLabel.resizeTextForBestFit = true;
            titleLabel.resizeTextMinSize = 10;
            titleLabel.resizeTextMaxSize = 14;
            titleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel = RuntimeUIFactory.MakeLabel(statusPanel.transform, "Status", string.Empty, 12, TextAnchor.MiddleCenter, new Vector2(12f, 0f), new Vector2(-12f, 0f), color: new Color(1f, 0.95f, 0.65f, 1f));

            modeButton = RuntimeUIFactory.MakeBtn(controlsPanel.transform, "ModeButton", string.Empty, new Color(0.16f, 0.48f, 0.86f, 0.96f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-94f, 0f), new Vector2(84f, 84f));
            modeButtonLabel = modeButton.GetComponentInChildren<Text>();
            modeButton.onClick.AddListener(ToggleBuildMode);
            if (modeButtonLabel != null)
            {
                modeButtonLabel.fontSize = 11;
                modeButtonLabel.alignment = TextAnchor.MiddleCenter;
            }

            inventoryButton = RuntimeUIFactory.MakeBtn(controlsPanel.transform, "InventoryButton", string.Empty, new Color(0.14f, 0.24f, 0.36f, 0.96f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(84f, 84f));
            inventoryButtonLabel = inventoryButton.GetComponentInChildren<Text>();
            inventoryButton.onClick.AddListener(ToggleInventoryPanel);
            if (inventoryButtonLabel != null)
            {
                inventoryButtonLabel.fontSize = 12;
                inventoryButtonLabel.alignment = TextAnchor.MiddleCenter;
            }

            float startX = -180f;
            for (int i = 0; i < BuildableBlocks.Length; i++)
            {
                BlockType blockType = BuildableBlocks[i];
                Button button = RuntimeUIFactory.MakeBtn(panel.transform, $"BuildBlock_{blockType}", string.Empty, new Color(0.22f, 0.26f, 0.34f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(startX + i * 120f, 14f), new Vector2(108f, 50f));
                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 10;
                    label.resizeTextMaxSize = 13;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    label.alignment = TextAnchor.MiddleCenter;
                }
                button.onClick.AddListener(() =>
                {
                    selectedBlockType = blockType;
                    RefreshBlockButtons();
                });

                blockButtons[blockType] = button;
                blockButtonLabels[blockType] = label;
            }

            if (statusPanel != null)
                statusPanel.SetActive(false);

            RefreshUITexts();
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool visible = CanBuildOnCurrentIsland();
            bool showControls = visible;
            bool showInventory = visible && IsBuildMode && isInventoryOpen;
            bool showInventoryButton = visible && IsBuildMode;

            if (controlsPanel != null)
                controlsPanel.SetActive(showControls);

            if (panel != null)
                panel.SetActive(showInventory);

            if (!visible)
            {
                HidePreview();
                isInventoryOpen = false;
                if (statusPanel != null)
                    statusPanel.SetActive(false);
                return;
            }

            if (inventoryButton != null)
                inventoryButton.gameObject.SetActive(showInventoryButton);

            foreach (KeyValuePair<BlockType, Button> kv in blockButtons)
            {
                if (kv.Value != null)
                    kv.Value.gameObject.SetActive(showInventory);
            }
        }

        private void RefreshUITexts()
        {
            if (titleLabel != null)
                titleLabel.text = Loc.T("build_inventory");

            if (modeButtonLabel != null)
                modeButtonLabel.text = IsBuildMode ? Loc.T("btn_mine") : Loc.T("btn_build");

            if (inventoryButtonLabel != null)
                inventoryButtonLabel.text = (mobileControls != null && mobileControls.IsActive) ? Loc.T("build_inventory_toggle") : "I";

            RefreshBlockButtons();
        }

        private void RefreshBlockButtons()
        {
            foreach (BlockType blockType in BuildableBlocks)
            {
                if (!blockButtons.TryGetValue(blockType, out Button button) || button == null)
                    continue;

                Text label = blockButtonLabels[blockType];
                int count = playerPickaxe != null ? playerPickaxe.GetBlockCount(blockType) : 0;
                if (label != null)
                    label.text = $"{Loc.T(BlockLocKey(blockType))}\n{count}";

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    bool isSelected = selectedBlockType == blockType;
                    bool hasResources = count > 0;
                    Color baseColor = hasResources ? new Color(0.22f, 0.26f, 0.34f, 1f) : new Color(0.16f, 0.16f, 0.18f, 0.95f);
                    image.color = isSelected ? Color.white : baseColor;
                }

                if (label != null)
                    label.color = selectedBlockType == blockType ? Color.black : Color.white;
            }
        }

        private void ToggleBuildMode()
        {
            if (!CanBuildOnCurrentIsland())
            {
                ShowStatus(Loc.T("build_island_only"));
                return;
            }

            SetBuildMode(!IsBuildMode);
        }

        private void ToggleInventoryPanel()
        {
            if (!IsBuildMode)
                return;

            isInventoryOpen = !isInventoryOpen;
            RefreshVisibility();
        }

        private void SetBuildMode(bool enabled)
        {
            IsBuildMode = enabled;
            isInventoryOpen = enabled && (mobileControls == null || !mobileControls.IsActive);
            HidePreview();
            RefreshVisibility();
            RefreshUITexts();
        }

        private bool CanBuildOnCurrentIsland()
        {
            return wellGenerator != null &&
                   wellGenerator.IsIslandGenerated &&
                   !wellGenerator.IsInLobbyMode &&
                   wellGenerator.PrivateIsland != null &&
                   wellGenerator.ActiveIsland == wellGenerator.PrivateIsland;
        }

        private void UpdateHover()
        {
            pendingPlacePos = null;

            if (buildCamera == null || !CanBuildOnCurrentIsland())
            {
                HidePreview();
                return;
            }

            bool mobileActive = mobileControls != null && mobileControls.IsActive;
            if (!mobileActive && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HidePreview();
                return;
            }

            Ray ray = buildCamera.ScreenPointToRay(GetPointerPos());
            int layerMask = placementLayers & ~(1 << 2);
            if (!Physics.Raycast(ray, out RaycastHit hit, placementRange, layerMask, QueryTriggerInteraction.Ignore))
            {
                HidePreview();
                return;
            }

            VoxelIsland island = wellGenerator.PrivateIsland;
            VoxelIsland hitIsland = hit.collider.GetComponentInParent<VoxelIsland>();
            if (island == null || hitIsland != island)
            {
                HidePreview();
                return;
            }

            Vector3 hitSurfaceLocalPoint = island.transform.InverseTransformPoint(hit.point);
            Vector3 hitLocalPoint = island.transform.InverseTransformPoint(hit.point + ray.direction * hoverSurfaceEpsilon);
            Vector3 localRayDirection = island.transform.InverseTransformDirection(ray.direction).normalized;
            Vector3Int hitGridPos = new Vector3Int(
                Mathf.FloorToInt(hitLocalPoint.x),
                Mathf.CeilToInt(-hitLocalPoint.y),
                Mathf.FloorToInt(hitLocalPoint.z));
            Vector3Int gridPos = hitGridPos + GetPlacementOffsetFromSurfacePoint(hitGridPos, hitSurfaceLocalPoint, localRayDirection);

            if (!IsBuildGridAllowed(island, gridPos) || island.IsSolid(gridPos.x, gridPos.y, gridPos.z))
            {
                HidePreview();
                return;
            }

            pendingPlacePos = gridPos;
            ShowPreview(island, gridPos, GetPreviewColor(selectedBlockType));
        }

        private void HandleInput()
        {
            if (!IsPlacePressedThisFrame() || !pendingPlacePos.HasValue)
                return;

            PlaceBuiltBlock(pendingPlacePos.Value);
        }

        private void PlaceBuiltBlock(Vector3Int pos)
        {
            VoxelIsland island = wellGenerator != null ? wellGenerator.PrivateIsland : null;
            if (island == null || island.IsSolid(pos.x, pos.y, pos.z))
                return;

            if (playerPickaxe == null || !playerPickaxe.TryConsumeResourceBlock(selectedBlockType))
            {
                ShowStatus(Loc.Tf("build_need_block", Loc.T(BlockLocKey(selectedBlockType))));
                return;
            }

            island.SetVoxel(pos.x, pos.y, pos.z, selectedBlockType, true);
            builtBlocks[pos] = selectedBlockType;
            FindFirstObjectByType<PlayerProgressPersistence>()?.NotifyGameplayStateChanged();
            RefreshBlockButtons();
            ShowStatus(Loc.Tf("build_place_status", Loc.T(BlockLocKey(selectedBlockType))));
        }

        public void NotifyBlockRemovedByMining(VoxelIsland island, Vector3Int pos)
        {
            if (island == null)
                return;

            if (wellGenerator == null || island != wellGenerator.PrivateIsland)
                return;

            if (builtBlocks.Remove(pos))
                FindFirstObjectByType<PlayerProgressPersistence>()?.NotifyGameplayStateChanged();
        }

        private bool IsBuildGridAllowed(VoxelIsland island, Vector3Int pos)
        {
            if (wellGenerator == null || island == null)
                return false;

            return island.InBounds(pos.x, pos.y, pos.z);
        }

        private static Vector3Int GetPlacementOffsetFromSurfacePoint(Vector3Int hitGridPos, Vector3 localSurfacePoint, Vector3 localRayDirection)
        {
            float minX = hitGridPos.x;
            float maxX = hitGridPos.x + 1f;
            float maxY = -hitGridPos.y + 1f;
            float minY = -hitGridPos.y;
            float minZ = hitGridPos.z;
            float maxZ = hitGridPos.z + 1f;

            float distLeft = Mathf.Abs(localSurfacePoint.x - minX);
            float distRight = Mathf.Abs(localSurfacePoint.x - maxX);
            float distTop = Mathf.Abs(localSurfacePoint.y - maxY);
            float distBottom = Mathf.Abs(localSurfacePoint.y - minY);
            float distBack = Mathf.Abs(localSurfacePoint.z - minZ);
            float distFront = Mathf.Abs(localSurfacePoint.z - maxZ);
            const float faceEpsilon = 0.04f;

            FaceCandidate[] candidates =
            {
                new FaceCandidate(distTop,    new Vector3(0f,  1f,  0f), new Vector3Int(0, -1,  0)),
                new FaceCandidate(distBottom, new Vector3(0f, -1f,  0f), new Vector3Int(0,  1,  0)),
                new FaceCandidate(distRight,  new Vector3(1f,  0f,  0f), Vector3Int.right),
                new FaceCandidate(distLeft,   new Vector3(-1f, 0f,  0f), Vector3Int.left),
                new FaceCandidate(distFront,  new Vector3(0f,  0f,  1f), new Vector3Int(0,  0,  1)),
                new FaceCandidate(distBack,   new Vector3(0f,  0f, -1f), new Vector3Int(0,  0, -1))
            };

            bool foundNearFace = false;
            float bestFacingDot = float.PositiveInfinity;
            Vector3Int bestOffset = new Vector3Int(0, -1, 0);

            for (int i = 0; i < candidates.Length; i++)
            {
                FaceCandidate candidate = candidates[i];
                if (candidate.distance > faceEpsilon)
                    continue;

                float facingDot = Vector3.Dot(candidate.normal, localRayDirection);
                if (!foundNearFace || facingDot < bestFacingDot)
                {
                    foundNearFace = true;
                    bestFacingDot = facingDot;
                    bestOffset = candidate.offset;
                }
            }

            if (foundNearFace)
                return bestOffset;

            float minDist = candidates[0].distance;
            bestOffset = candidates[0].offset;
            for (int i = 1; i < candidates.Length; i++)
            {
                if (candidates[i].distance < minDist)
                {
                    minDist = candidates[i].distance;
                    bestOffset = candidates[i].offset;
                }
            }

            return bestOffset;
        }

        private readonly struct FaceCandidate
        {
            public readonly float distance;
            public readonly Vector3 normal;
            public readonly Vector3Int offset;

            public FaceCandidate(float distance, Vector3 normal, Vector3Int offset)
            {
                this.distance = distance;
                this.normal = normal;
                this.offset = offset;
            }
        }

        private void ShowStatus(string text, float duration = 2f)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = text ?? string.Empty;
            if (statusPanel != null)
                statusPanel.SetActive(!string.IsNullOrEmpty(statusLabel.text));
            statusHideAt = Time.unscaledTime + Mathf.Max(0.1f, duration);
        }

        private void ShowPreview(VoxelIsland island, Vector3Int gridPos, Color color)
        {
            EnsurePreview();
            previewCube.SetActive(true);
            previewCube.transform.position = island.transform.TransformPoint(new Vector3(gridPos.x + 0.5f, -gridPos.y + 0.5f, gridPos.z + 0.5f));
            previewCube.transform.rotation = island.transform.rotation;
            previewCube.transform.localScale = island.transform.lossyScale;

            MeshRenderer renderer = previewCube.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        private void HidePreview()
        {
            if (previewCube != null)
                previewCube.SetActive(false);
        }

        private void EnsurePreview()
        {
            if (previewCube != null)
                return;

            previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewCube.name = "PlayerBuildPreview";
            previewCube.layer = 2;
            Destroy(previewCube.GetComponent<Collider>());

            MeshRenderer renderer = previewCube.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = previewPlaceColor;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static string BlockLocKey(BlockType type)
        {
            switch (type)
            {
                case BlockType.Dirt: return "block_dirt";
                case BlockType.Stone: return "block_stone";
                case BlockType.Iron: return "block_iron";
                case BlockType.Gold: return "block_gold";
                default: return "block_dirt";
            }
        }

        private Color GetPreviewColor(BlockType type)
        {
            switch (type)
            {
                case BlockType.Dirt: return new Color(0.55f, 0.27f, 0.07f, 0.45f);
                case BlockType.Stone: return new Color(0.5f, 0.5f, 0.5f, 0.45f);
                case BlockType.Iron: return new Color(0.65f, 0.44f, 0.40f, 0.45f);
                case BlockType.Gold: return new Color(1.00f, 0.84f, 0.00f, 0.45f);
                default: return previewPlaceColor;
            }
        }

        private Vector2 GetPointerPos()
        {
            if (mobileControls != null && mobileControls.IsActive)
                return mobileControls.StickyAimPosition;

#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private bool IsPlacePressedThisFrame()
        {
            if (mobileControls != null && mobileControls.IsActive)
                return mobileControls.MinePressedThisFrame || mobileControls.LookTapPressedThisFrame;

#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private bool IsTogglePressed()
        {
            if (mobileControls != null && mobileControls.IsActive)
                return false;

#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?[Key.Tab].wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleBuildModeKey);
#else
            return false;
#endif
        }

        private bool IsInventoryTogglePressed()
        {
            if (mobileControls != null && mobileControls.IsActive)
                return false;

#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?[Key.I].wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.I);
#else
            return false;
#endif
        }
    } 
}
