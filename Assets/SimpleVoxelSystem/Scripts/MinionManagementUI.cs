using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    public class MinionManagementUI : MonoBehaviour
    {
        private static MinionManagementUI instance;
        private MinionAI activeMinion;

        private GameObject panel;
        private GameObject overviewPanel;
        private Transform overviewContainer;
        private Text infoLabel;
        private Button sellBtn;
        private Button upgradeStrBtn;
        private Button upgradeCapBtn;

        private bool isOverviewMode = false;
        private MobileTouchControls mobileControls;
        private bool mobileControlsLookupDone;
        private MinionAI nearestMobileMinion;
        private float nextNearestRefreshAt;
        private const float MobileInteractRange = 3.0f;
        private const float NearestRefreshInterval = 0.2f;

        public static void Show(MinionAI minion)
        {
            EnsureInstance();
            instance.activeMinion = minion;
            instance.isOverviewMode = false;
            instance.Open();
        }

        public static void ToggleOverview()
        {
            EnsureInstance();
            if (instance.overviewPanel != null && instance.overviewPanel.activeSelf)
            {
                instance.overviewPanel.SetActive(false);
            }
            else
            {
                instance.isOverviewMode = true;
                instance.Open();
            }
        }

        private static void EnsureInstance()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("MinionManagementUI");
                instance = go.AddComponent<MinionManagementUI>();
            }
        }

        private void Awake()
        {
            instance = this;
            BuildUI();
        }

        private void BuildUI()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            panel = RuntimeUIFactory.MakePanel("MinionManagePanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 250));
            RuntimeUIFactory.EnableAdaptivePanelScale(panel, 0.94f, 0.90f, 0.60f);
            
            RuntimeUIFactory.MakeLabelFixed(panel.transform, "Title", "MINION DETAILS",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                pos: new Vector2(0f, -14f), size: new Vector2(240f, 28f),
                fontSize: 16, align: TextAnchor.MiddleCenter);

            infoLabel = RuntimeUIFactory.MakeLabelFixed(panel.transform, "Info", "Stats...",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                pos: new Vector2(0f, -78f), size: new Vector2(270f, 70f),
                fontSize: 13, align: TextAnchor.MiddleCenter);
            
            sellBtn = RuntimeUIFactory.MakeBtn(panel.transform, "SellBtn", "SELL INVENTORY", pos: new Vector2(0, -30));
            sellBtn.onClick.AddListener(OnSell);

            upgradeStrBtn = RuntimeUIFactory.MakeBtn(panel.transform, "UpgStr", "UPG STR ($200)", pos: new Vector2(-70, -80), size: new Vector2(130, 30));
            upgradeStrBtn.onClick.AddListener(OnUpgradeStrength);

            upgradeCapBtn = RuntimeUIFactory.MakeBtn(panel.transform, "UpgCap", "UPG CAP ($200)", pos: new Vector2(70, -80), size: new Vector2(130, 30));
            upgradeCapBtn.onClick.AddListener(OnUpgradeCapacity);

            Button closeBtn = RuntimeUIFactory.MakeBtn(panel.transform, "Close", "X",
                color: new Color(0.78f, 0.22f, 0.22f, 0.95f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-8f, -8f), size: new Vector2(34f, 34f));
            closeBtn.onClick.AddListener(() => panel.SetActive(false));

            panel.SetActive(false);

            // --- BUILD OVERVIEW PANEL ---
            overviewPanel = RuntimeUIFactory.MakePanel("MinionOverviewPanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 450));
            RuntimeUIFactory.EnableAdaptivePanelScale(overviewPanel, 0.94f, 0.90f, 0.52f);
            RuntimeUIFactory.MakeLabelFixed(overviewPanel.transform, "Title", "MINION OVERVIEW",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                pos: new Vector2(0f, -14f), size: new Vector2(340f, 30f),
                fontSize: 20, align: TextAnchor.MiddleCenter);
            
            overviewContainer = RuntimeUIFactory.MakeScrollContainer(overviewPanel.transform, new Vector2(10, 10), new Vector2(-10, -60));

            Button closeOverviewTopBtn = RuntimeUIFactory.MakeBtn(overviewPanel.transform, "CloseOverviewTop", "X",
                color: new Color(0.78f, 0.22f, 0.22f, 0.95f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-8f, -8f), size: new Vector2(34f, 34f));
            closeOverviewTopBtn.onClick.AddListener(() => overviewPanel.SetActive(false));
            
            Button closeOverviewBtn = RuntimeUIFactory.MakeBtn(overviewPanel.transform, "CloseOverview", "CLOSE", pos: new Vector2(0, -200), size: new Vector2(100, 40));
            closeOverviewBtn.onClick.AddListener(() => overviewPanel.SetActive(false));
            
            overviewPanel.SetActive(false);
        }

        private void Update()
        {
            WellGenerator wg = FindFirstObjectByType<WellGenerator>();
            bool onIsland = wg != null && !wg.IsInLobbyMode;

            TryResolveMobileControls();

            bool mPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) mPressed = Keyboard.current.mKey.wasPressedThisFrame;
#else
            mPressed = Input.GetKeyDown(KeyCode.M);
#endif

            if (onIsland && mPressed)
                ToggleOverview();

            bool mobileActive = mobileControls != null && mobileControls.IsActive;
            if (mobileActive && onIsland)
            {
                if (Time.unscaledTime >= nextNearestRefreshAt)
                {
                    nextNearestRefreshAt = Time.unscaledTime + NearestRefreshInterval;
                    nearestMobileMinion = FindNearestMinionForMobile();
                }

                if (mobileControls.MinionMenuPressedThisFrame)
                    ToggleOverview();

                if (!ShopZone.IsAnyLocalPlayerInsideZone && nearestMobileMinion != null)
                {
                    mobileControls.RequestInteractHint("MINION", 50, true);
                    if (mobileControls.InteractPressedThisFrame)
                        Show(nearestMobileMinion);
                }
            }

            if (panel != null && panel.activeSelf && activeMinion != null)
            {
                infoLabel.text = $"Strength: {activeMinion.strength}\nCapacity: {activeMinion.currentLoad}/{activeMinion.capacity}";
            }
        }

        private void Open()
        {
            if (isOverviewMode)
            {
                panel.SetActive(false);
                overviewPanel.SetActive(true);
                RefreshOverviewList();
            }
            else
            {
                overviewPanel.SetActive(false);
                panel.SetActive(true);
            }
        }

        private void RefreshOverviewList()
        {
            // Clear container
            foreach (Transform child in overviewContainer) Destroy(child.gameObject);

            MinionAI[] minions = FindObjectsByType<MinionAI>(FindObjectsSortMode.None);
            foreach (var minion in minions)
            {
                GameObject entry = new GameObject("MinionEntry");
                entry.transform.SetParent(overviewContainer, false);
                var rt = entry.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0, 50);
                entry.AddComponent<LayoutElement>().minHeight = 50;
                
                string txt = $"Minion: Str {minion.strength} | Load {minion.currentLoad}/{minion.capacity}";
                RuntimeUIFactory.MakeLabel(entry.transform, "Label", txt, 12, TextAnchor.MiddleLeft, new Vector2(10, 0));
                
                Button manageBtn = RuntimeUIFactory.MakeBtn(entry.transform, "Manage", "OPEN", pos: new Vector2(140, 0), size: new Vector2(60, 30));
                manageBtn.onClick.AddListener(() => Show(minion));
            }
        }

        private MinionAI FindNearestMinionForMobile()
        {
            Transform player = ResolveLocalPlayer();
            if (player == null)
                return null;

            MinionAI[] minions = FindObjectsByType<MinionAI>(FindObjectsSortMode.None);
            if (minions.Length == 0)
                return null;

            float bestSqr = MobileInteractRange * MobileInteractRange;
            MinionAI best = null;

            for (int i = 0; i < minions.Length; i++)
            {
                MinionAI m = minions[i];
                if (m == null || !m.isActiveAndEnabled)
                    continue;

                float sqr = (m.transform.position - player.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = m;
                }
            }

            return best;
        }

        private void TryResolveMobileControls()
        {
            if (mobileControls != null || mobileControlsLookupDone)
                return;

            mobileControls = MobileTouchControls.GetOrCreateIfNeeded();
            mobileControlsLookupDone = true;
        }

        private Transform ResolveLocalPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                return player.transform;

            PlayerPickaxe pp = FindFirstObjectByType<PlayerPickaxe>();
            return pp != null ? pp.transform : null;
        }

        private void OnSell()
        {
            activeMinion.EmptyInventory();
        }

        private void OnUpgradeStrength()
        {
            if (GlobalEconomy.Money >= 200)
            {
                GlobalEconomy.Money -= 200;
                activeMinion.strength++;
            }
        }

        private void OnUpgradeCapacity()
        {
            if (GlobalEconomy.Money >= 200)
            {
                GlobalEconomy.Money -= 200;
                activeMinion.capacity += 5;
            }
        }
    }
}
