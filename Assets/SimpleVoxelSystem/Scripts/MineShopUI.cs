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

        // ─── Runtime UI ──────────────────────────────────────────────
        private Canvas     rootCanvas;
        private GameObject shopPanel;
        private GameObject _overlay;       // тёмный фон под панелью
        private GameObject hud;
        private Text       moneyText;
        private Text       statusLabel;
        private Button     sellMineBtn;
        private Button     cancelBtn;
        private Button     switchWorldBtn;
        private Button     createIslandBtn;
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
            // Ищем MineMarket в сцене
            if (mineMarket == null)
                mineMarket = FindFirstObjectByType<MineMarket>();

            // Если не нашли — создаём на WellGenerator
            if (mineMarket == null)
            {
                WellGenerator wg = FindFirstObjectByType<WellGenerator>();
                if (wg != null)
                {
                    mineMarket = wg.gameObject.AddComponent<MineMarket>();
                    Debug.Log("[MineShopUI] MineMarket автоздан на " + wg.name);
                }
            }

            if (mineMarket == null)
            {
                Debug.LogWarning("[MineShopUI] WellGenerator тоже не найден. Добавьте его в сцену.");
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
            // ── Canvas ──────────────────────────────────────────────────
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                GameObject cGo = new GameObject("MineShopCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }

            // ── EventSystem ───────────────────────────────────────────────
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

            // ── HUD: деньги в левом верхнем углу ────────────────────
            hud = MakePanel("HUD", rootCanvas.transform,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                pos: new Vector2(10f, -10f), size: new Vector2(280f, 48f),
                color: ColHUD);

            moneyText = MakeLabelOffset(hud.transform, "MoneyText",
                "💰 0₽  |  ⚒️ Ур. 1", 16, TextAnchor.MiddleLeft,
                new Vector2(10, 0), new Vector2(-10, 0));

            statusLabel = MakeLabelOffset(hud.transform, "StatusLabel",
                "", 14, TextAnchor.MiddleCenter,
                new Vector2(10, -50), new Vector2(-10, -30));
            statusLabel.color = new Color(1f, 1f, 0.7f, 1f); // Мягкий бежевый

            // ── Кнопка Отмена (режим размещения) ─────────────────────
            cancelBtn = MakeButton(rootCanvas.transform, "CancelBtn",
                "✕ Отмена", ColBtnCancel,
                anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
                pos: new Vector2(-10f, 10f), size: new Vector2(120f, 40f));
            cancelBtn.onClick.AddListener(() => mineMarket.CancelPlacementPublic());
            cancelBtn.gameObject.SetActive(false);

            // ── Кнопка Продать ─────────────────────────────────────────────
            sellMineBtn = MakeButton(rootCanvas.transform, "SellMineBtn",
                "💰 Продать шахту", ColBtnSell,
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-10f, -10f), size: new Vector2(155f, 40f));
            sellMineBtn.onClick.AddListener(() => mineMarket.SellCurrentMine());
            sellMineBtn.gameObject.SetActive(false);

            // ── Кнопка Переключения Миров ───────────────────────────────────
            switchWorldBtn = MakeButton(rootCanvas.transform, "SwitchWorldBtn",
                "🏠 В Лобби", new Color(0.2f, 0.7f, 0.2f, 1f),
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

            // ── Кнопка Создать Остров (теперь сверху, в виде иконки/маленькой панели) ──
            createIslandBtn = MakeButton(rootCanvas.transform, "CreateIslandBtn",
                "🏝 Создать Свой Остров", new Color(0.15f, 0.45f, 0.85f, 0.9f),
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                pos: new Vector2(0, -10f), size: new Vector2(240f, 40f));
            createIslandBtn.onClick.AddListener(() => 
            {
                // Переключаемся в режим острова (это само вызовет генерацию, если его нет)
                mineMarket.WellGen.SwitchToMine();
            });
            createIslandBtn.gameObject.SetActive(true);

            // ── Тёмный оверлей-фон (под панелью) ─────────────────────
            GameObject overlay = MakePanel("ShopOverlay", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                pos: Vector2.zero, size: new Vector2(10000f, 10000f),
                color: new Color(0f, 0f, 0f, 0.55f));
            overlay.transform.SetSiblingIndex(0);
            overlay.SetActive(false);

            // ── Центральная панель магазина ───────────────────────
            shopPanel = MakePanel("ShopPanel", rootCanvas.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                pos: Vector2.zero, size: new Vector2(380f, 460f),
                color: ColPanel);

            // Заголовок
            MakeLabelOffset(shopPanel.transform, "ShopTitle",
                "🔨 МАГАЗИН ШАХТ", 20, TextAnchor.UpperCenter,
                new Vector2(0, -12), new Vector2(0, 0), bold: true);

            // Подзаголовокс деньгами
            MakeLabelOffset(shopPanel.transform, "ShopMoney",
                "💰 Баланс: 0₽  │  [B] — закрыть", 12, TextAnchor.UpperCenter,
                new Vector2(0, -42), new Vector2(0, -22), bold: false);

            // Горизонтальный разделитель
            MakeSeparator(shopPanel.transform, new Vector2(8, -62), new Vector2(-8, -60));

            // Контейнер с вертикальным layout
            buttonContainer = MakeScrollContainer(shopPanel.transform);

            // Берём оыв на оверлей (чтобы он вкл/выкл вместе с панелью)
            shopPanel.SetActive(false);
            overlay.SetActive(false);
            // Храним референс на оверлей
            _overlay = overlay;
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

            // Подстраиваем высоту панели: заголовок 70 + карточки
            float h = 80f + mineMarket.availableMines.Count * (86f + 8f);
            if (shopPanel != null)
            {
                RectTransform rt = shopPanel.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(200f, h));
            }
        }

        Button CreateMineButton(MineShopData data)
        {
            // Контейнер карточки
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

            // Левая полоска-акцент
            GameObject stripe = new GameObject("Stripe");
            stripe.transform.SetParent(go.transform, false);
            RectTransform srt = stripe.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.offsetMin = new Vector2(0f, 0f);
            srt.offsetMax = new Vector2(7f, 0f);
            stripe.AddComponent<Image>().color = data.labelColor;

            // Название
            MakeLabelOffset(go.transform, "Name",
                $"<b>{data.displayName}</b>", 15, TextAnchor.UpperLeft,
                new Vector2(14, -6), new Vector2(-12, -6));

            // Глубина
            MakeLabelOffset(go.transform, "Depth",
                $"🕳 Глубина: {data.depthMin}–{data.depthMax} сл.", 12, TextAnchor.UpperLeft,
                new Vector2(14, -28), new Vector2(-12, -28)).color = new Color(0.75f, 0.85f, 1f, 1f);

            // Состав (верхний слой)
            string comp = BuildCompositionLine(data);
            MakeLabelOffset(go.transform, "Comp",
                comp, 11, TextAnchor.UpperLeft,
                new Vector2(14, -46), new Vector2(-12, -46)).color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Цена справа
            Text priceT = MakeLabelOffset(go.transform, "Price",
                $"💰 {data.buyPrice}₽", 15, TextAnchor.MiddleRight,
                new Vector2(0, 0), new Vector2(-12, 0), bold: true);
            priceT.color = new Color(1f, 0.88f, 0.25f, 1f);

            MineShopData cap = data;
            btn.onClick.AddListener(() =>
            {
                bool canAfford = GlobalEconomy.Money >= cap.buyPrice;
                if (!canAfford)
                {
                    SetStatus($"⚠️ Не хватает денег! Нужно {cap.buyPrice}₽, есть {GlobalEconomy.Money}₽.");
                    return;
                }
                if (mineMarket.TryBuyMine(cap))
                {
                    SetPanelVisible(false);
                    SetStatus($"Кликните левой кнопкой, чтобы установить «{cap.displayName}». Escape — отменить.");
                }
            });

            return btn;
        }

        /// <summary>Глобальное нажатие клавиши B.</summary>
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
            if (data.layers == null || data.layers.Length == 0) return "—";
            var l = data.layers[data.layers.Length > 1 ? 1 : 0]; // посредний слой
            int total = l.dirtWeight + l.stoneWeight + l.ironWeight + l.goldWeight;
            if (total <= 0) return "—";
            var parts = new System.Collections.Generic.List<string>();
            if (l.dirtWeight  > 0) parts.Add($"🟫Земля {l.dirtWeight  * 100 / total}%");
            if (l.stoneWeight > 0) parts.Add($"⚪Камень {l.stoneWeight * 100 / total}%");
            if (l.ironWeight  > 0) parts.Add($"🔶Железо {l.ironWeight  * 100 / total}%");
            if (l.goldWeight  > 0) parts.Add($"🟡Золото {l.goldWeight  * 100 / total}%");
            return string.Join("  ", parts);
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

            bool islandBuilt = mineMarket.WellGen != null && mineMarket.WellGen.IsIslandGenerated;
            bool hasMine     = mineMarket.IsMineGenerated();
            bool inLobby     = mineMarket.WellGen != null && mineMarket.WellGen.IsInLobbyMode;
            bool hasPending  = mineMarket.IsPlacementMode && !inLobby; // На острове с шахтой в руках

            if (moneyText != null) 
                moneyText.text = $"💰 {GlobalEconomy.Money}₽  |  ⚒️ Ур. {GlobalEconomy.MiningLevel} ({GlobalEconomy.MiningXP} XP)";

            // Кнопка создания: только в лобби и пока острова нет
            if (createIslandBtn != null) 
                createIslandBtn.gameObject.SetActive(inLobby && !islandBuilt);

            // Кнопка переключения:
            if (switchWorldBtn != null)
            {
                switchWorldBtn.gameObject.SetActive(islandBuilt);
                var txt = switchWorldBtn.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.text = inLobby ? "🏝 К Острову" : "🏠 К Лобби";
            }

            // Кнопки покупки активны ТОЛЬКО в лобби
            foreach (var btn in mineButtons)
            {
                if (btn != null) btn.interactable = inLobby;
            }

            // Кнопка продажи: только если купили, но еще не поставили (в режиме размещения)
            if (sellMineBtn != null) 
            {
                // Пользователь хочет продавать только если купили, но еще не поставили.
                // В нашей логике это значит pendingMine != null.
                // Мы убираем кнопку Продать для уже установленных шахт.
                sellMineBtn.gameObject.SetActive(false); 
            }
            
            bool isPlacing = mineMarket.IsPlacementMode;

            // Кнопка отмены размещения: только когда купили, но еще не поставили
            if (cancelBtn != null) 
            {
                // Показываем кнопку возврата, если шахта куплена (в руках), 
                // независимо от того, в лобби мы или на острове.
                cancelBtn.gameObject.SetActive(isPlacing);
                
                var txt = cancelBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.text = "💰 Вернуть деньги";
            }

            // Статус размещения
            if (statusLabel != null)
            {
                if (isPlacing)
                {
                    statusLabel.text = inLobby 
                        ? "📦 <color=yellow>Шахта куплена!</color> Вернитесь на Остров для установки."
                        : "📍 <color=yellow>Режим установки.</color> Выберите место ЛКМ.";
                }
                else
                {
                    statusLabel.text = "";
                }
            }

            // HUD виден ВСЕГДА (чтобы видеть деньги и статус в лобби)
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
            // Обновляем строку баланса в панели
            if (v) UpdatePanelMoneyLabel();
        }

        void UpdatePanelMoneyLabel()
        {
            // Обновляем текст подзаголовка в панели (если есть)
            if (shopPanel == null) return;
            var txt = shopPanel.transform.Find("ShopMoney")?.GetComponent<Text>();
            if (txt != null)
                txt.text = $"💰 Баланс: {GlobalEconomy.Money}₽  │  [B] — закрыть";
        }

        void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
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

        // Горизонтальный разделитель
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

        // Вертикальный scroll-контейнер для карточек
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
