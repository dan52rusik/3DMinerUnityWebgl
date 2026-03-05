using UnityEngine;
using UnityEngine.UI;
using YG;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// MonoBehaviour — инициализирует Loc при старте игры и слушает YG2.onGetSDKData
    /// чтобы перечитать язык после полной загрузки SDK.
    /// Добавь на любой GameObject сцены (или он создастся сам через Bootstrap).
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizationManager : MonoBehaviour
    {
        [Header("Fallback Language (Editor / non-Yandex builds)")]
        [Tooltip("Язык по умолчанию если YG2.lang недоступен")]
        public string fallbackLanguage = Loc.LangRu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<LocalizationManager>() != null)
                return;

            GameObject go = new GameObject("LocalizationManager");
            DontDestroyOnLoad(go);
            go.AddComponent<LocalizationManager>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Loc.Initialize();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += OnSdkReady;
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= OnSdkReady;
        }

        private void OnSdkReady()
        {
            // SDK загружен — перечитываем язык (если игрок не выбирал вручную)
            Loc.Initialize();
        }
    }


    /// <summary>
    /// Вешается на любой GameObject с компонентом Text.
    /// Автоматически обновляет текст при смене языка.
    ///
    /// В поле locKey пиши ключ из Loc.cs (например "money", "sell", "mine_shop").
    /// Опционально: formatArgs — числовые аргументы для Loc.Tf(key, args).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocText : MonoBehaviour
    {
        [Tooltip("Ключ из Loc.cs (например: money, sell, mine_shop)")]
        public string locKey;

        [Tooltip("Если ключ содержит {0},{1}... — числовые аргументы (через запятую)")]
        public string formatArgs;

        private Text _text;

        private void Awake()
        {
            _text = GetComponent<Text>();
        }

        private void OnEnable()
        {
            Loc.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (_text == null || string.IsNullOrWhiteSpace(locKey))
                return;

            if (!string.IsNullOrWhiteSpace(formatArgs))
            {
                string[] parts = formatArgs.Split(',');
                object[] args = new object[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    args[i] = parts[i].Trim();
                _text.text = Loc.Tf(locKey, args);
            }
            else
            {
                _text.text = Loc.T(locKey);
            }
        }

        /// <summary>Сменить ключ в рантайме и сразу обновить текст.</summary>
        public void SetKey(string key)
        {
            locKey = key;
            Refresh();
        }
    }


    /// <summary>
    /// UI-панель переключения языка.
    /// Создаётся кодом или вешается на Canvas-объект в сцене.
    /// Содержит 3 кнопки: RU / EN / TR.
    /// </summary>
    public class LanguageSwitcherUI : MonoBehaviour
    {
        [Header("Buttons (опционально — если не назначены, создаются авто)")]
        public Button buttonRu;
        public Button buttonEn;
        public Button buttonTr;

        [Header("Визуал selected")]
        public Color selectedColor  = new Color(0.22f, 0.78f, 0.45f);
        public Color normalColor    = new Color(0.18f, 0.18f, 0.25f);
        public Color selectedText   = Color.white;
        public Color normalText     = new Color(0.7f, 0.7f, 0.7f);

        private void OnEnable()
        {
            Loc.OnLanguageChanged += UpdateButtonStates;
            SetupButtons();
            UpdateButtonStates();
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= UpdateButtonStates;
        }

        private void SetupButtons()
        {
            if (buttonRu != null) buttonRu.onClick.AddListener(() => Loc.SetLanguage(Loc.LangRu));
            if (buttonEn != null) buttonEn.onClick.AddListener(() => Loc.SetLanguage(Loc.LangEn));
            if (buttonTr != null) buttonTr.onClick.AddListener(() => Loc.SetLanguage(Loc.LangTr));
        }

        private void UpdateButtonStates()
        {
            ApplyButtonStyle(buttonRu, Loc.LangRu, Loc.T("lang_ru"));
            ApplyButtonStyle(buttonEn, Loc.LangEn, Loc.T("lang_en"));
            ApplyButtonStyle(buttonTr, Loc.LangTr, Loc.T("lang_tr"));
        }

        private void ApplyButtonStyle(Button btn, string lang, string label)
        {
            if (btn == null) return;
            bool isSelected = Loc.CurrentLanguage == lang;

            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = isSelected ? selectedColor : normalColor;

            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
                txt.color = isSelected ? selectedText : normalText;
                txt.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        // ── Статический фабричный метод — создаёт панель с нуля ─────────────
        /// <summary>
        /// Создаёт панель выбора языка внутри canvas и возвращает её.
        /// anchoredPosition — позиция внутри Canvas (по умолчанию правый нижний угол).
        /// </summary>
        public static LanguageSwitcherUI CreateInCanvas(Canvas canvas,
            Vector2? anchoredPosition = null)
        {
            if (canvas == null) return null;

            GameObject panel = new GameObject("LanguageSwitcher");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot     = new Vector2(1f, 0f);
            panelRt.anchoredPosition = anchoredPosition ?? new Vector2(-10f, 60f);
            panelRt.sizeDelta = new Vector2(200f, 44f);

            Image bgImg = panel.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.14f, 0.85f);

            var switcher = panel.AddComponent<LanguageSwitcherUI>();

            string[] langs = { Loc.LangRu, Loc.LangEn, Loc.LangTr };
            Button[] btns  = new Button[3];

            for (int i = 0; i < langs.Length; i++)
            {
                GameObject btnGo = new GameObject($"Btn_{langs[i].ToUpper()}");
                btnGo.transform.SetParent(panel.transform, false);

                RectTransform rt = btnGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-2f, -2f);

                Image img = btnGo.AddComponent<Image>();
                img.color = new Color(0.13f, 0.13f, 0.2f);

                Button btn = btnGo.AddComponent<Button>();
                btns[i] = btn;

                GameObject textGo = new GameObject("Label");
                textGo.transform.SetParent(btnGo.transform, false);
                RectTransform trt = textGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                Text txt = textGo.AddComponent<Text>();
                txt.text      = langs[i].ToUpper();
                txt.alignment = TextAnchor.MiddleCenter;
                txt.fontSize  = 18;
                txt.color     = Color.white;
                txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            switcher.buttonRu = btns[0];
            switcher.buttonEn = btns[1];
            switcher.buttonTr = btns[2];

            return switcher;
        }
    }
}
