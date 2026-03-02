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
        private TouchHoldButton removeButton;

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
            return controls.IsActive ? controls : null;
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

            jumpButton?.ResetFrameFlags();
            mineButton?.ResetFrameFlags();
            runButton?.ResetFrameFlags();
            removeButton?.ResetFrameFlags();
            lookPad?.ResetFrameFlags();
        }

        private bool ShouldEnable()
        {
#if UNITY_EDITOR
            return forceEnableInEditor;
#else
            return autoEnableOnMobile && Application.isMobilePlatform;
#endif
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
            jumpButton = CreateTapButton(parent, "JumpButton", "JUMP", new Vector2(1f, 0f), new Vector2(28f, 206f), new Color(0.2f, 0.6f, 0.95f, 0.8f));
            mineButton = CreateTapButton(parent, "MineButton", "MINE", new Vector2(1f, 0f), new Vector2(28f, 106f), new Color(0.95f, 0.45f, 0.2f, 0.85f));
            removeButton = CreateHoldButton(parent, "RemoveButton", "DEL", new Vector2(1f, 0f), new Vector2(148f, 206f), new Color(0.95f, 0.2f, 0.2f, 0.82f));
            runButton = CreateHoldButton(parent, "RunButton", "RUN", new Vector2(1f, 0f), new Vector2(148f, 106f), new Color(0.2f, 0.8f, 0.4f, 0.8f));
            zoomInButton = CreateHoldButton(parent, "ZoomInButton", "+", new Vector2(1f, 1f), new Vector2(28f, 120f), new Color(0.75f, 0.75f, 0.9f, 0.8f));
            zoomOutButton = CreateHoldButton(parent, "ZoomOutButton", "-", new Vector2(1f, 1f), new Vector2(28f, 60f), new Color(0.75f, 0.75f, 0.9f, 0.8f));
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

        private TouchTapButton CreateTapButton(Transform parent, string name, string text, Vector2 anchor, Vector2 anchoredFromEdge, Color color)
        {
            RectTransform rt = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(anchor.x == 1f ? -anchoredFromEdge.x : anchoredFromEdge.x, anchor.y == 1f ? -anchoredFromEdge.y : anchoredFromEdge.y);
            rt.sizeDelta = new Vector2(100f, 84f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;

            var label = CreateLabel(rt, text, 24);
            label.alignment = TextAnchor.MiddleCenter;

            return rt.gameObject.AddComponent<TouchTapButton>();
        }

        private TouchHoldButton CreateHoldButton(Transform parent, string name, string text, Vector2 anchor, Vector2 anchoredFromEdge, Color color)
        {
            RectTransform rt = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(anchor.x == 1f ? -anchoredFromEdge.x : anchoredFromEdge.x, anchor.y == 1f ? -anchoredFromEdge.y : anchoredFromEdge.y);
            rt.sizeDelta = new Vector2(100f, 84f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;

            var label = CreateLabel(rt, text, 24);
            label.alignment = TextAnchor.MiddleCenter;

            return rt.gameObject.AddComponent<TouchHoldButton>();
        }

        private static Text CreateLabel(Transform parent, string text, int size)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            var rt = labelGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var t = labelGo.AddComponent<Text>();
            t.font = RuntimeUiFont.Get();
            t.fontSize = size;
            t.supportRichText = false;
            t.color = Color.white;
            t.text = text;
            return t;
        }

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
