using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Fully autonomous mine shop UI.
    /// Add this component to any GameObject in the scene (or on a Canvas).
    /// It will find MineMarket and create all Canvas/buttons at startup.
    /// No manual binding required.
    /// </summary>
    public class MineShopUI : MonoBehaviour
    {
        // Optional manual references (filled automatically if null)
        [Header("Auto-search (leave empty)")]
        public MineMarket mineMarket;

        // ─── Runtime UI ─────────────────────────────────────────────
        private Canvas     rootCanvas;
        private GameObject shopPanel;
        private GameObject _overlay;       // dark background under the panel
        private GameObject hud;
        private Text       shopTitleText;
        private Text       moneyText;
        private Text       panelMoneyText;
        private Text       switchWorldBtnText;
        private Text       cancelBtnText;

        public bool IsVisible => shopPanel != null && shopPanel.activeSelf;
        public Button CreateIslandButton => createIslandBtn;
        public Button SwitchWorldButton => switchWorldBtn;

        private Text       statusLabel;
        private Button     sellMineBtn;
        private Button     cancelBtn;
        private Button     switchWorldBtn;
        private Button     createIslandBtn;
        private Button     setSpawnBtn;
        private Transform  buttonContainer;

        private readonly List<Button> mineButtons = new List<Button>();

        // UI Colors
        private static readonly Color ColPanel   = new Color(0.08f, 0.08f, 0.10f, 0.93f);
        private static readonly Color ColHUD     = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        private static readonly Color ColBtnShop = new Color(0.18f, 0.55f, 0.95f, 1f);
        private static readonly Color ColBtnSell = new Color(0.95f, 0.55f, 0.18f, 1f);
        private static readonly Color ColBtnCancel=new Color(0.85f, 0.20f, 0.20f, 1f);
        private static readonly Color ColText    = new Color(0.95f, 0.95f, 0.95f, 1f);

        // ═══════════════════════════════════════════════════════════════════════

        void Awake()
        {
            // Looking for MineMarket in the scene
            if (mineMarket == null)
                mineMarket = FindFirstObjectByType<MineMarket>();

            // If not found — create on WellGenerator
            if (mineMarket == null)
            {
                WellGenerator wg = FindFirstObjectByType<WellGenerator>();
                if (wg != null)
                {
                    mineMarket = wg.gameObject.AddComponent<MineMarket>();
                    Debug.Log("[MineShopUI] MineMarket auto-created on " + wg.name);
                }
            }

            if (mineMarket == null)
            {
                Debug.LogWarning("[MineShopUI] WellGenerator also not found. Add it to the scene.");
                enabled = false;
                return;
            }

            BuildUI();
        }

        void Start()
        {
            // Subscriptions
            mineMarket.OnMinePlaced         += OnMinePlaced;
            mineMarket.OnMineSold           += OnMineSold;
            mineMarket.OnPlacementCancelled += OnPlacementCancelled;
            Loc.OnLanguageChanged           += RefreshLocalization;

            BuildShopButtons();
            SetPanelVisible(false);
            RefreshHUD();
            RefreshLocalization(); // применяем перевод сразу
        }

        void OnDestroy()
        {
            if (mineMarket != null)
            {
                mineMarket.OnMinePlaced         -= OnMinePlaced;
                mineMarket.OnMineSold           -= OnMineSold;
                mineMarket.OnPlacementCancelled -= OnPlacementCancelled;
            }
            Loc.OnLanguageChanged -= RefreshLocalization;
        }

        void Update()
        {
            RefreshHUD();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Create all UI
        // ═══════════════════════════════════════════════════════════════════════

        void BuildUI()
        {
            // ── Canvas ───────────────────────────────────────────────────────
            // IMPORTANT: always create a dedicated canvas — never grab the tutorial
            // or any other existing canvas, as buttons parented there would render
            // inside the tutorial overlay and their text would be hidden behind it.
            GameObject cGo = new GameObject("MineShopCanvas");
            DontDestroyOnLoad(cGo);
            rootCanvas = cGo.AddComponent<Canvas>();
            rootCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 4000;   // ABOVE mobile controls (3000), BELOW tutorial (7000)
            rootCanvas.pixelPerfect = true;
            var cs = cGo.AddComponent<CanvasScaler>();
            cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1600f, 900f);
            cs.matchWidthOrHeight  = 0.5f;
            cGo.AddComponent<GraphicRaycaster>();

            // ── EventSystem ──────────────────────────────────────────────────
            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            // HUD: money in top left corner ────────────────────
            hud = RuntimeUIFactory.MakePanel("HUD", rootCanvas.transform,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                pos: new Vector2(10f, -10f), size: new Vector2(420f, 78f),
                color: ColHUD);

            moneyText = RuntimeUIFactory.MakeLabel(hud.transform, "MoneyText",
                "$ 0  |  Lv. 1", 18, TextAnchor.MiddleLeft,
                new Vector2(10, 32), new Vector2(-10, 0));

            statusLabel = RuntimeUIFactory.MakeLabel(hud.transform, "StatusLabel",
                "", 14, TextAnchor.MiddleLeft,
                new Vector2(10, 0), new Vector2(-10, -34));
            statusLabel.color = new Color(1f, 1f, 0.7f, 1f); // Soft beige

            // Cancel button (placement mode) ─────────────────────
            cancelBtn = RuntimeUIFactory.MakeBtn(rootCanvas.transform, "CancelBtn",
                Loc.T("btn_cancel"), ColBtnCancel,
                anchor: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f),
                pos: new Vector2(24f, 24f), size: new Vector2(160f, 70f));
            cancelBtn.onClick.AddListener(() => mineMarket.CancelPlacementPublic());
            cancelBtnText = cancelBtn.GetComponentInChildren<Text>();
            cancelBtn.gameObject.SetActive(false);

            // Sell Button ─────────────────────────────────────────────
            sellMineBtn = RuntimeUIFactory.MakeBtn(rootCanvas.transform, "SellMineBtn",
                Loc.T("btn_sell_mine"), ColBtnSell,
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-10f, -10f), size: new Vector2(155f, 40f));
            sellMineBtn.onClick.AddListener(() => mineMarket.SellCurrentMine());
            sellMineBtn.gameObject.SetActive(false);

            // World Switch Button ──────────────────────────────────
            switchWorldBtn = RuntimeUIFactory.MakeBtn(rootCanvas.transform, "SwitchWorldBtn",
                Loc.T("to_lobby"), new Color(0.2f, 0.7f, 0.2f, 1f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-170f, -10f), size: new Vector2(140f, 80f));
            switchWorldBtn.onClick.AddListener(() =>
            {
                if (mineMarket.WellGen.IsInLobbyMode)
                    mineMarket.WellGen.SwitchToMine();
                else
                    mineMarket.WellGen.SwitchToLobby();
            });
            switchWorldBtnText = switchWorldBtn.GetComponentInChildren<Text>();
            switchWorldBtn.gameObject.SetActive(false);

            // Create Island Button (now at top, as an icon/small panel) ──
            createIslandBtn = RuntimeUIFactory.MakeBtn(rootCanvas.transform, "CreateIslandBtn",
                Loc.T("create_island"), new Color(0.15f, 0.45f, 0.85f, 0.9f),
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                pos: new Vector2(0, -10f), size: new Vector2(240f, 40f));
            createIslandBtn.onClick.AddListener(() =>
            {
                // Switch to island mode (triggers generation if not exists)
                mineMarket.WellGen.SwitchToMine();
            });
            createIslandBtn.gameObject.SetActive(true);

            setSpawnBtn = RuntimeUIFactory.MakeBtn(rootCanvas.transform, "SetSpawnBtn",
                Loc.T("btn_set_spawn"), new Color(0.15f, 0.65f, 0.85f, 0.95f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-320f, -10f), size: new Vector2(140f, 80f));
            setSpawnBtn.onClick.AddListener(() =>
            {
                bool ok = mineMarket != null && mineMarket.WellGen != null && mineMarket.WellGen.SaveCurrentIslandSpawnPoint();
                SetStatus(ok
                    ? Loc.T("status_spawn_saved")
                    : Loc.T("status_spawn_save_failed"));
            });
            setSpawnBtn.gameObject.SetActive(false);

            // Dark overlay background (under panel) ─────────────────────
            GameObject overlay = RuntimeUIFactory.MakePanel("ShopOverlay", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                pos: Vector2.zero, size: new Vector2(10000f, 10000f),
                color: new Color(0f, 0f, 0f, 0.55f));
            overlay.transform.SetSiblingIndex(0);
            overlay.SetActive(false);

            // Central Shop Panel ───────────────────────
            shopPanel = RuntimeUIFactory.MakePanel("ShopPanel", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                pos: Vector2.zero, size: new Vector2(380f, 460f),
                color: ColPanel);
            RuntimeUIFactory.EnableAdaptivePanelScale(shopPanel, 0.94f, 0.90f, 0.50f);

            // Title
            shopTitleText = RuntimeUIFactory.MakeLabel(shopPanel.transform, "ShopTitle",
                Loc.T("mine_shop_title"), 22, TextAnchor.UpperCenter,
                new Vector2(0, -12), new Vector2(0, 0), bold: true);

            Button closeShopBtn = RuntimeUIFactory.MakeBtn(shopPanel.transform, "CloseShopBtn", "X",
                new Color(0.78f, 0.22f, 0.22f, 0.95f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-8f, -8f), size: new Vector2(34f, 34f));
            closeShopBtn.onClick.AddListener(() => SetPanelVisible(false));

            // Subtitle with money
            {
                string bal  = Loc.T("shop_balance");
                string cls  = Loc.T("btn_close").ToLower();
                panelMoneyText = RuntimeUIFactory.MakeLabel(shopPanel.transform, "ShopMoney",
                    bal + ": $0  |  [B]/[X] " + cls, 14, TextAnchor.UpperCenter,
                    new Vector2(0, -42), new Vector2(0, -22), bold: false);
            }

            // Horizontal Separator
            RuntimeUIFactory.MakeSeparator(shopPanel.transform, -62f, 8f);

            // Container with vertical layout
            buttonContainer = RuntimeUIFactory.MakeScrollContainer(shopPanel.transform);

            // Turn on overlay (so it enables/disables with the panel)
            shopPanel.SetActive(false);
            overlay.SetActive(false);
            // Store reference to the overlay
            _overlay = overlay;
        }

        // ───────────────────────────────────────────────────────────────────
        // Mine Buttons
        // ───────────────────────────────────────────────────────────────────

        void BuildShopButtons()
        {
            if (mineMarket?.availableMines == null) return;

            foreach (Button b in mineButtons)
                if (b != null) Destroy(b.gameObject);
            mineButtons.Clear();

            foreach (MineShopData data in mineMarket.availableMines)
            {
                if (data == null) continue;
                Button btn = CreateMineButton(data);
                mineButtons.Add(btn);
            }

            // Adjust panel height: header 80 + cards
            float h = 80f + mineMarket.availableMines.Count * (86f + 8f);
            if (shopPanel != null)
            {
                RectTransform rt = shopPanel.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Clamp(h, 260f, 760f));
            }
        }

        Button CreateMineButton(MineShopData data)
        {
            // Card container
            GameObject go = new GameObject(data.displayName + "_Btn");
            go.transform.SetParent(buttonContainer, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350f, 86f);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(
                data.labelColor.r * 0.22f,
                data.labelColor.g * 0.22f,
                data.labelColor.b * 0.22f, 0.97f);

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;
            btn.targetGraphic = bg;

            // Left accent stripe
            GameObject stripe = new GameObject("Stripe");
            stripe.transform.SetParent(go.transform, false);
            RectTransform srt = stripe.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.offsetMin = new Vector2(0f, 0f);
            srt.offsetMax = new Vector2(7f, 0f);
            stripe.AddComponent<Image>().color = data.labelColor;

            // Name
            RuntimeUIFactory.MakeLabel(go.transform, "Name",
                $"<b>{data.displayName}</b>", 17, TextAnchor.UpperLeft,
                new Vector2(14, -6), new Vector2(-12, -6));

            // Depth
            RuntimeUIFactory.MakeLabel(go.transform, "Depth",
                Loc.Tf("mine_depth_format", data.depthMin, data.depthMax), 13, TextAnchor.UpperLeft,
                new Vector2(14, -28), new Vector2(-12, -28), color: new Color(0.75f, 0.85f, 1f, 1f));

            // Composition
            string comp = BuildCompositionLine(data);
            RuntimeUIFactory.MakeLabel(go.transform, "Comp",
                comp, 12, TextAnchor.UpperLeft,
                new Vector2(14, -46), new Vector2(-12, -46), color: new Color(0.8f, 0.8f, 0.8f, 1f));

            // Price on right
            Text priceT = RuntimeUIFactory.MakeLabel(go.transform, "Price",
                $"$ {data.buyPrice}", 17, TextAnchor.MiddleRight,
                new Vector2(0, 0), new Vector2(-12, 0), bold: true, color: new Color(1f, 0.88f, 0.25f, 1f));

            MineShopData cap = data;
            btn.onClick.AddListener(() =>
            {
                bool canAfford = GlobalEconomy.Money >= cap.buyPrice;
                if (!canAfford)
                {
                    SetStatus(Loc.Tf("status_not_enough_money_detail", cap.buyPrice, GlobalEconomy.Money));
                    return;
                }
                if (mineMarket.TryBuyMine(cap))
                {
                    SetPanelVisible(false);
                    SetStatus(Loc.Tf("status_place_mine_hint", cap.displayName));
                }
            });

            return btn;
        }

        /// <summary>Global B key press.</summary>
        bool IsBPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current?[UnityEngine.InputSystem.Key.B].wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.B);
#else
            return false;
#endif
        }

        static string BuildCompositionLine(MineShopData data)
        {
            if (data.layers == null || data.layers.Length == 0) return "-";
            var l = data.layers[data.layers.Length > 1 ? 1 : 0];
            int total = l.dirtWeight + l.stoneWeight + l.ironWeight + l.goldWeight;
            if (total <= 0) return "-";
            var parts = new System.Collections.Generic.List<string>();
            if (l.dirtWeight  > 0) parts.Add($"{Loc.T("block_dirt")} {l.dirtWeight  * 100 / total}%");
            if (l.stoneWeight > 0) parts.Add($"{Loc.T("block_stone")} {l.stoneWeight * 100 / total}%");
            if (l.ironWeight  > 0) parts.Add($"{Loc.T("block_iron")} {l.ironWeight  * 100 / total}%");
            if (l.goldWeight  > 0) parts.Add($"{Loc.T("block_gold")} {l.goldWeight  * 100 / total}%");
            return string.Join("  ", parts);
        }

        // ───────────────────────────────────────────────────────────────────
        // Callbacks
        // ───────────────────────────────────────────────────────────────────

        void OnMinePlaced(MineInstance mine)
        {
            SetStatus(Loc.Tf("status_mine_placed", mine.shopData.displayName, mine.rolledDepth));
        }

        void OnMineSold(MineInstance mine)
        {
            SetStatus(Loc.Tf("status_mine_sold", mine.SellPrice));
        }

        void OnPlacementCancelled()
        {
            SetStatus(Loc.T("status_placement_cancelled"));
        }

        // ───────────────────────────────────────────────────────────────────
        // HUD Update
        // ───────────────────────────────────────────────────────────────────

        void RefreshHUD()
        {
            if (mineMarket == null) return;

            bool islandBuilt = mineMarket.WellGen != null && mineMarket.WellGen.IsIslandGenerated;
            bool inLobby     = mineMarket.WellGen != null && mineMarket.WellGen.IsInLobbyMode;
            bool isPlacing   = mineMarket.IsPlacementMode;

            if (moneyText != null)
                moneyText.text = Loc.Tf("balance_bar_format", GlobalEconomy.Money, Loc.T("lv_short"), GlobalEconomy.MiningLevel, GlobalEconomy.MiningXP, Loc.T("xp_short"));

            if (createIslandBtn != null)
                createIslandBtn.gameObject.SetActive(inLobby && !islandBuilt);

            if (setSpawnBtn != null)
                setSpawnBtn.gameObject.SetActive(!inLobby && islandBuilt);

            if (switchWorldBtn != null)
            {
                switchWorldBtn.gameObject.SetActive(islandBuilt);
                if (switchWorldBtnText != null)
                    switchWorldBtnText.text = inLobby ? Loc.T("to_island") : Loc.T("to_lobby");
            }

            foreach (var btn in mineButtons)
                if (btn != null) btn.interactable = inLobby;

            if (sellMineBtn != null)
                sellMineBtn.gameObject.SetActive(false);

            if (cancelBtn != null)
            {
                cancelBtn.gameObject.SetActive(isPlacing);
                if (cancelBtnText != null)
                    cancelBtnText.text = Loc.T("btn_cancel");
            }

            if (statusLabel != null)
                statusLabel.text = isPlacing
                    ? (inLobby ? Loc.T("status_mine_bought")
                               : Loc.T("status_placement"))
                    : "";

            if (hud != null) hud.SetActive(true);
        }

        public void TogglePanel()
        {
            if (shopPanel == null) return;
            if (mineMarket != null && mineMarket.IsPlacementMode) return;
            bool next = !shopPanel.activeSelf;
            SetPanelVisible(next);
        }

        public void SetPanelVisible(bool v)
        {
            if (shopPanel != null)
            {
                shopPanel.SetActive(v);
                GameUIWindow.SetWindowActive(shopPanel, v);
            }
            if (_overlay != null) _overlay.SetActive(v);
            if (v) UpdatePanelMoneyLabel();
        }

        void UpdatePanelMoneyLabel()
        {
            if (panelMoneyText != null)
                panelMoneyText.text = Loc.Tf("balance_header", GlobalEconomy.Money);
        }

        void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }

        /// <summary>Обновить все переводимые строки после смены языка.</summary>
        private void RefreshLocalization()
        {
            if (mineMarket != null)
                mineMarket.ApplyLocalizationToAvailableMines();

            if (shopTitleText != null)
                shopTitleText.text = Loc.T("mine_shop_title");

            UpdatePanelMoneyLabel();

            if (createIslandBtn != null)
            {
                var t = createIslandBtn.GetComponentInChildren<Text>();
                if (t != null) t.text = Loc.T("create_island");
            }
            if (setSpawnBtn != null)
            {
                var t = setSpawnBtn.GetComponentInChildren<Text>();
                if (t != null) t.text = Loc.T("btn_set_spawn");
            }
            if (sellMineBtn != null)
            {
                var t = sellMineBtn.GetComponentInChildren<Text>();
                if (t != null) t.text = Loc.T("btn_sell_mine");
            }

            BuildShopButtons();
            // switchWorldBtnText обновляется в RefreshHUD() каждый Update
        }

        // (Helper methods removed, now using RuntimeUIFactory)
    }
}
