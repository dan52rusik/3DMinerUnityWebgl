using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Centralized UI factory for creating procedural UI elements at runtime.
    /// This ensures visual consistency across all menus and HUDs.
    /// </summary>
    public static class RuntimeUIFactory
    {
        private static Font _cachedFont;
        public static Font GetStandardFont() => _cachedFont ??= RuntimeUiFont.Get();

        // Default UI Colors
        public static readonly Color PanelBG = new Color(0.08f, 0.08f, 0.12f, 0.94f);
        public static readonly Color ButtonDefault = new Color(0.25f, 0.45f, 0.85f, 1f);
        public static readonly Color TextDefault = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color TextLabel = new Color(0.8f, 0.8f, 0.8f, 1f);
        public static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.12f);

        /// <summary>Creates a basic UI Panel (Image component with RectTransform).</summary>
        public static GameObject MakePanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = color ?? PanelBG;
            return go;
        }

        /// <summary>Creates a UI Panel that stretches horizontally.</summary>
        public static GameObject MakePanelStretchX(string name, Transform parent, float topY, float height, float paddingSide = 10f, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f); 
            rt.offsetMin = new Vector2(paddingSide, 0f);
            rt.offsetMax = new Vector2(-paddingSide, 0f);
            rt.anchoredPosition = new Vector2(0, topY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            go.AddComponent<Image>().color = color ?? PanelBG;
            return go;
        }

        /// <summary>Creates a standard Button with any anchor/pivot.</summary>
        public static Button MakeBtn(Transform parent, string name, string label, Color? color = null, Vector2? anchor = null, Vector2? pivot = null, Vector2? pos = null, Vector2? size = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            rt.anchorMin = a; rt.anchorMax = a;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos ?? Vector2.zero;
            rt.sizeDelta = size ?? new Vector2(160f, 40f);

            var img = go.AddComponent<Image>(); img.color = color ?? ButtonDefault;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;

            var tGo = new GameObject("Label");
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            
            var txt = tGo.AddComponent<Text>();
            txt.font = GetStandardFont(); txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white; txt.text = label;
            
            return btn;
        }

        /// <summary>Creates a text label using stretch anchors (0,0 to 1,1) and pixel offsets.</summary>
        public static Text MakeLabel(Transform parent, string name, string text, int fontSize = 13, TextAnchor align = TextAnchor.MiddleLeft, Vector2? offsetMin = null, Vector2? offsetMax = null, bool bold = false, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero; 
            rt.offsetMax = offsetMax ?? Vector2.zero;
            
            var txt = go.AddComponent<Text>();
            txt.font = GetStandardFont(); txt.fontSize = fontSize;
            txt.alignment = align; 
            txt.color = color ?? TextDefault;
            txt.text = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        /// <summary>Creates a text label with specific anchors.</summary>
        public static Text MakeLabelAnchored(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, int fontSize = 13, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            
            var txt = go.AddComponent<Text>();
            txt.font = GetStandardFont(); txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color ?? TextDefault;
            txt.text = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        /// <summary>
        /// Creates a text label with fixed position/size (non-stretch).
        /// Use this when you need explicit placement instead of offset-based stretch labels.
        /// </summary>
        public static Text MakeLabelFixed(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize = 13, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = GetStandardFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color ?? TextDefault;
            txt.text = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        /// <summary>Creates an InputField with a descriptive label above it.</summary>
        public static InputField MakeInputField(Transform parent, string name, string label, ref float offsetY, float width = 280f)
        {
            // Title Label
            var lGo = new GameObject(name + "_Label");
            lGo.transform.SetParent(parent, false);
            var lrt = lGo.AddComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f); lrt.anchoredPosition = new Vector2(0f, offsetY);
            lrt.sizeDelta = new Vector2(width, 22f);
            var lt = lGo.AddComponent<Text>();
            lt.font = GetStandardFont(); lt.fontSize = 12;
            lt.alignment = TextAnchor.MiddleLeft; lt.color = TextLabel;
            lt.text = label;

            offsetY -= 26f;
            
            // Input Box
            var iGo = new GameObject(name);
            iGo.transform.SetParent(parent, false);
            var irt = iGo.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f); irt.anchoredPosition = new Vector2(0f, offsetY);
            irt.sizeDelta = new Vector2(width, 32f);
            var bg = iGo.AddComponent<Image>(); bg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(iGo.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 2); trt.offsetMax = new Vector2(-6, -2);
            var txt = tGo.AddComponent<Text>();
            txt.font = GetStandardFont(); txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleLeft; txt.color = Color.white;
            txt.supportRichText = false;

            var field = iGo.AddComponent<InputField>();
            field.textComponent = txt; field.contentType = InputField.ContentType.IntegerNumber;
            field.targetGraphic = bg;

            offsetY -= 36f;
            return field;
        }

        /// <summary>Creates a 1-pixel horizontal separator line.</summary>
        public static void MakeSeparator(Transform parent, float topOffset, float paddingSide = 10f)
        {
            var go = new GameObject("Separator");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(paddingSide, 0f);
            rt.offsetMax = new Vector2(-paddingSide, 0f);
            rt.anchoredPosition = new Vector2(0f, topOffset);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1f);
            go.AddComponent<Image>().color = SeparatorColor;
        }

        /// <summary>
        /// Creates a container with vertical layout and dynamic height.
        /// Returned transform is the direct parent for list entries.
        /// </summary>
        public static Transform MakeScrollContainer(Transform parent, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            GameObject go = new GameObject("ScrollContainer");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? new Vector2(10f, 10f);
            rt.offsetMax = offsetMax ?? new Vector2(-10f, -70f);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(0, 0, 4, 4);

            ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            return go.transform;
        }

        /// <summary>
        /// Ensures centered panel stays inside screen bounds by shrinking its scale on smaller screens.
        /// Works best for fixed-size modal panels.
        /// </summary>
        public static void EnableAdaptivePanelScale(GameObject panel, float maxWidthPercent = 0.92f, float maxHeightPercent = 0.88f, float minScale = 0.55f)
        {
            if (panel == null)
                return;

            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt == null)
                return;

            RuntimeUIPanelAutoScale scaler = panel.GetComponent<RuntimeUIPanelAutoScale>();
            if (scaler == null)
                scaler = panel.AddComponent<RuntimeUIPanelAutoScale>();

            scaler.maxWidthPercent = Mathf.Clamp(maxWidthPercent, 0.4f, 1f);
            scaler.maxHeightPercent = Mathf.Clamp(maxHeightPercent, 0.4f, 1f);
            scaler.minScale = Mathf.Clamp(minScale, 0.25f, 1f);
            scaler.RefreshNow();
        }
    }

    [DisallowMultipleComponent]
    public sealed class RuntimeUIPanelAutoScale : MonoBehaviour
    {
        [Range(0.4f, 1f)] public float maxWidthPercent = 0.92f;
        [Range(0.4f, 1f)] public float maxHeightPercent = 0.88f;
        [Range(0.25f, 1f)] public float minScale = 0.55f;

        private RectTransform rt;
        private Vector2 lastScreenSize;
        private Vector2 lastPanelSize;

        void Awake()
        {
            rt = GetComponent<RectTransform>();
            RefreshNow();
        }

        void OnEnable()
        {
            RefreshNow();
        }

        void Update()
        {
            if (rt == null)
                rt = GetComponent<RectTransform>();
            if (rt == null)
                return;

            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 panelSize = rt.sizeDelta;
            if (screenSize != lastScreenSize || panelSize != lastPanelSize)
                RefreshNow();
        }

        public void RefreshNow()
        {
            if (rt == null)
                rt = GetComponent<RectTransform>();
            if (rt == null)
                return;

            float panelW = Mathf.Max(1f, rt.sizeDelta.x);
            float panelH = Mathf.Max(1f, rt.sizeDelta.y);
            float allowedW = Mathf.Max(1f, Screen.width * maxWidthPercent);
            float allowedH = Mathf.Max(1f, Screen.height * maxHeightPercent);

            float scale = Mathf.Min(allowedW / panelW, allowedH / panelH, 1f);
            scale = Mathf.Clamp(scale, minScale, 1f);

            rt.localScale = new Vector3(scale, scale, 1f);
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            lastPanelSize = rt.sizeDelta;
        }
    }

    /// <summary>
    /// Helper for loading/creating fonts at runtime.
    /// </summary>
    public static class RuntimeUiFont
    {
        private static Font cached;

        public static Font Get()
        {
            if (cached != null) return cached;

            // Try to load from Resources
            cached = Resources.Load<Font>("Roboto-Regular");
            if (cached != null) return cached;

            // Try to create from System Fonts
            try
            {
                cached = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Roboto", "Noto Sans", "Tahoma", "Verdana" },
                    16
                );
            }
            catch { cached = null; }

            // Fallback to Unity built-in font
            if (cached == null)
                cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return cached;
        }
    }
}
