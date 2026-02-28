using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Полностью автономный UI магазина шахт.
    /// Добавьте этот компонент на любой GameObject в сцене (или на Canvas).
    /// Он сам найдёт MineMarket и создаст весь Canvas/кнопки при запуске.
    /// Ничего не нужно привязывать вручную.
    /// </summary>
    public class MineShopUI : MonoBehaviour
    {
        // Опциональные ручные ссылки (заполняются автоматически если null)
        [Header("Автопоиск (оставьте пустым)")]
        public MineMarket mineMarket;

        // ─── Runtime UI ─────────────────────────────────────────────────────
        private Canvas     rootCanvas;
        private GameObject shopPanel;
        private GameObject hud;            // Верхняя панель (деньги + кнопка магазина)
        private Text       moneyText;
        private Text       statusText;
        private Button     openShopBtn;
        private Button     sellMineBtn;
        private Button     cancelBtn;
        private Transform  buttonContainer;

        private readonly List<Button> mineButtons = new List<Button>();

        // Цвета UI
        private static readonly Color ColPanel   = new Color(0.08f, 0.08f, 0.10f, 0.93f);
        private static readonly Color ColHUD     = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        private static readonly Color ColBtnShop = new Color(0.18f, 0.55f, 0.95f, 1f);
        private static readonly Color ColBtnSell = new Color(0.95f, 0.55f, 0.18f, 1f);
        private static readonly Color ColBtnCancel=new Color(0.85f, 0.20f, 0.20f, 1f);
        private static readonly Color ColText    = new Color(0.95f, 0.95f, 0.95f, 1f);

        // ════════════════════════════════════════════════════════════════════

        void Awake()
        {
            if (mineMarket == null)
                mineMarket = FindFirstObjectByType<MineMarket>();

            if (mineMarket == null)
            {
                Debug.LogWarning("[MineShopUI] MineMarket не найден в сцене! Убедитесь, что Setup Scene запущен.");
                enabled = false;
                return;
            }

            BuildUI();
        }

        void Start()
        {
            // Подписки
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

        // ════════════════════════════════════════════════════════════════════
        // Создание всего UI
        // ════════════════════════════════════════════════════════════════════

        void BuildUI()
        {
            // ── Canvas ──────────────────────────────────────────────────────
            // Ищем существующий Canvas, иначе создаём
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                GameObject cGo = new GameObject("MineShopCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }

            // ── EventSystem ─────────────────────────────────────────────────
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ── HUD (верхняя полоска: деньги + кнопка магазина) ─────────────
            hud = MakePanel("HUD", rootCanvas.transform,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                pos: new Vector2(10f, -10f), size: new Vector2(340f, 54f),
                color: ColHUD);

            moneyText = MakeLabelOffset(hud.transform, "MoneyText",
                "💰 0₽", 18, TextAnchor.MiddleLeft,
                new Vector2(10, 0), new Vector2(-160, 0));

            openShopBtn = MakeButton(hud.transform, "BuyMineBtn",
                "🛒 Купить шахту", ColBtnShop,
                new Vector2(190, 7), new Vector2(140, 40));
            openShopBtn.onClick.AddListener(TogglePanel);

            // ── Статусная строка (снизу) ─────────────────────────────────────
            GameObject statusBar = MakePanel("StatusBar", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
                pos: new Vector2(0f, 8f), size: new Vector2(500f, 36f),
                color: new Color(0.05f, 0.05f, 0.1f, 0.75f));

            statusText = MakeLabelOffset(statusBar.transform, "StatusText",
                "Участок свободен. Купите шахту в магазине.", 13, TextAnchor.MiddleCenter,
                new Vector2(8, 0), new Vector2(-8, 0));

            // ── Кнопка Отмена (появляется в режиме размещения) ──────────────
            cancelBtn = MakeButton(rootCanvas.transform, "CancelBtn",
                "✕ Отмена", ColBtnCancel,
                anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
                pos: new Vector2(-10f, 10f), size: new Vector2(120f, 40f));
            cancelBtn.onClick.AddListener(() => mineMarket.CancelPlacementPublic());
            cancelBtn.gameObject.SetActive(false);

            // ── Кнопка Продать (появляется когда шахта стоит) ───────────────
            sellMineBtn = MakeButton(rootCanvas.transform, "SellMineBtn",
                "💰 Продать шахту", ColBtnSell,
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-10f, -10f), size: new Vector2(155f, 40f));
            sellMineBtn.onClick.AddListener(() => mineMarket.SellCurrentMine());
            sellMineBtn.gameObject.SetActive(false);

            // ── Панель магазина (список шахт) ────────────────────────────────
            shopPanel = MakePanel("ShopPanel", rootCanvas.transform,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                pos: new Vector2(10f, -74f), size: new Vector2(270f, 300f),
                color: ColPanel);

            // Заголовок
            MakeLabelOffset(shopPanel.transform, "ShopTitle",
                "═══ Магазин шахт ═══", 15, TextAnchor.MiddleCenter,
                new Vector2(0, -36), new Vector2(0, 0), bold: true);

            // Контейнер с вертикальным layout
            buttonContainer = MakeScrollContainer(shopPanel.transform);
        }

        // ════════════════════════════════════════════════════════════════════
        // Кнопки шахт
        // ════════════════════════════════════════════════════════════════════

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

            // Подстраиваем высоту панели
            float h = 46f + mineMarket.availableMines.Count * 78f;
            if (shopPanel != null)
            {
                RectTransform rt = shopPanel.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            }
        }

        Button CreateMineButton(MineShopData data)
        {
            // Контейнер кнопки
            GameObject go = new GameObject(data.displayName + "_Btn");
            go.transform.SetParent(buttonContainer, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 68f);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(
                data.labelColor.r * 0.6f,
                data.labelColor.g * 0.6f,
                data.labelColor.b * 0.6f, 0.9f);

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = cb;
            btn.targetGraphic = bg;

            // Левая полоска-цвет
            GameObject stripe = new GameObject("Stripe");
            stripe.transform.SetParent(go.transform, false);
            RectTransform srt = stripe.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.offsetMin = new Vector2(0f, 0f);
            srt.offsetMax = new Vector2(6f, 0f);
            Image sImg = stripe.AddComponent<Image>();
            sImg.color = data.labelColor;

            // Название
            Text nameT = MakeLabelOffset(go.transform, "Name",
                $"<b>{data.displayName}</b>", 14, TextAnchor.UpperLeft,
                new Vector2(14, -6), new Vector2(-8, -6));

            // Описание
            Text descT = MakeLabelOffset(go.transform, "Desc",
                $"Глубина: {data.depthMin}–{data.depthMax} сл.", 11, TextAnchor.UpperLeft,
                new Vector2(14, -26), new Vector2(-8, -26));
            descT.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // Цена справа
            Text priceT = MakeLabelOffset(go.transform, "Price",
                $"💰 {data.buyPrice}₽", 14, TextAnchor.MiddleRight,
                new Vector2(0, 0), new Vector2(-10, 0), bold: true);
            priceT.color = new Color(1f, 0.9f, 0.3f, 1f);

            MineShopData cap = data;
            btn.onClick.AddListener(() =>
            {
                if (mineMarket.TryBuyMine(cap))
                {
                    SetPanelVisible(false);
                    SetStatus($"Кликните левой кнопкой, чтобы установить «{cap.displayName}». Escape — отменить.");
                }
            });

            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        // Callbacks
        // ════════════════════════════════════════════════════════════════════

        void OnMinePlaced(MineInstance mine)
        {
            SetStatus($"✅ Шахта «{mine.shopData.displayName}» установлена! Глубина: {mine.rolledDepth} сл.");
        }

        void OnMineSold(MineInstance mine)
        {
            SetStatus($"💰 Шахта продана за {mine.SellPrice}₽. Участок свободен.");
        }

        void OnPlacementCancelled()
        {
            SetStatus("Установка отменена. Деньги возвращены.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Обновление HUD
        // ════════════════════════════════════════════════════════════════════

        void RefreshHUD()
        {
            if (mineMarket == null) return;

            bool hasMine   = mineMarket.IsMineGenerated();
            bool placing   = mineMarket.IsPlacementMode;

            if (moneyText   != null) moneyText.text = $"💰 {GlobalEconomy.Money}₽";
            if (openShopBtn != null) openShopBtn.interactable = !hasMine && !placing;
            if (sellMineBtn != null) sellMineBtn.gameObject.SetActive(hasMine && !placing);
            if (cancelBtn   != null) cancelBtn.gameObject.SetActive(placing);
        }

        void TogglePanel()
        {
            if (shopPanel == null) return;
            bool next = !shopPanel.activeSelf;
            if (next && mineMarket.IsPlacementMode) return;
            SetPanelVisible(next);
        }

        void SetPanelVisible(bool v)
        {
            if (shopPanel != null) shopPanel.SetActive(v);
        }

        void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        // ════════════════════════════════════════════════════════════════════
        // Вспомогательные фабрики UI-элементов
        // ════════════════════════════════════════════════════════════════════

        static Font _font;
        static Font GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        // Панель
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

        // Кнопка (с anchor/pivot)
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

        // Кнопка (без anchor — по умолчанию left/top)
        static Button MakeButton(Transform parent, string name, string label, Color color,
            Vector2 pos, Vector2 size)
            => MakeButton(parent, name, label, color,
                new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);

        // Текстовый лейбл с anchoredPosition
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

        // Текстовый лейбл: stretch (anchorMin=0,0 / anchorMax=1,1) + offsetMin/offsetMax
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

        // Вертикальный scroll-контейнер для кнопок
        static Transform MakeScrollContainer(Transform parent)
        {
            GameObject go = new GameObject("ButtonContainer");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-8f, -44f);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing            = 6f;
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
