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

        // ─── UI refs (built by TutorialUIBuilder) ─────────────────────────────
        private TutorialUIRefs ui;

        // Convenience aliases — keeps the rest of the code readable
        private Canvas        canvas      => ui.Canvas;
        private RectTransform canvasRect  => ui.CanvasRect;
        private Image         dimmer      => ui.Dimmer;
        private GameObject    card        => ui.Card;
        private Text          cardTitle   => ui.CardTitle;
        private Text          cardBody    => ui.CardBody;
        private Text          cardTapHint => ui.CardTapHint;
        private GameObject    hlBox       => ui.HlBox;
        private RectTransform hlRect      => ui.HlRect;
        private Text          arrowLabel  => ui.ArrowLabel;
        private LineRenderer  beam        => ui.Beam;

        private float dimTarget;
        private float dimCurrent;

        private RectTransform hlTarget;
        private bool          hlArrow;

        // ─── Beam ─────────────────────────────────────────────────────────────
        private bool          beamActive;
        private ShopZoneType  beamZoneType = ShopZoneType.Mine;
        private float         nextZoneScan;

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
            ui = TutorialUIBuilder.Build(transform);
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
                    bool acknowledged = elapsed > 3f && Application.isFocused && TutorialInputReader.IsContinuePressed();
                    if (acknowledged)
                    {
                        Debug.Log("[OnboardingTutorial] PC tutorial acknowledged by keyboard.");
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
                    if (elapsed > 0.5f && TutorialInputReader.WasTapped())
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
                    if (elapsed > 8f || (elapsed > 2f && TutorialInputReader.WasTapped()))
                        GoStep(Step.MinionHint);
                    break;

                // ─────────────────────────────────────────────────────────────
                case Step.MinionHint:
                    if (elapsed > 2f && TutorialInputReader.WasTapped())
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
    }
}
