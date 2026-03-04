using UnityEngine;
using UnityEngine.UI;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Displays permanent upgrades (Strength and Backpack) in separate side panels.
    /// This keeps character progression distinct from tool tools.
    /// </summary>
    public class UpgradeHUD : MonoBehaviour
    {
        private UpgradeManager upgradeManager;
        private PlayerPickaxe playerPickaxe;
        private PickaxeShopUI pickaxeShopUI;

        private GameObject leftPanel;
        private GameObject rightPanel;

        private Text strengthLabel;
        private Text backpackLabel;
        private Text strengthPriceText;
        private Text backpackPriceText;

        private void Start()
        {
            upgradeManager = FindFirstObjectByType<UpgradeManager>();
            playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();
            pickaxeShopUI = FindFirstObjectByType<PickaxeShopUI>();

            CreateUI();
        }

        private void Update()
        {
            if (leftPanel == null)
            {
                CreateUI();
                return;
            }

            if (pickaxeShopUI == null)
                pickaxeShopUI = FindFirstObjectByType<PickaxeShopUI>();

            // Only show panels when the pickaxe shop menu is open
            bool shouldShow = pickaxeShopUI != null && pickaxeShopUI.gameObject.activeInHierarchy && IsShopPanelActive();
            if (leftPanel.activeSelf != shouldShow)
            {
                leftPanel.SetActive(shouldShow);
                rightPanel.SetActive(shouldShow);
            }

            if (shouldShow)
                UpdateLabels();
        }

        private bool IsShopPanelActive()
        {
            return pickaxeShopUI != null && pickaxeShopUI.IsVisible;
        }

        private void CreateUI()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            if (playerPickaxe == null)
                playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();

            Color headerColor = new Color(0.12f, 0.12f, 0.15f, 0.98f);
            Color bodyColor = new Color(0.08f, 0.08f, 0.1f, 0.90f);

            // --- LEFT PANEL (Strength) ---
            leftPanel = RuntimeUIFactory.MakePanel("StrengthUpgradePanel", canvas.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(180f, 150f), bodyColor);
            
            // Header
            GameObject headerLeft = RuntimeUIFactory.MakePanel("Header", leftPanel.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -15), new Vector2(0, 30), headerColor);
            RuntimeUIFactory.MakeLabel(headerLeft.transform, "Title", "STRRENGTH", 14, TextAnchor.MiddleCenter, color: new Color(1, 0.8f, 0.2f));

            strengthLabel = RuntimeUIFactory.MakeLabel(leftPanel.transform, "Stats", "Bonus: +0", 13, TextAnchor.MiddleCenter, new Vector2(0, 5));
            
            Button btnStr = RuntimeUIFactory.MakeBtn(leftPanel.transform, "BtnUpgrade", "BUY", pos: new Vector2(0, -45), size: new Vector2(140, 34));
            strengthPriceText = btnStr.GetComponentInChildren<Text>();
            btnStr.onClick.AddListener(OnUpgradeStrength);

            // --- RIGHT PANEL (Backpack) ---
            rightPanel = RuntimeUIFactory.MakePanel("BackpackUpgradePanel", canvas.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(180f, 150f), bodyColor);
            
            // Header
            GameObject headerRight = RuntimeUIFactory.MakePanel("Header", rightPanel.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -15), new Vector2(0, 30), headerColor);
            RuntimeUIFactory.MakeLabel(headerRight.transform, "Title", "BACKPACK", 14, TextAnchor.MiddleCenter, color: new Color(0.2f, 0.8f, 1f));

            backpackLabel = RuntimeUIFactory.MakeLabel(rightPanel.transform, "Stats", "Slots: 10", 13, TextAnchor.MiddleCenter, new Vector2(0, 5));
            
            Button btnCap = RuntimeUIFactory.MakeBtn(rightPanel.transform, "BtnUpgrade", "BUY", pos: new Vector2(0, -45), size: new Vector2(140, 34));
            backpackPriceText = btnCap.GetComponentInChildren<Text>();
            btnCap.onClick.AddListener(OnUpgradeBackpack);

            UpdateLabels();
        }


        private void UpdateLabels()
        {
            if (playerPickaxe == null || upgradeManager == null) return;

            strengthLabel.text = $"Current Bonus:\n<color=#FFD700>+{playerPickaxe.playerStrength}</color>";
            strengthPriceText.text = $"UPGRADE: ${upgradeManager.playerStrengthCost}";

            backpackLabel.text = $"Current Capacity:\n<color=#00EAFF>{playerPickaxe.maxBackpackCapacity}</color>";
            backpackPriceText.text = $"UPGRADE: ${upgradeManager.backpackCapacityCost}";
        }

        private void OnUpgradeStrength()
        {
            if (upgradeManager != null) upgradeManager.UpgradePlayerStrength();
        }

        private void OnUpgradeBackpack()
        {
            if (upgradeManager != null) upgradeManager.UpgradeBackpackCapacity();
        }
    }
}
