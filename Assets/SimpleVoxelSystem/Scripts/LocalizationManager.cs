using UnityEngine;
using UnityEngine.UI;
using YG;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class LocalizationManager : MonoBehaviour
    {
        [Header("Fallback Language (Editor / non-Yandex builds)")]
        [Tooltip("Default language if SDK language is unavailable")]
        public string fallbackLanguage = Loc.LangRu;

        private float _nextPlatformLangPollTime;

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
#if Localization_yg
            YG2.onSwitchLang += OnYgLanguageChanged;
            YG2.onCorrectLang += OnYgLanguageChanged;
#endif
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= OnSdkReady;
#if Localization_yg
            YG2.onSwitchLang -= OnYgLanguageChanged;
            YG2.onCorrectLang -= OnYgLanguageChanged;
#endif
        }

        private void OnSdkReady()
        {
            Loc.Initialize();
        }

#if Localization_yg
        private void OnYgLanguageChanged(string _)
        {
            Loc.RefreshFromPlatformLanguageIfAuto();
        }
#endif

        private void Update()
        {
            if (Time.unscaledTime < _nextPlatformLangPollTime)
                return;

            _nextPlatformLangPollTime = Time.unscaledTime + 0.5f;
            Loc.RefreshFromPlatformLanguageIfAuto();
        }
    }

    [RequireComponent(typeof(Text))]
    public class LocText : MonoBehaviour
    {
        [Tooltip("Key from Loc.cs")]
        public string locKey;

        [Tooltip("Optional comma-separated format args for Loc.Tf")]
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

        public void SetKey(string key)
        {
            locKey = key;
            Refresh();
        }
    }

    public class LanguageSwitcherUI : MonoBehaviour
    {
        [Header("Buttons")]
        public Button buttonRu;
        public Button buttonEn;
        public Button buttonTr;

        [Header("Selected State")]
        public Color selectedColor = new Color(0.22f, 0.78f, 0.45f);
        public Color normalColor = new Color(0.18f, 0.18f, 0.25f);
        public Color selectedText = Color.white;
        public Color normalText = new Color(0.7f, 0.7f, 0.7f);

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
            if (btn == null)
                return;

            bool isSelected = Loc.CurrentLanguage == lang;

            Image img = btn.GetComponent<Image>();
            if (img != null)
                img.color = isSelected ? selectedColor : normalColor;

            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
                txt.color = isSelected ? selectedText : normalText;
                txt.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public static LanguageSwitcherUI CreateInCanvas(Canvas canvas, Vector2? anchoredPosition = null)
        {
            if (canvas == null)
                return null;

            GameObject panel = new GameObject("LanguageSwitcher");
            panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.anchoredPosition = anchoredPosition ?? new Vector2(-10f, 60f);
            panelRt.sizeDelta = new Vector2(200f, 44f);

            Image bgImg = panel.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.14f, 0.85f);

            LanguageSwitcherUI switcher = panel.AddComponent<LanguageSwitcherUI>();

            string[] langs = { Loc.LangRu, Loc.LangEn, Loc.LangTr };
            Button[] btns = new Button[3];

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
                txt.text = langs[i].ToUpper();
                txt.alignment = TextAnchor.MiddleCenter;
                txt.fontSize = 18;
                txt.color = Color.white;
                txt.font = RuntimeUiFont.Get();
            }

            switcher.buttonRu = btns[0];
            switcher.buttonEn = btns[1];
            switcher.buttonTr = btns[2];

            return switcher;
        }
    }
}
