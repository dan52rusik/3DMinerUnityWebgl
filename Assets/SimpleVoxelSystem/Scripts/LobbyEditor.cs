using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SimpleVoxelSystem.Data;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    // ─── Данные сохранения вокселей ───────────────────────────────────────────

    [System.Serializable]
    public class LobbyVoxelEntry
    {
        public int x, y, z;
        public int blockTypeId; // -1 = удалён из пола
    }

    [System.Serializable]
    public class LobbyLayoutSaveData
    {
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    // ─── Данные сохранения зон магазина ──────────────────────────────────────

    [System.Serializable]
    public class ShopZoneEntry
    {
        public float worldX, worldY, worldZ;
        public int   sizeX, sizeY, sizeZ;
    }

    [System.Serializable]
    public class ShopZoneSaveData
    {
        public List<ShopZoneEntry> zones = new List<ShopZoneEntry>();
    }

    // ─── Режим инструмента ────────────────────────────────────────────────────

    public enum EditorToolMode { Block, Shop }

    // ─── Основной скрипт редактора ────────────────────────────────────────────

    /// <summary>
    /// Редактор лобби-площадки.
    /// F2 — включить/выключить.
    /// В режиме Block: ЛКМ ставит воксель, ПКМ удаляет.
    /// В режиме Shop:  ЛКМ открывает диалог размера → ставит невидимый триггер-куб.
    /// </summary>
    public class LobbyEditor : MonoBehaviour
    {
        [Header("Ссылки")]
        public WellGenerator wellGenerator;
        public Camera        editorCamera;

        [Header("Горячая клавиша")]
        public KeyCode toggleKey = KeyCode.F2;

        [Header("Дальность")]
        public float placementRange = 200f;
        public LayerMask miningLayers = Physics.DefaultRaycastLayers;

        public Color previewColorPlace  = new Color(0.2f, 1f, 0.5f,  0.40f);
        public Color previewColorRemove = new Color(1f,   0.2f, 0.2f, 0.40f);
        public Color previewColorShop   = new Color(0.3f, 0.6f, 1.0f, 0.45f);

        // ─── Runtime ─────────────────────────────────────────────────────────
        public bool          IsEditMode   { get; private set; }
        public EditorToolMode ToolMode    { get; private set; } = EditorToolMode.Block;

        private BlockType   selectedBlockType = BlockType.Stone;
        private VoxelIsland island;
        private GameObject  previewCube;
        private Vector3Int? pendingPlacePos;
        private Vector3Int? pendingRemovePos;
        private Vector3?    pendingShopWorldPos;  // мировая позиция для shop
        private ShopZone    hoveredZone;          // зона под курсором

        // ─── Сохранение вокселей ─────────────────────────────────────────────
        private static string VoxelSavePath =>
            Path.Combine(Application.persistentDataPath, "lobby_layout.json");
        private LobbyLayoutSaveData saveData = new LobbyLayoutSaveData();

        // ─── Сохранение зон магазина ─────────────────────────────────────────
        private static string ShopSavePath =>
            Path.Combine(Application.persistentDataPath, "lobby_shopzones.json");
        private ShopZoneSaveData shopSaveData = new ShopZoneSaveData();
        private readonly List<ShopZone> spawnedZones = new List<ShopZone>();

        // ─── UI ──────────────────────────────────────────────────────────────
        private Canvas     rootCanvas;
        private GameObject editorPanel;
        private Button     toggleBtn;
        private readonly List<Button> typeButtons = new List<Button>();
        private Button     shopToolBtn;

        // Диалог размера зоны
        private GameObject dialogPanel;
        private InputField  inputSizeX, inputSizeY, inputSizeZ;
        private bool        dialogOpen;

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
            "Земля", "Камень", "Железо", "Золото"
        };

        // ══════════════════════════════════════════════════════════════════════
        // Unity
        // ══════════════════════════════════════════════════════════════════════

        void Awake()
        {
            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (editorCamera == null)
                editorCamera = Camera.main;
            if (wellGenerator != null)
                wellGenerator.OnFlatPlotReady += OnFlatPlotReady;
            BuildUI();
        }

        void Start()
        {
            if (wellGenerator != null)
                island = wellGenerator.GetComponent<VoxelIsland>();
        }

        void OnDestroy()
        {
            if (wellGenerator != null)
                wellGenerator.OnFlatPlotReady -= OnFlatPlotReady;
        }

        void Update()
        {
            if (IsToggleKeyDown()) ToggleEditMode();
            if (!IsEditMode) { HidePreview(); return; }
            if (dialogOpen) return; // Ввод диалога блокирует всё остальное

            UpdateHover();
            HandleInput();
        }

        private void OnFlatPlotReady()
        {
            if (wellGenerator != null)
                island = wellGenerator.GetComponent<VoxelIsland>();
            LoadAndApplyLayout();
            LoadAndApplyShopZones();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Hover
        // ══════════════════════════════════════════════════════════════════════

        void UpdateHover()
        {
            pendingPlacePos  = null;
            pendingRemovePos = null;
            pendingShopWorldPos = null;

            if (editorCamera == null || island == null) { HidePreview(); return; }

            Ray ray = editorCamera.ScreenPointToRay(GetPointerPos());
            if (!Physics.Raycast(ray, out RaycastHit hit, placementRange, miningLayers,
                                  QueryTriggerInteraction.Ignore))
            { HidePreview(); return; }

            VoxelIsland hitIsland = hit.collider.GetComponentInParent<VoxelIsland>();
            if (hitIsland != island) { HidePreview(); return; }

            bool rmb = IsRightHeld();

            if (ToolMode == EditorToolMode.Shop)
            {
                // В Shop-режиме проверяем триггеры через отдельный рейкаст
                ShopZone newHovered = null;

                if (IsRightHeld())
                {
                    // ПКМ: ищем зону для удаления через trigger raycast
                    if (Physics.Raycast(ray, out RaycastHit trigHit, placementRange, miningLayers,
                                        QueryTriggerInteraction.Collide))
                    {
                        newHovered = trigHit.collider.GetComponentInParent<ShopZone>();
                    }
                    if (newHovered != null)
                    {
                        HidePreview();
                        pendingShopWorldPos = null;
                    }
                    else
                    {
                        // Наводим на пол — показываем превью для новой зоны
                        VoxelIsland hi = hit.collider.GetComponentInParent<VoxelIsland>();
                        if (hi == island)
                        {
                            Vector3 lp = island.transform.InverseTransformPoint(hit.point + hit.normal * 0.5f);
                            int px = Mathf.FloorToInt(lp.x), py = -Mathf.FloorToInt(lp.y), pz = Mathf.FloorToInt(lp.z);
                            pendingShopWorldPos = island.transform.TransformPoint(new Vector3(px + 0.5f, -py + 0.5f, pz + 0.5f));
                        }
                    }
                }
                else
                {
                    // ЛКМ: ставим зону на пол
                    VoxelIsland hi = hit.collider.GetComponentInParent<VoxelIsland>();
                    if (hi == island)
                    {
                        Vector3 lp = island.transform.InverseTransformPoint(hit.point + hit.normal * 0.5f);
                        int px = Mathf.FloorToInt(lp.x), py = -Mathf.FloorToInt(lp.y), pz = Mathf.FloorToInt(lp.z);
                        pendingShopWorldPos = island.transform.TransformPoint(new Vector3(px + 0.5f, -py + 0.5f, pz + 0.5f));
                        ShowPreview(new Vector3(px, -py, pz), previewColorShop);
                    }
                }

                // Обновляем hover зоны
                if (hoveredZone != newHovered)
                {
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(false);
                    hoveredZone = newHovered;
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(true);
                }
            }
            else if (rmb)
            {
                Vector3 lp = island.transform.InverseTransformPoint(hit.point - hit.normal * 0.5f);
                int rx = Mathf.FloorToInt(lp.x);
                int ry = -Mathf.FloorToInt(lp.y);
                int rz = Mathf.FloorToInt(lp.z);
                if (island.IsSolid(rx, ry, rz))
                {
                    pendingRemovePos = new Vector3Int(rx, ry, rz);
                    ShowPreview(new Vector3(rx, -ry, rz), previewColorRemove);
                }
                else HidePreview();
            }
            else
            {
                Vector3 lp = island.transform.InverseTransformPoint(hit.point + hit.normal * 0.5f);
                int px = Mathf.FloorToInt(lp.x);
                int py = -Mathf.FloorToInt(lp.y);
                int pz = Mathf.FloorToInt(lp.z);
                pendingPlacePos = new Vector3Int(px, py, pz);
                Color bc = BtnColors[(int)selectedBlockType];
                ShowPreview(new Vector3(px, -py, pz),
                    new Color(bc.r, bc.g, bc.b, 0.45f));
            }
        }

        void HandleInput()
        {
            if (ToolMode == EditorToolMode.Shop)
            {
                if (IsRightJustPressed() && hoveredZone != null)
                    DeleteShopZone(hoveredZone);
                else if (IsLeftJustPressed() && pendingShopWorldPos.HasValue)
                    OpenSizeDialog(pendingShopWorldPos.Value);
            }
            else
            {
                if (IsLeftJustPressed()  && pendingPlacePos.HasValue)  PlaceBlock(pendingPlacePos.Value);
                if (IsRightJustPressed() && pendingRemovePos.HasValue) RemoveBlock(pendingRemovePos.Value);
            }
        }

        void DeleteShopZone(ShopZone zone)
        {
            int idx = spawnedZones.IndexOf(zone);
            if (idx >= 0)
            {
                spawnedZones.RemoveAt(idx);
                shopSaveData.zones.RemoveAt(idx);
                SaveShopZones();
            }
            hoveredZone = null;
            Destroy(zone.gameObject);
            Debug.Log("[LobbyEditor] Зона магазина удалена.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Размещение вокселей
        // ══════════════════════════════════════════════════════════════════════

        void PlaceBlock(Vector3Int pos)
        {
            if (island == null || island.IsSolid(pos.x, pos.y, pos.z)) return;
            island.SetVoxel(pos.x, pos.y, pos.z, selectedBlockType);
            island.RebuildMesh();
            UpsertSave(pos.x, pos.y, pos.z, (int)selectedBlockType);
            SaveLayout();
        }

        void RemoveBlock(Vector3Int pos)
        {
            if (island == null || !island.IsSolid(pos.x, pos.y, pos.z)) return;
            island.RemoveVoxel(pos.x, pos.y, pos.z, true);
            int floorY = wellGenerator != null ? wellGenerator.LobbyFloorY : 0;
            if (pos.y == floorY)
                UpsertSave(pos.x, pos.y, pos.z, -1);
            else
                saveData.entries.RemoveAll(e => e.x == pos.x && e.y == pos.y && e.z == pos.z);
            SaveLayout();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Диалог размера Shop-зоны
        // ══════════════════════════════════════════════════════════════════════

        void OpenSizeDialog(Vector3 worldPos)
        {
            dialogOpen = true;
            HidePreview();

            if (dialogPanel != null) { Destroy(dialogPanel); }

            // Создаём панель диалога по центру экрана
            dialogPanel = MakePanel("ShopSizeDialog", rootCanvas.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(320f, 260f),
                new Color(0.08f, 0.08f, 0.14f, 0.97f));

            // Заголовок
            MakeLabelOff(dialogPanel.transform, "DlgTitle",
                "🛒 ЗОНА МАГАЗИНА\nВведите размер:", 14, TextAnchor.UpperCenter,
                new Vector2(10, -36), new Vector2(-10, 0), bold: true);

            // Поля ввода
            float y = -95f;
            inputSizeX = MakeInputField(dialogPanel.transform, "InputX", "Ширина X (блоков):", ref y);
            inputSizeY = MakeInputField(dialogPanel.transform, "InputY", "Высота Y (блоков):", ref y);
            inputSizeZ = MakeInputField(dialogPanel.transform, "InputZ", "Длина  Z (блоков):", ref y);

            inputSizeX.text = "3";
            inputSizeY.text = "3";
            inputSizeZ.text = "3";

            // Кнопки подтвердить / отмена
            Button okBtn = MakeBtn(dialogPanel.transform, "OkBtn",
                "✅ Поставить зону",
                new Color(0.2f, 0.65f, 0.3f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-80f, 14f), new Vector2(148f, 36f));
            okBtn.onClick.AddListener(() => ConfirmShopPlace(worldPos));

            Button cancelBtn = MakeBtn(dialogPanel.transform, "CancelBtn",
                "✖ Отмена",
                new Color(0.6f, 0.2f, 0.2f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(80f, 14f), new Vector2(148f, 36f));
            cancelBtn.onClick.AddListener(CancelDialog);
        }

        void ConfirmShopPlace(Vector3 worldPos)
        {
            int sx = Mathf.Max(1, ParseInt(inputSizeX?.text, 3));
            int sy = Mathf.Max(1, ParseInt(inputSizeY?.text, 3));
            int sz = Mathf.Max(1, ParseInt(inputSizeZ?.text, 3));

            SpawnShopZone(worldPos, sx, sy, sz);

            shopSaveData.zones.Add(new ShopZoneEntry
            {
                worldX = worldPos.x, worldY = worldPos.y, worldZ = worldPos.z,
                sizeX = sx, sizeY = sy, sizeZ = sz
            });
            SaveShopZones();
            CloseDialog();
        }

        void CancelDialog() => CloseDialog();

        void CloseDialog()
        {
            dialogOpen = false;
            if (dialogPanel != null) { Destroy(dialogPanel); dialogPanel = null; }
        }

        void SpawnShopZone(Vector3 worldPos, int sx, int sy, int sz)
        {
            var go = new GameObject($"ShopZone_{spawnedZones.Count}");
            go.transform.position = worldPos;
            var zone = go.AddComponent<ShopZone>();
            zone.sizeX = sx;
            zone.sizeY = sy;
            zone.sizeZ = sz;
            spawnedZones.Add(zone);
            Debug.Log($"[LobbyEditor] Зона магазина поставлена {sx}x{sy}x{sz} @ {worldPos}");
        }

        static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out int v) ? v : def;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Save / Load вокселей
        // ══════════════════════════════════════════════════════════════════════

        public void SaveLayout()
        {
            try { File.WriteAllText(VoxelSavePath, JsonUtility.ToJson(saveData, true)); }
            catch (System.Exception ex) { Debug.LogError($"[LobbyEditor] Сохранение карты: {ex.Message}"); }
        }

        public void LoadAndApplyLayout()
        {
            if (!File.Exists(VoxelSavePath)) { saveData = new LobbyLayoutSaveData(); return; }
            try { saveData = JsonUtility.FromJson<LobbyLayoutSaveData>(File.ReadAllText(VoxelSavePath)) ?? new LobbyLayoutSaveData(); }
            catch { saveData = new LobbyLayoutSaveData(); return; }
            if (island == null) return;
            bool changed = false;
            foreach (var e in saveData.entries)
            {
                if (e.blockTypeId < 0) island.RemoveVoxel(e.x, e.y, e.z, false);
                else island.SetVoxel(e.x, e.y, e.z, (BlockType)e.blockTypeId);
                changed = true;
            }
            if (changed) island.RebuildMesh();
        }

        void UpsertSave(int x, int y, int z, int tid)
        {
            var ex = saveData.entries.Find(e => e.x == x && e.y == y && e.z == z);
            if (ex != null) ex.blockTypeId = tid;
            else saveData.entries.Add(new LobbyVoxelEntry { x=x, y=y, z=z, blockTypeId=tid });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Save / Load зон магазина
        // ══════════════════════════════════════════════════════════════════════

        public void SaveShopZones()
        {
            try { File.WriteAllText(ShopSavePath, JsonUtility.ToJson(shopSaveData, true)); }
            catch (System.Exception ex) { Debug.LogError($"[LobbyEditor] Сохранение зон: {ex.Message}"); }
        }

        public void LoadAndApplyShopZones()
        {
            // Удаляем старые
            foreach (var z in spawnedZones) if (z != null) Destroy(z.gameObject);
            spawnedZones.Clear();

            if (!File.Exists(ShopSavePath)) { shopSaveData = new ShopZoneSaveData(); return; }
            try { shopSaveData = JsonUtility.FromJson<ShopZoneSaveData>(File.ReadAllText(ShopSavePath)) ?? new ShopZoneSaveData(); }
            catch { shopSaveData = new ShopZoneSaveData(); return; }

            foreach (var e in shopSaveData.zones)
                SpawnShopZone(new Vector3(e.worldX, e.worldY, e.worldZ), e.sizeX, e.sizeY, e.sizeZ);

            Debug.Log($"[LobbyEditor] Загружено {spawnedZones.Count} зон магазина.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Preview cube
        // ══════════════════════════════════════════════════════════════════════

        void ShowPreview(Vector3 gridLocalOrigin, Color color)
        {
            EnsurePreview();
            if (island == null) return;
            Vector3 worldPos = island.transform.TransformPoint(gridLocalOrigin + new Vector3(0.5f, 0.5f, 0.5f));
            previewCube.transform.position   = worldPos;
            previewCube.transform.localScale = island.transform.lossyScale;
            var mr = previewCube.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = color;
            previewCube.SetActive(true);
        }

        void HidePreview() { if (previewCube != null) previewCube.SetActive(false); }

        void EnsurePreview()
        {
            if (previewCube != null) return;
            previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewCube.name = "LobbyEditorPreview";
            Destroy(previewCube.GetComponent<Collider>());
            var mr = previewCube.GetComponent<MeshRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            mat.color = previewColorPlace;
            if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface",  1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Toggle
        // ══════════════════════════════════════════════════════════════════════

        public void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            if (!IsEditMode)
            {
                HidePreview();
                CloseDialog();
                // Сбрасываем hover-состояние
                if (hoveredZone != null) { hoveredZone.SetDeleteHover(false); hoveredZone = null; }
            }
            RefreshZoneVisibility();
            RefreshUI();
        }

        void RefreshZoneVisibility()
        {
            foreach (var zone in spawnedZones)
                if (zone != null) zone.SetEditorVisible(IsEditMode);
        }

        void SetToolMode(EditorToolMode mode)
        {
            ToolMode = mode;
            RefreshUI();
        }

        // ══════════════════════════════════════════════════════════════════════
        // UI
        // ══════════════════════════════════════════════════════════════════════

        void BuildUI()
        {
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                var cGo = new GameObject("LobbyEditorCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Кнопка-тоггл
            toggleBtn = MakeBtn(rootCanvas.transform, "LobbyEditToggle",
                "✏️ Редактор [F2]",
                new Color(0.25f, 0.65f, 0.25f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -110f), new Vector2(168f, 36f));
            toggleBtn.onClick.AddListener(ToggleEditMode);

            // Панель инструментов (высота 400 — учитывает и Shop-кнопку)
            editorPanel = MakePanel("LobbyEditorPanel", rootCanvas.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(168f, 400f),
                new Color(0.07f, 0.07f, 0.11f, 0.93f));

            MakeLabelOff(editorPanel.transform, "EdTitle",
                "✏️ РЕДАКТОР ЛОББИ", 13, TextAnchor.UpperCenter,
                new Vector2(4, -30), new Vector2(-4, 0), bold: true);
            MakeLabelOff(editorPanel.transform, "EdHint",
                "ЛКМ — поставить\nПКМ — удалить",
                11, TextAnchor.UpperCenter,
                new Vector2(4, -54), new Vector2(-4, -32));

            // Блочные инструменты
            for (int i = 0; i < BtnTypes.Length; i++)
            {
                int idx = i;
                Color c = BtnColors[i];
                Button btn = MakeBtn(editorPanel.transform, $"BType_{i}",
                    BtnLabels[i],
                    new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -(86f + i * 46f)), new Vector2(148f, 38f));
                btn.onClick.AddListener(() =>
                {
                    selectedBlockType = BtnTypes[idx];
                    SetToolMode(EditorToolMode.Block);
                });
                typeButtons.Add(btn);
            }

            // Кнопка инструмента «Магазин»
            shopToolBtn = MakeBtn(editorPanel.transform, "ShopTool",
                "🛒 Зона магазина",
                new Color(0.15f, 0.35f, 0.80f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + BtnTypes.Length * 46f)), new Vector2(148f, 38f));
            shopToolBtn.onClick.AddListener(() => SetToolMode(EditorToolMode.Shop));

            // Сохранить
            Button saveBtn = MakeBtn(editorPanel.transform, "ManualSaveBtn",
                "💾 Сохранить",
                new Color(0.2f, 0.45f, 0.9f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(148f, 38f));
            saveBtn.onClick.AddListener(() => { SaveLayout(); SaveShopZones(); });

            editorPanel.SetActive(false);
        }

        void RefreshUI()
        {
            if (editorPanel != null) editorPanel.SetActive(IsEditMode);
            if (toggleBtn != null)
            {
                var img = toggleBtn.GetComponent<Image>();
                if (img != null)
                    img.color = IsEditMode ? new Color(0.9f, 0.55f, 0.1f, 1f)
                                           : new Color(0.25f, 0.65f, 0.25f, 1f);
            }
            // Выделяем активный блок
            for (int i = 0; i < typeButtons.Count && i < BtnTypes.Length; i++)
            {
                if (typeButtons[i] == null) continue;
                bool sel = ToolMode == EditorToolMode.Block && BtnTypes[i] == selectedBlockType;
                var img = typeButtons[i].GetComponent<Image>();
                Color c = BtnColors[i];
                if (img != null) img.color = sel ? Color.white : new Color(c.r*0.65f, c.g*0.65f, c.b*0.65f, 1f);
                var txt = typeButtons[i].GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }
            // Выделяем кнопку Shop
            if (shopToolBtn != null)
            {
                var img = shopToolBtn.GetComponent<Image>();
                if (img != null)
                    img.color = ToolMode == EditorToolMode.Shop
                        ? Color.white
                        : new Color(0.15f, 0.35f, 0.80f, 1f);
                var txt = shopToolBtn.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.color = ToolMode == EditorToolMode.Shop ? Color.black : Color.white;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Input
        // ══════════════════════════════════════════════════════════════════════

        Vector2 GetPointerPos()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.position.ReadValue() ?? Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        bool IsToggleKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?[Key.F2].wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleKey);
#else
            return false;
#endif
        }

        bool IsLeftJustPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        bool IsRightJustPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.rightButton.wasPressedThisFrame ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        bool IsRightHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current?.rightButton.isPressed ?? false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        // ══════════════════════════════════════════════════════════════════════
        // UI factories
        // ══════════════════════════════════════════════════════════════════════

        static Font _font;
        static Font GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        static GameObject MakePanel(string name, Transform parent,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        static Button MakeBtn(Transform parent, string name, string label, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
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

        static Text MakeLabelOff(Transform parent, string name, string text,
            int fontSize, TextAnchor align,
            Vector2 offsetMin, Vector2 offsetMax, bool bold = false)
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

        // Строка «Метка + InputField»
        static InputField MakeInputField(Transform parent, string name, string label, ref float offsetY)
        {
            // Метка
            var lGo = new GameObject(name + "_Label");
            lGo.transform.SetParent(parent, false);
            var lrt = lGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 1f); lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, offsetY);
            lrt.sizeDelta = new Vector2(280f, 22f);
            var lt = lGo.AddComponent<Text>();
            lt.font = GetFont(); lt.fontSize = 12;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            lt.text = label;

            offsetY -= 26f;

            // Input background
            var iGo = new GameObject(name);
            iGo.transform.SetParent(parent, false);
            var irt = iGo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, offsetY);
            irt.sizeDelta = new Vector2(280f, 32f);
            var bg = iGo.AddComponent<Image>(); bg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            // Text child
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(iGo.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 2); trt.offsetMax = new Vector2(-6, -2);
            var txt = tGo.AddComponent<Text>();
            txt.font = GetFont(); txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white; txt.supportRichText = false;

            var field = iGo.AddComponent<InputField>();
            field.textComponent = txt;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.targetGraphic = bg;

            offsetY -= 36f;
            return field;
        }
    }
}
