using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        private static readonly Color ColBtn = new Color(0.25f, 0.45f, 0.25f, 1f);
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
            availablePickaxes = new List<PickaxeData>();

            // Мы можем создать их процедурно, если не хватает ассетов
            availablePickaxes.Add(CreateData("Каменная кирка", "Крепче дерева. Позволяет копать быстрее.", 500, 2, 3, new Color(0.5f, 0.5f, 0.5f)));
            availablePickaxes.Add(CreateData("Железная кирка", "Надежный инструмент для серьезных руд.", 2000, 5, 7, new Color(0.8f, 0.8f, 0.8f)));
            availablePickaxes.Add(CreateData("Золотая кирка", "Очень быстрая, но дорогая.", 5000, 10, 12, new Color(1f, 0.9f, 0f)));
            availablePickaxes.Add(CreateData("Алмазная кирка", "Лучшее, что можно найти.", 15000, 25, 20, new Color(0.3f, 0.9f, 1f)));
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
            if (rootCanvas == null) return;

            overlay = MakePanel("PickaxeOverlay", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(10000, 10000), new Color(0, 0, 0, 0.6f));
            shopPanel = MakePanel("PickaxePanel", rootCanvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(400, 500), ColPanel);

            MakeLabel(shopPanel.transform, "Title", "⚒️ МАГАЗИН КИРОК", 20, TextAnchor.UpperCenter).rectTransform.anchoredPosition = new Vector2(0, -15);
            levelText = MakeLabel(shopPanel.transform, "LevelInfo", "Ваш уровень копки: 1", 14, TextAnchor.UpperCenter);
            levelText.rectTransform.anchoredPosition = new Vector2(0, -45);

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
                item.AddComponent<RectTransform>().sizeDelta = new Vector2(360, 80);
                Image img = item.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0.05f);

                Text tName = MakeLabel(item.transform, "Name", data.displayName, 16, TextAnchor.UpperLeft);
                tName.rectTransform.offsetMin = new Vector2(10, 0);
                tName.rectTransform.offsetMax = new Vector2(-80, -5);

                Text tDesc = MakeLabel(item.transform, "Desc", data.description, 10, TextAnchor.UpperLeft);
                tDesc.rectTransform.offsetMin = new Vector2(10, -30);
                tDesc.rectTransform.offsetMax = new Vector2(-80, -5);
                tDesc.color = new Color(0.7f, 0.7f, 0.7f);

                Text tLvl = MakeLabel(item.transform, "Lvl", $"Ур. {data.requiredMiningLevel}", 12, TextAnchor.LowerLeft);
                tLvl.rectTransform.offsetMin = new Vector2(10, 5);
                tLvl.color = new Color(0.5f, 0.8f, 1f);

                Text tPrice = MakeLabel(item.transform, "Price", $"{data.buyPrice}₽", 16, TextAnchor.MiddleRight);
                tPrice.rectTransform.offsetMax = new Vector2(-10, 0);
                tPrice.color = Color.yellow;

                Button btn = item.AddComponent<Button>();
                btn.onClick.AddListener(() => TryBuy(data));
            }
        }

        void Update()
        {
            if (IsPPressed()) 
            {
                Debug.Log("[PickaxeShopUI] Клавиша P нажата, переключаю панель.");
                Toggle();
            }

            if (shopPanel != null && shopPanel.activeSelf)
            {
                levelText.text = $"Ваш уровень копки: {GlobalEconomy.MiningLevel}  ({GlobalEconomy.MiningXP} XP)";
            }
        }

        private bool IsPPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.pKey.wasPressedThisFrame;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.P);
#else
            // Если никакая система не определена, пробуем легаси (но это может вызвать ошибку, если включен только новый)
            // Чтобы избежать ошибки, просто вернем false.
            return false; 
#endif
        }

        public void Toggle()
        {
            bool next = !shopPanel.activeSelf;
            shopPanel.SetActive(next);
            overlay.SetActive(next);
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

            GlobalEconomy.Money -= data.buyPrice;
            playerPickaxe.currentPickaxe = data;
            Debug.Log($"<color=green>Куплена: {data.displayName}!</color>");
            Toggle();
        }

        // Factory methods
        Text MakeLabel(Transform parent, string name, string text, int size, TextAnchor align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.alignment = align; t.color = ColText;
            t.supportRichText = true;
            return t;
        }

        GameObject MakePanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        Transform MakeScrollContainer(Transform parent)
        {
            GameObject go = new GameObject("Container");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10, 10); rt.offsetMax = new Vector2(-10, -80);
            go.AddComponent<VerticalLayoutGroup>().spacing = 5;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }
    }
}
