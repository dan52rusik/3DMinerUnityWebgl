using UnityEngine;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Manages the mine shop:
    ///   — list of available mine classes (MineShopData)
    ///   — purchase → creates MineInstance and starts placement mode
    ///   — placement → calls WellGenerator.GenerateMine()
    ///   — selling depleted mine → WellGenerator.DemolishMine() + money
    ///
    /// Attach this component to the same GameObject as WellGenerator.
    /// Assign the availableMines list in the inspector.
    /// </summary>
    public class MineMarket : MonoBehaviour
    {
        [Header("Mine Shop")]
        public List<MineShopData> availableMines;

        [Header("Placement Mode")]
        [Tooltip("Semi-transparent preview cube (optional)")]
        public GameObject placementPreviewPrefab;
        public Color previewColor = new Color(0f, 1f, 0.5f, 0.3f);
        [Tooltip("Maximum number of mines that can be placed simultaneously. 0 = unlimited.")]
        [Min(0)] public int maxPlacedMines = 5;
        public bool verboseLogs = false;

        // ─── Runtime ────────────────────────────────────────────────────────
        public bool IsPlacementMode { get; private set; }

        [Header("References")]
        public WellGenerator WellGen { get; private set; }
        private MineInstance    pendingMine;       // mine pending placement
        private GameObject      previewInstance;   // preview ghost
        private MobileTouchControls mobileControls; // cached in Awake
        private bool            wasOnIslandPlacing; // for tracking transition

        // Events for UI
        public event System.Action<MineInstance> OnMinePlaced;
        public event System.Action<MineInstance> OnMineSold;
        public event System.Action              OnPlacementCancelled;

        // ────────────────────────────────────────────────────────────────────

        void Awake()
        {
            WellGen = GetComponent<WellGenerator>();
            if (WellGen == null)
                WellGen = GetComponentInParent<WellGenerator>();

            mobileControls = MobileTouchControls.GetOrCreateIfNeeded();

            CreateDefaultMinesIfEmpty();
            ApplyLocalizationToAvailableMines();
            EnsureShopUI();
        }

        void OnEnable()
        {
            Loc.OnLanguageChanged += ApplyLocalizationToAvailableMines;
        }

        void OnDisable()
        {
            Loc.OnLanguageChanged -= ApplyLocalizationToAvailableMines;
        }

        /// <summary>
        /// Creates MineShopUI in the scene if it doesn't already exist.
        /// Searches for an existing Canvas; adds it there; if no Canvas exists, creates a separate GO.
        /// </summary>
        void EnsureShopUI()
        {
            if (FindFirstObjectByType<MineShopUI>() != null) return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            GameObject host;
            if (canvas != null)
                host = canvas.gameObject;
            else
            {
                host = new GameObject("MineShopUI_Host");
                if (verboseLogs) Debug.Log("[MineMarket] Canvas not found, created separate GO for MineShopUI.");
            }

            var shopUI = host.AddComponent<MineShopUI>();
            shopUI.mineMarket = this;
            if (verboseLogs) Debug.Log("[MineMarket] MineShopUI auto-created on \"" + host.name + "\".");
        }

        /// <summary>
        /// Creates a default set of mines at runtime if the list is empty or not assigned.
        /// Allows bypassing the editor setup (Tools → Mine System → Setup Scene).
        /// </summary>
        void CreateDefaultMinesIfEmpty()
        {
            if (availableMines != null && availableMines.Count > 0) return;

            availableMines = new List<MineShopData>();

            // Bronze
            var bronze = ScriptableObject.CreateInstance<MineShopData>();
            bronze.displayName  = Loc.T("mine_bronze_name");
            bronze.description  = Loc.T("mine_bronze_desc");
            bronze.labelColor   = new Color(0.80f, 0.50f, 0.20f);
            bronze.buyPrice     = EconomyTuning.BronzeMinePrice;
            bronze.sellBackRatio= EconomyTuning.BronzeMineSellBackRatio;
            bronze.depthMin     = EconomyTuning.BronzeMineDepthMin;  bronze.depthMax = EconomyTuning.BronzeMineDepthMax;
            bronze.wellWidth    = EconomyTuning.DefaultMineWellWidth;  bronze.wellLength = EconomyTuning.DefaultMineWellLength; bronze.padding = EconomyTuning.DefaultMinePadding;
            bronze.layers       = new BlockLayer[]
            {
                new BlockLayer { maxDepth=2,  dirtWeight=90, stoneWeight=10, ironWeight=0,  goldWeight=0 },
                new BlockLayer { maxDepth=30, dirtWeight=40, stoneWeight=55, ironWeight=5,  goldWeight=0 },
            };
            availableMines.Add(bronze);

            // Silver
            var silver = ScriptableObject.CreateInstance<MineShopData>();
            silver.displayName  = Loc.T("mine_silver_name");
            silver.description  = Loc.T("mine_silver_desc");
            silver.labelColor   = new Color(0.70f, 0.70f, 0.80f);
            silver.buyPrice     = EconomyTuning.SilverMinePrice;
            silver.sellBackRatio= EconomyTuning.SilverMineSellBackRatio;
            silver.depthMin     = EconomyTuning.SilverMineDepthMin;  silver.depthMax = EconomyTuning.SilverMineDepthMax;
            silver.wellWidth    = EconomyTuning.DefaultMineWellWidth;  silver.wellLength = EconomyTuning.DefaultMineWellLength; silver.padding = EconomyTuning.DefaultMinePadding;
            silver.layers       = new BlockLayer[]
            {
                new BlockLayer { maxDepth=2,  dirtWeight=70, stoneWeight=30, ironWeight=0,  goldWeight=0 },
                new BlockLayer { maxDepth=6,  dirtWeight=20, stoneWeight=55, ironWeight=25, goldWeight=0 },
                new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=55, ironWeight=35, goldWeight=5 },
            };
            availableMines.Add(silver);

            // Gold
            var gold = ScriptableObject.CreateInstance<MineShopData>();
            gold.displayName  = Loc.T("mine_gold_name");
            gold.description  = Loc.T("mine_gold_desc");
            gold.labelColor   = new Color(1.00f, 0.84f, 0.10f);
            gold.buyPrice     = EconomyTuning.GoldMinePrice;
            gold.sellBackRatio= EconomyTuning.GoldMineSellBackRatio;
            gold.depthMin     = EconomyTuning.GoldMineDepthMin; gold.depthMax = EconomyTuning.GoldMineDepthMax;
            gold.wellWidth    = EconomyTuning.DefaultMineWellWidth;  gold.wellLength = EconomyTuning.DefaultMineWellLength; gold.padding = EconomyTuning.DefaultMinePadding;
            gold.layers       = new BlockLayer[]
            {
                new BlockLayer { maxDepth=2,  dirtWeight=50, stoneWeight=50, ironWeight=0,  goldWeight=0  },
                new BlockLayer { maxDepth=6,  dirtWeight=10, stoneWeight=50, ironWeight=35, goldWeight=5  },
                new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=35, ironWeight=35, goldWeight=25 },
            };
            availableMines.Add(gold);

            if (verboseLogs) Debug.Log("[MineMarket] Default mines created (3 types).");
        }

        public void ApplyLocalizationToAvailableMines()
        {
            if (availableMines == null) return;

            for (int i = 0; i < availableMines.Count; i++)
            {
                MineShopData data = availableMines[i];
                if (data == null) continue;

                switch (data.buyPrice)
                {
                    case EconomyTuning.BronzeMinePrice:
                        data.displayName = Loc.T("mine_bronze_name");
                        data.description = Loc.T("mine_bronze_desc");
                        break;
                    case EconomyTuning.SilverMinePrice:
                        data.displayName = Loc.T("mine_silver_name");
                        data.description = Loc.T("mine_silver_desc");
                        break;
                    case EconomyTuning.GoldMinePrice:
                        data.displayName = Loc.T("mine_gold_name");
                        data.description = Loc.T("mine_gold_desc");
                        break;
                }
            }
        }

        void Update()
        {
            // Lazy-catch mobile controls if they were created after this Awake
            if (mobileControls == null)
                mobileControls = MobileTouchControls.Instance;

            // If mine not purchased — do nothing
            if (pendingMine == null)
            {
                if (IsPlacementMode) { IsPlacementMode = false; ShowPreview(false); }
                UpdatePlacementButton(false);
                wasOnIslandPlacing = false;
                return;
            }

            IsPlacementMode = true;

            // But only show the ghost on the Island
            bool onIsland = WellGen != null && !WellGen.IsInLobbyMode;
            ShowPreview(onIsland);

            // Show / hide mobile PLACE button
            UpdatePlacementButton(onIsland);
            wasOnIslandPlacing = onIsland;

            if (onIsland)
            {
                UpdatePlacementPreview();

                // Left click = confirm placement
                if (IsConfirmPressed())
                {
                    ConfirmPlacement();
                }
            }

            // Cancel purchase (Escape)
            if (IsCancelPressed())
            {
                CancelPlacement();
            }
        }

        // Enables / disables PLACE button on mobile
        private void UpdatePlacementButton(bool visible)
        {
            mobileControls?.SetPlacementModeVisible(visible);
        }

        private Vector3 lastValidPlacementPos;

        void UpdatePlacementPreview()
        {
            if (previewInstance == null || !previewInstance.activeSelf) return;

            Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
            ray.origin += ray.direction * 0.1f; 

            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                VoxelIsland land = hit.collider.GetComponentInParent<VoxelIsland>();
                if (land != null)
                {
                    Vector3 localPos = land.transform.InverseTransformPoint(hit.point);
                    int gx = Mathf.RoundToInt(localPos.x);
                    int gz = Mathf.RoundToInt(localPos.z);
                    
                    Vector3 snappedLocal = new Vector3(gx, -WellGen.LobbyFloorY + 1.1f, gz);
                    previewInstance.transform.position = land.transform.TransformPoint(snappedLocal);
                    
                    lastValidPlacementPos = previewInstance.transform.position;
                    SetPreviewVisibility(true);
                }
                else
                {
                    // If not hit the island — hide preview (so it doesn't hang in air)
                    SetPreviewVisibility(false);
                }
            }
            else
            {
                SetPreviewVisibility(false);
            }
        }

        private void SetPreviewVisibility(bool visible)
        {
            if (previewInstance == null) return;
            MeshRenderer mr = previewInstance.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = visible;
        }

        Vector2 GetMousePosition()
        {
            // Mobile: use the "sticky" position — where the player last tapped/touched
            // on the right side of the screen (LookPad area).
            // This allows the preview to stay at the tapped location while the
            // player moves their hand to hit the PLACE confirmation button.
            if (mobileControls != null && mobileControls.IsActive)
                return mobileControls.StickyAimPosition;

            if (Cursor.lockState == CursorLockMode.Locked)
                return new Vector2(Screen.width / 2f, Screen.height / 2f);
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        public bool TryBuyMine(MineShopData data)
        {
            if (data == null) return false;
            if (GlobalEconomy.Money < data.buyPrice) { if (verboseLogs) Debug.Log("[MineMarket] Not enough money."); return false; }

            // FIX #16: проверяем лимит шахт
            if (maxPlacedMines > 0 && WellGen != null && WellGen.PlacedMines != null && WellGen.PlacedMines.Count >= maxPlacedMines)
            {
                if (verboseLogs) Debug.Log($"[MineMarket] Mine limit reached ({maxPlacedMines}). Sell an existing mine first.");
                return false;
            }
            // Notify persistence BEFORE deducting so it can snapshot the pre-purchase
            // state and won't overwrite with a stale load arriving moments later.
            var persistence = UnityEngine.Object.FindFirstObjectByType<PlayerProgressPersistence>();
            if (persistence != null)
                persistence.NotifyEconomyTouched();

            GlobalEconomy.Money -= data.buyPrice;

            int depth = data.RollDepth();
            pendingMine = new MineInstance(data, depth, 0);
            AsyncGameplayEvents.PublishBuyMine(data.displayName, depth, -data.buyPrice);

            if (verboseLogs) Debug.Log($"[MineMarket] Bought '{data.displayName}'. Installation mode ON.");
            IsPlacementMode = true; 
            return true;
        }

        public void SellCurrentMine()
        {
            if (WellGen == null || !WellGen.IsMineGenerated || WellGen.ActiveMine == null) return;
            MineInstance mine = WellGen.ActiveMine;
            
            GlobalEconomy.Money += mine.SellPrice;

            AsyncGameplayEvents.PublishSellMine(mine.shopData != null ? mine.shopData.displayName : string.Empty, mine.SellPrice);
            OnMineSold?.Invoke(mine);
            WellGen.DemolishMine();
        }

        public void CancelPlacementPublic() => CancelPlacement();
        public bool IsMineGenerated() => WellGen != null && WellGen.IsMineGenerated;

        void ConfirmPlacement()
        {
            if (pendingMine == null) return;

            // FIX #10: защита от NullReferenceException — previewInstance может не существовать,
            // если игрок нажал PLACE не наведя курсор на остров
            if (previewInstance == null || !previewInstance.activeSelf)
            {
                if (verboseLogs) Debug.LogWarning("[MineMarket] ConfirmPlacement: previewInstance is null or inactive. Placement cancelled.");
                return;
            }

            if (WellGen == null || WellGen.ActiveIsland == null)
            {
                if (verboseLogs) Debug.LogWarning("[MineMarket] ConfirmPlacement: ActiveIsland not available.");
                return;
            }

            Vector3 worldPos = previewInstance.transform.position;
            Vector3 localPos = WellGen.ActiveIsland.transform.InverseTransformPoint(worldPos);
            int gx = Mathf.RoundToInt(localPos.x);
            int gz = Mathf.RoundToInt(localPos.z);

            if (verboseLogs) Debug.Log($"[MineMarket] Installing mine to grid: {gx}, {gz}");
            WellGen.GenerateMineAt(pendingMine, gx, gz);
            AsyncGameplayEvents.PublishPlaceMine(pendingMine.shopData != null ? pendingMine.shopData.displayName : string.Empty, gx, gz);
            OnMinePlaced?.Invoke(pendingMine);
            pendingMine = null;
            IsPlacementMode = false;
            ShowPreview(false);
        }

        void CancelPlacement()
        {
            if (pendingMine != null)
            {
                // FIX #2: возвращаем локально, но без превышения начального баланса
                GlobalEconomy.Money = Mathf.Max(0, GlobalEconomy.Money + pendingMine.shopData.buyPrice);
            }
            pendingMine = null;
            IsPlacementMode = false;
            ShowPreview(false);
            OnPlacementCancelled?.Invoke();
        }

        void ShowPreview(bool show)
        {
            if (!show) { if (previewInstance != null) previewInstance.SetActive(false); return; }
            if (previewInstance == null)
            {
                previewInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                previewInstance.name = "MinePlacementPreview";
                foreach (var c in previewInstance.GetComponentsInChildren<Collider>()) c.enabled = false;
                
                MeshRenderer mr = previewInstance.GetComponent<MeshRenderer>();
                // Try several shaders to avoid pink color (Magenta)
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                if (s == null) s = Shader.Find("Sprites/Default");
                
                Material mat = new Material(s);
                mat.color = new Color(0, 1, 0.4f, 0.4f);
                
                // Transparency setup for Standard/Lit
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
                mr.material = mat;
            }
            if (pendingMine != null)
            {
                float w = pendingMine.shopData.wellWidth + pendingMine.shopData.padding * 2;
                float l = pendingMine.shopData.wellLength + pendingMine.shopData.padding * 2;
                previewInstance.transform.localScale = new Vector3(w, 0.5f, l);
            }
            previewInstance.SetActive(true);
        }



        bool IsConfirmPressed()
        {
            // ── Mobile: dedicated PLACE button ───────────────────────────────
            if (mobileControls != null && mobileControls.IsActive)
                return mobileControls.PlaceMinePressedThisFrame;

            // ── Desktop: left mouse click (not over UI) ──────────────────────
            bool triggered = false;
#if ENABLE_INPUT_SYSTEM
            triggered = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
            triggered = Input.GetMouseButtonDown(0);
#endif
            if (triggered)
            {
                bool overUI = (Cursor.lockState != CursorLockMode.Locked) &&
                              (UnityEngine.EventSystems.EventSystem.current != null &&
                               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());
                return !overUI;
            }
            return false;
        }

        bool IsCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}


