using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class OnboardingTutorial : MonoBehaviour
    {
        // ─── PlayerPrefs Keys ─────────────────────────────────────────────────
        private const string KeyMobileDone = "svs.tutorial.mobile.v2.done";
        private const string KeyPcShown    = "svs.tutorial.pc.v2.shown";

        // ─── Static API ───────────────────────────────────────────────────────
        public static OnboardingTutorial Instance { get; private set; }

        /// <summary>True while the player must not mine, move or open shops.</summary>
        public static bool IsGameplayInputBlocked { get; private set; }

        /// <summary>Called by ShopZone to check whether this zone should be blocked.</summary>
        public static bool IsShopInteractionBlocked(ShopZoneType _) => IsGameplayInputBlocked;

        // ─── Debug / Editor ───────────────────────────────────────────────────
        [Header("Debug")]
        [Tooltip("Принудительно запустить обучение заново при старте (только для тестирования)")]
        public bool forceRestartInEditor = false;

        /// <summary>Сбросить флаги обучения прямо в Inspector (ПКМ → Reset Tutorial).</summary>
        [ContextMenu("Reset Tutorial (clear PlayerPrefs)")]
        public void ResetTutorial()
        {
            PlayerPrefs.DeleteKey(KeyMobileDone);
            PlayerPrefs.DeleteKey(KeyPcShown);
            PlayerPrefs.Save();
            Debug.Log("[OnboardingTutorial] PlayerPrefs сброшены. Перезапусти сцену.");
        }

        /// <summary>Статический сброс, удобно вызвать из кода или консоли.</summary>
        public static void ResetTutorialStatic()
        {
            PlayerPrefs.DeleteKey(KeyMobileDone);
            PlayerPrefs.DeleteKey(KeyPcShown);
            PlayerPrefs.Save();
        }

        // ─── Tutorial Steps ───────────────────────────────────────────────────
        private enum Step
        {
            Idle             = 0,
            PcControls       = 1,   // PC: show WASD + LMB hint, dismiss on first use
            MobJoystick      = 2,   // Mobile: dark screen + joystick highlight
            MobButtons       = 3,   // Mobile: show all button labels, tap to continue
            MobCreateIsland  = 4,   // Mobile: highlight Create Island, block everything else
            MobOnIsland      = 5,   // "Here is your island…" + highlight To Lobby
            MobBuyMine       = 6,   // Lobby: beam to mine shop + text
            MobPlaceMine     = 7,   // "Go place it on your island"
            Mining           = 8,   // Mine blocks to fill backpack
            BackpackFull     = 9,   // Backpack full -> go sell
            SellOre          = 10,  // Beam to sell zone
            UpgradePickaxe   = 11,  // Beam to pickaxe shop
            MinionHint       = 12,  // Beam to minion shop — final step
            Done             = 99,
        }

        // ─── Scene References (resolved lazily) ──────────────────────────────
        private MobileTouchControls mobile;
        private WellGenerator       wellGen;
        private MineMarket          mineMarket;
        private MineShopUI          mineShopUI;
        private PlayerPickaxe       playerPickaxe;
        private Transform           playerTf;
        private ShopZone            mineZone;

        // ─── UI Elements ──────────────────────────────────────────────────────
        private Canvas          canvas;
        private RectTransform   canvasRect;

        private Image           dimmer;
        private float           dimTarget;
        private float           dimCurrent;

        private GameObject      card;           // dark card at top
        private Text            cardTitle;
        private Text            cardBody;
        private Text            cardTapHint;

        private GameObject      hlBox;          // golden highlight rectangle
        private RectTransform   hlRect;
        private RectTransform   hlTarget;       // world-space rect to track
        private bool            hlArrow;

        private Text            arrowLabel;     // "↓" label drawn near highlight

        private LineRenderer    beam;           // world-space beam to shop zone
        private bool            beamActive;
        private ShopZoneType    beamZoneType = ShopZoneType.Mine;
        private float           nextZoneScan;

        // ─── State ────────────────────────────────────────────────────────────
        private Step   step = Step.Idle;
        private float  stepTime;
        private float  joystickHoldSec;
        private bool   isMobile;
        private float  nextRefScan;

        // ═════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ═════════════════════════════════════════════════════════════════════

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IsGameplayInputBlocked = false;
        }

        void Start()
        {
            BuildUI();
            DetermineFlow();
        }

        void Update()
        {
            if (Time.unscaledTime > nextRefScan) { ScanRefs(); nextRefScan = Time.unscaledTime + 0.4f; }

            UpdateStep();
            AnimateDimmer();
            TrackHighlight();
            UpdateBeam();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Flow entry
        // ═════════════════════════════════════════════════════════════════════

        void DetermineFlow()
        {
            mobile   = MobileTouchControls.GetOrCreateIfNeeded();
            isMobile = mobile != null && mobile.IsActive;

#if UNITY_EDITOR
            if (forceRestartInEditor)
            {
                PlayerPrefs.DeleteKey(KeyMobileDone);
                PlayerPrefs.DeleteKey(KeyPcShown);
                PlayerPrefs.Save();
                Debug.Log("[OnboardingTutorial] forceRestartInEditor=true — сброс PlayerPrefs.");
            }
#endif

            if (isMobile)
            {
                Debug.Log("[OnboardingTutorial] Мобильный поток. Done=" + PlayerPrefs.GetInt(KeyMobileDone, 0));
                if (PlayerPrefs.GetInt(KeyMobileDone, 0) == 1) { GoStep(Step.Done); return; }
                GoStep(Step.MobJoystick);
            }
            else
            {
                Debug.Log("[OnboardingTutorial] ПК поток. Done=" + PlayerPrefs.GetInt(KeyPcShown, 0));
                if (PlayerPrefs.GetInt(KeyPcShown, 0) == 1) { GoStep(Step.Done); return; }
                GoStep(Step.PcControls);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Step transitions
        // ═════════════════════════════════════════════════════════════════════

        void GoStep(Step next)
        {
            step     = next;
            stepTime = Time.unscaledTime;
            joystickHoldSec = 0f;

            Debug.Log($"[OnboardingTutorial] Step -> {next}");

            IsGameplayInputBlocked = (next == Step.MobCreateIsland);

            ApplyUI(next);
        }

        void ApplyUI(Step s)
        {
            switch (s)
            {
                // ── PC: show controls card, auto-dismiss ─────────────────────
                case Step.PcControls:
                    ShowCard("УПРАВЛЕНИЕ",
                        "Движение:  W A S D\n" +
                        "Прыжок:   Пробел\n" +
                        "Копать:   Левая кнопка мыши\n\n" +
                        "Нажми любую клавишу или покликай, чтобы начать.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(false);
                    break;

                // ── Mobile step 1: dark screen + joystick callout ────────────
                case Step.MobJoystick:
                    ShowCard("УПРАВЛЕНИЕ",
                        "Это джойстик движения.\nПодвигай его, чтобы идти вперёд.",
                        tapHint: false);
                    SetDim(0.72f);
                    SetHighlight(mobile?.MoveAreaRect, arrow: true);
                    SetBeam(false);
                    break;

                // ── Mobile step 2: button guide ──────────────────────────────
                case Step.MobButtons:
                    ShowCard("КНОПКИ",
                        "MINE  — копать блок\n" +
                        "JUMP  — прыжок\n" +
                        "ACT   — взаимодействие\n" +
                        "RUN   — бег\n" +
                        "MINIONS — меню миньонов\n\n" +
                        "Нажми на экран, чтобы продолжить.",
                        tapHint: true);
                    SetDim(0.62f);
                    SetHighlight(mobile?.MineButtonRect, arrow: true);
                    SetBeam(false);
                    break;

                // ── Mobile step 3: MUST create island ───────────────────────
                case Step.MobCreateIsland:
                    ShowCard("СОЗДАЙ ОСТРОВ",
                        "Нажми кнопку Create Island.\n" +
                        "До этого все остальные действия заблокированы.",
                        tapHint: false);
                    SetDim(0.55f);
                    SetHighlight(GetCreateIslandRect(), arrow: true);
                    SetBeam(false);
                    break;

                // ── Mobile step 4: explore island ────────────────────────────
                case Step.MobOnIsland:
                    ShowCard("ВОТ И ТВОЙ ОСТРОВ",
                        "Вот и твой остров, мой дорогой шахтер!\n" +
                        "Исследуй его. Когда насладишься видами —\n" +
                        "возвращайся обратно в лобби.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(GetSwitchWorldRect(), arrow: true);
                    SetBeam(false);
                    break;

                // ── Mobile step 5: buy mine ──────────────────────────────────
                case Step.MobBuyMine:
                    ShowCard("ПЕРВАЯ ШАХТА",
                        "Для начала приобрети свою первую шахту.\n" +
                        "Луч указывает на магазин шахт.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(true);
                    break;

                // ── Mobile step 6: go place it ───────────────────────────────
                case Step.MobPlaceMine:
                    ShowCard("РАЗМЕСТИ ШАХТУ",
                        "Отлично! Возвращайся на свой остров\n" +
                        "и размести купленную шахту.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(GetSwitchWorldRect(), arrow: true);
                    SetBeam(false);
                    break;

                // ── Mining ───────────────────────────────────────────────────────
                case Step.Mining:
                    if (isMobile)
                        ShowCard("ДОБЫЧА",
                            "Подойди к шахте и нажми на блок который хочешь сломать.\n" +
                            "Зажми экран на блоке или нажми кнопку MINE.",
                            tapHint: false);
                    else
                        ShowCard("ДОБЫЧА",
                            "Подойди к своей шахте.\n" +
                            "Нажми ЛЕВУЮ КНОПКУ МЫШИ на блок, чтобы копать.\n" +
                            "Заполни рюкзак до отказа!",
                            tapHint: false);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(false);
                    break;

                // ── Backpack full ─────────────────────────────────────────────────
                case Step.BackpackFull:
                    ShowCard("РЮКЗАК ПОЛОН!",
                        "Отличная работа! Пора разгрузиться.\n" +
                        "Вернись в лобби — луч покажет точку продажи руды.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(true, ShopZoneType.Sell);
                    break;

                // ── Sell ore ─────────────────────────────────────────────────────
                case Step.SellOre:
                    ShowCard("ПРОДАЙ РУДУ",
                        "Подойди к точке продажи и сдай содержимое рюкзака.\n" +
                        "Луч указывает направление.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(true, ShopZoneType.Sell);
                    break;

                // ── Upgrade pickaxe ──────────────────────────────────────────────
                case Step.UpgradePickaxe:
                    ShowCard("УЛУЧШИ СНАРЯЖЕНИЕ",
                        "Хочешь копать быстрее и добраться до редких руд?\n" +
                        "Подойди к магазину кирок и прокачай снаряжение.",
                        tapHint: false);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(true, ShopZoneType.Pickaxe);
                    break;

                // ── Minion hint ──────────────────────────────────────────────────
                case Step.MinionHint:
                    ShowCard("АВТОМАТИЗИРУЙ ШАХТУ",
                        "Если захочешь автоматизировать добычу —\n" +
                        "приходи сюда и найми своего первого работника-миньона!\n\n" +
                        "Нажми любую клавишу / тапни, чтобы закрыть.",
                        tapHint: true);
                    SetDim(0f);
                    SetHighlight(null);
                    SetBeam(true, ShopZoneType.Minion);
                    break;

                // ── Done ─────────────────────────────────────────────────────
                default:
                    HideAll();
                    IsGameplayInputBlocked = false;
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Per-step update logic
        // ═════════════════════════════════════════════════════════════════════

        void UpdateStep()
        {
            float elapsed = Time.unscaledTime - stepTime;

            switch (step)
            {
                // ─────────────────────────────────────────────────────────────
                case Step.PcControls:
                    // Минимум 3 секунды показа — не закрывается случайно при загрузке
                    bool acknowledged = elapsed > 3f && Application.isFocused && IsPcContinuePressed();
                    if (acknowledged)
                    {
                        Debug.Log("[OnboardingTutorial] PC tutorial acknowledged by keyboard.");
                        // Не сохраняем Done — идём дальше на шаги с островом и шахтой
                        GoStep(Step.MobCreateIsland);
                    }
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MobJoystick:
                    if (mobile == null) return;
                    if (mobile.MoveVector.sqrMagnitude > 0.01f)
                        joystickHoldSec += Time.unscaledDeltaTime;
                    else
                        joystickHoldSec = 0f;

                    if (joystickHoldSec >= 0.25f)
                        GoStep(Step.MobButtons);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MobButtons:
                    if (elapsed > 0.5f && WasTapped())
                        GoStep(Step.MobCreateIsland);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MobCreateIsland:
                    // Re-anchor highlight if button appeared late
                    if (Time.frameCount % 15 == 0)
                        SetHighlight(GetCreateIslandRect(), arrow: true);

                    if (wellGen != null && !wellGen.IsInLobbyMode && wellGen.IsIslandGenerated)
                        GoStep(Step.MobOnIsland);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MobOnIsland:
                    // Refresh "To Lobby" highlight if not tracked yet
                    if (Time.frameCount % 15 == 0)
                        SetHighlight(GetSwitchWorldRect(), arrow: true);

                    if (wellGen != null && wellGen.IsInLobbyMode)
                        GoStep(Step.MobBuyMine);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MobBuyMine:
                    if (mineMarket != null && mineMarket.IsPlacementMode)
                        GoStep(Step.MobPlaceMine);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MobPlaceMine:
                    if (Time.frameCount % 15 == 0)
                        SetHighlight(GetSwitchWorldRect(), arrow: true);

                    if (mineMarket != null && !mineMarket.IsPlacementMode
                        && mineMarket.IsMineGenerated()
                        && wellGen != null && !wellGen.IsInLobbyMode)
                    {
                        GoStep(Step.Mining);
                    }
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.Mining:
                    if (playerPickaxe != null
                        && playerPickaxe.currentBackpackLoad >= playerPickaxe.maxBackpackCapacity)
                        GoStep(Step.BackpackFull);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.BackpackFull:
                    if (wellGen != null && wellGen.IsInLobbyMode)
                        GoStep(Step.SellOre);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.SellOre:
                    if (playerPickaxe != null
                        && playerPickaxe.currentBackpackLoad == 0
                        && elapsed > 1.5f)
                        GoStep(Step.UpgradePickaxe);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.UpgradePickaxe:
                    if (elapsed > 8f || (elapsed > 2f && WasTapped()))
                        GoStep(Step.MinionHint);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MinionHint:
                    if (elapsed > 2f && WasTapped())
                    {
                        PlayerPrefs.SetInt(KeyMobileDone, 1);
                        PlayerPrefs.SetInt(KeyPcShown, 1);
                        PlayerPrefs.Save();
                        GoStep(Step.Done);
                    }
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI helpers
        // ═════════════════════════════════════════════════════════════════════

        void ShowCard(string title, string body, bool tapHint)
        {
            if (canvas != null) canvas.enabled = true;
            if (card != null)   card.SetActive(true);
            if (cardTitle != null)   cardTitle.text   = title;
            if (cardBody != null)    cardBody.text     = body;
            if (cardTapHint != null)
            {
                cardTapHint.gameObject.SetActive(tapHint);
                cardTapHint.text = tapHint ? "Нажми на экран, чтобы продолжить ›" : "";
            }
        }

        void HideAll()
        {
            if (canvas != null) canvas.enabled = false;
            SetHighlight(null);
            SetBeam(false);
        }

        void SetDim(float alpha)
        {
            dimTarget = Mathf.Clamp01(alpha);
        }

        void AnimateDimmer()
        {
            if (dimmer == null) return;
            dimCurrent = Mathf.MoveTowards(dimCurrent, dimTarget, Time.unscaledDeltaTime * 3f);
            Color c = dimmer.color;
            c.a = dimCurrent;
            dimmer.color = c;
        }

        void SetHighlight(RectTransform target, bool arrow = false)
        {
            hlTarget = target;
            hlArrow  = arrow;
            if (hlBox != null) hlBox.SetActive(target != null);
            if (arrowLabel != null) arrowLabel.gameObject.SetActive(target != null && arrow);
        }

        void TrackHighlight()
        {
            if (hlTarget == null || hlRect == null || canvasRect == null) return;

            Vector3[] corners = new Vector3[4];
            hlTarget.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Vector2 screenCenter = (min + max) * 0.5f;
            Vector2 screenSize   = (max - min) + new Vector2(20f, 16f);
            screenSize.x = Mathf.Max(screenSize.x, 80f);
            screenSize.y = Mathf.Max(screenSize.y, 54f);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenCenter, null, out Vector2 local))
            {
                hlBox.SetActive(true);
                hlRect.anchoredPosition = local;
                hlRect.sizeDelta        = screenSize;

                if (arrowLabel != null && hlArrow)
                {
                    arrowLabel.gameObject.SetActive(true);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    arrowLabel.color = new Color(1f, 0.9f, 0.15f, 0.6f + 0.4f * pulse);
                    ((RectTransform)arrowLabel.transform).anchoredPosition =
                        local + new Vector2(0f, screenSize.y * 0.5f + 22f);
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Beam
        // ═════════════════════════════════════════════════════════════════════

        void SetBeam(bool active, ShopZoneType zoneType = ShopZoneType.Mine)
        {
            beamActive   = active;
            beamZoneType = zoneType;
            if (beam != null) beam.enabled = active;
            if (!active) { mineZone = null; }
        }

        void UpdateBeam()
        {
            if (!beamActive || beam == null) return;

            if (Time.unscaledTime >= nextZoneScan)
            {
                nextZoneScan = Time.unscaledTime + 0.7f;
                mineZone = FindNearestZone(beamZoneType);
            }

            if (playerTf == null || mineZone == null) { beam.enabled = false; return; }

            beam.enabled = true;
            Vector3 from = playerTf.position + Vector3.up * 1.4f;
            Vector3 to   = mineZone.transform.position + Vector3.up * 1.2f;
            Collider col = mineZone.GetComponent<Collider>();
            if (col != null) to = col.bounds.center + Vector3.up * 0.5f;

            // Animate beam colour
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            beam.startColor = Color.Lerp(new Color(1f, 0.9f, 0.1f, 0.9f), new Color(1f, 0.5f, 0.05f, 0.9f), t);
            beam.endColor   = beam.startColor;

            beam.SetPosition(0, from);
            beam.SetPosition(1, to);
        }

        ShopZone FindNearestZone(ShopZoneType type)
        {
            var all = FindObjectsByType<ShopZone>(FindObjectsSortMode.None);
            ShopZone best = null;
            float bestD = float.MaxValue;
            Vector3 origin = playerTf != null ? playerTf.position : Vector3.zero;

            foreach (var z in all)
            {
                if (z == null || z.zoneType != type) continue;
                float d = (z.transform.position - origin).sqrMagnitude;
                if (d < bestD) { bestD = d; best = z; }
            }
            return best;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Reference scanning
        // ═════════════════════════════════════════════════════════════════════

        void ScanRefs()
        {
            if (wellGen    == null) wellGen    = FindFirstObjectByType<WellGenerator>();
            if (mineMarket == null) mineMarket = FindFirstObjectByType<MineMarket>();
            if (mineShopUI    == null) mineShopUI    = FindFirstObjectByType<MineShopUI>();
            if (playerPickaxe == null) playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();
            if (mobile        == null) mobile        = MobileTouchControls.Instance ?? MobileTouchControls.GetOrCreateIfNeeded();

            if (playerTf == null)
            {
                var pcc = FindFirstObjectByType<PlayerCharacterController>();
                if (pcc != null) { playerTf = pcc.transform; return; }
                var pp = FindFirstObjectByType<PlayerPickaxe>();
                if (pp != null) playerTf = pp.transform;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Rect helpers
        // ═════════════════════════════════════════════════════════════════════

        RectTransform GetCreateIslandRect()
        {
            if (mineShopUI == null || mineShopUI.CreateIslandButton == null) return null;
            return mineShopUI.CreateIslandButton.GetComponent<RectTransform>();
        }

        RectTransform GetSwitchWorldRect()
        {
            if (mineShopUI == null || mineShopUI.SwitchWorldButton == null) return null;
            return mineShopUI.SwitchWorldButton.GetComponent<RectTransform>();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Input helpers
        // ═════════════════════════════════════════════════════════════════════

        bool IsPcMovePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f
                || Mathf.Abs(Input.GetAxisRaw("Vertical"))   > 0.01f;
#else
            return false;
#endif
        }

        bool IsPcContinuePressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyDown = false;
            if (Keyboard.current != null)
            {
                Keyboard kb = Keyboard.current;
                keyDown = kb.wKey.wasPressedThisFrame
                    || kb.aKey.wasPressedThisFrame
                    || kb.sKey.wasPressedThisFrame
                    || kb.dKey.wasPressedThisFrame
                    || kb.spaceKey.wasPressedThisFrame;
            }
            return keyDown;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool keyDown = Input.GetKeyDown(KeyCode.W)
                || Input.GetKeyDown(KeyCode.A)
                || Input.GetKeyDown(KeyCode.S)
                || Input.GetKeyDown(KeyCode.D)
                || Input.GetKeyDown(KeyCode.Space);
            return keyDown;
#else
            return false;
#endif
        }

        bool IsPcMinePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        bool WasTapped()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
            if (Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI construction (pure code, no prefabs required)
        // ═════════════════════════════════════════════════════════════════════

        void BuildUI()
        {
            // ── Canvas ────────────────────────────────────────────────────────
            GameObject cGo = new GameObject("OnboardingCanvas");
            cGo.transform.SetParent(transform, false);

            canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7000;
            cGo.AddComponent<GraphicRaycaster>();

            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasRect = canvas.GetComponent<RectTransform>();

            // ── Full-screen dimmer ────────────────────────────────────────────
            var dimGo = MakePanel("Dimmer", cGo.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, 0), stretch: true);
            dimmer = dimGo.GetComponent<Image>();
            dimmer.raycastTarget = false;

            // ── Info card (top-centre) ────────────────────────────────────────
            card = MakePanel("TutCard", cGo.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(880f, 210f),
                new Color(0.04f, 0.06f, 0.12f, 0.95f));
            // Set pivot to top-center so anchoredPosition positions the TOP edge, not the center
            card.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            card.GetComponent<Image>().raycastTarget = false;

            // Rounded feel via Outline
            var outline = card.AddComponent<Outline>();
            outline.effectColor    = new Color(0.35f, 0.55f, 1f, 0.45f);
            outline.effectDistance = new Vector2(2f, 2f);

            // Title: bottom-right corner of the card — never overlaps body text
            cardTitle = MakeLabel(card.transform, "Title",
                "", 22, TextAnchor.LowerRight,
                new Vector2(0, 8), new Vector2(-16, 8), bold: true,
                color: new Color(0.55f, 0.72f, 1f, 0.85f));

            // Body: takes the full card from top, plenty of room
            cardBody = MakeLabel(card.transform, "Body",
                "", 20, TextAnchor.UpperLeft,
                new Vector2(28, -12), new Vector2(-28, -26), bold: false,
                color: new Color(0.88f, 0.93f, 1f));

            cardTapHint = MakeLabel(card.transform, "TapHint",
                "", 17, TextAnchor.LowerRight,
                new Vector2(28, 8), new Vector2(-28, 8), bold: false,
                color: new Color(1f, 0.88f, 0.35f));
            cardTapHint.gameObject.SetActive(false);

            if (cardTitle   != null) cardTitle.raycastTarget   = false;
            if (cardBody    != null) cardBody.raycastTarget     = false;
            if (cardTapHint != null) cardTapHint.raycastTarget  = false;

            // ── Highlight box ─────────────────────────────────────────────────
            hlBox = MakePanel("Highlight", cGo.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(120, 60),
                new Color(1f, 0.85f, 0.1f, 0.08f));
            hlRect = hlBox.GetComponent<RectTransform>();
            hlBox.GetComponent<Image>().raycastTarget = false;

            var hlOutline = hlBox.AddComponent<Outline>();
            hlOutline.effectColor    = new Color(1f, 0.88f, 0.05f, 1f);
            hlOutline.effectDistance = new Vector2(3f, 3f);
            hlBox.SetActive(false);

            // ── Arrow label ───────────────────────────────────────────────────
            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(cGo.transform, false);
            var arrowRt = arrowGo.AddComponent<RectTransform>();
            arrowRt.sizeDelta = new Vector2(80, 36);
            arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
            arrowLabel = arrowGo.AddComponent<Text>();
            arrowLabel.text      = "▼";
            arrowLabel.fontSize  = 32;
            arrowLabel.alignment = TextAnchor.MiddleCenter;
            arrowLabel.color     = new Color(1f, 0.9f, 0.15f, 1f);
            arrowLabel.font      = GetSafeFont();
            arrowLabel.raycastTarget = false;
            arrowGo.SetActive(false);

            // ── World-space beam ──────────────────────────────────────────────
            var beamGo = new GameObject("TutBeam");
            beamGo.transform.SetParent(transform, false);
            beam = beamGo.AddComponent<LineRenderer>();
            beam.positionCount  = 2;
            beam.startWidth     = 0.07f;
            beam.endWidth       = 0.07f;
            beam.useWorldSpace  = true;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            var beamMat = new Material(sh);
            beamMat.color = new Color(1f, 0.88f, 0.1f, 0.92f);
            beam.material = beamMat;
            beam.enabled  = false;

            canvas.enabled = false; // hidden until first step
        }

        // ── Tiny helpers ──────────────────────────────────────────────────────

        static GameObject MakePanel(string name, Transform parent,
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

        static Text MakeLabel(Transform parent, string name,
            string text, int fontSize, TextAnchor anchor,
            Vector2 offsetMin, Vector2 offsetMax,
            bool bold = false, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = offsetMin;
            rt.offsetMax  = offsetMax;
            var t = go.AddComponent<Text>();
            t.text      = text;
            t.fontSize  = fontSize;
            t.alignment = anchor;
            t.color     = color ?? Color.white;
            t.font      = GetSafeFont();
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }

        // Returns a font that works in Editor, standalone AND WebGL/Yandex builds.
        // LegacyRuntime.ttf is Editor-only; Arial is always available at runtime.
        static Font _safeFont;
        static Font GetSafeFont()
        {
            if (_safeFont != null) return _safeFont;

            // 0. Preferred bundled Unicode font.
            _safeFont = Resources.Load<Font>("LiberationSans");
            if (_safeFont != null) return _safeFont;

            // 1. Try fonts in Resources (Roboto-Regular usually has better Cyrillic support)
            _safeFont = Resources.Load<Font>("Roboto-Regular");
            if (_safeFont != null) return _safeFont;

            // 2. Try built-in Arial (works in all builds including WebGL)
            _safeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_safeFont != null) return _safeFont;

            // 3. OS font fallback
            _safeFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (_safeFont != null) return _safeFont;

            // 4. Any font in Resources
            _safeFont = Resources.FindObjectsOfTypeAll<Font>().Length > 0
                ? Resources.FindObjectsOfTypeAll<Font>()[0] : null;

            return _safeFont;
        }
    }
}
