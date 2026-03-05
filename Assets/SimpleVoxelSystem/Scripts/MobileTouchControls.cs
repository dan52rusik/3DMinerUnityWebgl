using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class MobileTouchControls : MonoBehaviour
    {
        public static MobileTouchControls Instance { get; private set; }

        [Header("Activation")]
        public bool autoEnableOnMobile = true;
        public bool forceEnableInEditor = false;
        public bool autoForceEnableInEditor = true;

        [Header("Tuning")]
        [Range(0.02f, 0.5f)] public float lookScale = 0.12f;
        [Range(40f, 160f)] public float joystickRadius = 72f;

        public bool IsActive { get; private set; }
        public Vector2 MoveVector { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public float ZoomDelta { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool MinePressedThisFrame { get; private set; }
        public bool LookTapPressedThisFrame { get; private set; }
        public bool InteractPressedThisFrame { get; private set; }
        public bool MinionMenuPressedThisFrame { get; private set; }
        public bool MenuPressedThisFrame => MinionMenuPressedThisFrame;
        public bool RemovePressedThisFrame { get; private set; }
        public bool RunHeld { get; private set; }
        public bool RemoveHeld { get; private set; }
        public bool IsLookHeld { get; private set; }
        public Vector2 AimScreenPosition { get; private set; }
        /// <summary>
        /// Last touch position on the look-pad. Unlike AimScreenPosition this does NOT
        /// reset to screen-center when the finger lifts — it "sticks" at the last tap.
        /// Used by mine placement so the preview stays where the player last tapped
        /// while they reach for the PLACE button.
        /// </summary>
        public Vector2 StickyAimPosition { get; private set; }
        /// <summary>True on the frame the PLACE button was tapped (mine placement).</summary>
        public bool PlaceMinePressedThisFrame { get; private set; }
        /// <summary>Screen position of the PLACE tap — used for placement raycast.</summary>
        public Vector2 PlaceMineTouchScreenPos { get; private set; }
        public RectTransform MoveAreaRect => joystick != null ? joystick.GetComponent<RectTransform>() : null;
        public RectTransform MineButtonRect => mineButton != null ? mineButton.GetComponent<RectTransform>() : null;
        public RectTransform JumpButtonRect => jumpButton != null ? jumpButton.GetComponent<RectTransform>() : null;
        public RectTransform InteractButtonRect => interactButton != null ? interactButton.GetComponent<RectTransform>() : null;
        public RectTransform RunButtonRect => runButton != null ? runButton.GetComponent<RectTransform>() : null;
        public RectTransform MinionMenuButtonRect => minionMenuButton != null ? minionMenuButton.GetComponent<RectTransform>() : null;

        private TouchJoystick joystick;
        private TouchLookPad lookPad;
        private TouchHoldButton runButton;
        private TouchHoldButton zoomInButton;
        private TouchHoldButton zoomOutButton;
        private TouchTapButton jumpButton;
        private TouchTapButton mineButton;
        private TouchTapButton interactButton;
        private TouchTapButton minionMenuButton;
        private TouchHoldButton removeButton;
        private TouchTapButton placeMineButton;   // shown only during mine placement
        private Text interactButtonLabel;
        private float actionScale = 1f;

        // Локализация — ссылки на Text кнопок для обновления при смене языка
        private Text mineButtonLabel;
        private Text jumpButtonLabel;
        private Text runButtonLabel;
        private Text minionMenuButtonLabel;
        private Text placeMineButtonLabel;
        private Text removeButtonLabel;

        private const string DefaultInteractLabel = "ACT"; // fallback если Loc не инициализирован
        private static string LocalizedActLabel => Loc.T("btn_act");
        private bool interactHintRequested;
        private string interactHintText = DefaultInteractLabel;
        private int interactHintPriority = int.MinValue;
        private bool interactHintVisibleRequested = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int SVS_IsMobileBrowser();
#endif

        public static MobileTouchControls GetOrCreateIfNeeded()
        {
            if (Instance != null)
                return Instance.IsActive ? Instance : null;

            var existing = FindFirstObjectByType<MobileTouchControls>(FindObjectsInactive.Include);
            if (existing != null)
                return existing.IsActive ? existing : null;

            GameObject go = new GameObject("MobileTouchControls");
            DontDestroyOnLoad(go);
            var controls = go.AddComponent<MobileTouchControls>();
            if (!controls.IsActive)
            {
                Destroy(go);
                return null;
            }

            return controls;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
            if (autoForceEnableInEditor)
                forceEnableInEditor = true;
#endif

            IsActive = ShouldEnable();
            if (!IsActive)
                return;

            EnsureEventSystem();
            BuildUI();

            // FIX LOC: подписываемся на смену языка
            Loc.OnLanguageChanged += RefreshButtonLabels;

            // Initialize sticky position to center so it's not (0,0) before first touch
            StickyAimPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        void OnDestroy()
        {
            Loc.OnLanguageChanged -= RefreshButtonLabels;
        }

        void Update()
        {
            if (!IsActive)
                return;

            MoveVector = joystick != null ? joystick.Direction : Vector2.zero;
            LookDelta = lookPad != null ? lookPad.ConsumeDelta() * lookScale : Vector2.zero;
            IsLookHeld = lookPad != null && lookPad.IsHeld;

            JumpPressedThisFrame = jumpButton != null && jumpButton.PressedThisFrame;
            MinePressedThisFrame = mineButton != null && mineButton.PressedThisFrame;
            LookTapPressedThisFrame = lookPad != null && lookPad.TappedThisFrame;
            InteractPressedThisFrame = interactButton != null && interactButton.PressedThisFrame;
            MinionMenuPressedThisFrame = minionMenuButton != null && minionMenuButton.PressedThisFrame;
            RemovePressedThisFrame = removeButton != null && removeButton.PressedThisFrame;
            RunHeld = runButton != null && runButton.Held;
            RemoveHeld = removeButton != null && removeButton.Held;

            // PLACE button: record tap + save screen position for placement raycast
            if (placeMineButton != null && placeMineButton.PressedThisFrame)
            {
                PlaceMinePressedThisFrame = true;
                // Use the touch/pointer position captured by the LookPad zone (same right half),
                // or fall back to screen center so the raycast always has a valid origin.
                PlaceMineTouchScreenPos = (lookPad != null && lookPad.HasPointerPosition)
                    ? lookPad.CurrentPointerScreenPos
                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
            else
            {
                PlaceMinePressedThisFrame = false;
            }

            float zoom = 0f;
            if (zoomInButton != null && zoomInButton.Held) zoom += 1f;
            if (zoomOutButton != null && zoomOutButton.Held) zoom -= 1f;
            ZoomDelta = zoom;

            if (lookPad != null && lookPad.HasPointerPosition)
            {
                AimScreenPosition = lookPad.CurrentPointerScreenPos;
                StickyAimPosition = AimScreenPosition; // Update sticky position
            }
            else
            {
                AimScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }

        void LateUpdate()
        {
            if (!IsActive)
                return;

            ApplyInteractHintState();

            jumpButton?.ResetFrameFlags();
            mineButton?.ResetFrameFlags();
            interactButton?.ResetFrameFlags();
            minionMenuButton?.ResetFrameFlags();
            runButton?.ResetFrameFlags();
            removeButton?.ResetFrameFlags();
            placeMineButton?.ResetFrameFlags();
            lookPad?.ResetFrameFlags();
        }

        public void RequestInteractHint(string label, int priority = 0, bool visible = true)
        {
            if (!IsActive)
                return;

            if (!interactHintRequested || priority >= interactHintPriority)
            {
                interactHintText = string.IsNullOrWhiteSpace(label) ? DefaultInteractLabel : label.ToUpperInvariant();
                interactHintPriority = priority;
                interactHintVisibleRequested = visible;
            }

            interactHintRequested = true;
        }

        private bool ShouldEnable()
        {
#if UNITY_EDITOR
            return forceEnableInEditor;
#else
            if (!autoEnableOnMobile)
                return false;

            // WebGL desktop can report touch support; prefer explicit browser-side check.
#if UNITY_WEBGL
            try
            {
                return SVS_IsMobileBrowser() == 1;
            }
            catch
            {
                // fall through to C# heuristics
            }
#endif

            if (Application.isMobilePlatform)
                return true;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && !HasDesktopPointer())
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchSupported && !HasDesktopPointer())
                return true;
#endif
            return false;
#endif
        }

        private static bool HasDesktopPointer()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null || Keyboard.current != null)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.mousePresent)
                return true;
#endif
            return false;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("MobileControlsCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;
            canvas.pixelPerfect = true;
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var safe = CreateRect("Safe", canvasGo.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            actionScale = CalculateSafeScale();

            BuildLeftJoystick(safe);
            BuildRightLookPad(safe);
            BuildButtons(safe);
            SetEditorModeVisible(false);
        }

        private void BuildLeftJoystick(Transform parent)
        {
            RectTransform area = CreateRect("MoveArea", parent, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var areaImage = area.gameObject.AddComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0.01f);

            float bgSize = 180f * actionScale;
            RectTransform bg = CreateRect("MoveBG", area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-bgSize * 0.5f, -bgSize * 0.5f), new Vector2(bgSize * 0.5f, bgSize * 0.5f));
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.2f);
            bg.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.2f);

            float knobSize = 88f * actionScale;
            RectTransform knob = CreateRect("MoveKnob", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-knobSize * 0.5f, -knobSize * 0.5f), new Vector2(knobSize * 0.5f, knobSize * 0.5f));
            var knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.color = new Color(1f, 1f, 1f, 0.5f);
            knob.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.5f);

            joystick = area.gameObject.AddComponent<TouchJoystick>();
            joystick.background = bg;
            joystick.knob = knob;
            joystick.radius = joystickRadius * actionScale;
        }

        private void BuildRightLookPad(Transform parent)
        {
            RectTransform lookArea = CreateRect("LookArea", parent, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            var image = lookArea.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);

            lookPad = lookArea.gameObject.AddComponent<TouchLookPad>();
        }

        private void BuildButtons(Transform parent)
        {
            float m = actionScale;

            // --- ACTION CLUSTER (Bottom Right) ---
            mineButton       = CreateTapButton(parent, "MineButton",    Loc.T("btn_mine"),    new Vector2(1f, 0f), new Vector2(24f * m, 24f * m),  new Vector2(252f * m, 252f * m), new Color(0.95f, 0.45f, 0.2f, 0.72f), Mathf.RoundToInt(30f * m));
            jumpButton       = CreateTapButton(parent, "JumpButton",    Loc.T("btn_jump"),    new Vector2(1f, 0f), new Vector2(340f * m, 154f * m), new Vector2(92f * m, 92f * m),   new Color(0.2f, 0.62f, 0.95f, 0.7f),  Mathf.RoundToInt(18f * m));
            runButton        = CreateHoldButton(parent, "RunButton",    Loc.T("btn_run"),     new Vector2(1f, 0f), new Vector2(340f * m, 54f * m),  new Vector2(92f * m, 92f * m),   new Color(0.2f, 0.82f, 0.42f, 0.68f), Mathf.RoundToInt(16f * m));
            interactButton   = CreateTapButton(parent, "InteractButton",DefaultInteractLabel,  new Vector2(1f, 0f), new Vector2(24f * m, 286f * m), new Vector2(252f * m, 100f * m), new Color(0.98f, 0.78f, 0.18f, 0.7f),  Mathf.RoundToInt(24f * m));
            minionMenuButton = CreateTapButton(parent, "MinionsButton", Loc.T("btn_minions"), new Vector2(1f, 0f), new Vector2(107f * m, 792f * m), new Vector2(88f * m, 88f * m),   new Color(0.22f, 0.34f, 0.56f, 0.68f),Mathf.RoundToInt(14f * m));

            removeButton     = CreateHoldButton(parent, "RemoveButton", Loc.T("btn_del"),   new Vector2(1f, 1f), new Vector2(20f * m, 20f * m),  new Vector2(68f * m, 68f * m),   new Color(0.95f, 0.2f, 0.2f, 0.68f),  Mathf.RoundToInt(16f * m));
            zoomInButton     = CreateHoldButton(parent, "ZoomInButton", "+",                new Vector2(1f, 1f), new Vector2(20f * m, 145f * m), new Vector2(64f * m, 64f * m),   new Color(0.75f, 0.75f, 0.9f, 0.68f), Mathf.RoundToInt(24f * m));
            zoomOutButton    = CreateHoldButton(parent, "ZoomOutButton","-",                new Vector2(1f, 1f), new Vector2(20f * m, 218f * m), new Vector2(64f * m, 64f * m),   new Color(0.75f, 0.75f, 0.9f, 0.68f), Mathf.RoundToInt(24f * m));

            placeMineButton  = CreateTapButton(parent, "PlaceMineButton", Loc.T("btn_place"),
                new Vector2(1f, 0f), new Vector2(24f * m, 430f * m), new Vector2(150f * m, 74f * m), new Color(0.1f, 0.75f, 0.2f, 0.72f), Mathf.RoundToInt(22f * m));
            placeMineButton.gameObject.SetActive(false);

            if (interactButton    != null) interactButtonLabel    = interactButton.GetComponentInChildren<Text>();
            if (mineButton        != null) mineButtonLabel        = mineButton.GetComponentInChildren<Text>();
            if (jumpButton        != null) jumpButtonLabel        = jumpButton.GetComponentInChildren<Text>();
            if (runButton         != null) runButtonLabel         = runButton.GetComponentInChildren<Text>();
            if (minionMenuButton  != null) minionMenuButtonLabel  = minionMenuButton.GetComponentInChildren<Text>();
            if (placeMineButton   != null) placeMineButtonLabel   = placeMineButton.GetComponentInChildren<Text>();
            if (removeButton      != null) removeButtonLabel      = removeButton.GetComponentInChildren<Text>();
        }

        /// <summary>
        /// Shows or hides the PLACE button. Call from MineMarket when entering/leaving
        /// placement mode on the island.
        /// </summary>
        public void SetPlacementModeVisible(bool visible)
        {
            if (!IsActive) return;
            if (placeMineButton != null)
                placeMineButton.gameObject.SetActive(visible);
        }

        /// <summary>Обновить лейблы всех кнопок при смене языка.</summary>
        private void RefreshButtonLabels()
        {
            if (mineButtonLabel       != null) mineButtonLabel.text       = Loc.T("btn_mine");
            if (jumpButtonLabel       != null) jumpButtonLabel.text       = Loc.T("btn_jump");
            if (runButtonLabel        != null) runButtonLabel.text        = Loc.T("btn_run");
            if (minionMenuButtonLabel != null) minionMenuButtonLabel.text = Loc.T("btn_minions");
            if (placeMineButtonLabel  != null) placeMineButtonLabel.text  = Loc.T("btn_place");
            if (removeButtonLabel     != null) removeButtonLabel.text     = Loc.T("btn_del");
        }

        public void SetEditorModeVisible(bool visible)
        {
            if (!IsActive)
                return;

            if (removeButton != null)
                removeButton.gameObject.SetActive(visible);
        }

        private float CalculateSafeScale()
        {
            Rect safe = Screen.safeArea;
            float wRatio = safe.width / Mathf.Max(1f, Screen.width);
            float hRatio = safe.height / Mathf.Max(1f, Screen.height);
            float ratio = Mathf.Min(wRatio, hRatio);
            return Mathf.Clamp(ratio, 0.85f, 1f);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private TouchTapButton CreateTapButton(Transform parent, string name, string text, Vector2 anchor, Vector2 anchoredFromEdge, Vector2 size, Color color, int fontSize)
        {
            Vector2 pos = new Vector2(anchor.x == 1f ? -anchoredFromEdge.x : anchoredFromEdge.x, anchor.y == 1f ? -anchoredFromEdge.y : anchoredFromEdge.y);
            GameObject go = RuntimeUIFactory.MakePanel(name, parent, anchor, anchor, pos, size, color);
            RuntimeUIFactory.MakeLabel(go.transform, "Label", text, fontSize, TextAnchor.MiddleCenter, color: Color.white);
            
            // Premium look
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);
            
            return go.AddComponent<TouchTapButton>();
        }

        private TouchHoldButton CreateHoldButton(Transform parent, string name, string text, Vector2 anchor, Vector2 anchoredFromEdge, Vector2 size, Color color, int fontSize)
        {
            Vector2 pos = new Vector2(anchor.x == 1f ? -anchoredFromEdge.x : anchoredFromEdge.x, anchor.y == 1f ? -anchoredFromEdge.y : anchoredFromEdge.y);
            GameObject go = RuntimeUIFactory.MakePanel(name, parent, anchor, anchor, pos, size, color);
            RuntimeUIFactory.MakeLabel(go.transform, "Label", text, fontSize, TextAnchor.MiddleCenter, color: Color.white);
            
            // Premium look
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);

            return go.AddComponent<TouchHoldButton>();
        }

        private void ApplyInteractHintState()
        {
            if (interactButton != null)
                interactButton.gameObject.SetActive(interactHintRequested ? interactHintVisibleRequested : true);

            // Используем переводимый лейбл ACT
            string actLabel = LocalizedActLabel;
            if (interactButtonLabel != null)
                interactButtonLabel.text = interactHintRequested ? interactHintText : actLabel;

            interactHintRequested = false;
            interactHintText = actLabel;
            interactHintPriority = int.MinValue;
            interactHintVisibleRequested = true;
        }

        // (Helper methods removed, now using RuntimeUIFactory)

        private class TouchTapButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool PressedThisFrame { get; private set; }
            private Image image;
            private float idleAlpha = 0.7f;

            void Awake()
            {
                image = GetComponent<Image>();
                if (image != null)
                    idleAlpha = image.color.a;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                PressedThisFrame = true;
                SetAlpha(1f);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                SetAlpha(idleAlpha);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                SetAlpha(idleAlpha);
            }

            void OnDisable()
            {
                SetAlpha(idleAlpha);
            }

            public void ResetFrameFlags()
            {
                PressedThisFrame = false;
            }

            private void SetAlpha(float a)
            {
                if (image == null)
                    return;
                Color c = image.color;
                c.a = a;
                image.color = c;
            }
        }

        private class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool Held { get; private set; }
            public bool PressedThisFrame { get; private set; }
            private Image image;
            private float idleAlpha = 0.7f;

            void Awake()
            {
                image = GetComponent<Image>();
                if (image != null)
                    idleAlpha = image.color.a;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                Held = true;
                PressedThisFrame = true;
                SetAlpha(1f);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                Held = false;
                SetAlpha(idleAlpha);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Held = false;
                SetAlpha(idleAlpha);
            }

            void OnDisable()
            {
                Held = false;
                SetAlpha(idleAlpha);
            }

            public void ResetFrameFlags()
            {
                PressedThisFrame = false;
            }

            private void SetAlpha(float a)
            {
                if (image == null)
                    return;
                Color c = image.color;
                c.a = a;
                image.color = c;
            }
        }

        private class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public RectTransform background;
            public RectTransform knob;
            public float radius = 72f;
            public Vector2 Direction { get; private set; }
            private RectTransform area;
            private Vector2 centerLocal;
            private Image backgroundImage;
            private Image knobImage;
            private float bgIdleAlpha;
            private float knobIdleAlpha;

            void Awake()
            {
                area = GetComponent<RectTransform>();
                if (background != null)
                {
                    backgroundImage = background.GetComponent<Image>();
                    if (backgroundImage != null)
                        bgIdleAlpha = backgroundImage.color.a;
                }

                if (knob != null)
                {
                    knobImage = knob.GetComponent<Image>();
                    if (knobImage != null)
                        knobIdleAlpha = knobImage.color.a;
                }

                SetVisualActive(false);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                UpdateCenter(eventData);
                SetVisualActive(true);
                UpdateFromPointer(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                UpdateFromPointer(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                Direction = Vector2.zero;
                if (knob != null)
                    knob.anchoredPosition = Vector2.zero;
                SetVisualActive(false);
            }

            private void UpdateFromPointer(PointerEventData eventData)
            {
                if (background == null || area == null)
                    return;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
                Vector2 delta = localPoint - centerLocal;
                Vector2 clamped = Vector2.ClampMagnitude(delta, radius);
                Direction = clamped / Mathf.Max(radius, 0.001f);
                if (knob != null)
                    knob.anchoredPosition = clamped;
            }

            private void UpdateCenter(PointerEventData eventData)
            {
                if (background == null || area == null)
                    return;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out centerLocal);
                background.anchoredPosition = centerLocal;
                if (knob != null)
                    knob.anchoredPosition = Vector2.zero;
            }

            private void SetVisualActive(bool active)
            {
                if (backgroundImage != null)
                {
                    Color c = backgroundImage.color;
                    c.a = active ? 1f : bgIdleAlpha;
                    backgroundImage.color = c;
                }

                if (knobImage != null)
                {
                    Color c = knobImage.color;
                    c.a = active ? 1f : knobIdleAlpha;
                    knobImage.color = c;
                }

                if (background != null)
                    background.gameObject.SetActive(active);
            }
        }

        private class TouchLookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public bool IsHeld { get; private set; }
            public Vector2 CurrentPointerScreenPos { get; private set; }
            public bool TappedThisFrame { get; private set; }
            public bool HasPointerPosition { get; private set; }

            private Vector2 lastScreenPos;
            private Vector2 accumulatedDelta;

            public void OnPointerDown(PointerEventData eventData)
            {
                IsHeld = true;
                lastScreenPos = eventData.position;
                CurrentPointerScreenPos = eventData.position;
                HasPointerPosition = true;
                TappedThisFrame = true;
            }

            public void OnDrag(PointerEventData eventData)
            {
                Vector2 current = eventData.position;
                accumulatedDelta += current - lastScreenPos;
                lastScreenPos = current;
                CurrentPointerScreenPos = current;
                HasPointerPosition = true;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                IsHeld = false;
                lastScreenPos = Vector2.zero;
            }

            public Vector2 ConsumeDelta()
            {
                Vector2 delta = accumulatedDelta;
                accumulatedDelta = Vector2.zero;
                return delta;
            }

            public void ResetFrameFlags()
            {
                TappedThisFrame = false;
            }
        }

        [DisallowMultipleComponent]
        private sealed class SafeAreaFitter : MonoBehaviour
        {
            private RectTransform rect;
            private Rect lastSafeArea;
            private Vector2Int lastScreen;

            void Awake()
            {
                rect = GetComponent<RectTransform>();
                ApplySafeArea();
            }

            void OnEnable()
            {
                ApplySafeArea();
            }

            void Update()
            {
                if (rect == null)
                    rect = GetComponent<RectTransform>();
                if (rect == null)
                    return;

                Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
                Rect safe = Screen.safeArea;
                if (safe != lastSafeArea || screen != lastScreen)
                    ApplySafeArea();
            }

            private void ApplySafeArea()
            {
                if (rect == null)
                    rect = GetComponent<RectTransform>();
                if (rect == null)
                    return;

                Rect safe = Screen.safeArea;
                float minX = safe.xMin / Mathf.Max(1f, Screen.width);
                float minY = safe.yMin / Mathf.Max(1f, Screen.height);
                float maxX = safe.xMax / Mathf.Max(1f, Screen.width);
                float maxY = safe.yMax / Mathf.Max(1f, Screen.height);

                rect.anchorMin = new Vector2(minX, minY);
                rect.anchorMax = new Vector2(maxX, maxY);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                lastSafeArea = safe;
                lastScreen = new Vector2Int(Screen.width, Screen.height);
            }
        }
    }
}
