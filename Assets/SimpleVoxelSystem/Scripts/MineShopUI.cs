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
    /// ÐŸÐ¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ Ð°Ð²Ñ‚Ð¾Ð½Ð¾Ð¼Ð½Ñ‹Ð¹ UI Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð° ÑˆÐ°Ñ…Ñ‚.
    /// Ð”Ð¾Ð±Ð°Ð²ÑŒÑ‚Ðµ ÑÑ‚Ð¾Ñ‚ ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚ Ð½Ð° Ð»ÑŽÐ±Ð¾Ð¹ GameObject Ð² ÑÑ†ÐµÐ½Ðµ (Ð¸Ð»Ð¸ Ð½Ð° Canvas).
    /// ÐžÐ½ ÑÐ°Ð¼ Ð½Ð°Ð¹Ð´Ñ‘Ñ‚ MineMarket Ð¸ ÑÐ¾Ð·Ð´Ð°ÑÑ‚ Ð²ÐµÑÑŒ Canvas/ÐºÐ½Ð¾Ð¿ÐºÐ¸ Ð¿Ñ€Ð¸ Ð·Ð°Ð¿ÑƒÑÐºÐµ.
    /// ÐÐ¸Ñ‡ÐµÐ³Ð¾ Ð½Ðµ Ð½ÑƒÐ¶Ð½Ð¾ Ð¿Ñ€Ð¸Ð²ÑÐ·Ñ‹Ð²Ð°Ñ‚ÑŒ Ð²Ñ€ÑƒÑ‡Ð½ÑƒÑŽ.
    /// </summary>
    public class MineShopUI : MonoBehaviour
    {
        // ÐžÐ¿Ñ†Ð¸Ð¾Ð½Ð°Ð»ÑŒÐ½Ñ‹Ðµ Ñ€ÑƒÑ‡Ð½Ñ‹Ðµ ÑÑÑ‹Ð»ÐºÐ¸ (Ð·Ð°Ð¿Ð¾Ð»Ð½ÑÑŽÑ‚ÑÑ Ð°Ð²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸ ÐµÑÐ»Ð¸ null)
        [Header("ÐÐ²Ñ‚Ð¾Ð¿Ð¾Ð¸ÑÐº (Ð¾ÑÑ‚Ð°Ð²ÑŒÑ‚Ðµ Ð¿ÑƒÑÑ‚Ñ‹Ð¼)")]
        public MineMarket mineMarket;

        // â”€â”€â”€ Runtime UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Canvas     rootCanvas;
        private GameObject shopPanel;
        private GameObject _overlay;       // Ñ‚Ñ‘Ð¼Ð½Ñ‹Ð¹ Ñ„Ð¾Ð½ Ð¿Ð¾Ð´ Ð¿Ð°Ð½ÐµÐ»ÑŒÑŽ
        private GameObject hud;
        private Text       moneyText;
        private Text       statusLabel;
        private Button     sellMineBtn;
        private Button     cancelBtn;
        private Button     switchWorldBtn;
        private Button     createIslandBtn;
        private Transform  buttonContainer;

        private readonly List<Button> mineButtons = new List<Button>();

        // Ð¦Ð²ÐµÑ‚Ð° UI
        private static readonly Color ColPanel   = new Color(0.08f, 0.08f, 0.10f, 0.93f);
        private static readonly Color ColHUD     = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        private static readonly Color ColBtnShop = new Color(0.18f, 0.55f, 0.95f, 1f);
        private static readonly Color ColBtnSell = new Color(0.95f, 0.55f, 0.18f, 1f);
        private static readonly Color ColBtnCancel=new Color(0.85f, 0.20f, 0.20f, 1f);
        private static readonly Color ColText    = new Color(0.95f, 0.95f, 0.95f, 1f);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void Awake()
        {
            // Ð˜Ñ‰ÐµÐ¼ MineMarket Ð² ÑÑ†ÐµÐ½Ðµ
            if (mineMarket == null)
                mineMarket = FindFirstObjectByType<MineMarket>();

            // Ð•ÑÐ»Ð¸ Ð½Ðµ Ð½Ð°ÑˆÐ»Ð¸ â€” ÑÐ¾Ð·Ð´Ð°Ñ‘Ð¼ Ð½Ð° WellGenerator
            if (mineMarket == null)
            {
                WellGenerator wg = FindFirstObjectByType<WellGenerator>();
                if (wg != null)
                {
                    mineMarket = wg.gameObject.AddComponent<MineMarket>();
                    Debug.Log("[MineShopUI] MineMarket Ð°Ð²Ñ‚Ð¾Ð·Ð´Ð°Ð½ Ð½Ð° " + wg.name);
                }
            }

            if (mineMarket == null)
            {
                Debug.LogWarning("[MineShopUI] WellGenerator Ñ‚Ð¾Ð¶Ðµ Ð½Ðµ Ð½Ð°Ð¹Ð´ÐµÐ½. Ð”Ð¾Ð±Ð°Ð²ÑŒÑ‚Ðµ ÐµÐ³Ð¾ Ð² ÑÑ†ÐµÐ½Ñƒ.");
                enabled = false;
                return;
            }

            BuildUI();
        }

        void Start()
        {
            // ÐŸÐ¾Ð´Ð¿Ð¸ÑÐºÐ¸
            mineMarket.OnMinePlaced         += OnMinePlaced;
            mineMarket.OnMineSold           += OnMineSold;
            mineMarket.OnPlacementCancelled += OnPlacementCancelled;

            BuildShopButtons();
            SetPanelVisible(false);
            RefreshHUD();
        }

        void OnDestroy()
        {
            if (mineMarket == null) return;
            mineMarket.OnMinePlaced         -= OnMinePlaced;
            mineMarket.OnMineSold           -= OnMineSold;
            mineMarket.OnPlacementCancelled -= OnPlacementCancelled;
        }

        void Update()
        {
            RefreshHUD();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Ð¡Ð¾Ð·Ð´Ð°Ð½Ð¸Ðµ Ð²ÑÐµÐ³Ð¾ UI
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void BuildUI()
        {
            // â”€â”€ Canvas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                GameObject cGo = new GameObject("MineShopCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }

            // â”€â”€ EventSystem â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

            // â”€â”€ HUD: Ð´ÐµÐ½ÑŒÐ³Ð¸ Ð² Ð»ÐµÐ²Ð¾Ð¼ Ð²ÐµÑ€Ñ…Ð½ÐµÐ¼ ÑƒÐ³Ð»Ñƒ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            hud = MakePanel("HUD", rootCanvas.transform,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                pos: new Vector2(10f, -10f), size: new Vector2(280f, 48f),
                color: ColHUD);

            moneyText = MakeLabelOffset(hud.transform, "MoneyText",
                "ðŸ’° 0â‚½  |  âš’ï¸ Ð£Ñ€. 1", 16, TextAnchor.MiddleLeft,
                new Vector2(10, 0), new Vector2(-10, 0));

            statusLabel = MakeLabelOffset(hud.transform, "StatusLabel",
                "", 14, TextAnchor.MiddleCenter,
                new Vector2(10, -50), new Vector2(-10, -30));
            statusLabel.color = new Color(1f, 1f, 0.7f, 1f); // ÐœÑÐ³ÐºÐ¸Ð¹ Ð±ÐµÐ¶ÐµÐ²Ñ‹Ð¹

            // â”€â”€ ÐšÐ½Ð¾Ð¿ÐºÐ° ÐžÑ‚Ð¼ÐµÐ½Ð° (Ñ€ÐµÐ¶Ð¸Ð¼ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            cancelBtn = MakeButton(rootCanvas.transform, "CancelBtn",
                "âœ• ÐžÑ‚Ð¼ÐµÐ½Ð°", ColBtnCancel,
                anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
                pos: new Vector2(-10f, 10f), size: new Vector2(120f, 40f));
            cancelBtn.onClick.AddListener(() => mineMarket.CancelPlacementPublic());
            cancelBtn.gameObject.SetActive(false);

            // â”€â”€ ÐšÐ½Ð¾Ð¿ÐºÐ° ÐŸÑ€Ð¾Ð´Ð°Ñ‚ÑŒ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            sellMineBtn = MakeButton(rootCanvas.transform, "SellMineBtn",
                "ðŸ’° ÐŸÑ€Ð¾Ð´Ð°Ñ‚ÑŒ ÑˆÐ°Ñ…Ñ‚Ñƒ", ColBtnSell,
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-10f, -10f), size: new Vector2(155f, 40f));
            sellMineBtn.onClick.AddListener(() => mineMarket.SellCurrentMine());
            sellMineBtn.gameObject.SetActive(false);

            // â”€â”€ ÐšÐ½Ð¾Ð¿ÐºÐ° ÐŸÐµÑ€ÐµÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ñ ÐœÐ¸Ñ€Ð¾Ð² â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            switchWorldBtn = MakeButton(rootCanvas.transform, "SwitchWorldBtn",
                "ðŸ  Ð’ Ð›Ð¾Ð±Ð±Ð¸", new Color(0.2f, 0.7f, 0.2f, 1f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-170f, -10f), size: new Vector2(140f, 40f));
            switchWorldBtn.onClick.AddListener(() => 
            {
                if (mineMarket.WellGen.IsInLobbyMode)
                    mineMarket.WellGen.SwitchToMine();
                else
                    mineMarket.WellGen.SwitchToLobby();
            });
            switchWorldBtn.gameObject.SetActive(false);

            // â”€â”€ ÐšÐ½Ð¾Ð¿ÐºÐ° Ð¡Ð¾Ð·Ð´Ð°Ñ‚ÑŒ ÐžÑÑ‚Ñ€Ð¾Ð² (Ñ‚ÐµÐ¿ÐµÑ€ÑŒ ÑÐ²ÐµÑ€Ñ…Ñƒ, Ð² Ð²Ð¸Ð´Ðµ Ð¸ÐºÐ¾Ð½ÐºÐ¸/Ð¼Ð°Ð»ÐµÐ½ÑŒÐºÐ¾Ð¹ Ð¿Ð°Ð½ÐµÐ»Ð¸) â”€â”€
            createIslandBtn = MakeButton(rootCanvas.transform, "CreateIslandBtn",
                "ðŸ Ð¡Ð¾Ð·Ð´Ð°Ñ‚ÑŒ Ð¡Ð²Ð¾Ð¹ ÐžÑÑ‚Ñ€Ð¾Ð²", new Color(0.15f, 0.45f, 0.85f, 0.9f),
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                pos: new Vector2(0, -10f), size: new Vector2(240f, 40f));
            createIslandBtn.onClick.AddListener(() => 
            {
                // ÐŸÐµÑ€ÐµÐºÐ»ÑŽÑ‡Ð°ÐµÐ¼ÑÑ Ð² Ñ€ÐµÐ¶Ð¸Ð¼ Ð¾ÑÑ‚Ñ€Ð¾Ð²Ð° (ÑÑ‚Ð¾ ÑÐ°Ð¼Ð¾ Ð²Ñ‹Ð·Ð¾Ð²ÐµÑ‚ Ð³ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸ÑŽ, ÐµÑÐ»Ð¸ ÐµÐ³Ð¾ Ð½ÐµÑ‚)
                mineMarket.WellGen.SwitchToMine();
            });
            createIslandBtn.gameObject.SetActive(true);

            // â”€â”€ Ð¢Ñ‘Ð¼Ð½Ñ‹Ð¹ Ð¾Ð²ÐµÑ€Ð»ÐµÐ¹-Ñ„Ð¾Ð½ (Ð¿Ð¾Ð´ Ð¿Ð°Ð½ÐµÐ»ÑŒÑŽ) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            GameObject overlay = MakePanel("ShopOverlay", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                pos: Vector2.zero, size: new Vector2(10000f, 10000f),
                color: new Color(0f, 0f, 0f, 0.55f));
            overlay.transform.SetSiblingIndex(0);
            overlay.SetActive(false);

            // â”€â”€ Ð¦ÐµÐ½Ñ‚Ñ€Ð°Ð»ÑŒÐ½Ð°Ñ Ð¿Ð°Ð½ÐµÐ»ÑŒ Ð¼Ð°Ð³Ð°Ð·Ð¸Ð½Ð° â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            shopPanel = MakePanel("ShopPanel", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                pos: Vector2.zero, size: new Vector2(380f, 460f),
                color: ColPanel);

            // Ð—Ð°Ð³Ð¾Ð»Ð¾Ð²Ð¾Ðº
            MakeLabelOffset(shopPanel.transform, "ShopTitle",
                "ðŸ”¨ ÐœÐÐ“ÐÐ—Ð˜Ð Ð¨ÐÐ¥Ð¢", 20, TextAnchor.UpperCenter,
                new Vector2(0, -12), new Vector2(0, 0), bold: true);

            // ÐŸÐ¾Ð´Ð·Ð°Ð³Ð¾Ð»Ð¾Ð²Ð¾ÐºÑ Ð´ÐµÐ½ÑŒÐ³Ð°Ð¼Ð¸
            MakeLabelOffset(shopPanel.transform, "ShopMoney",
                "ðŸ’° Ð‘Ð°Ð»Ð°Ð½Ñ: 0â‚½  â”‚  [B] â€” Ð·Ð°ÐºÑ€Ñ‹Ñ‚ÑŒ", 12, TextAnchor.UpperCenter,
                new Vector2(0, -42), new Vector2(0, -22), bold: false);

            // Ð“Ð¾Ñ€Ð¸Ð·Ð¾Ð½Ñ‚Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€Ð°Ð·Ð´ÐµÐ»Ð¸Ñ‚ÐµÐ»ÑŒ
            MakeSeparator(shopPanel.transform, new Vector2(8, -62), new Vector2(-8, -60));

            // ÐšÐ¾Ð½Ñ‚ÐµÐ¹Ð½ÐµÑ€ Ñ Ð²ÐµÑ€Ñ‚Ð¸ÐºÐ°Ð»ÑŒÐ½Ñ‹Ð¼ layout
            buttonContainer = MakeScrollContainer(shopPanel.transform);

            // Ð‘ÐµÑ€Ñ‘Ð¼ Ð¾Ñ‹Ð² Ð½Ð° Ð¾Ð²ÐµÑ€Ð»ÐµÐ¹ (Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð¾Ð½ Ð²ÐºÐ»/Ð²Ñ‹ÐºÐ» Ð²Ð¼ÐµÑÑ‚Ðµ Ñ Ð¿Ð°Ð½ÐµÐ»ÑŒÑŽ)
            shopPanel.SetActive(false);
            overlay.SetActive(false);
            // Ð¥Ñ€Ð°Ð½Ð¸Ð¼ Ñ€ÐµÑ„ÐµÑ€ÐµÐ½Ñ Ð½Ð° Ð¾Ð²ÐµÑ€Ð»ÐµÐ¹
            _overlay = overlay;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ÐšÐ½Ð¾Ð¿ÐºÐ¸ ÑˆÐ°Ñ…Ñ‚
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

            // ÐŸÐ¾Ð´ÑÑ‚Ñ€Ð°Ð¸Ð²Ð°ÐµÐ¼ Ð²Ñ‹ÑÐ¾Ñ‚Ñƒ Ð¿Ð°Ð½ÐµÐ»Ð¸: Ð·Ð°Ð³Ð¾Ð»Ð¾Ð²Ð¾Ðº 70 + ÐºÐ°Ñ€Ñ‚Ð¾Ñ‡ÐºÐ¸
            float h = 80f + mineMarket.availableMines.Count * (86f + 8f);
            if (shopPanel != null)
            {
                RectTransform rt = shopPanel.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(200f, h));
            }
        }

        Button CreateMineButton(MineShopData data)
        {
            // ÐšÐ¾Ð½Ñ‚ÐµÐ¹Ð½ÐµÑ€ ÐºÐ°Ñ€Ñ‚Ð¾Ñ‡ÐºÐ¸
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

            // Ð›ÐµÐ²Ð°Ñ Ð¿Ð¾Ð»Ð¾ÑÐºÐ°-Ð°ÐºÑ†ÐµÐ½Ñ‚
            GameObject stripe = new GameObject("Stripe");
            stripe.transform.SetParent(go.transform, false);
            RectTransform srt = stripe.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.offsetMin = new Vector2(0f, 0f);
            srt.offsetMax = new Vector2(7f, 0f);
            stripe.AddComponent<Image>().color = data.labelColor;

            // ÐÐ°Ð·Ð²Ð°Ð½Ð¸Ðµ
            MakeLabelOffset(go.transform, "Name",
                $"<b>{data.displayName}</b>", 15, TextAnchor.UpperLeft,
                new Vector2(14, -6), new Vector2(-12, -6));

            // Ð“Ð»ÑƒÐ±Ð¸Ð½Ð°
            MakeLabelOffset(go.transform, "Depth",
                $"ðŸ•³ Ð“Ð»ÑƒÐ±Ð¸Ð½Ð°: {data.depthMin}â€“{data.depthMax} ÑÐ».", 12, TextAnchor.UpperLeft,
                new Vector2(14, -28), new Vector2(-12, -28)).color = new Color(0.75f, 0.85f, 1f, 1f);

            // Ð¡Ð¾ÑÑ‚Ð°Ð² (Ð²ÐµÑ€Ñ…Ð½Ð¸Ð¹ ÑÐ»Ð¾Ð¹)
            string comp = BuildCompositionLine(data);
            MakeLabelOffset(go.transform, "Comp",
                comp, 11, TextAnchor.UpperLeft,
                new Vector2(14, -46), new Vector2(-12, -46)).color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Ð¦ÐµÐ½Ð° ÑÐ¿Ñ€Ð°Ð²Ð°
            Text priceT = MakeLabelOffset(go.transform, "Price",
                $"ðŸ’° {data.buyPrice}â‚½", 15, TextAnchor.MiddleRight,
                new Vector2(0, 0), new Vector2(-12, 0), bold: true);
            priceT.color = new Color(1f, 0.88f, 0.25f, 1f);

            MineShopData cap = data;
            btn.onClick.AddListener(() =>
            {
                bool canAfford = GlobalEconomy.Money >= cap.buyPrice;
                if (!canAfford)
                {
                    SetStatus($"âš ï¸ ÐÐµ Ñ…Ð²Ð°Ñ‚Ð°ÐµÑ‚ Ð´ÐµÐ½ÐµÐ³! ÐÑƒÐ¶Ð½Ð¾ {cap.buyPrice}â‚½, ÐµÑÑ‚ÑŒ {GlobalEconomy.Money}â‚½.");
                    return;
                }
                if (mineMarket.TryBuyMine(cap))
                {
                    SetPanelVisible(false);
                    SetStatus($"ÐšÐ»Ð¸ÐºÐ½Ð¸Ñ‚Ðµ Ð»ÐµÐ²Ð¾Ð¹ ÐºÐ½Ð¾Ð¿ÐºÐ¾Ð¹, Ñ‡Ñ‚Ð¾Ð±Ñ‹ ÑƒÑÑ‚Ð°Ð½Ð¾Ð²Ð¸Ñ‚ÑŒ Â«{cap.displayName}Â». Escape â€” Ð¾Ñ‚Ð¼ÐµÐ½Ð¸Ñ‚ÑŒ.");
                }
            });

            return btn;
        }

        /// <summary>Ð“Ð»Ð¾Ð±Ð°Ð»ÑŒÐ½Ð¾Ðµ Ð½Ð°Ð¶Ð°Ñ‚Ð¸Ðµ ÐºÐ»Ð°Ð²Ð¸ÑˆÐ¸ B.</summary>
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
            if (data.layers == null || data.layers.Length == 0) return "â€”";
            var l = data.layers[data.layers.Length > 1 ? 1 : 0]; // Ð¿Ð¾ÑÑ€ÐµÐ´Ð½Ð¸Ð¹ ÑÐ»Ð¾Ð¹
            int total = l.dirtWeight + l.stoneWeight + l.ironWeight + l.goldWeight;
            if (total <= 0) return "â€”";
            var parts = new System.Collections.Generic.List<string>();
            if (l.dirtWeight  > 0) parts.Add($"ðŸŸ«Ð—ÐµÐ¼Ð»Ñ {l.dirtWeight  * 100 / total}%");
            if (l.stoneWeight > 0) parts.Add($"âšªÐšÐ°Ð¼ÐµÐ½ÑŒ {l.stoneWeight * 100 / total}%");
            if (l.ironWeight  > 0) parts.Add($"ðŸ”¶Ð–ÐµÐ»ÐµÐ·Ð¾ {l.ironWeight  * 100 / total}%");
            if (l.goldWeight  > 0) parts.Add($"ðŸŸ¡Ð—Ð¾Ð»Ð¾Ñ‚Ð¾ {l.goldWeight  * 100 / total}%");
            return string.Join("  ", parts);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Callbacks
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void OnMinePlaced(MineInstance mine)
        {
            SetStatus($"âœ… Ð¨Ð°Ñ…Ñ‚Ð° Â«{mine.shopData.displayName}Â» ÑƒÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð°! Ð“Ð»ÑƒÐ±Ð¸Ð½Ð°: {mine.rolledDepth} ÑÐ».");
        }

        void OnMineSold(MineInstance mine)
        {
            SetStatus($"ðŸ’° Ð¨Ð°Ñ…Ñ‚Ð° Ð¿Ñ€Ð¾Ð´Ð°Ð½Ð° Ð·Ð° {mine.SellPrice}â‚½. Ð£Ñ‡Ð°ÑÑ‚Ð¾Ðº ÑÐ²Ð¾Ð±Ð¾Ð´ÐµÐ½.");
        }

        void OnPlacementCancelled()
        {
            SetStatus("Ð£ÑÑ‚Ð°Ð½Ð¾Ð²ÐºÐ° Ð¾Ñ‚Ð¼ÐµÐ½ÐµÐ½Ð°. Ð”ÐµÐ½ÑŒÐ³Ð¸ Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‰ÐµÐ½Ñ‹.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ÐžÐ±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ðµ HUD
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        void RefreshHUD()
        {
            if (mineMarket == null) return;

            bool islandBuilt = mineMarket.WellGen != null && mineMarket.WellGen.IsIslandGenerated;
            bool hasMine     = mineMarket.IsMineGenerated();
            bool inLobby     = mineMarket.WellGen != null && mineMarket.WellGen.IsInLobbyMode;
            bool hasPending  = mineMarket.IsPlacementMode && !inLobby; // ÐÐ° Ð¾ÑÑ‚Ñ€Ð¾Ð²Ðµ Ñ ÑˆÐ°Ñ…Ñ‚Ð¾Ð¹ Ð² Ñ€ÑƒÐºÐ°Ñ…

            if (moneyText != null) 
                moneyText.text = $"ðŸ’° {GlobalEconomy.Money}â‚½  |  âš’ï¸ Ð£Ñ€. {GlobalEconomy.MiningLevel} ({GlobalEconomy.MiningXP} XP)";

            // ÐšÐ½Ð¾Ð¿ÐºÐ° ÑÐ¾Ð·Ð´Ð°Ð½Ð¸Ñ: Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² Ð»Ð¾Ð±Ð±Ð¸ Ð¸ Ð¿Ð¾ÐºÐ° Ð¾ÑÑ‚Ñ€Ð¾Ð²Ð° Ð½ÐµÑ‚
            if (createIslandBtn != null) 
                createIslandBtn.gameObject.SetActive(inLobby && !islandBuilt);

            // ÐšÐ½Ð¾Ð¿ÐºÐ° Ð¿ÐµÑ€ÐµÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ñ:
            if (switchWorldBtn != null)
            {
                switchWorldBtn.gameObject.SetActive(islandBuilt);
                var txt = switchWorldBtn.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.text = inLobby ? "ðŸ Ðš ÐžÑÑ‚Ñ€Ð¾Ð²Ñƒ" : "ðŸ  Ðš Ð›Ð¾Ð±Ð±Ð¸";
            }

            // ÐšÐ½Ð¾Ð¿ÐºÐ¸ Ð¿Ð¾ÐºÑƒÐ¿ÐºÐ¸ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹ Ð¢ÐžÐ›Ð¬ÐšÐž Ð² Ð»Ð¾Ð±Ð±Ð¸
            foreach (var btn in mineButtons)
            {
                if (btn != null) btn.interactable = inLobby;
            }

            // ÐšÐ½Ð¾Ð¿ÐºÐ° Ð¿Ñ€Ð¾Ð´Ð°Ð¶Ð¸: Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐµÑÐ»Ð¸ ÐºÑƒÐ¿Ð¸Ð»Ð¸, Ð½Ð¾ ÐµÑ‰Ðµ Ð½Ðµ Ð¿Ð¾ÑÑ‚Ð°Ð²Ð¸Ð»Ð¸ (Ð² Ñ€ÐµÐ¶Ð¸Ð¼Ðµ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ)
            if (sellMineBtn != null) 
            {
                // ÐŸÐ¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÐµÐ»ÑŒ Ñ…Ð¾Ñ‡ÐµÑ‚ Ð¿Ñ€Ð¾Ð´Ð°Ð²Ð°Ñ‚ÑŒ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐµÑÐ»Ð¸ ÐºÑƒÐ¿Ð¸Ð»Ð¸, Ð½Ð¾ ÐµÑ‰Ðµ Ð½Ðµ Ð¿Ð¾ÑÑ‚Ð°Ð²Ð¸Ð»Ð¸.
                // Ð’ Ð½Ð°ÑˆÐµÐ¹ Ð»Ð¾Ð³Ð¸ÐºÐµ ÑÑ‚Ð¾ Ð·Ð½Ð°Ñ‡Ð¸Ñ‚ pendingMine != null.
                // ÐœÑ‹ ÑƒÐ±Ð¸Ñ€Ð°ÐµÐ¼ ÐºÐ½Ð¾Ð¿ÐºÑƒ ÐŸÑ€Ð¾Ð´Ð°Ñ‚ÑŒ Ð´Ð»Ñ ÑƒÐ¶Ðµ ÑƒÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð½Ñ‹Ñ… ÑˆÐ°Ñ…Ñ‚.
                sellMineBtn.gameObject.SetActive(false); 
            }
            
            bool isPlacing = mineMarket.IsPlacementMode;

            // ÐšÐ½Ð¾Ð¿ÐºÐ° Ð¾Ñ‚Ð¼ÐµÐ½Ñ‹ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ: Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐºÐ¾Ð³Ð´Ð° ÐºÑƒÐ¿Ð¸Ð»Ð¸, Ð½Ð¾ ÐµÑ‰Ðµ Ð½Ðµ Ð¿Ð¾ÑÑ‚Ð°Ð²Ð¸Ð»Ð¸
            if (cancelBtn != null) 
            {
                // ÐŸÐ¾ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÐ¼ ÐºÐ½Ð¾Ð¿ÐºÑƒ Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‚Ð°, ÐµÑÐ»Ð¸ ÑˆÐ°Ñ…Ñ‚Ð° ÐºÑƒÐ¿Ð»ÐµÐ½Ð° (Ð² Ñ€ÑƒÐºÐ°Ñ…), 
                // Ð½ÐµÐ·Ð°Ð²Ð¸ÑÐ¸Ð¼Ð¾ Ð¾Ñ‚ Ñ‚Ð¾Ð³Ð¾, Ð² Ð»Ð¾Ð±Ð±Ð¸ Ð¼Ñ‹ Ð¸Ð»Ð¸ Ð½Ð° Ð¾ÑÑ‚Ñ€Ð¾Ð²Ðµ.
                cancelBtn.gameObject.SetActive(isPlacing);
                
                var txt = cancelBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.text = "ðŸ’° Ð’ÐµÑ€Ð½ÑƒÑ‚ÑŒ Ð´ÐµÐ½ÑŒÐ³Ð¸";
            }

            // Ð¡Ñ‚Ð°Ñ‚ÑƒÑ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ
            if (statusLabel != null)
            {
                if (isPlacing)
                {
                    statusLabel.text = inLobby 
                        ? "ðŸ“¦ <color=yellow>Ð¨Ð°Ñ…Ñ‚Ð° ÐºÑƒÐ¿Ð»ÐµÐ½Ð°!</color> Ð’ÐµÑ€Ð½Ð¸Ñ‚ÐµÑÑŒ Ð½Ð° ÐžÑÑ‚Ñ€Ð¾Ð² Ð´Ð»Ñ ÑƒÑÑ‚Ð°Ð½Ð¾Ð²ÐºÐ¸."
                        : "ðŸ“ <color=yellow>Ð ÐµÐ¶Ð¸Ð¼ ÑƒÑÑ‚Ð°Ð½Ð¾Ð²ÐºÐ¸.</color> Ð’Ñ‹Ð±ÐµÑ€Ð¸Ñ‚Ðµ Ð¼ÐµÑÑ‚Ð¾ Ð›ÐšÐœ.";
                }
                else
                {
                    statusLabel.text = "";
                }
            }

            // HUD Ð²Ð¸Ð´ÐµÐ½ Ð’Ð¡Ð•Ð“Ð”Ð (Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð²Ð¸Ð´ÐµÑ‚ÑŒ Ð´ÐµÐ½ÑŒÐ³Ð¸ Ð¸ ÑÑ‚Ð°Ñ‚ÑƒÑ Ð² Ð»Ð¾Ð±Ð±Ð¸)
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
            if (shopPanel != null)  shopPanel.SetActive(v);
            if (_overlay  != null)  _overlay.SetActive(v);
            // ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÐ¼ ÑÑ‚Ñ€Ð¾ÐºÑƒ Ð±Ð°Ð»Ð°Ð½ÑÐ° Ð² Ð¿Ð°Ð½ÐµÐ»Ð¸
            if (v) UpdatePanelMoneyLabel();
        }

        void UpdatePanelMoneyLabel()
        {
            // ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÐ¼ Ñ‚ÐµÐºÑÑ‚ Ð¿Ð¾Ð´Ð·Ð°Ð³Ð¾Ð»Ð¾Ð²ÐºÐ° Ð² Ð¿Ð°Ð½ÐµÐ»Ð¸ (ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ)
            if (shopPanel == null) return;
            var txt = shopPanel.transform.Find("ShopMoney")?.GetComponent<Text>();
            if (txt != null)
                txt.text = $"ðŸ’° Ð‘Ð°Ð»Ð°Ð½Ñ: {GlobalEconomy.Money}â‚½  â”‚  [B] â€” Ð·Ð°ÐºÑ€Ñ‹Ñ‚ÑŒ";
        }

        void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Ð’ÑÐ¿Ð¾Ð¼Ð¾Ð³Ð°Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ðµ Ñ„Ð°Ð±Ñ€Ð¸ÐºÐ¸ UI-ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚Ð¾Ð²
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        static Font _font;
        static Font GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        // ÐŸÐ°Ð½ÐµÐ»ÑŒ
        static GameObject MakePanel(string name, Transform parent,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot     = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        // ÐšÐ½Ð¾Ð¿ÐºÐ° (Ñ anchor/pivot)
        static Button MakeButton(Transform parent, string name, string label, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = color;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            Text txt = MakeLabelOffset(go.transform, "Label", label, 13, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            return btn;
        }

        // ÐšÐ½Ð¾Ð¿ÐºÐ° (Ð±ÐµÐ· anchor â€” Ð¿Ð¾ ÑƒÐ¼Ð¾Ð»Ñ‡Ð°Ð½Ð¸ÑŽ left/top)
        static Button MakeButton(Transform parent, string name, string label, Color color,
            Vector2 pos, Vector2 size)
            => MakeButton(parent, name, label, color,
                new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);

        // Ð¢ÐµÐºÑÑ‚Ð¾Ð²Ñ‹Ð¹ Ð»ÐµÐ¹Ð±Ð» Ñ anchoredPosition
        static Text MakeLabel(Transform parent, string name, string text, int fontSize,
            TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, bool bold = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text txt = go.AddComponent<Text>();
            txt.font      = GetFont();
            txt.fontSize  = fontSize;
            txt.alignment = align;
            txt.color     = ColText;
            txt.text      = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        // Ð¢ÐµÐºÑÑ‚Ð¾Ð²Ñ‹Ð¹ Ð»ÐµÐ¹Ð±Ð»: stretch (anchorMin=0,0 / anchorMax=1,1) + offsetMin/offsetMax
        static Text MakeLabelOffset(Transform parent, string name, string text, int fontSize,
            TextAnchor align, Vector2 offsetMin, Vector2 offsetMax, bool bold = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Text txt = go.AddComponent<Text>();
            txt.font      = GetFont();
            txt.fontSize  = fontSize;
            txt.alignment = align;
            txt.color     = ColText;
            txt.text      = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        // Ð“Ð¾Ñ€Ð¸Ð·Ð¾Ð½Ñ‚Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€Ð°Ð·Ð´ÐµÐ»Ð¸Ñ‚ÐµÐ»ÑŒ
        static void MakeSeparator(Transform parent, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Separator");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(offsetMin.x, 0f);
            rt.offsetMax = new Vector2(offsetMax.x, 0f);
            rt.anchoredPosition = new Vector2(0f, offsetMin.y);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1f);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        }

        // Ð’ÐµÑ€Ñ‚Ð¸ÐºÐ°Ð»ÑŒÐ½Ñ‹Ð¹ scroll-ÐºÐ¾Ð½Ñ‚ÐµÐ¹Ð½ÐµÑ€ Ð´Ð»Ñ ÐºÐ°Ñ€Ñ‚Ð¾Ñ‡ÐµÐº
        static Transform MakeScrollContainer(Transform parent)
        {
            GameObject go = new GameObject("ButtonContainer");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -70f);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing            = 8f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.padding = new RectOffset(0, 0, 4, 4);

            ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go.transform;
        }
    }
}
