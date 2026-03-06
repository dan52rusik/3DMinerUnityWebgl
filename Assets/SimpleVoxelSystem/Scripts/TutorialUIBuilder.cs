// TutorialUIBuilder.cs â€” constructs all tutorial UI elements in code (no prefabs needed).
// Called once from OnboardingTutorial.Start via BuildUI().

using UnityEngine;
using UnityEngine.UI;

namespace SimpleVoxelSystem
{
    // â”€â”€â”€ Value type that bundles every UI reference built by the builder â”€â”€â”€â”€â”€â”€â”€â”€
    internal struct TutorialUIRefs
    {
        public Canvas         Canvas;
        public RectTransform  CanvasRect;

        // Spotlight dimmer â€” 4 panels that surround the highlight hole
        // [0]=Top  [1]=Bottom  [2]=Left  [3]=Right
        public Image[]         DimPanels;
        public RectTransform[] DimRects;

        // Info card
        public GameObject     Card;
        public Text           CardTitle;
        public Text           CardBody;
        public Text           CardTapHint;

        // Highlight box
        public GameObject     HlBox;
        public RectTransform  HlRect;

        // Animated arrow
        public Text           ArrowLabel;

        // World-space beam
        public LineRenderer   Beam;
    }

    // â”€â”€â”€ Builder â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    internal static class TutorialUIBuilder
    {
        /// <summary>
        /// Creates the entire tutorial overlay hierarchy and returns references to
        /// every interactive element. <paramref name="owner"/> is the tutorial
        /// MonoBehaviour's transform (used as parent for world-space objects).
        /// </summary>
        public static TutorialUIRefs Build(Transform owner)
        {
            var refs = new TutorialUIRefs();

            // â”€â”€ Canvas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var cGo = new GameObject("OnboardingCanvas");
            cGo.transform.SetParent(owner, false);

            refs.Canvas = cGo.AddComponent<Canvas>();
            refs.Canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            refs.Canvas.sortingOrder = 7000;
            refs.Canvas.pixelPerfect = true;
            cGo.AddComponent<GraphicRaycaster>();

            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight  = 0.5f;

            refs.CanvasRect = refs.Canvas.GetComponent<RectTransform>();

            // â”€â”€ Spotlight dimmer â€” 4 surrounding panels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Together they create a transparent "hole" over the highlighted UI
            // element so its text/icon remains perfectly readable.
            refs.DimPanels = new Image[4];
            refs.DimRects  = new RectTransform[4];
            string[] dimNames = { "Dim_Top", "Dim_Bottom", "Dim_Left", "Dim_Right" };
            for (int i = 0; i < 4; i++)
            {
                var dGo = new GameObject(dimNames[i]);
                dGo.transform.SetParent(cGo.transform, false);
                var dRt = dGo.AddComponent<RectTransform>();
                // Start as full-screen top half â€” will be repositioned on first frame
                dRt.anchorMin = Vector2.zero;
                dRt.anchorMax = Vector2.one;
                dRt.offsetMin = Vector2.zero;
                dRt.offsetMax = Vector2.zero;
                var dImg = dGo.AddComponent<Image>();
                dImg.color = new Color(0, 0, 0, 0);
                dImg.raycastTarget = false;
                refs.DimPanels[i] = dImg;
                refs.DimRects[i]  = dRt;
            }

            // â”€â”€ Info card (screen-centre) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            refs.Card = MakePanel("TutCard", cGo.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 220f), new Vector2(880f, 210f),
                new Color(0.04f, 0.06f, 0.12f, 0.95f));
            var cardRt = refs.Card.GetComponent<RectTransform>();
            cardRt.pivot = new Vector2(0.5f, 0.5f);         // centre pivot â†’ card is centred
            refs.Card.GetComponent<Image>().raycastTarget = false;

            var outline = refs.Card.AddComponent<Outline>();
            outline.effectColor    = new Color(0.35f, 0.55f, 1f, 0.45f);
            outline.effectDistance = new Vector2(2f, 2f);

            refs.CardTitle = MakeLabel(refs.Card.transform, "Title",
                "", 22, TextAnchor.LowerRight,
                new Vector2(0, 8), new Vector2(-16, 8), bold: true,
                color: new Color(0.55f, 0.72f, 1f, 0.85f));

            refs.CardBody = MakeLabel(refs.Card.transform, "Body",
                "", 20, TextAnchor.UpperLeft,
                new Vector2(28, -12), new Vector2(-28, -26),
                color: new Color(0.88f, 0.93f, 1f));

            refs.CardTapHint = MakeLabel(refs.Card.transform, "TapHint",
                "", 17, TextAnchor.LowerRight,
                new Vector2(28, 8), new Vector2(-28, 8),
                color: new Color(1f, 0.88f, 0.35f));
            refs.CardTapHint.gameObject.SetActive(false);

            refs.CardTitle.raycastTarget   = false;
            refs.CardBody.raycastTarget    = false;
            refs.CardTapHint.raycastTarget = false;

            // â”€â”€ Highlight box â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            refs.HlBox = MakePanel("Highlight", cGo.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(120, 60),
                new Color(0f, 0f, 0f, 0f));   // fully transparent â€” only Outline border visible
            refs.HlRect = refs.HlBox.GetComponent<RectTransform>();
            refs.HlBox.GetComponent<Image>().raycastTarget = false;

            var hlOutline = refs.HlBox.AddComponent<Outline>();
            hlOutline.effectColor    = new Color(1f, 0.88f, 0.05f, 1f);
            hlOutline.effectDistance = new Vector2(3f, 3f);
            refs.HlBox.SetActive(false);

            // â”€â”€ Arrow label â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(cGo.transform, false);
            var arrowRt = arrowGo.AddComponent<RectTransform>();
            arrowRt.sizeDelta        = new Vector2(80, 36);
            arrowRt.anchorMin        = arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
            refs.ArrowLabel          = arrowGo.AddComponent<Text>();
            refs.ArrowLabel.text     = "â–¼";
            refs.ArrowLabel.fontSize = 32;
            refs.ArrowLabel.alignment    = TextAnchor.MiddleCenter;
            refs.ArrowLabel.color        = new Color(1f, 0.9f, 0.15f, 1f);
            refs.ArrowLabel.font         = TutorialFontProvider.GetSafeFont();
            refs.ArrowLabel.raycastTarget = false;
            arrowGo.SetActive(false);

            // â”€â”€ World-space beam â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var beamGo = new GameObject("TutBeam");
            beamGo.transform.SetParent(owner, false);
            refs.Beam = beamGo.AddComponent<LineRenderer>();
            refs.Beam.positionCount = 2;
            refs.Beam.startWidth    = 0.07f;
            refs.Beam.endWidth      = 0.07f;
            refs.Beam.useWorldSpace = true;
            refs.Beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            refs.Beam.receiveShadows    = false;
            Shader sh     = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            var beamMat   = new Material(sh);
            beamMat.color = new Color(1f, 0.88f, 0.1f, 0.92f);
            refs.Beam.material = beamMat;
            refs.Beam.enabled  = false;

            refs.Canvas.enabled = false; // hidden until first step
            return refs;
        }

        // â”€â”€ Internal UI factory helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static GameObject MakePanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size,
            Color color, bool stretch = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            if (stretch)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta        = size;
            }
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static Text MakeLabel(Transform parent, string name,
            string text, int fontSize, TextAnchor anchor,
            Vector2 offsetMin, Vector2 offsetMax,
            bool bold = false, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.AddComponent<Text>();
            t.text      = text;
            t.fontSize  = fontSize;
            t.alignment = anchor;
            t.color     = color ?? Color.white;
            t.font      = TutorialFontProvider.GetSafeFont();
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }
    }
}
