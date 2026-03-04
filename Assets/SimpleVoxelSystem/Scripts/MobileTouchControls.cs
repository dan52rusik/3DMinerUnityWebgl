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
        private Text interactButtonLabel;

        private const string DefaultInteractLabel = "ACT";
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

            IsActive = ShouldEnable();
            if (!IsActive)
                return;

            EnsureEventSystem();
            BuildUI();
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

            float zoom = 0f;
            if (zoomInButton != null && zoomInButton.Held) zoom += 1f;
            if (zoomOutButton != null && zoomOutButton.Held) zoom -= 1f;
            ZoomDelta = zoom;

            if (lookPad != null && lookPad.HasPointerPosition)
                AimScreenPosition = lookPad.CurrentPointerScreenPos;
            else
                AimScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
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
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var safe = CreateRect("Safe", canvasGo.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            BuildLeftJoystick(safe);
            BuildRightLookPad(safe);
            BuildButtons(safe);
        }

        private void BuildLeftJoystick(Transform parent)
        {
            RectTransform area = CreateRect("MoveArea", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(300f, 300f));
            var areaImage = area.gameObject.AddComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0.08f);

            RectTransform bg = CreateRect("MoveBG", area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -90f), new Vector2(180f, 180f));
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.18f);

            RectTransform knob = CreateRect("MoveKnob", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-44f, -44f), new Vector2(88f, 88f));
            var knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.color = new Color(1f, 1f, 1f, 0.5f);

            joystick = area.gameObject.AddComponent<TouchJoystick>();
            joystick.background = bg;
            joystick.knob = knob;
            joystick.radius = joystickRadius;
        }

        private void BuildRightLookPad(Transform parent)
        {
            RectTransform lookArea = CreateRect("LookArea", parent, new Vector2(0.35f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            var image = lookArea.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);

            lookPad = lookArea.gameObject.AddComponent<TouchLookPad>();
        }

        private void BuildButtons(Transform parent)
        {
            mineButton = CreateTapButton(parent, "MineButton", "MINE", new Vector2(1f, 0f), new Vector2(24f, 26f), new Vector2(128f, 112f), new Color(0.95f, 0.45f, 0.2f, 0.88f), 26);
            jumpButton = CreateTapButton(parent, "JumpButton", "JUMP", new Vector2(1f, 0f), new Vector2(24f, 148f), new Vector2(108f, 92f), new Color(0.2f, 0.62f, 0.95f, 0.86f), 22);
            interactButton = CreateTapButton(parent, "InteractButton", DefaultInteractLabel, new Vector2(1f, 0f), new Vector2(146f, 148f), new Vector2(118f, 92f), new Color(0.98f, 0.78f, 0.18f, 0.9f), 24);
            runButton = CreateHoldButton(parent, "RunButton", "RUN", new Vector2(1f, 0f), new Vector2(146f, 26f), new Vector2(108f, 92f), new Color(0.2f, 0.82f, 0.42f, 0.82f), 22);
            removeButton = CreateHoldButton(parent, "RemoveButton", "DEL", new Vector2(1f, 1f), new Vector2(24f, 182f), new Vector2(92f, 72f), new Color(0.95f, 0.2f, 0.2f, 0.82f), 20);
            zoomInButton = CreateHoldButton(parent, "ZoomInButton", "+", new Vector2(1f, 1f), new Vector2(24f, 102f), new Vector2(72f, 60f), new Color(0.75f, 0.75f, 0.9f, 0.82f), 30);
            zoomOutButton = CreateHoldButton(parent, "ZoomOutButton", "-", new Vector2(1f, 1f), new Vector2(24f, 36f), new Vector2(72f, 60f), new Color(0.75f, 0.75f, 0.9f, 0.82f), 30);
            minionMenuButton = CreateTapButton(parent, "MinionsButton", "MINIONS", new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(160f, 62f), new Color(0.22f, 0.34f, 0.56f, 0.88f), 18);

            if (interactButton != null)
                interactButtonLabel = interactButton.GetComponentInChildren<Text>();
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
            return go.AddComponent<TouchTapButton>();
        }

        private TouchHoldButton CreateHoldButton(Transform parent, string name, string text, Vector2 anchor, Vector2 anchoredFromEdge, Vector2 size, Color color, int fontSize)
        {
            Vector2 pos = new Vector2(anchor.x == 1f ? -anchoredFromEdge.x : anchoredFromEdge.x, anchor.y == 1f ? -anchoredFromEdge.y : anchoredFromEdge.y);
            GameObject go = RuntimeUIFactory.MakePanel(name, parent, anchor, anchor, pos, size, color);
            RuntimeUIFactory.MakeLabel(go.transform, "Label", text, fontSize, TextAnchor.MiddleCenter, color: Color.white);
            return go.AddComponent<TouchHoldButton>();
        }

        private void ApplyInteractHintState()
        {
            if (interactButton != null)
                interactButton.gameObject.SetActive(interactHintRequested ? interactHintVisibleRequested : true);

            if (interactButtonLabel != null)
                interactButtonLabel.text = interactHintRequested ? interactHintText : DefaultInteractLabel;

            interactHintRequested = false;
            interactHintText = DefaultInteractLabel;
            interactHintPriority = int.MinValue;
            interactHintVisibleRequested = true;
        }

        // (Helper methods removed, now using RuntimeUIFactory)

        private class TouchTapButton : MonoBehaviour, IPointerDownHandler
        {
            public bool PressedThisFrame { get; private set; }

            public void OnPointerDown(PointerEventData eventData)
            {
                PressedThisFrame = true;
            }

            public void ResetFrameFlags()
            {
                PressedThisFrame = false;
            }
        }

        private class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool Held { get; private set; }
            public bool PressedThisFrame { get; private set; }

            public void OnPointerDown(PointerEventData eventData)
            {
                Held = true;
                PressedThisFrame = true;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                Held = false;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Held = false;
            }

            public void ResetFrameFlags()
            {
                PressedThisFrame = false;
            }
        }

        private class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public RectTransform background;
            public RectTransform knob;
            public float radius = 72f;
            public Vector2 Direction { get; private set; }

            public void OnPointerDown(PointerEventData eventData)
            {
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
            }

            private void UpdateFromPointer(PointerEventData eventData)
            {
                if (background == null)
                    return;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, radius);
                Direction = clamped / Mathf.Max(radius, 0.001f);
                if (knob != null)
                    knob.anchoredPosition = clamped;
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
    }
}
