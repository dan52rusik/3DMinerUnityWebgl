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
        private Text titleText;
        private Text levelText;
        private Transform buttonContainer;
        private UpgradeManager upgradeManager;

        private readonly List<GameObject> builtItems = new List<GameObject>();
        private readonly List<PickaxeData> runtimeGeneratedPickaxes = new List<PickaxeData>();
        private readonly HashSet<int> ownedPickaxeIndices = new HashSet<int>();
        private bool buttonsDirty = true;
        private bool usingRuntimeLocalizedPickaxes;

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

        private void OnEnable()
        {
            Loc.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= HandleLanguageChanged;
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
                CreateData(Loc.T("pickaxe_stone_name"), Loc.T("pickaxe_stone_desc"), EconomyTuning.StonePickaxePrice, EconomyTuning.StonePickaxePower, EconomyTuning.StonePickaxeRequiredLevel, new Color(0.5f, 0.5f, 0.5f)),
                CreateData(Loc.T("pickaxe_iron_name"), Loc.T("pickaxe_iron_desc"), EconomyTuning.IronPickaxePrice, EconomyTuning.IronPickaxePower, EconomyTuning.IronPickaxeRequiredLevel, new Color(0.8f, 0.8f, 0.8f)),
                CreateData(Loc.T("pickaxe_gold_name"), Loc.T("pickaxe_gold_desc"), EconomyTuning.GoldPickaxePrice, EconomyTuning.GoldPickaxePower, EconomyTuning.GoldPickaxeRequiredLevel, new Color(1f, 0.9f, 0f)),
                CreateData(Loc.T("pickaxe_diamond_name"), Loc.T("pickaxe_diamond_desc"), EconomyTuning.DiamondPickaxePrice, EconomyTuning.DiamondPickaxePower, EconomyTuning.DiamondPickaxeRequiredLevel, new Color(0.3f, 0.9f, 1f))
            };
            usingRuntimeLocalizedPickaxes = true;
        }

        private PickaxeData CreateData(string n, string d, int p, int pow, int lvl, Color c)
        {
            PickaxeData data = ScriptableObject.CreateInstance<PickaxeData>();
            data.hideFlags = HideFlags.DontSave;
            data.displayName = n;
            data.description = d;
            data.buyPrice = p;
            data.miningPower = pow;
            data.requiredMiningLevel = lvl;
            data.iconColor = c;
            runtimeGeneratedPickaxes.Add(data);
            return data;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeGeneratedPickaxes.Count; i++)
            {
                PickaxeData data = runtimeGeneratedPickaxes[i];
                if (data != null)
                    Destroy(data);
            }
            runtimeGeneratedPickaxes.Clear();
        }

        private void BuildUI()
        {
            // Create dedicated PickaxeShopCanvas at layer 4000
            GameObject pGo = new GameObject("PickaxeShopCanvas");
            rootCanvas = pGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 4000;
            rootCanvas.pixelPerfect = true;
            pGo.AddComponent<GraphicRaycaster>();
            
            CanvasScaler ps = pGo.AddComponent<CanvasScaler>();
            ps.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2(1600f, 900f);
            ps.matchWidthOrHeight = 0.5f;



            overlay = RuntimeUIFactory.MakePanel("PickaxeOverlay", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(10000f, 10000f), new Color(0f, 0f, 0f, 0.6f));
            shopPanel = RuntimeUIFactory.MakePanel("PickaxePanel", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(420f, 520f), ColPanel);
            RuntimeUIFactory.EnableAdaptivePanelScale(shopPanel, 0.94f, 0.90f, 0.52f);

            titleText = RuntimeUIFactory.MakeLabel(shopPanel.transform, "Title", Loc.T("pickaxe_shop_title"), 22, TextAnchor.UpperCenter);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -15f);

            levelText = RuntimeUIFactory.MakeLabel(shopPanel.transform, "LevelInfo", BuildLevelText(), 16, TextAnchor.UpperCenter);
            levelText.rectTransform.anchoredPosition = new Vector2(0f, -45f);

            Button closeBtn = RuntimeUIFactory.MakeBtn(shopPanel.transform, "CloseBtn", "X",
                new Color(0.78f, 0.22f, 0.22f, 0.95f),
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                pos: new Vector2(-8f, -8f), size: new Vector2(34f, 34f));
            closeBtn.onClick.AddListener(() => SetPanelVisible(false));

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
                    $"{Loc.T("stats_power")}: {Mathf.Max(1, data.miningPower)}    {Loc.T("stats_req_lv")}: {Mathf.Max(1, data.requiredMiningLevel)}    " +
                    $"{Loc.T("stats_price")}: ${data.buyPrice}    [{BuildStateLabel(index)}]";

                Text tInfo = RuntimeUIFactory.MakeLabel(item.transform, "Info", summary, 14, TextAnchor.UpperLeft, new Vector2(10f, 6f), new Vector2(-10f, -6f), color: ColText);
                tInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
                tInfo.verticalOverflow = VerticalWrapMode.Truncate;

                Button btn = item.AddComponent<Button>();
                btn.onClick.AddListener(() => TryBuy(index, data));
            }

            buttonsDirty = false;
        }

        private void Update()
        {
            if (shopPanel != null && shopPanel.activeSelf && levelText != null)
                levelText.text = BuildLevelText();
        }

        public void Toggle()
        {
            EnsureUIBuilt();
            if (shopPanel == null || overlay == null) return;
            if (buttonsDirty)
                BuildButtons();
            bool next = !shopPanel.activeSelf;
            SetPanelVisible(next);
        }

        public void SetPanelVisible(bool visible)
        {
            EnsureUIBuilt();
            if (visible && buttonsDirty)
                BuildButtons();
            if (shopPanel != null)
            {
                shopPanel.SetActive(visible);
                GameUIWindow.SetWindowActive(shopPanel, visible);
            }
            if (overlay != null) overlay.SetActive(visible);
        }

        private void EnsureUIBuilt()
        {
            if (shopPanel != null && overlay != null)
                return;

            BuildUI();
        }

        private void HandleLanguageChanged()
        {
            if (usingRuntimeLocalizedPickaxes)
                RebuildRuntimePickaxes();

            if (titleText != null)
                titleText.text = Loc.T("pickaxe_shop_title");

            if (levelText != null)
                levelText.text = BuildLevelText();

            buttonsDirty = true;
            if (shopPanel != null && shopPanel.activeSelf)
                BuildButtons();
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
                buttonsDirty = true;
                if (shopPanel != null && shopPanel.activeSelf)
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

            GlobalEconomy.Money -= data.buyPrice;

            ownedPickaxeIndices.Add(index);
            SaveOwnedState();
            PlayerPrefs.SetInt(EquippedPrefsKey, index);
            PlayerPrefs.Save();

            if (playerPickaxe != null)
                playerPickaxe.currentPickaxe = data;

            Debug.Log($"Bought: {data.displayName}");
            buttonsDirty = true;
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
            if (isEquipped) return Loc.T("btn_equipped");
            if (ownedPickaxeIndices.Contains(index)) return Loc.T("btn_owned");
            return Loc.T("btn_buy");
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

        private void RebuildRuntimePickaxes()
        {
            int equippedIndex = -1;
            if (playerPickaxe != null && playerPickaxe.currentPickaxe != null && availablePickaxes != null)
                equippedIndex = availablePickaxes.IndexOf(playerPickaxe.currentPickaxe);

            for (int i = 0; i < runtimeGeneratedPickaxes.Count; i++)
            {
                if (runtimeGeneratedPickaxes[i] != null)
                    Destroy(runtimeGeneratedPickaxes[i]);
            }
            runtimeGeneratedPickaxes.Clear();
            availablePickaxes = null;

            EnsureDefaultPickaxes();

            if (playerPickaxe != null && equippedIndex >= 0 && availablePickaxes != null && equippedIndex < availablePickaxes.Count)
                playerPickaxe.currentPickaxe = availablePickaxes[equippedIndex];
        }

        private static string BuildLevelText()
        {
            return Loc.Tf(
                "mining_level_format",
                Loc.T("lv_short"),
                GlobalEconomy.MiningLevel,
                GlobalEconomy.MiningXP,
                Loc.T("xp_short"));
        }

        // (Helper methods removed, now using RuntimeUIFactory)
    }
}
