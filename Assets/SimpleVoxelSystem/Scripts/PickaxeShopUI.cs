using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class PickaxeShopUI : MonoBehaviour
    {
        public PlayerPickaxe playerPickaxe;
        public List<PickaxeData> availablePickaxes;

        private Canvas rootCanvas;
        private GameObject shopPanel;
        private GameObject overlay;
        private Text levelText;
        private Transform buttonContainer;
        private UpgradeManager upgradeManager;

        private readonly List<GameObject> builtItems = new List<GameObject>();
        private readonly HashSet<int> ownedPickaxeIndices = new HashSet<int>();

        private const string OwnedPrefsKey = "svs_pickaxe_owned_indices_v1";
        private const string EquippedPrefsKey = "svs_pickaxe_equipped_index_v1";

        public bool IsVisible => shopPanel != null && shopPanel.activeSelf;

        private static readonly Color ColPanel = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        private static readonly Color ColText = new Color(0.95f, 0.95f, 0.95f, 1f);

        [Serializable]
        private class IntListWrapper
        {
            public List<int> items = new List<int>();
        }

        private void Awake()
        {
            if (playerPickaxe == null)
                playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();

            EnsureDefaultPickaxes();
            LoadPickaxeState();
            BuildUI();
        }

        private void EnsureDefaultPickaxes()
        {
            if (availablePickaxes != null && availablePickaxes.Count > 0)
            {
                bool valid = true;
                foreach (PickaxeData d in availablePickaxes)
                {
                    if (d == null) { valid = false; break; }
                }
                if (valid) return;
            }

            availablePickaxes = new List<PickaxeData>
            {
                CreateData("Stone Pickaxe", "Faster than default.", 500, 2, 1, new Color(0.5f, 0.5f, 0.5f)),
                CreateData("Iron Pickaxe", "Solid upgrade for mining.", 2000, 5, 3, new Color(0.8f, 0.8f, 0.8f)),
                CreateData("Gold Pickaxe", "Very fast but expensive.", 5000, 10, 6, new Color(1f, 0.9f, 0f)),
                CreateData("Diamond Pickaxe", "Top tier pickaxe.", 15000, 25, 10, new Color(0.3f, 0.9f, 1f))
            };
        }

        private PickaxeData CreateData(string n, string d, int p, int pow, int lvl, Color c)
        {
            PickaxeData data = ScriptableObject.CreateInstance<PickaxeData>();
            data.displayName = n;
            data.description = d;
            data.buyPrice = p;
            data.miningPower = pow;
            data.requiredMiningLevel = lvl;
            data.iconColor = c;
            return data;
        }

        private void BuildUI()
        {
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                GameObject cGo = new GameObject("PickaxeShopCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }

            overlay = RuntimeUIFactory.MakePanel("PickaxeOverlay", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(10000f, 10000f), new Color(0f, 0f, 0f, 0.6f));
            shopPanel = RuntimeUIFactory.MakePanel("PickaxePanel", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(420f, 520f), ColPanel);

            RuntimeUIFactory.MakeLabel(shopPanel.transform, "Title", "PICKAXE SHOP", 20, TextAnchor.UpperCenter).rectTransform.anchoredPosition = new Vector2(0f, -15f);
            levelText = RuntimeUIFactory.MakeLabel(shopPanel.transform, "LevelInfo", "Mining level: 1 (0 XP)", 14, TextAnchor.UpperCenter);
            levelText.rectTransform.anchoredPosition = new Vector2(0f, -45f);

            buttonContainer = RuntimeUIFactory.MakeScrollContainer(shopPanel.transform, new Vector2(10f, 10f), new Vector2(-10f, -80f));
            
            upgradeManager = FindFirstObjectByType<UpgradeManager>();
            
            BuildButtons();

            overlay.SetActive(false);
            shopPanel.SetActive(false);
        }

        private void BuildButtons()
        {
            ClearButtons();
            if (availablePickaxes == null)
                return;

            for (int i = 0; i < availablePickaxes.Count; i++)
            {
                PickaxeData data = availablePickaxes[i];
                if (data == null)
                    continue;

                int index = i;
                string safeName = string.IsNullOrWhiteSpace(data.displayName) ? $"Pickaxe #{i + 1}" : data.displayName;
                string safeDesc = string.IsNullOrWhiteSpace(data.description) ? "No description." : data.description;

                GameObject item = new GameObject(safeName + "_Item");
                item.transform.SetParent(buttonContainer, false);
                builtItems.Add(item);

                RectTransform itemRt = item.AddComponent<RectTransform>();
                itemRt.anchorMin = new Vector2(0f, 1f);
                itemRt.anchorMax = new Vector2(1f, 1f);
                itemRt.pivot = new Vector2(0.5f, 1f);
                itemRt.sizeDelta = new Vector2(0f, 108f);

                LayoutElement le = item.AddComponent<LayoutElement>();
                le.minHeight = 108f;
                le.preferredHeight = 108f;

                Image img = item.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.06f);

                string summary =
                    $"{safeName}\n" +
                    $"{safeDesc}\n" +
                    $"Power: {Mathf.Max(1, data.miningPower)}    Req Lv: {Mathf.Max(1, data.requiredMiningLevel)}    " +
                    $"Price: ${data.buyPrice}    [{BuildStateLabel(index)}]";

                Text tInfo = RuntimeUIFactory.MakeLabel(item.transform, "Info", summary, 13, TextAnchor.UpperLeft, new Vector2(10f, 6f), new Vector2(-10f, -6f), color: ColText);
                tInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
                tInfo.verticalOverflow = VerticalWrapMode.Truncate;

                Button btn = item.AddComponent<Button>();
                btn.onClick.AddListener(() => TryBuy(index, data));
            }
        }

        private void Update()
        {
            if (shopPanel != null && shopPanel.activeSelf && levelText != null)
                levelText.text = $"Mining level: {GlobalEconomy.MiningLevel} ({GlobalEconomy.MiningXP} XP)";
        }

        public void Toggle()
        {
            EnsureUIBuilt();
            if (shopPanel == null || overlay == null) return;
            BuildButtons();
            bool next = !shopPanel.activeSelf;
            SetPanelVisible(next);
        }

        public void SetPanelVisible(bool visible)
        {
            EnsureUIBuilt();
            if (shopPanel != null) shopPanel.SetActive(visible);
            if (overlay != null) overlay.SetActive(visible);
        }

        private void EnsureUIBuilt()
        {
            if (shopPanel != null && overlay != null)
                return;

            BuildUI();
        }

        private void TryBuy(int index, PickaxeData data)
        {
            if (data == null)
                return;

            if (playerPickaxe != null && playerPickaxe.currentPickaxe == data)
            {
                Debug.Log($"{data.displayName} is already equipped.");
                return;
            }

            if (ownedPickaxeIndices.Contains(index))
            {
                if (playerPickaxe != null)
                    playerPickaxe.currentPickaxe = data;
                PlayerPrefs.SetInt(EquippedPrefsKey, index);
                PlayerPrefs.Save();
                Debug.Log($"Equipped: {data.displayName}");
                BuildButtons();
                return;
            }

            if (GlobalEconomy.MiningLevel < data.requiredMiningLevel)
            {
                Debug.Log($"Need mining level {data.requiredMiningLevel}.");
                return;
            }

            if (GlobalEconomy.Money < data.buyPrice)
            {
                Debug.Log("Not enough money.");
                return;
            }

            var networkAvatar = playerPickaxe != null ? playerPickaxe.GetComponent<Net.NetPlayerAvatar>() : null;
            if (networkAvatar != null && networkAvatar.IsSpawned)
            {
                networkAvatar.AddRewardsServerRpc(-data.buyPrice, 0);
            }
            else
            {
                GlobalEconomy.Money -= data.buyPrice;
            }

            ownedPickaxeIndices.Add(index);
            SaveOwnedState();
            PlayerPrefs.SetInt(EquippedPrefsKey, index);
            PlayerPrefs.Save();

            if (playerPickaxe != null)
                playerPickaxe.currentPickaxe = data;

            Debug.Log($"Bought: {data.displayName}");
            BuildButtons();
            SetPanelVisible(false);
        }

        private void ClearButtons()
        {
            for (int i = 0; i < builtItems.Count; i++)
            {
                if (builtItems[i] != null)
                    Destroy(builtItems[i]);
            }
            builtItems.Clear();
        }

        private string BuildStateLabel(int index)
        {
            PickaxeData equipped = playerPickaxe != null ? playerPickaxe.currentPickaxe : null;
            bool isEquipped = equipped != null && index >= 0 && index < availablePickaxes.Count && availablePickaxes[index] == equipped;
            if (isEquipped) return "EQUIPPED";
            if (ownedPickaxeIndices.Contains(index)) return "OWNED";
            return "BUY";
        }

        private void LoadPickaxeState()
        {
            ownedPickaxeIndices.Clear();

            string json = PlayerPrefs.GetString(OwnedPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    IntListWrapper wrapper = JsonUtility.FromJson<IntListWrapper>(json);
                    if (wrapper != null && wrapper.items != null)
                    {
                        for (int i = 0; i < wrapper.items.Count; i++)
                        {
                            int idx = wrapper.items[i];
                            if (idx >= 0 && availablePickaxes != null && idx < availablePickaxes.Count)
                                ownedPickaxeIndices.Add(idx);
                        }
                    }
                }
                catch { }
            }

            int equippedIndex = PlayerPrefs.GetInt(EquippedPrefsKey, -1);
            if (playerPickaxe != null && equippedIndex >= 0 && availablePickaxes != null && equippedIndex < availablePickaxes.Count)
                playerPickaxe.currentPickaxe = availablePickaxes[equippedIndex];
        }

        private void SaveOwnedState()
        {
            IntListWrapper wrapper = new IntListWrapper();
            foreach (int idx in ownedPickaxeIndices)
                wrapper.items.Add(idx);

            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(OwnedPrefsKey, json);
            PlayerPrefs.Save();
        }

        // (Helper methods removed, now using RuntimeUIFactory)
    }
}
