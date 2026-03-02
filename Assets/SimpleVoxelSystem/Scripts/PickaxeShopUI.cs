using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
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

        private static readonly Color ColPanel = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        private static readonly Color ColText = new Color(0.95f, 0.95f, 0.95f, 1f);

        void Awake()
        {
            if (playerPickaxe == null)
                playerPickaxe = FindFirstObjectByType<PlayerPickaxe>();

            EnsureDefaultPickaxes();
            BuildUI();
        }

        void EnsureDefaultPickaxes()
        {
            if (availablePickaxes != null && availablePickaxes.Count > 0) return;
            availablePickaxes = new List<PickaxeData>
            {
                CreateData("Каменная кирка", "Крепче дерева. Позволяет копать быстрее.", 500, 2, 3, new Color(0.5f, 0.5f, 0.5f)),
                CreateData("Железная кирка", "Надежный инструмент для серьезных руд.", 2000, 5, 7, new Color(0.8f, 0.8f, 0.8f)),
                CreateData("Золотая кирка", "Очень быстрая, но дорогая.", 5000, 10, 12, new Color(1f, 0.9f, 0f)),
                CreateData("Алмазная кирка", "Лучшее, что можно найти.", 15000, 25, 20, new Color(0.3f, 0.9f, 1f))
            };
        }

        PickaxeData CreateData(string n, string d, int p, int pow, int lvl, Color c)
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

        void BuildUI()
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

            overlay = MakePanel("PickaxeOverlay", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(10000f, 10000f), new Color(0f, 0f, 0f, 0.6f));
            shopPanel = MakePanel("PickaxePanel", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(400f, 500f), ColPanel);

            MakeLabel(shopPanel.transform, "Title", "⚒️ МАГАЗИН КИРОК", 20, TextAnchor.UpperCenter).rectTransform.anchoredPosition = new Vector2(0f, -15f);
            levelText = MakeLabel(shopPanel.transform, "LevelInfo", "Ваш уровень копки: 1 (0 XP)", 14, TextAnchor.UpperCenter);
            levelText.rectTransform.anchoredPosition = new Vector2(0f, -45f);

            buttonContainer = MakeScrollContainer(shopPanel.transform);
            BuildButtons();

            overlay.SetActive(false);
            shopPanel.SetActive(false);
        }

        void BuildButtons()
        {
            foreach (var data in availablePickaxes)
            {
                GameObject item = new GameObject(data.displayName + "_Item");
                item.transform.SetParent(buttonContainer, false);

                RectTransform itemRt = item.AddComponent<RectTransform>();
                itemRt.anchorMin = new Vector2(0f, 1f);
                itemRt.anchorMax = new Vector2(1f, 1f);
                itemRt.pivot = new Vector2(0.5f, 1f);
                itemRt.sizeDelta = new Vector2(0f, 88f);

                LayoutElement le = item.AddComponent<LayoutElement>();
                le.minHeight = 88f;
                le.preferredHeight = 88f;

                Image img = item.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.06f);

                MakeLabelRect(item.transform, "Name", data.displayName, 16, TextAnchor.UpperLeft,
                    new Vector2(10f, -26f), new Vector2(-110f, -2f), ColText);

                Text tDesc = MakeLabelRect(item.transform, "Desc", data.description, 11, TextAnchor.UpperLeft,
                    new Vector2(10f, -58f), new Vector2(-110f, -26f), new Color(0.72f, 0.72f, 0.72f));
                tDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
                tDesc.verticalOverflow = VerticalWrapMode.Truncate;

                MakeLabelRect(item.transform, "Lvl", $"Ур. {data.requiredMiningLevel}", 12, TextAnchor.LowerLeft,
                    new Vector2(10f, -4f), new Vector2(-160f, 22f), new Color(0.5f, 0.8f, 1f));

                MakeLabelRect(item.transform, "Price", $"{data.buyPrice}₽", 16, TextAnchor.MiddleRight,
                    new Vector2(-108f, -2f), new Vector2(-10f, 24f), Color.yellow);

                Button btn = item.AddComponent<Button>();
                btn.onClick.AddListener(() => TryBuy(data));
            }
        }

        void Update()
        {
            if (shopPanel != null && shopPanel.activeSelf && levelText != null)
                levelText.text = $"Ваш уровень копки: {GlobalEconomy.MiningLevel} ({GlobalEconomy.MiningXP} XP)";
        }

        public void Toggle()
        {
            EnsureUIBuilt();
            if (shopPanel == null || overlay == null) return;
            bool next = !shopPanel.activeSelf;
            SetPanelVisible(next);
        }

        public void SetPanelVisible(bool visible)
        {
            EnsureUIBuilt();
            if (shopPanel != null) shopPanel.SetActive(visible);
            if (overlay != null) overlay.SetActive(visible);
        }

        void EnsureUIBuilt()
        {
            if (shopPanel != null && overlay != null)
                return;

            BuildUI();
        }

        void TryBuy(PickaxeData data)
        {
            if (GlobalEconomy.Money < data.buyPrice)
            {
                Debug.Log("<color=red>Не хватает денег!</color>");
                return;
            }

            if (GlobalEconomy.MiningLevel < data.requiredMiningLevel)
            {
                Debug.Log($"<color=orange>Нужен уровень {data.requiredMiningLevel}!</color>");
                return;
            }

            // Синхронизация через сервер
            var networkAvatar = playerPickaxe != null ? playerPickaxe.GetComponent<Net.NetPlayerAvatar>() : null;
            if (networkAvatar != null && networkAvatar.IsSpawned)
            {
                networkAvatar.AddRewardsServerRpc(-data.buyPrice, 0);
            }
            else
            {
                GlobalEconomy.Money -= data.buyPrice;
            }

            if (playerPickaxe != null)
                playerPickaxe.currentPickaxe = data;

            Debug.Log($"<color=green>Куплена: {data.displayName}!</color>");
            SetPanelVisible(false);
        }

        Text MakeLabel(Transform parent, string name, string text, int size, TextAnchor align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text t = go.AddComponent<Text>();
            t.font = RuntimeUiFont.Get();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = ColText;
            t.supportRichText = true;
            return t;
        }

        Text MakeLabelRect(Transform parent, string name, string text, int size, TextAnchor align,
            Vector2 offsetTopLeft, Vector2 offsetBottomRight, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(offsetTopLeft.x, offsetBottomRight.y);
            rt.offsetMax = new Vector2(offsetBottomRight.x, offsetTopLeft.y);

            Text t = go.AddComponent<Text>();
            t.font = RuntimeUiFont.Get();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            return t;
        }

        GameObject MakePanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        Transform MakeScrollContainer(Transform parent)
        {
            GameObject go = new GameObject("Container");
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -80f);

            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go.transform;
        }
    }
}
