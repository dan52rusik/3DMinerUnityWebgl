using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    public class MinionShopUI : MonoBehaviour
    {
        private GameObject shopPanel;
        private GameObject overlay;
        private Transform container;

        public bool IsVisible => shopPanel != null && shopPanel.activeSelf;

        private void Start()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            overlay = RuntimeUIFactory.MakePanel("MinionShopOverlay", canvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(10000, 10000), new Color(0, 0, 0, 0.6f));
            shopPanel = RuntimeUIFactory.MakePanel("MinionShopPanel", canvas.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(400, 300));
            
            RuntimeUIFactory.MakeLabel(shopPanel.transform, "Title", "MINION SHOP", 20, TextAnchor.UpperCenter, new Vector2(0, -15));
            
            container = RuntimeUIFactory.MakeScrollContainer(shopPanel.transform, new Vector2(10, 10), new Vector2(-10, -50));
            
            CreateShopItem("Standard Minion", "A small helper to mine for you.", 1000);

            shopPanel.SetActive(false);
            overlay.SetActive(false);
        }

        private void CreateShopItem(string name, string desc, int price)
        {
            GameObject item = new GameObject(name + "_Item");
            item.transform.SetParent(container, false);
            
            RectTransform rt = item.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 80);

            LayoutElement le = item.AddComponent<LayoutElement>();
            le.minHeight = 80; le.preferredHeight = 80;

            Image img = item.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.05f);

            RuntimeUIFactory.MakeLabel(item.transform, "Info", $"{name}\n{desc}\nPrice: ${price}", 13, TextAnchor.MiddleLeft, new Vector2(10, 0), new Vector2(-100, 0));
            
            Button btn = RuntimeUIFactory.MakeBtn(item.transform, "BuyBtn", "BUY", pos: new Vector2(130, 0), size: new Vector2(80, 40));
            btn.onClick.AddListener(() => TryBuyMinion(price));
        }

        private void TryBuyMinion(int price)
        {
            if (GlobalEconomy.Money >= price)
            {
                GlobalEconomy.Money -= price;
                SpawnMinion();
                SetPanelVisible(false);
            }
            else
            {
                Debug.Log("Not enough money for a minion!");
            }
        }

        private void SpawnMinion()
        {
            WellGenerator wellGen = FindFirstObjectByType<WellGenerator>();
            Vector3 spawnPos = Vector3.zero;

            if (wellGen != null)
            {
                // Minion should always be spawned on private island.
                if (wellGen.IsInLobbyMode)
                    wellGen.SwitchToMine();

                VoxelIsland island = wellGen.ActiveIsland;
                if (island != null)
                {
                    float cx = island.TotalX * 0.5f;
                    float cz = island.TotalZ * 0.5f;
                    spawnPos = island.transform.TransformPoint(new Vector3(cx, -wellGen.LobbyFloorY, cz));
                    spawnPos.y += 1.0f;
                }
            }

            if (spawnPos == Vector3.zero)
            {
                PlayerPickaxe player = FindFirstObjectByType<PlayerPickaxe>();
                spawnPos = player != null ? player.transform.position + Vector3.right * 2f : Vector3.zero;
            }

            Debug.Log($"Spawning minion on island at: {spawnPos}");

            GameObject minion = new GameObject("Minion");
            minion.transform.position = spawnPos;

            minion.AddComponent<MinionAI>(); // Visuals are built by MinionAI (BlockyMixCharacter).
            
            // Logic for management UI triggers
            BoxCollider bc = minion.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.9f, 0f);
            bc.size = new Vector3(0.9f, 1.8f, 0.9f);
        }

        public void Toggle()
        {
            if (shopPanel == null) BuildUI();
            if (shopPanel != null) SetPanelVisible(!shopPanel.activeSelf);
        }

        public void SetPanelVisible(bool v)
        {
            if (shopPanel == null && v) BuildUI();
            if (shopPanel != null) shopPanel.SetActive(v);
            if (overlay != null) overlay.SetActive(v);
        }
    }
}
