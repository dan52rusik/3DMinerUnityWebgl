using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Кнопка "☰ МЕНЮ" в левом верхнем углу.
    /// Открывает панель меню с выбором языка (флаги RU / EN / TR).
    /// Всё строится кодом — никаких префабов не нужно.
    /// Bootstrap создаёт объект автоматически при старте.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameMenuUI : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static GameMenuUI Instance { get; private set; }

        // ── Настройки ─────────────────────────────────────────────────────────
        [Header("Appearance")]
        public Color menuBtnColor      = new Color(0.08f, 0.09f, 0.16f, 0.92f);
        public Color menuBtnTextColor  = new Color(0.88f, 0.90f, 1.00f, 1.00f);
        public Color panelBgColor      = new Color(0.07f, 0.08f, 0.14f, 0.96f);
        public Color closeBtnColor     = new Color(0.80f, 0.22f, 0.22f, 1.00f);
        public Color backdropColor     = new Color(0f, 0f, 0f, 0.45f);
        [Range(0.05f, 0.5f)] public float animDuration = 0.18f;
        private static readonly Color LangSectionBg = new Color(0.11f, 0.12f, 0.19f, 0.92f);
        private static readonly Color LangCardBg = new Color(0.15f, 0.16f, 0.24f, 1.00f);
        private static readonly Color LangCardSelectedBg = new Color(0.19f, 0.22f, 0.32f, 1.00f);
        private static readonly Color LangCardBorder = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color LangCardSelectedBorder = new Color(0.47f, 0.87f, 0.62f, 0.95f);
        private static readonly Color LangCodeColor = new Color(0.82f, 0.86f, 0.95f, 0.86f);
        private static readonly Color LangCodeSelectedColor = new Color(0.98f, 0.99f, 1f, 1f);
        private static readonly Color LangAccent = new Color(0.35f, 0.88f, 0.59f, 1f);

        // ── Runtime ───────────────────────────────────────────────────────────
        private Canvas    _canvas;
        private Image     _backdrop;
        private RectTransform _panel;
        private Button    _menuBtn;
        private bool      _isOpen;
        private Coroutine _anim;
        private Text      _accountLabel;
        private Text      _playerNameValue;
        private Text      _bestMoneyValue;
        private Text      _authButtonLabel;

        // Flag button references для обновления выделения
        private FlagBtn[] _flagBtns;
        private Text      _langLabel;    // ссылка на "Язык:" для обновления при смене языка
        private Text      _menuBtnLabel; // ссылка на "☰ Настройки" для обновления

        // ── Bootstrap ─────────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<GameMenuUI>() != null) return;
            var go = new GameObject("GameMenuUI");
            DontDestroyOnLoad(go);
            go.AddComponent<GameMenuUI>();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Loc.Initialize() НЕ вызываем — это делает LocalizationManager
            BuildUI();
            // Явно применяем текущий язык после построения меню
            OnLangChanged();
        }

        private void OnEnable()
        {
            Loc.OnLanguageChanged += OnLangChanged;
            PlayerIdentity.OnIdentityChanged += RefreshAccountSection;
            GlobalEconomy.OnBestMoneyChanged += OnBestMoneyChanged;
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= OnLangChanged;
            PlayerIdentity.OnIdentityChanged -= RefreshAccountSection;
            GlobalEconomy.OnBestMoneyChanged -= OnBestMoneyChanged;
        }

        private void OnLangChanged()
        {
            RefreshFlagButtons();
            if (_menuBtnLabel != null)
                _menuBtnLabel.text = "\u2630  " + Loc.T("settings");
            if (_langLabel != null)
                _langLabel.text = Loc.T("language") + ":";
            if (_accountLabel != null)
                _accountLabel.text = Loc.T("account") + ":";
            RefreshAccountSection();
        }


        // ══════════════════════════════════════════════════════════════════════
        // UI Build
        // ══════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── Собственный Canvas всегда поверх всего ─────────────────────────
            var cgo = new GameObject("GameMenuCanvas");
            cgo.transform.SetParent(transform); // дочерний к GameMenuUI (DontDestroyOnLoad)
            _canvas = cgo.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 3100;
            _canvas.pixelPerfect = true; // критично для чёткости текста

            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f); // то же что MineShopCanvas
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;

            cgo.AddComponent<GraphicRaycaster>();

            // ── Backdrop (закрывает меню при клике вне панели) ─────────────────
            _backdrop = CreateImage(null, "Backdrop");
            _backdrop.transform.SetParent(_canvas.transform, false);
            var brt = _backdrop.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            _backdrop.color = new Color(0, 0, 0, 0);
            _backdrop.raycastTarget = true;
            var backBtn = _backdrop.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            backBtn.onClick.AddListener(CloseMenu);
            _backdrop.gameObject.SetActive(false);

            // ── Menu button (☰) — левый верхний угол ──────────────────────────
            var menuBtnGo = new GameObject("MenuBtn");
            menuBtnGo.transform.SetParent(_canvas.transform, false);
            var mrt = menuBtnGo.AddComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 1f);
            mrt.pivot     = new Vector2(0f, 1f);
            // -150px в reference-пространстве (ниже HUD ~50px + запас)
            mrt.anchoredPosition = new Vector2(10f, -150f);
            mrt.sizeDelta = new Vector2(120f, 44f);

            var mbImg = menuBtnGo.AddComponent<Image>();
            mbImg.color = menuBtnColor;
            RoundCorners(mbImg);

            _menuBtn = menuBtnGo.AddComponent<Button>();
            SetButtonColors(_menuBtn, menuBtnColor, Lighten(menuBtnColor, 0.12f));
            _menuBtn.onClick.AddListener(ToggleMenu);

            // Текст кнопки — через RuntimeUIFactory для единого стиля
            _menuBtnLabel = RuntimeUIFactory.MakeLabel(menuBtnGo.transform, "Label",
                "☰  " + Loc.T("settings"), 15, TextAnchor.MiddleCenter);
            _menuBtnLabel.color     = menuBtnTextColor;
            _menuBtnLabel.fontStyle = FontStyle.Bold;

            // ── Панель меню ────────────────────────────────────────────────────
            var panelGo = new GameObject("MenuPanel");
            panelGo.transform.SetParent(_canvas.transform, false);
            _panel = panelGo.AddComponent<RectTransform>();
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            // панель прямо под кнопкой: -150 - 44 - 6 = -200
            _panel.anchoredPosition = new Vector2(-340f, -200f);
            _panel.sizeDelta = new Vector2(290f, 0f);

            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = panelBgColor;
            RoundCorners(panelImg);

            // Тень
            var shadow = panelGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // ── Содержимое панели — вертикальный Layout ──────────────────────
            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 16);
            layout.spacing = 12f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;

            var csf = panelGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Строка заголовка меню ─────────────────────────────────────────
            BuildHeaderRow(panelGo.transform);

            // ── Разделитель ───────────────────────────────────────────────────
            BuildDivider(panelGo.transform);

            // ── Секция языка ──────────────────────────────────────────────────
            BuildLanguageSection(panelGo.transform);
            BuildDivider(panelGo.transform);
            BuildAccountSection(panelGo.transform);

            // Скрыта изначально
            panelGo.SetActive(false);
        }

        // ── Header Row (заголовок + кнопка ×) ────────────────────────────────
        private void BuildHeaderRow(Transform parent)
        {
            var row = CreateHorizontalGroup(parent, "HeaderRow", 8f);
            var rlg = row.GetComponent<HorizontalLayoutGroup>();
            rlg.childAlignment = TextAnchor.MiddleCenter;

            // Заголовок
            var title = CreateText(row.transform, "Title", "⚙  МЕНЮ");
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.88f, 0.90f, 1f);
            title.alignment = TextAnchor.MiddleLeft;
            var tle = title.GetComponent<LayoutElement>();
            tle.flexibleWidth = 1f;
            tle.minHeight = 32f;

            // Кнопка ×
            var closeGo = new GameObject("CloseBtn");
            closeGo.transform.SetParent(row.transform, false);
            var cle = closeGo.AddComponent<LayoutElement>();
            cle.preferredWidth = 34f;
            cle.preferredHeight = 34f;
            cle.minWidth = 34f;
            cle.minHeight = 34f;

            var cImg = closeGo.AddComponent<Image>();
            cImg.color = closeBtnColor;
            RoundCorners(cImg);

            var closeBtn = closeGo.AddComponent<Button>();
            SetButtonColors(closeBtn, closeBtnColor, new Color(0.95f, 0.30f, 0.30f));
            closeBtn.onClick.AddListener(CloseMenu);

            var xt = CreateText(closeGo.transform, "X", "✕");
            xt.alignment = TextAnchor.MiddleCenter;
            xt.fontSize = 18;
            xt.fontStyle = FontStyle.Bold;
            xt.color = Color.white;
            xt.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            xt.GetComponent<RectTransform>().anchorMax = Vector2.one;
        }

        // ── Разделитель ───────────────────────────────────────────────────────
        private void BuildDivider(Transform parent)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.08f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            le.minHeight = 1f;
        }

        // ── Секция выбора языка ───────────────────────────────────────────────
        private void BuildLanguageSection(Transform parent)
        {
            var section = new GameObject("LanguageSection");
            section.transform.SetParent(parent, false);
            section.AddComponent<RectTransform>();
            var sectionImage = section.AddComponent<Image>();
            sectionImage.color = LangSectionBg;
            RoundCorners(sectionImage);
            var sectionLayout = section.AddComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(12, 12, 10, 12);
            sectionLayout.spacing = 10f;
            sectionLayout.childAlignment = TextAnchor.UpperLeft;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            var sectionFit = section.AddComponent<ContentSizeFitter>();
            sectionFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Заголовок секции — сохраняем ссылку для обновления при смене языка
            _langLabel = CreateText(section.transform, "LangLabel", Loc.T("language") + ":");
            _langLabel.fontSize = 15;
            _langLabel.fontStyle = FontStyle.Bold;
            _langLabel.color = new Color(0.82f, 0.86f, 0.95f, 0.95f);
            _langLabel.alignment = TextAnchor.MiddleLeft;
            var le = _langLabel.GetComponent<LayoutElement>();
            le.preferredHeight = 22f;
            le.minHeight = 22f;

            // Ряд с флагами
            var row = CreateHorizontalGroup(section.transform, "FlagRow", 10f);
            var rlg = row.GetComponent<HorizontalLayoutGroup>();
            rlg.childAlignment = TextAnchor.MiddleLeft;
            rlg.padding = new RectOffset(0, 0, 0, 0);
            var rle = row.GetComponent<LayoutElement>();
            rle.preferredHeight = 84f;
            rle.minHeight = 84f;

            _flagBtns = new FlagBtn[3];
            _flagBtns[0] = BuildFlagBtn(row.transform, Loc.LangRu);
            _flagBtns[1] = BuildFlagBtn(row.transform, Loc.LangEn);
            _flagBtns[2] = BuildFlagBtn(row.transform, Loc.LangTr);

            RefreshFlagButtons();
        }

        private void BuildAccountSection(Transform parent)
        {
            var section = new GameObject("AccountSection");
            section.transform.SetParent(parent, false);
            section.AddComponent<RectTransform>();
            var sectionImage = section.AddComponent<Image>();
            sectionImage.color = LangSectionBg;
            RoundCorners(sectionImage);

            var sectionLayout = section.AddComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(12, 12, 10, 12);
            sectionLayout.spacing = 8f;
            sectionLayout.childAlignment = TextAnchor.UpperLeft;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;

            var sectionFit = section.AddComponent<ContentSizeFitter>();
            sectionFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _accountLabel = CreateText(section.transform, "AccountLabel", Loc.T("account") + ":");
            _accountLabel.fontSize = 15;
            _accountLabel.fontStyle = FontStyle.Bold;
            _accountLabel.color = new Color(0.82f, 0.86f, 0.95f, 0.95f);
            _accountLabel.alignment = TextAnchor.MiddleLeft;
            var titleLayout = _accountLabel.GetComponent<LayoutElement>();
            titleLayout.preferredHeight = 22f;
            titleLayout.minHeight = 22f;

            _playerNameValue = CreateText(section.transform, "PlayerNameValue", string.Empty);
            _playerNameValue.fontSize = 14;
            _playerNameValue.color = new Color(0.96f, 0.97f, 1f, 1f);
            _playerNameValue.alignment = TextAnchor.MiddleLeft;
            var playerLayout = _playerNameValue.GetComponent<LayoutElement>();
            playerLayout.preferredHeight = 22f;
            playerLayout.minHeight = 22f;

            _bestMoneyValue = CreateText(section.transform, "BestMoneyValue", string.Empty);
            _bestMoneyValue.fontSize = 14;
            _bestMoneyValue.color = new Color(0.96f, 0.97f, 1f, 1f);
            _bestMoneyValue.alignment = TextAnchor.MiddleLeft;
            var bestLayout = _bestMoneyValue.GetComponent<LayoutElement>();
            bestLayout.preferredHeight = 22f;
            bestLayout.minHeight = 22f;

            var authButton = RuntimeUIFactory.MakeBtn(section.transform, "AuthButton", Loc.T("auth_sign_in"),
                color: new Color(0.20f, 0.46f, 0.87f, 1f), size: new Vector2(0f, 34f));
            var authLayout = authButton.gameObject.GetComponent<LayoutElement>();
            if (authLayout == null)
                authLayout = authButton.gameObject.AddComponent<LayoutElement>();
            authLayout.preferredHeight = 34f;
            authLayout.minHeight = 34f;
            authLayout.preferredWidth = 0f;

            _authButtonLabel = authButton.GetComponentInChildren<Text>();
            authButton.onClick.AddListener(AuthorizationIdentitySync.TryOpenAuthDialog);

            RefreshAccountSection();
        }

        // ── Одна кнопка-флаг ────────────────────────────────────────────────
        private FlagBtn BuildFlagBtn(Transform parent, string lang)
        {
            var go = new GameObject($"Flag_{lang.ToUpper()}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = 74f;
            le.preferredHeight = 84f;
            le.minWidth        = 74f;

            var container = go.AddComponent<Image>();
            container.color = LangCardBg;
            RoundCorners(container);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = LangCardBorder;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            var flagFrame = new GameObject("FlagFrame");
            flagFrame.transform.SetParent(go.transform, false);
            var frameImage = flagFrame.AddComponent<Image>();
            frameImage.color = new Color(0.05f, 0.06f, 0.10f, 0.65f);
            var frameRt = flagFrame.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 1f);
            frameRt.anchorMax = new Vector2(0.5f, 1f);
            frameRt.pivot = new Vector2(0.5f, 1f);
            frameRt.anchoredPosition = new Vector2(0f, -8f);
            frameRt.sizeDelta = new Vector2(62f, 34f);

            // Mask — обрезает всё что выходит за границу флага (нужно для диагоналей UK)
            frameImage.color = new Color(1f, 1f, 1f, 1f); // mask требует непрозрачный Image
            var mask = flagFrame.AddComponent<Mask>();
            mask.showMaskGraphic = false; // скрыть саму рамку, показать только содержимое

            // Строим флаг внутри рамки.
            BuildFlagStripes(flagFrame.transform, lang, 58f, 30f);

            // Текст кода страны — через RuntimeUIFactory как весь HUD
            var code = RuntimeUIFactory.MakeLabel(go.transform, "Code",
                lang.ToUpper(), 12, TextAnchor.LowerCenter,
                offsetMin: new Vector2(0f, 6f),
                offsetMax: new Vector2(0f, -58f));
            code.fontStyle = FontStyle.Bold;
            code.color     = LangCodeColor;

            var accent = new GameObject("Accent");
            accent.transform.SetParent(go.transform, false);
            var accentImg = accent.AddComponent<Image>();
            accentImg.color = new Color(LangAccent.r, LangAccent.g, LangAccent.b, 0f);
            var art = accent.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.5f, 0f);
            art.anchorMax = new Vector2(0.5f, 0f);
            art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = new Vector2(0f, 4f);
            art.sizeDelta = new Vector2(28f, 3f);

            // Кнопка
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            var captureLang = lang;
            btn.onClick.AddListener(() =>
            {
                Loc.SetLanguage(captureLang);
                CloseMenu();
            });

            return new FlagBtn
            {
                root = go,
                card = container,
                cardOutline = outline,
                accent = accentImg,
                codeText = code,
                lang = lang
            };
        }

        // ── Строит полосы флага ──────────────────────────────────────────────
        private void BuildFlagStripes(Transform parent, string lang, float w, float h)
        {
            var root = new GameObject("FlagArt");
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = new Vector2(0f, -2f);
            rootRt.sizeDelta = new Vector2(w, h);

            if (lang == Loc.LangRu)
            {
                // Белый / Синий / Красный — горизонтальные полосы
                BuildHorizontalFlag(root.transform, h, new[]
                {
                    new Color(1.00f, 1.00f, 1.00f),
                    new Color(0.05f, 0.24f, 0.73f),
                    new Color(0.83f, 0.11f, 0.13f)
                });
                return;
            }

            if (lang == Loc.LangEn)
            {
                // 1. Тёмно-синий фон
                MakeStretchRect(root.transform, "Field", new Color(0.02f, 0.16f, 0.55f));

                // 2. Белый диагональный X (крест Святого Андрея + Святого Патрика, широкий)
                MakeRotatedRect(root.transform, "DiagW1", Color.white,  45f, new Vector2(w * 2f, h * 0.30f));
                MakeRotatedRect(root.transform, "DiagW2", Color.white, -45f, new Vector2(w * 2f, h * 0.30f));

                // 3. Красный диагональный X (крест Святого Патрика, узкий)
                MakeRotatedRect(root.transform, "DiagR1", new Color(0.78f, 0.06f, 0.14f),  45f, new Vector2(w * 2f, h * 0.13f));
                MakeRotatedRect(root.transform, "DiagR2", new Color(0.78f, 0.06f, 0.14f), -45f, new Vector2(w * 2f, h * 0.13f));

                // 4. Белый прямой крест (крест Святого Георгия, широкий)
                MakeStretchRect(root.transform, "CrossV_W", Color.white, anchorMinX: 0.40f, anchorMaxX: 0.60f);
                MakeStretchRect(root.transform, "CrossH_W", Color.white, anchorMinY: 0.33f, anchorMaxY: 0.67f);

                // 5. Красный прямой крест (крест Святого Георгия)
                MakeStretchRect(root.transform, "CrossV_R", new Color(0.78f, 0.06f, 0.14f), anchorMinX: 0.445f, anchorMaxX: 0.555f);
                MakeStretchRect(root.transform, "CrossH_R", new Color(0.78f, 0.06f, 0.14f), anchorMinY: 0.405f, anchorMaxY: 0.595f);
                return;
            }

            if (lang == Loc.LangTr)
            {
                var go = new GameObject("TurkeyFlag");
                go.transform.SetParent(root.transform, false);
                var img = go.AddComponent<Image>();
                img.sprite = BuildTurkeyFlagSprite(Mathf.RoundToInt(w), Mathf.RoundToInt(h));
                img.preserveAspect = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                return;
            }

            // Fallback
            MakeStretchRect(root.transform, "Field", Color.gray);
        }

        /// <summary>Повёрнутый прямоугольник (для диагональных полос флага). Центр — середина root.</summary>
        private static void MakeRotatedRect(Transform parent, string name, Color color, float angleDeg, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            go.transform.localEulerAngles = new Vector3(0f, 0f, angleDeg);
        }


        /// <summary>Прямоугольник на весь родитель (stretch). Можно ограничить по одной оси через anchor.</summary>
        private static void MakeStretchRect(Transform parent, string name, Color color,
            float anchorMinX = 0f, float anchorMaxX = 1f,
            float anchorMinY = 0f, float anchorMaxY = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorMinX, anchorMinY);
            rt.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Прямоугольник фиксированного размера, якорь в заданной нормализованной точке родителя.</summary>
        private static void MakeAnchoredRect(Transform parent, string name, Color color,
            Vector2 anchorNorm, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorNorm;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        private static void BuildHorizontalFlag(Transform parent, float height, Color[] stripes)
        {
            float stripeH = height / stripes.Length;
            for (int i = 0; i < stripes.Length; i++)
            {
                var stripe = new GameObject($"Stripe_{i}");
                stripe.transform.SetParent(parent, false);
                var img = stripe.AddComponent<Image>();
                img.color = stripes[i];
                var rt = stripe.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -stripeH * i);
                rt.sizeDelta = new Vector2(0f, stripeH);
            }
        }

        private static Sprite BuildTurkeyFlagSprite(int width, int height)
        {
            Color red = new Color(0.89f, 0.04f, 0.10f, 1f);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = red;

            float cy = (height - 1) * 0.5f;
            float outerCx = width * 0.38f;
            float innerCx = width * 0.44f;
            float outerR = height * 0.30f;
            float innerR = height * 0.24f;

            FillCircle(pixels, width, height, outerCx, cy, outerR, Color.white);
            FillCircle(pixels, width, height, innerCx, cy, innerR, red);

            Vector2 starCenter = new Vector2(width * 0.60f, cy);
            Vector2[] star = BuildStarPoints(starCenter, height * 0.15f, height * 0.06f);
            FillPolygon(pixels, width, height, star, Color.white);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void FillCircle(Color[] pixels, int width, int height, float cx, float cy, float radius, Color color)
        {
            float radiusSq = radius * radius;
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radius));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radiusSq)
                        pixels[y * width + x] = color;
                }
            }
        }

        private static Vector2[] BuildStarPoints(Vector2 center, float outerRadius, float innerRadius)
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = Mathf.Deg2Rad * (-90f + i * 36f);
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                points[i] = new Vector2(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius);
            }
            return points;
        }

        private static void FillPolygon(Color[] pixels, int width, int height, Vector2[] polygon, Color color)
        {
            float minXf = polygon[0].x;
            float maxXf = polygon[0].x;
            float minYf = polygon[0].y;
            float maxYf = polygon[0].y;
            for (int i = 1; i < polygon.Length; i++)
            {
                minXf = Mathf.Min(minXf, polygon[i].x);
                maxXf = Mathf.Max(maxXf, polygon[i].x);
                minYf = Mathf.Min(minYf, polygon[i].y);
                maxYf = Mathf.Max(maxYf, polygon[i].y);
            }

            int minX = Mathf.Max(0, Mathf.FloorToInt(minXf));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(maxXf));
            int minY = Mathf.Max(0, Mathf.FloorToInt(minYf));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(maxYf));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), polygon))
                        pixels[y * width + x] = color;
                }
            }
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                bool intersects =
                    ((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                    (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / ((polygon[j].y - polygon[i].y) + Mathf.Epsilon) + polygon[i].x);
                if (intersects) inside = !inside;
            }
            return inside;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Menu open/close
        // ═════════════════════════════════════════════════════════════════════

        public void ToggleMenu()
        {
            if (_isOpen) CloseMenu();
            else OpenMenu();
        }

        public void OpenMenu()
        {
            if (_isOpen) return;
            _isOpen = true;
            _panel.gameObject.SetActive(true);
            _backdrop.gameObject.SetActive(true);

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimatePanel(open: true));

            RefreshFlagButtons();
            RefreshMenuBtnLabel();
        }

        public void CloseMenu()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimatePanel(open: false));
            RefreshMenuBtnLabel();
        }

        // ── Анимация панели (слайд + fade backdrop) ──────────────────────────
        private IEnumerator AnimatePanel(bool open)
        {
            float t = 0f;
            float startX  = _panel.anchoredPosition.x;
            float targetX = open ? 10f : -340f; // 340 = ширина 290 + запас
            float startA  = _backdrop.color.a;
            float targetA = open ? backdropColor.a : 0f;

            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));

                _panel.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, p), _panel.anchoredPosition.y);
                _backdrop.color = new Color(0f, 0f, 0f, Mathf.Lerp(startA, targetA, p));
                yield return null;
            }

            _panel.anchoredPosition = new Vector2(targetX, _panel.anchoredPosition.y);
            _backdrop.color = new Color(0f, 0f, 0f, targetA);

            if (!open)
            {
                _panel.gameObject.SetActive(false);
                _backdrop.gameObject.SetActive(false);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Refresh
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshFlagButtons()
        {
            if (_flagBtns == null) return;
            foreach (var fb in _flagBtns)
            {
                if (fb == null) continue;
                bool selected = Loc.CurrentLanguage == fb.lang;

                if (fb.card != null)
                    fb.card.color = selected ? LangCardSelectedBg : LangCardBg;

                if (fb.cardOutline != null)
                    fb.cardOutline.effectColor = selected ? LangCardSelectedBorder : LangCardBorder;

                if (fb.accent != null)
                    fb.accent.color = selected
                        ? new Color(LangAccent.r, LangAccent.g, LangAccent.b, 1f)
                        : new Color(LangAccent.r, LangAccent.g, LangAccent.b, 0f);

                if (fb.codeText != null)
                {
                    fb.codeText.color = selected
                        ? LangCodeSelectedColor
                        : LangCodeColor;
                    fb.codeText.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        private void RefreshMenuBtnLabel()
        {
            if (_menuBtn == null) return;
            var t = _menuBtn.GetComponentInChildren<Text>();
            if (t == null) return;
            string icon = _isOpen ? "✕ " : "☰  ";
            t.text = icon + Loc.T("settings");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Builder helpers
        // ═════════════════════════════════════════════════════════════════════

        private void OnBestMoneyChanged(int _)
        {
            RefreshAccountSection();
        }

        private void RefreshAccountSection()
        {
            if (_playerNameValue != null)
            {
                string playerName = PlayerIdentity.IsGuest ? Loc.T("guest_player") : PlayerIdentity.PlayerName;
                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = Loc.T("guest_player");

                _playerNameValue.text = $"{Loc.T("player_name_label")}: {playerName}";
            }

            if (_bestMoneyValue != null)
                _bestMoneyValue.text = $"{Loc.T("best_money_label")}: ${GlobalEconomy.BestMoney}";

            if (_authButtonLabel != null)
                _authButtonLabel.text = PlayerIdentity.IsGuest ? Loc.T("auth_sign_in") : Loc.T("auth_connected");
        }

        private static Image CreateImage(Transform parent, string name)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<Image>();
        }

        private static Text CreateText(Transform parent, string name, string content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.font = RuntimeUiFont.Get();  // тот же шрифт что у всего игрового UI
            t.text = content;
            t.color = Color.white;
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;

            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = false;

            return t;
        }

        private static GameObject CreateHorizontalGroup(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var lg = go.AddComponent<HorizontalLayoutGroup>();
            lg.spacing = spacing;
            lg.childForceExpandWidth  = false;
            lg.childForceExpandHeight = true;
            lg.childControlHeight = true;
            lg.childControlWidth = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 40f;
            return go;
        }

        private static void RoundCorners(Image img)
        {
            // Unity built-in: нет встроенного радиуса — добавляем тень для эффекта глубины
            var sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor    = new Color(0, 0, 0, 0.35f);
            sh.effectDistance = new Vector2(2f, -2f);
        }

        private static void SetButtonColors(Button btn, Color normal, Color highlighted)
        {
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); // слегка светлее
            colors.pressedColor     = new Color(0.80f, 0.80f, 0.80f, 1f); // слегка темнее
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;
        }

        private static Color Lighten(Color c, float amount)
            => new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);

        // ── Внутренний класс для хранения данных кнопки-флага ────────────────
        private class FlagBtn
        {
            public string lang;
            public GameObject root;
            public Image card;
            public Outline cardOutline;
            public Image accent;
            public Text  codeText;
        }
    }
}
