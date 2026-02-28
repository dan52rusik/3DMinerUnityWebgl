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
    // ─── Данные для сериализации ──────────────────────────────────────────────

    [System.Serializable]
    public class LobbyVoxelEntry
    {
        public int x, y, z;
        /// <summary>
        /// (int)BlockType для поставленных блоков.
        /// -1 = блок удалён (был частью базового пола).
        /// </summary>
        public int blockTypeId;
    }

    [System.Serializable]
    public class LobbyLayoutSaveData
    {
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    // ─── Редактор лобби ───────────────────────────────────────────────────────

    /// <summary>
    /// Режим редактирования лобби-площадки.
    /// Добавьте этот компонент на любой GameObject в сцене.
    /// Он сам найдёт WellGenerator / VoxelIsland и создаст UI.
    ///
    /// ЛКМ  = поставить блок выбранного типа.
    /// ПКМ  = удалить блок.
    /// F2   = включить/выключить режим редактирования.
    ///
    /// Изменения сохраняются в <persistentDataPath>/lobby_layout.json
    /// и автоматически загружаются при каждом старте.
    /// </summary>
    public class LobbyEditor : MonoBehaviour
    {
        [Header("Ссылки (заполняются автоматически)")]
        public WellGenerator wellGenerator;
        public Camera        editorCamera;

        [Header("Горячая клавиша")]
        public KeyCode toggleKey = KeyCode.F2;

        [Header("Дальность размещения")]
        public float placementRange = 200f;

        public LayerMask miningLayers = Physics.DefaultRaycastLayers;

        // ─── Цвета превью ────────────────────────────────────────────────────
        public Color previewColorPlace  = new Color(0.2f, 1f, 0.5f,  0.40f);
        public Color previewColorRemove = new Color(1f,   0.2f, 0.2f, 0.40f);

        // ─── Runtime ─────────────────────────────────────────────────────────
        public bool IsEditMode { get; private set; }

        private BlockType     selectedBlockType = BlockType.Stone;
        private VoxelIsland   island;
        private GameObject    previewCube;
        private Vector3Int?   pendingPlacePos;
        private Vector3Int?   pendingRemovePos;

        // ─── Сохранение ──────────────────────────────────────────────────────
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "lobby_layout.json");

        private LobbyLayoutSaveData saveData = new LobbyLayoutSaveData();

        // ─── UI ──────────────────────────────────────────────────────────────
        private Canvas          rootCanvas;
        private GameObject      editorPanel;
        private Button          toggleBtn;
        private readonly List<Button> typeButtons = new List<Button>();

        // цвета блоков для кнопок
        private static readonly Color[] BtnColors =
        {
            new Color(0.55f, 0.27f, 0.07f), // Dirt
            new Color(0.50f, 0.50f, 0.50f), // Stone
            new Color(0.65f, 0.44f, 0.40f), // Iron
            new Color(1.00f, 0.84f, 0.00f), // Gold
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
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════════

        void Awake()
        {
            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();

            if (editorCamera == null)
                editorCamera = Camera.main;

            // Подписываемся на событие — загружаем блоки ПОСЛЕ генерации пола
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
            // Горячая клавиша
            if (IsToggleKeyDown())
                ToggleEditMode();

            if (!IsEditMode)
            {
                HidePreview();
                return;
            }

            UpdateHover();
            HandleInput();
        }

        // ──────────────────────────────────────────────────────────────────────

        private void OnFlatPlotReady()
        {
            // Пол только что пересоздан — применяем сохранённые изменения поверх
            if (wellGenerator != null)
                island = wellGenerator.GetComponent<VoxelIsland>();

            LoadAndApplyLayout();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Hover / Preview
        // ══════════════════════════════════════════════════════════════════════

        void UpdateHover()
        {
            pendingPlacePos  = null;
            pendingRemovePos = null;

            if (editorCamera == null || island == null)
            {
                HidePreview();
                return;
            }

            Ray ray = editorCamera.ScreenPointToRay(GetPointerPos());
            if (!Physics.Raycast(ray, out RaycastHit hit, placementRange, miningLayers,
                                  QueryTriggerInteraction.Ignore))
            {
                HidePreview();
                return;
            }

            // Убеждаемся, что попали в наш VoxelIsland
            VoxelIsland hitIsland = hit.collider.GetComponentInParent<VoxelIsland>();
            if (hitIsland != island)
            {
                HidePreview();
                return;
            }

            bool rmb = IsRightHeld();

            if (rmb)
            {
                // Блок под курсором (удалить)
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
                // Позиция рядом с гранью (поставить)
                Vector3 lp = island.transform.InverseTransformPoint(hit.point + hit.normal * 0.5f);
                int px = Mathf.FloorToInt(lp.x);
                int py = -Mathf.FloorToInt(lp.y);
                int pz = Mathf.FloorToInt(lp.z);
                pendingPlacePos = new Vector3Int(px, py, pz);
                ShowPreview(new Vector3(px, -py, pz),
                    // Цвет = цвет выбранного блока с прозрачностью
                    new Color(BtnColors[(int)selectedBlockType].r,
                              BtnColors[(int)selectedBlockType].g,
                              BtnColors[(int)selectedBlockType].b, 0.45f));
            }
        }

        void HandleInput()
        {
            if (IsLeftJustPressed() && pendingPlacePos.HasValue)
                PlaceBlock(pendingPlacePos.Value);

            if (IsRightJustPressed() && pendingRemovePos.HasValue)
                RemoveBlock(pendingRemovePos.Value);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Блоки
        // ══════════════════════════════════════════════════════════════════════

        void PlaceBlock(Vector3Int pos)
        {
            if (island == null) return;
            if (island.IsSolid(pos.x, pos.y, pos.z)) return; // уже занято

            island.SetVoxel(pos.x, pos.y, pos.z, selectedBlockType);
            island.RebuildMesh();

            UpsertSave(pos.x, pos.y, pos.z, (int)selectedBlockType);
            SaveLayout();

            Debug.Log($"[LobbyEditor] Поставлен {selectedBlockType} [{pos.x},{pos.y},{pos.z}].");
        }

        void RemoveBlock(Vector3Int pos)
        {
            if (island == null) return;
            if (!island.IsSolid(pos.x, pos.y, pos.z)) return;

            island.RemoveVoxel(pos.x, pos.y, pos.z, true);

            // Если блок был на базовом уровне пола — помечаем как удалённый (-1)
            // иначе — просто стираем запись (блок вернулся к воздуху)
            int floorY = wellGenerator != null ? wellGenerator.LobbyFloorY : 0;
            if (pos.y == floorY)
                UpsertSave(pos.x, pos.y, pos.z, -1); // -1 = «удалён из пола»
            else
                saveData.entries.RemoveAll(e => e.x == pos.x && e.y == pos.y && e.z == pos.z);

            SaveLayout();
            Debug.Log($"[LobbyEditor] Удалён блок [{pos.x},{pos.y},{pos.z}].");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Save / Load
        // ══════════════════════════════════════════════════════════════════════

        public void SaveLayout()
        {
            try
            {
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[LobbyEditor] Сохранено {saveData.entries.Count} записей → {SavePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyEditor] Ошибка сохранения: {ex.Message}");
            }
        }

        /// <summary>Загрузить JSON и применить поверх текущего состояния острова.</summary>
        public void LoadAndApplyLayout()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[LobbyEditor] Файл лобби не найден — начинаем с чистого пола.");
                saveData = new LobbyLayoutSaveData();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                saveData = JsonUtility.FromJson<LobbyLayoutSaveData>(json)
                           ?? new LobbyLayoutSaveData();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyEditor] Ошибка загрузки: {ex.Message}");
                saveData = new LobbyLayoutSaveData();
                return;
            }

            if (island == null) return;

            bool changed = false;
            foreach (var e in saveData.entries)
            {
                if (e.blockTypeId < 0)
                    island.RemoveVoxel(e.x, e.y, e.z, false); // удалённый пол
                else
                    island.SetVoxel(e.x, e.y, e.z, (BlockType)e.blockTypeId);
                changed = true;
            }

            if (changed) island.RebuildMesh();
            Debug.Log($"[LobbyEditor] Загружено {saveData.entries.Count} записей лобби.");
        }

        void UpsertSave(int x, int y, int z, int blockTypeId)
        {
            var existing = saveData.entries.Find(e => e.x == x && e.y == y && e.z == z);
            if (existing != null)
                existing.blockTypeId = blockTypeId;
            else
                saveData.entries.Add(new LobbyVoxelEntry { x = x, y = y, z = z,
                                                           blockTypeId = blockTypeId });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Preview cube
        // ══════════════════════════════════════════════════════════════════════

        void ShowPreview(Vector3 gridLocalOrigin, Color color)
        {
            EnsurePreview();
            if (island == null) return;

            // Центр блока в локальных координатах острова (0.5 offset)
            Vector3 worldPos = island.transform.TransformPoint(
                gridLocalOrigin + new Vector3(0.5f, 0.5f, 0.5f));
            previewCube.transform.position = worldPos;
            previewCube.transform.localScale = island.transform.lossyScale;

            var mr = previewCube.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = color;

            previewCube.SetActive(true);
        }

        void HidePreview()
        {
            if (previewCube != null) previewCube.SetActive(false);
        }

        void EnsurePreview()
        {
            if (previewCube != null) return;

            previewCube      = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewCube.name = "LobbyEditorPreview";
            Destroy(previewCube.GetComponent<Collider>());

            var mr  = previewCube.GetComponent<MeshRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
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
        // Edit Mode toggle
        // ══════════════════════════════════════════════════════════════════════

        public void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            if (!IsEditMode) HidePreview();
            RefreshUI();
            Debug.Log($"[LobbyEditor] Режим редактирования: {(IsEditMode ? "ВКЛ ✏️" : "ВЫКЛ")}");
        }

        // ══════════════════════════════════════════════════════════════════════
        // UI (procedural, same style as MineShopUI)
        // ══════════════════════════════════════════════════════════════════════

        void BuildUI()
        {
            // Canvas
            rootCanvas = FindFirstObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                var cGo = new GameObject("LobbyEditorCanvas");
                rootCanvas = cGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cGo.AddComponent<CanvasScaler>().uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cGo.AddComponent<GraphicRaycaster>();
            }

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ── Кнопка-тоггл (правый верхний угол, чуть ниже возможных других кнопок) ──
            toggleBtn = MakeBtn(rootCanvas.transform, "LobbyEditToggle",
                "✏️ Редактор [F2]",
                new Color(0.25f, 0.65f, 0.25f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -110f), new Vector2(168f, 36f));
            toggleBtn.onClick.AddListener(ToggleEditMode);

            // ── Панель выбора блока (правый край, по центру по Y) ────────────
            editorPanel = MakePanel("LobbyEditorPanel", rootCanvas.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f),
                new Vector2(168f, 320f),
                new Color(0.07f, 0.07f, 0.11f, 0.93f));

            // Заголовок
            MakeLabelOff(editorPanel.transform, "EdTitle",
                "✏️ РЕДАКТОР ЛОББИ", 13, TextAnchor.UpperCenter,
                new Vector2(4, -30), new Vector2(-4, 0), bold: true);

            // Подсказка
            MakeLabelOff(editorPanel.transform, "EdHint",
                "ЛКМ — поставить\nПКМ — удалить",
                11, TextAnchor.UpperCenter,
                new Vector2(4, -54), new Vector2(-4, -32));

            // Кнопки типов блока
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
                    RefreshUI();
                });
                typeButtons.Add(btn);
            }

            // Кнопка «Сохранить вручную»
            Button saveBtn = MakeBtn(editorPanel.transform, "ManualSaveBtn",
                "💾 Сохранить",
                new Color(0.2f, 0.45f, 0.9f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(148f, 38f));
            saveBtn.onClick.AddListener(SaveLayout);

            editorPanel.SetActive(false);
        }

        void RefreshUI()
        {
            if (editorPanel != null) editorPanel.SetActive(IsEditMode);

            // Цвет кнопки-тоггла
            if (toggleBtn != null)
            {
                var img = toggleBtn.GetComponent<Image>();
                if (img != null)
                    img.color = IsEditMode
                        ? new Color(0.9f, 0.55f, 0.1f, 1f)
                        : new Color(0.25f, 0.65f, 0.25f, 1f);
            }

            // Выделяем активный блок
            for (int i = 0; i < typeButtons.Count && i < BtnTypes.Length; i++)
            {
                if (typeButtons[i] == null) continue;
                bool sel = BtnTypes[i] == selectedBlockType;
                var img = typeButtons[i].GetComponent<Image>();
                Color c = BtnColors[i];
                if (img != null)
                    img.color = sel ? Color.white
                        : new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 1f);
                var txt = typeButtons[i].GetComponentInChildren<Text>();
                if (txt != null)
                    txt.color = sel ? Color.black : Color.white;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Input helpers
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
            return Keyboard.current != null &&
                   Keyboard.current[Key.F2].wasPressedThisFrame;
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
        // UI factories (style matching MineShopUI)
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
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var tGo = new GameObject("Label");
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = tGo.AddComponent<Text>();
            txt.font = GetFont();
            txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;

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
            txt.font = GetFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            txt.text = bold ? $"<b>{text}</b>" : text;
            txt.supportRichText = true;
            return txt;
        }
    }
}
