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
            // Кнопка-тоггл
            toggleBtn = LobbyEditorFactory.MakeBtn(rootCanvasTrans, "LobbyEditToggle",
                "Lobby Editor [F2]",
                new Color(0.25f, 0.65f, 0.25f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -110f), new Vector2(168f, 36f));
            toggleBtn.onClick.AddListener(() => toggleAction());

            // Панель инструментов
            editorPanel = LobbyEditorFactory.MakePanel("LobbyEditorPanel", rootCanvasTrans,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(168f, 500f),
                new Color(0.07f, 0.07f, 0.11f, 0.93f));

            LobbyEditorFactory.MakeLabelOff(editorPanel.transform, "EdTitle",
                "LOBBY EDITOR", 13, TextAnchor.UpperCenter,
                new Vector2(4, -30), new Vector2(-4, 0), bold: true);
            LobbyEditorFactory.MakeLabelOff(editorPanel.transform, "EdHint",
                "LMB - place\nRMB - remove",
                11, TextAnchor.UpperCenter,
                new Vector2(4, -54), new Vector2(-4, -32));

            // Блочные инструменты
            for (int i = 0; i < BtnTypes.Length; i++)
            {
                int idx = i;
                Color c = BtnColors[i];
                Button btn = LobbyEditorFactory.MakeBtn(editorPanel.transform, $"BType_{i}",
                    BtnLabels[i],
                    new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -(86f + i * 46f)), new Vector2(148f, 38f));
                btn.onClick.AddListener(() =>
                {
                    setBlockAction(BtnTypes[idx]);
                    setToolAction(EditorToolMode.Block);
                });
                typeButtons.Add(btn);
            }

            // Магазины
            shopToolBtn = LobbyEditorFactory.MakeBtn(editorPanel.transform, "ShopTool", "Mine Shop Zone",
                new Color(0.15f, 0.35f, 0.80f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + BtnTypes.Length * 46f)), new Vector2(148f, 38f));
            shopToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.Shop));

            pickaxeShopToolBtn = LobbyEditorFactory.MakeBtn(editorPanel.transform, "PickaxeShopTool", "Pickaxe Zone",
                new Color(0.25f, 0.45f, 0.25f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + (BtnTypes.Length + 1) * 46f)), new Vector2(148f, 38f));
            pickaxeShopToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.PickaxeShop));

            sellPointToolBtn = LobbyEditorFactory.MakeBtn(editorPanel.transform, "SellPointTool", "Sell Point",
                new Color(0.60f, 0.45f, 0.12f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + (BtnTypes.Length + 2) * 46f)), new Vector2(148f, 38f));
            sellPointToolBtn.onClick.AddListener(() => setToolAction(EditorToolMode.SellPoint));

            // Управление
            Button saveBtn = LobbyEditorFactory.MakeBtn(editorPanel.transform, "ManualSaveBtn", "Save",
                new Color(0.2f, 0.45f, 0.9f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(148f, 38f));
            saveBtn.onClick.AddListener(() => saveAction());

            Button clearBtn = LobbyEditorFactory.MakeBtn(editorPanel.transform, "ClearLobbyBtn", "Clear Lobby",
                new Color(0.6f, 0.2f, 0.2f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 56f), new Vector2(148f, 34f));
            clearBtn.onClick.AddListener(() => clearAction());

            editorPanel.SetActive(false);
        }

        public void RefreshUI(bool isEditMode, EditorToolMode toolMode, BlockType selectedBlockType)
        {
            if (editorPanel != null) editorPanel.SetActive(isEditMode);
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

            dialogPanel = LobbyEditorFactory.MakePanel("ShopSizeDialog", rootCanvasTrans,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(320f, 260f),
                new Color(0.08f, 0.08f, 0.14f, 0.97f));

            string title = type == ShopZoneType.Mine ? "MINE SHOP" : (type == ShopZoneType.Pickaxe ? "PICKAXE SHOP" : "SELL POINT");
            LobbyEditorFactory.MakeLabelOff(dialogPanel.transform, "DlgTitle",
                $"{title}\nEnter zone size:", 14, TextAnchor.UpperCenter,
                new Vector2(10, -36), new Vector2(-10, 0), bold: true);

            float y = -95f;
            inputSizeX = LobbyEditorFactory.MakeInputField(dialogPanel.transform, "InputX", "Width X (blocks):", ref y);
            inputSizeY = LobbyEditorFactory.MakeInputField(dialogPanel.transform, "InputY", "Height Y (blocks):", ref y);
            inputSizeZ = LobbyEditorFactory.MakeInputField(dialogPanel.transform, "InputZ", "Length Z (blocks):", ref y);

            inputSizeX.text = "3"; inputSizeY.text = "3"; inputSizeZ.text = "3";

            Button okBtn = LobbyEditorFactory.MakeBtn(dialogPanel.transform, "OkBtn", "Place Zone",
                new Color(0.2f, 0.65f, 0.3f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-80f, 14f), new Vector2(148f, 36f));
            okBtn.onClick.AddListener(() => {
                int sx = Mathf.Max(1, int.Parse(inputSizeX.text));
                int sy = Mathf.Max(1, int.Parse(inputSizeY.text));
                int sz = Mathf.Max(1, int.Parse(inputSizeZ.text));
                onConfirm(worldPos, sx, sy, sz, type);
                CloseDialog();
            });

            Button cancelBtn = LobbyEditorFactory.MakeBtn(dialogPanel.transform, "CancelBtn", "Cancel",
                new Color(0.6f, 0.2f, 0.2f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(80f, 14f), new Vector2(148f, 36f));
            cancelBtn.onClick.AddListener(CloseDialog);
        }

        public void CloseDialog()
        {
            if (dialogPanel != null) { GameObject.Destroy(dialogPanel); dialogPanel = null; }
        }
    }

    public static class LobbyEditorFactory
    {
        private static Font _font;
        private static Font GetFont() => _font ??= RuntimeUiFont.Get();

        public static GameObject MakePanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        public static Button MakeBtn(Transform parent, string name, string label, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = color;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var tGo = new GameObject("Label");
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = tGo.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white; txt.text = label;
            return btn;
        }

        public static Text MakeLabelOff(Transform parent, string name, string text, int fontSize, TextAnchor align, Vector2 offsetMin, Vector2 offsetMax, bool bold = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var txt = go.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = fontSize;
            txt.alignment = align; txt.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            txt.text = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }

        public static InputField MakeInputField(Transform parent, string name, string label, ref float offsetY)
        {
            var lGo = new GameObject(name + "_Label");
            lGo.transform.SetParent(parent, false);
            var lrt = lGo.AddComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f); lrt.anchoredPosition = new Vector2(0f, offsetY);
            lrt.sizeDelta = new Vector2(280f, 22f);
            var lt = lGo.AddComponent<Text>();
            lt.font = GetFont(); lt.fontSize = 12;
            lt.alignment = TextAnchor.MiddleLeft; lt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            lt.text = label;

            offsetY -= 26f;
            var iGo = new GameObject(name);
            iGo.transform.SetParent(parent, false);
            var irt = iGo.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f); irt.anchoredPosition = new Vector2(0f, offsetY);
            irt.sizeDelta = new Vector2(280f, 32f);
            var bg = iGo.AddComponent<Image>(); bg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(iGo.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 2); trt.offsetMax = new Vector2(-6, -2);
            var txt = tGo.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleLeft; txt.color = Color.white;
            txt.supportRichText = false;

            var field = iGo.AddComponent<InputField>();
            field.textComponent = txt; field.contentType = InputField.ContentType.IntegerNumber;
            field.targetGraphic = bg;

            offsetY -= 36f;
            return field;
        }
    }
}
