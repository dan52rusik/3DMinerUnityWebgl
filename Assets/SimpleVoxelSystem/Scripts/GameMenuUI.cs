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

        // ── Runtime ───────────────────────────────────────────────────────────
        private Canvas    _canvas;
        private Image     _backdrop;
        private RectTransform _panel;
        private Button    _menuBtn;
        private bool      _isOpen;
        private Coroutine _anim;

        // Flag button references для обновления выделения
        private FlagBtn[] _flagBtns;
        private Text      _langLabel; // ссылка на "Язык:" для обновления при смене языка

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
            Loc.Initialize();
            BuildUI();
        }

        private void OnEnable()
        {
            Loc.OnLanguageChanged += OnLangChanged;
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= OnLangChanged;
        }

        private void OnLangChanged()
        {
            RefreshFlagButtons();
            RefreshMenuBtnLabel();
            // Обновить заголовок секции языка
            if (_langLabel != null)
                _langLabel.text = Loc.T("language") + ":";
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
            _canvas.sortingOrder = 3100; // выше MobileControlsCanvas (3000) — наша кнопка перехватывает тач первой

            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
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

            var mbLabel = CreateText(menuBtnGo.transform, "Label", "☰  " + Loc.T("settings"));
            var lrt = mbLabel.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            mbLabel.alignment = TextAnchor.MiddleCenter;
            mbLabel.color = menuBtnTextColor;
            mbLabel.fontSize = 17;
            mbLabel.fontStyle = FontStyle.Bold;

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
            // Заголовок секции — сохраняем ссылку для обновления при смене языка
            _langLabel = CreateText(parent, "LangLabel", Loc.T("language") + ":");
            _langLabel.fontSize = 14;
            _langLabel.fontStyle = FontStyle.Bold;
            _langLabel.color = new Color(0.6f, 0.65f, 0.8f);
            _langLabel.alignment = TextAnchor.MiddleLeft;
            var le = _langLabel.GetComponent<LayoutElement>();
            le.preferredHeight = 20f;
            le.minHeight = 20f;

            // Ряд с флагами
            var row = CreateHorizontalGroup(parent, "FlagRow", 10f);
            var rlg = row.GetComponent<HorizontalLayoutGroup>();
            rlg.childAlignment = TextAnchor.MiddleLeft;
            rlg.padding = new RectOffset(0, 0, 0, 0);
            var rle = row.GetComponent<LayoutElement>();
            rle.preferredHeight = 60f;
            rle.minHeight = 60f;

            _flagBtns = new FlagBtn[3];
            _flagBtns[0] = BuildFlagBtn(row.transform, Loc.LangRu);
            _flagBtns[1] = BuildFlagBtn(row.transform, Loc.LangEn);
            _flagBtns[2] = BuildFlagBtn(row.transform, Loc.LangTr);

            RefreshFlagButtons();
        }

        // ── Одна кнопка-флаг ────────────────────────────────────────────────
        private FlagBtn BuildFlagBtn(Transform parent, string lang)
        {
            var go = new GameObject($"Flag_{lang.ToUpper()}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 64f;
            le.preferredHeight = 56f;
            le.minWidth = 64f;

            var container = go.AddComponent<Image>();
            container.color = new Color(0, 0, 0, 0); // прозрачный контейнер

            // Строим флаг из полос
            BuildFlagStripes(go.transform, lang, 64f, 42f);

            // Текст кода страны под флагом
            var code = CreateText(go.transform, "Code", lang.ToUpper());
            code.fontSize = 13;
            code.fontStyle = FontStyle.Bold;
            code.color = Color.white;
            code.alignment = TextAnchor.MiddleCenter;
            var crt = code.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot     = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 1f);
            crt.sizeDelta = new Vector2(0f, 16f);

            // Обводка (выделение выбранного)
            var border = new GameObject("Border");
            border.transform.SetParent(go.transform, false);
            var bImg = border.AddComponent<Image>();
            bImg.color = new Color(0.3f, 0.9f, 0.5f, 0f); // скрыта изначально
            var brt = border.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-3f, 13f); // по высоте флага
            brt.offsetMax = new Vector2(3f, 3f);
            border.AddComponent<Outline>().effectColor = new Color(0.3f, 0.95f, 0.5f, 0f);

            // Кнопка
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var captureLang = lang;
            btn.onClick.AddListener(() =>
            {
                Loc.SetLanguage(captureLang);
                CloseMenu();
            });

            return new FlagBtn { root = go, border = bImg, codeText = code, lang = lang };
        }

        // ── Строит полосы флага ──────────────────────────────────────────────
        private void BuildFlagStripes(Transform parent, string lang, float w, float h)
        {
            Color[] stripes;
            bool hasCircle = false;
            switch (lang)
            {
                case Loc.LangRu: // Белый, Синий, Красный
                    stripes = new[] {
                        new Color(1.00f, 1.00f, 1.00f),
                        new Color(0.00f, 0.22f, 0.66f),
                        new Color(0.80f, 0.08f, 0.08f)
                    };
                    break;
                case Loc.LangEn: // Синий, Красный, Белый (упрощённый Union Jack)
                    stripes = new[] {
                        new Color(0.00f, 0.14f, 0.56f),
                        new Color(0.80f, 0.08f, 0.08f),
                        new Color(1.00f, 1.00f, 1.00f)
                    };
                    break;
                case Loc.LangTr: // Красный, Красный, Красный + белый текст полумесяц
                    stripes = new[] {
                        new Color(0.84f, 0.09f, 0.09f),
                        new Color(0.84f, 0.09f, 0.09f),
                        new Color(0.84f, 0.09f, 0.09f)
                    };
                    hasCircle = true;
                    break;
                default:
                    stripes = new[] { Color.grey, Color.grey, Color.grey };
                    break;
            }

            float stripeH = h / 3f;
            for (int i = 0; i < 3; i++)
            {
                var sg = new GameObject($"S{i}");
                sg.transform.SetParent(parent, false);
                var sImg = sg.AddComponent<Image>();
                sImg.color = stripes[i];
                var srt = sg.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0f, 1f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot     = new Vector2(0.5f, 1f);
                srt.anchoredPosition = new Vector2(0f, -(stripeH * i));
                srt.sizeDelta = new Vector2(0f, stripeH);
            }

            // Полумесяц для Турции
            if (hasCircle)
            {
                var ct = CreateText(parent, "Crescent", "☽★");
                ct.fontSize = 18;
                ct.color = Color.white;
                ct.alignment = TextAnchor.MiddleCenter;
                var crt = ct.GetComponent<RectTransform>();
                crt.anchorMin = Vector2.zero;
                crt.anchorMax = new Vector2(1f, 1f);
                crt.offsetMin = new Vector2(0f, 14f);
                crt.offsetMax = Vector2.zero;
            }

            // Крест для UK — белые горизонтальная и вертикальная полосы
            if (lang == Loc.LangEn)
            {
                // Вертикальная полоса
                var v = new GameObject("VCross");
                v.transform.SetParent(parent, false);
                var vi = v.AddComponent<Image>();
                vi.color = Color.white;
                var vrt = v.GetComponent<RectTransform>();
                vrt.anchorMin = new Vector2(0.5f, 0f);
                vrt.anchorMax = new Vector2(0.5f, 1f);
                vrt.offsetMin = new Vector2(-4f, 14f);
                vrt.offsetMax = new Vector2(4f, 0f);

                // Горизонтальная полоса
                var hz = new GameObject("HCross");
                hz.transform.SetParent(parent, false);
                var hi = hz.AddComponent<Image>();
                hi.color = Color.white;
                var hrt = hz.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 0.5f);
                hrt.anchorMax = new Vector2(1f, 0.5f);
                hrt.offsetMin = new Vector2(0f, 14f + h / 2f - 4f - h / 2f);
                hrt.offsetMax = new Vector2(0f, 14f + h / 2f + 4f - h / 2f);
                // упрощаем
                hrt.anchorMin = new Vector2(0f, 0f);
                hrt.anchorMax = new Vector2(1f, 0f);
                hrt.offsetMin = new Vector2(0f, 14f + h / 2f - 4f);
                hrt.offsetMax = new Vector2(0f, 14f + h / 2f + 4f);
            }
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

                // Обводка
                if (fb.border != null)
                    fb.border.color = selected
                        ? new Color(0.25f, 0.92f, 0.48f, 0.9f)
                        : new Color(0f, 0f, 0f, 0f);

                // Текст кода
                if (fb.codeText != null)
                    fb.codeText.color = selected
                        ? new Color(0.3f, 1f, 0.6f)
                        : Color.white;
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
            public Image border;
            public Text  codeText;
        }
    }
}
