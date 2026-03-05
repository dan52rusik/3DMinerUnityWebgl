using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class LobbyEditorUIManager
    {
        private readonly LobbyEditor editor;
        private readonly Transform rootCanvasTrans;
        private Canvas rootCanvas;
        private GameObject editorPanel;
        private Button toggleBtn;
        private List<Button> typeButtons = new List<Button>();
        private Button shopToolBtn;
        private Button pickaxeShopToolBtn;
        private Button sellPointToolBtn;
        private Button minionShopToolBtn;

        private GameObject dialogPanel;
        private InputField inputSizeX, inputSizeY, inputSizeZ;

        private static readonly Color[] BtnColors =
        {
            new Color(0.55f, 0.27f, 0.07f),
            new Color(0.50f, 0.50f, 0.50f),
            new Color(0.65f, 0.44f, 0.40f),
            new Color(1.00f, 0.84f, 0.00f),
        };
        private static readonly BlockType[] BtnTypes =
        {
            BlockType.Dirt, BlockType.Stone, BlockType.Iron, BlockType.Gold
        };
        private static readonly string[] BtnLabels =
        {
            "Dirt", "Stone", "Iron", "Gold"
        };

        public bool IsDialogOpen => dialogPanel != null;

        public LobbyEditorUIManager(LobbyEditor editor, Transform rootCanvasTrans)
        {
            this.editor = editor;
            this.rootCanvasTrans = rootCanvasTrans;
        }

        public void BuildUI(System.Action toggleAction, System.Action<BlockType> setBlockAction, 
            System.Action<EditorToolMode> setToolAction, System.Action saveAction, System.Action clearAction)
        {
            // Clear old panels (if left from past launches or errors)
            var oldPanel = rootCanvasTrans.Find("LobbyEditorPanel");
            if (oldPanel != null) GameObject.DestroyImmediate(oldPanel.gameObject);
            var oldToggle = rootCanvasTrans.Find("LobbyEditToggle");
            if (oldToggle != null) GameObject.DestroyImmediate(oldToggle.gameObject);

            Debug.Log("[LobbyEditor] Building UI with Minion Shop tool included.");

            // Toggle button
            toggleBtn = RuntimeUIFactory.MakeBtn(rootCanvasTrans, "LobbyEditToggle",
                "Lobby Editor [F2]",
                new Color(0.25f, 0.65f, 0.25f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -110f), new Vector2(168f, 36f));
            toggleBtn.onClick.AddListener(() => toggleAction());

            // Tools panel
            editorPanel = RuntimeUIFactory.MakePanel("LobbyEditorPanel", rootCanvasTrans,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(168f, 580f),
                new Color(0.07f, 0.07f, 0.11f, 0.93f));

            RuntimeUIFactory.MakeLabel(editorPanel.transform, "EdTitle",
                "LOBBY EDITOR", 13, TextAnchor.UpperCenter,
                new Vector2(4, -30), new Vector2(-4, 0), bold: true);
            RuntimeUIFactory.MakeLabel(editorPanel.transform, "EdHint",
                "LMB - place\nRMB - remove",
                11, TextAnchor.UpperCenter,
                new Vector2(4, -54), new Vector2(-4, -32));

            float currentY = -86f;
            float stepY = 46f;

            // Block tools
            for (int i = 0; i < BtnTypes.Length; i++)
            {
                int idx = i;
                Color c = BtnColors[i];
                Button btn = RuntimeUIFactory.MakeBtn(editorPanel.transform, $"BType_{i}",
                    BtnLabels[i],
                    new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, currentY), new Vector2(148f, 38f));
                btn.onClick.AddListener(() =>
                {
                    setBlockAction(BtnTypes[idx]);
                    setToolAction(EditorToolMode.Block);
                });
                typeButtons.Add(btn);
                currentY -= stepY;
            }

            // Shops
            shopToolBtn = RuntimeUIFactory.MakeBtn(editorPanel.transform, "ShopTool", "Mine Shop Zone",
                new Color(0.15f, 0.35f, 0.80f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, currentY), new Vector2(148f, 38f));
            shopToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.Shop));
            currentY -= stepY;

            pickaxeShopToolBtn = RuntimeUIFactory.MakeBtn(editorPanel.transform, "PickaxeShopTool", "Pickaxe Zone",
                new Color(0.25f, 0.45f, 0.25f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, currentY), new Vector2(148f, 38f));
            pickaxeShopToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.PickaxeShop));
            currentY -= stepY;

            sellPointToolBtn = RuntimeUIFactory.MakeBtn(editorPanel.transform, "SellPointTool", "Sell Point",
                new Color(0.60f, 0.45f, 0.12f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, currentY), new Vector2(148f, 38f));
            sellPointToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.SellPoint));
            currentY -= stepY;

            minionShopToolBtn = RuntimeUIFactory.MakeBtn(editorPanel.transform, "MinionShopTool", "!!! MINION SHOP !!!",
                new Color(0f, 0.8f, 1f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, currentY), new Vector2(148f, 38f));
            minionShopToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.MinionShop));
            currentY -= stepY;

            // Controls
            Button saveBtn = RuntimeUIFactory.MakeBtn(editorPanel.transform, "ManualSaveBtn", "Save",
                new Color(0.2f, 0.45f, 0.9f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(148f, 38f));
            saveBtn.onClick.AddListener(() => saveAction());

            Button clearBtn = RuntimeUIFactory.MakeBtn(editorPanel.transform, "ClearLobbyBtn", "Clear Lobby",
                new Color(0.6f, 0.2f, 0.2f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(148f, 34f));
            clearBtn.onClick.AddListener(() => clearAction());

            editorPanel.SetActive(false);
        }

        public void RefreshUI(bool isEditMode, EditorToolMode toolMode, BlockType selectedBlockType)
        {
            if (editorPanel != null) 
            {
                editorPanel.SetActive(isEditMode);
                GameUIWindow.SetWindowActive(editorPanel, isEditMode);
            }
            if (toggleBtn != null)
            {
                var img = toggleBtn.GetComponent<Image>();
                if (img != null)
                    img.color = isEditMode ? new Color(0.9f, 0.55f, 0.1f, 1f)
                                           : new Color(0.25f, 0.65f, 0.25f, 1f);
            }

            for (int i = 0; i < typeButtons.Count && i < BtnTypes.Length; i++)
            {
                bool sel = toolMode == EditorToolMode.Block && BtnTypes[i] == selectedBlockType;
                var img = typeButtons[i].GetComponent<Image>();
                Color c = BtnColors[i];
                if (img != null) img.color = sel ? Color.white : new Color(c.r*0.65f, c.g*0.65f, c.b*0.65f, 1f);
                var txt = typeButtons[i].GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }

            SetToolBtnColor(shopToolBtn, toolMode == EditorToolMode.Shop, new Color(0.15f, 0.35f, 0.80f, 1f));
            SetToolBtnColor(pickaxeShopToolBtn, toolMode == EditorToolMode.PickaxeShop, new Color(0.25f, 0.45f, 0.25f, 1f));
            SetToolBtnColor(sellPointToolBtn, toolMode == EditorToolMode.SellPoint, new Color(0.60f, 0.45f, 0.12f, 1f));
            SetToolBtnColor(minionShopToolBtn, toolMode == EditorToolMode.MinionShop, new Color(0.15f, 0.65f, 0.85f, 1f));
        }

        private void SetToolBtnColor(Button btn, bool selected, Color defaultColor)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = selected ? Color.white : defaultColor;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.color = selected ? Color.black : Color.white;
        }

        public void OpenSizeDialog(Vector3 worldPos, ShopZoneType type, System.Action<Vector3, int, int, int, ShopZoneType> onConfirm)
        {
            if (dialogPanel != null) { GameObject.Destroy(dialogPanel); }

            dialogPanel = RuntimeUIFactory.MakePanel("ShopSizeDialog", rootCanvasTrans,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(320f, 260f),
                new Color(0.08f, 0.08f, 0.14f, 0.97f));
            
            GameUIWindow.SetWindowActive(dialogPanel, true);

            string title = "MINE SHOP";
            if (type == ShopZoneType.Pickaxe) title = "PICKAXE SHOP";
            else if (type == ShopZoneType.Sell) title = "SELL POINT";
            else if (type == ShopZoneType.Minion) title = "MINION SHOP";

            RuntimeUIFactory.MakeLabel(dialogPanel.transform, "DlgTitle",
                $"{title}\nEnter zone size:", 14, TextAnchor.UpperCenter,
                new Vector2(10, -36), new Vector2(-10, 0), bold: true);

            float y = -95f;
            inputSizeX = RuntimeUIFactory.MakeInputField(dialogPanel.transform, "InputX", "Width X (blocks):", ref y);
            inputSizeY = RuntimeUIFactory.MakeInputField(dialogPanel.transform, "InputY", "Height Y (blocks):", ref y);
            inputSizeZ = RuntimeUIFactory.MakeInputField(dialogPanel.transform, "InputZ", "Length Z (blocks):", ref y);

            inputSizeX.text = "3"; inputSizeY.text = "3"; inputSizeZ.text = "3";

            Button okBtn = RuntimeUIFactory.MakeBtn(dialogPanel.transform, "OkBtn", "Place Zone",
                new Color(0.2f, 0.65f, 0.3f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-80f, 14f), new Vector2(148f, 36f));
            okBtn.onClick.AddListener(() => {
                int sx = Mathf.Max(1, int.Parse(inputSizeX.text));
                int sy = Mathf.Max(1, int.Parse(inputSizeY.text));
                int sz = Mathf.Max(1, int.Parse(inputSizeZ.text));
                onConfirm(worldPos, sx, sy, sz, type);
                CloseDialog();
            });

            Button cancelBtn = RuntimeUIFactory.MakeBtn(dialogPanel.transform, "CancelBtn", "Cancel",
                new Color(0.6f, 0.2f, 0.2f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(80f, 14f), new Vector2(148f, 36f));
            cancelBtn.onClick.AddListener(CloseDialog);
        }

        public void CloseDialog()
        {
            if (dialogPanel != null) 
            { 
                GameUIWindow.SetWindowActive(dialogPanel, false);
                GameObject.Destroy(dialogPanel); 
                dialogPanel = null; 
            }
        }
    }

}
