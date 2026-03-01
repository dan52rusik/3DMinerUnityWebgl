using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SimpleVoxelSystem.Data;

using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace SimpleVoxelSystem
{
    // ─── Данные сохранения вокселей ───────────────────────────────────────────

    [System.Serializable]
    public class LobbyVoxelEntry
    {
        public int x, y, z;
        public int blockTypeId;
    }

    [System.Serializable]
    public class LobbyLayoutSaveData
    {
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    // ─── Данные одного чанка ──────────────────────────────────────────────────

    [System.Serializable]
    public class ChunkSaveData
    {
        public int chunkX, chunkZ;   // координаты чанка в чанковом пространстве
        public List<LobbyVoxelEntry> entries = new List<LobbyVoxelEntry>();
    }

    // ─── Данные сохранения зон магазина ──────────────────────────────────────

    [System.Serializable]
    public class ShopZoneEntry
    {
        public float worldX, worldY, worldZ;
        public int   sizeX, sizeY, sizeZ;
        public ShopZoneType zoneType = ShopZoneType.Mine;
    }

    [System.Serializable]
    public class ShopZoneSaveData
    {
        public List<ShopZoneEntry> zones = new List<ShopZoneEntry>();
    }

    // ─── Режим инструмента ────────────────────────────────────────────────────

    public enum EditorToolMode { Block, Shop, PickaxeShop }

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
        [Tooltip("Малое смещение луча по нормали при выборе ячейки, чтобы не перескакивать на соседний блок.")]
        public float hoverSurfaceEpsilon = 0.01f;

        [Header("Дебаг чанков")]
        [Tooltip("Показывать границы чанков 16×16 в режиме редактора.")]
        public bool showChunkDebug = true;

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

        // ─── Чанковое сохранение вокселей ────────────────────────────────────
        private const int ChunkSize = 16;
        private static string ChunkDir =>
            Path.Combine(Application.persistentDataPath, "lobby_chunks");
        private readonly HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();

        private static Vector2Int VoxelToChunk(int x, int z)
            => new Vector2Int(x / ChunkSize, z / ChunkSize);
        private static string ChunkFilePath(int cx, int cz)
            => Path.Combine(ChunkDir, $"chunk_{cx}_{cz}.json");

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
        private Button     pickaxeShopToolBtn;

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
                editorCamera = ResolveEditorCamera();
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

            if (showChunkDebug) DrawChunkDebug();
        }

        private void OnFlatPlotReady()
        {
            // Жесткое условие: лобби грузим только когда мы в центре мира и в режиме лобби
            if (wellGenerator == null || !wellGenerator.IsInLobbyMode) return;
            if (Vector3.SqrMagnitude(wellGenerator.transform.position) > 1.0f) return;

            island = wellGenerator.GetComponent<VoxelIsland>();
            LoadAndApplyLayout();
            LoadAndApplyShopZones();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Визуализация чанков (Debug.DrawLine — видно в Scene view во время Play)
        // ══════════════════════════════════════════════════════════════════════

        void DrawChunkDebug()
        {
            if (island == null || wellGenerator == null) return;

            int totalX = island.TotalX;
            int totalZ = island.TotalZ;

            // Y поверхности пола = localY = -(lobbyFloorY) + 1 (на 1 блок выше поверхности пола)
            float ly = -(wellGenerator.LobbyFloorY - 1) + 0.05f;

            int ccX = Mathf.CeilToInt((float)totalX / ChunkSize);
            int ccZ = Mathf.CeilToInt((float)totalZ / ChunkSize);

            // Голубые линии — вся сетка чанков
            Color gridColor = new Color(0f, 0.9f, 1f, 0.9f); // циан

            // Линии по X
            for (int cx = 0; cx <= ccX; cx++)
            {
                int gx = Mathf.Min(cx * ChunkSize, totalX);
                Vector3 p0 = island.transform.TransformPoint(new Vector3(gx, ly, 0));
                Vector3 p1 = island.transform.TransformPoint(new Vector3(gx, ly, totalZ));
                Debug.DrawLine(p0, p1, gridColor);
            }

            // Линии по Z
            for (int cz = 0; cz <= ccZ; cz++)
            {
                int gz = Mathf.Min(cz * ChunkSize, totalZ);
                Vector3 p0 = island.transform.TransformPoint(new Vector3(0,      ly, gz));
                Vector3 p1 = island.transform.TransformPoint(new Vector3(totalX, ly, gz));
                Debug.DrawLine(p0, p1, gridColor);
            }

            // Жёлтый/оранжевый — dirty-чанки (изменёны, ещё уже записаны авто)
            foreach (var cc in dirtyChunks)
                DrawChunkRect(cc.x, cc.y, ly, new Color(1f, 0.75f, 0f, 1f));

            // Зелёный — чанки с файлом на диске
            for (int cx = 0; cx < ccX; cx++)
            for (int cz = 0; cz < ccZ; cz++)
                if (dirtyChunks.Contains(new Vector2Int(cx, cz)) == false
                    && File.Exists(ChunkFilePath(cx, cz)))
                    DrawChunkRect(cx, cz, ly, new Color(0.2f, 1f, 0.3f, 1f));
        }

        /// <summary>
        /// Рисует рамку чанка цветными линиями + диагональ для хорошей видимости.
        /// </summary>
        void DrawChunkRect(int cx, int cz, float localY, Color color)
        {
            if (island == null) return;
            int x0 = cx * ChunkSize,      x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
            int z0 = cz * ChunkSize,      z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);

            Vector3 a = island.transform.TransformPoint(new Vector3(x0, localY, z0));
            Vector3 b = island.transform.TransformPoint(new Vector3(x1, localY, z0));
            Vector3 c = island.transform.TransformPoint(new Vector3(x1, localY, z1));
            Vector3 d = island.transform.TransformPoint(new Vector3(x0, localY, z1));

            Debug.DrawLine(a, b, color);
            Debug.DrawLine(b, c, color);
            Debug.DrawLine(c, d, color);
            Debug.DrawLine(d, a, color);
            // Диагонали
            Debug.DrawLine(a, c, color * 0.7f);
            Debug.DrawLine(b, d, color * 0.7f);
        }

        void UpdateHover()
        {
            pendingPlacePos  = null;
            pendingRemovePos = null;
            pendingShopWorldPos = null;

            // Находим корректную локальную камеру в мультиплеере.
            editorCamera = ResolveEditorCamera();
            if (editorCamera == null) { HidePreview(); return; }

            if (IsPointerOverUI()) { HidePreview(); return; }

            Vector2 pointerPos = GetPointerPos();
            Ray ray = editorCamera.ScreenPointToRay(pointerPos);
            
            // Игнорируем превью (Layer 2)
            int layerMask = miningLayers & ~(1 << 2);

            if (!Physics.Raycast(ray, out RaycastHit hit, placementRange, layerMask, QueryTriggerInteraction.Ignore))
            { 
               HidePreview(); 
               return; 
            }

            VoxelIsland hitIsland = hit.collider.GetComponentInParent<VoxelIsland>();
            if (hitIsland == null) { HidePreview(); return; }
            
            // Если в редакторе не закреплен конкретный остров, работаем с тем, в который попали
            var activeIsland = (island != null) ? island : hitIsland;
            if (hitIsland != activeIsland) { HidePreview(); return; }

            bool rmb = IsRightHeld();

            if (ToolMode == EditorToolMode.Shop || ToolMode == EditorToolMode.PickaxeShop)
            {
                // В Shop-режиме используем исходную маску (чтобы ловить коллайдеры зон)
                ShopZone newHovered = null;

                if (Physics.Raycast(ray, out RaycastHit trigHit, placementRange, miningLayers, QueryTriggerInteraction.Collide))
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
                    Vector3 lp = activeIsland.transform.InverseTransformPoint(hit.point + hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x), py = -Mathf.FloorToInt(lp.y), pz = Mathf.FloorToInt(lp.z);
                    
                    if (activeIsland.InBounds(px, py, pz))
                    {
                        pendingShopWorldPos = activeIsland.transform.TransformPoint(new Vector3(px + 0.5f, -py + 0.5f, pz + 0.5f));
                        ShowPreview(activeIsland, new Vector3(px, -py, pz), previewColorShop);
                    }
                    else HidePreview();
                }

                // Обновляем hover зоны
                if (hoveredZone != newHovered)
                {
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(false);
                    hoveredZone = newHovered;
                    if (hoveredZone != null) hoveredZone.SetDeleteHover(true);
                }
            }
            else // Block editing mode
            {
                if (!rmb)
                {
                    // ЛКМ (place) - ставим в ячейку под курсором/по нормали без прыжка через блок
                    Vector3 lp = activeIsland.transform.InverseTransformPoint(hit.point + hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x);
                    int py = -Mathf.FloorToInt(lp.y);
                    int pz = Mathf.FloorToInt(lp.z);

                    if (activeIsland.InBounds(px, py, pz))
                    {
                        pendingPlacePos = new Vector3Int(px, py, pz);
                        Color bc = BtnColors[(int)selectedBlockType];
                        ShowPreview(activeIsland, new Vector3(px, -py, pz),
                            new Color(bc.r, bc.g, bc.b, 0.45f));
                    }
                    else HidePreview();
                }
                else
                {
                    // ПКМ (remove) - удаляем саму поверхность
                    Vector3 lp = activeIsland.transform.InverseTransformPoint(hit.point - hit.normal * hoverSurfaceEpsilon);
                    int px = Mathf.FloorToInt(lp.x);
                    int py = -Mathf.FloorToInt(lp.y);
                    int pz = Mathf.FloorToInt(lp.z);

                    if (activeIsland.IsSolid(px, py, pz))
                    {
                        pendingRemovePos = new Vector3Int(px, py, pz);
                        ShowPreview(activeIsland, new Vector3(px, -py, pz), previewColorRemove);
                        hoveredZone = null; // Ensure shop zone hover is cleared when in block mode
                    }
                    else HidePreview();
                }
            }
        }

        void HandleInput()
        {
            if (ToolMode == EditorToolMode.Shop || ToolMode == EditorToolMode.PickaxeShop)
            {
                if (IsRightJustPressed() && hoveredZone != null)
                    DeleteShopZone(hoveredZone);
                else if (IsLeftJustPressed() && pendingShopWorldPos.HasValue)
                    OpenSizeDialog(pendingShopWorldPos.Value, (ToolMode == EditorToolMode.PickaxeShop) ? ShopZoneType.Pickaxe : ShopZoneType.Mine);
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
            dirtyChunks.Add(VoxelToChunk(pos.x, pos.z));
            SaveLayout();
        }

        void RemoveBlock(Vector3Int pos)
        {
            if (island == null || !island.IsSolid(pos.x, pos.y, pos.z)) return;
            island.RemoveVoxel(pos.x, pos.y, pos.z, true);
            dirtyChunks.Add(VoxelToChunk(pos.x, pos.z));
            SaveLayout();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Диалог размера Shop-зоны
        // ══════════════════════════════════════════════════════════════════════

        void OpenSizeDialog(Vector3 worldPos, ShopZoneType type)
        {
            dialogOpen = true;
            HidePreview();

            if (dialogPanel != null) { Destroy(dialogPanel); }

            // Создаём панель диалога по центру экрана
            dialogPanel = MakePanel("ShopSizeDialog", rootCanvas.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(320f, 260f),
                new Color(0.08f, 0.08f, 0.14f, 0.97f));

            string title = (type == ShopZoneType.Pickaxe) ? "⚒️ МАГАЗИН КИРОК" : "🛒 МАГАЗИН ШАХТ";
            // Заголовок
            MakeLabelOff(dialogPanel.transform, "DlgTitle",
                $"{title}\nВведите размер:", 14, TextAnchor.UpperCenter,
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
            okBtn.onClick.AddListener(() => ConfirmShopPlace(worldPos, type));

            Button cancelBtn = MakeBtn(dialogPanel.transform, "CancelBtn",
                "✖ Отмена",
                new Color(0.6f, 0.2f, 0.2f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(80f, 14f), new Vector2(148f, 36f));
            cancelBtn.onClick.AddListener(CancelDialog);
        }

        void ConfirmShopPlace(Vector3 worldPos, ShopZoneType type)
        {
            int sx = Mathf.Max(1, ParseInt(inputSizeX?.text, 3));
            int sy = Mathf.Max(1, ParseInt(inputSizeY?.text, 3));
            int sz = Mathf.Max(1, ParseInt(inputSizeZ?.text, 3));

            SpawnShopZone(worldPos, sx, sy, sz, type);

            shopSaveData.zones.Add(new ShopZoneEntry
            {
                worldX = worldPos.x, worldY = worldPos.y, worldZ = worldPos.z,
                sizeX = sx, sizeY = sy, sizeZ = sz,
                zoneType = type
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

        void SpawnShopZone(Vector3 worldPos, int sx, int sy, int sz, ShopZoneType type = ShopZoneType.Mine)
        {
            var go = new GameObject($"ShopZone_{spawnedZones.Count}");
            go.transform.position = worldPos;
            var zone = go.AddComponent<ShopZone>();
            zone.zoneType = type;
            zone.sizeX = sx;
            zone.sizeY = sy;
            zone.sizeZ = sz;
            spawnedZones.Add(zone);
            Debug.Log($"[LobbyEditor] Зона магазина ({type}) поставлена {sx}x{sy}x{sz} @ {worldPos}");
        }

        static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out int v) ? v : def;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Чанковое сохранение — Minecraft-стиль
        //  Каждый чанк 16×16 хранится в lobby_chunks/chunk_cx_cz.json.
        //  При изменении блока помечается только его чанк (dirty).
        //  SaveLayout() пишет только dirty-чанки на диск.
        //  LoadAndApplyLayout() читает все файлы и перекрывает именно их область;
        //  чанки без файла сохраняют базовый пол из GenerateFlatPlot().         
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Сохраняет на диск только изменённые (dirty) чанки.
        /// Помечает все чанки как чистые после записи.
        /// </summary>
        public void SaveLayout()
        {
            if (island == null || dirtyChunks.Count == 0) return;

            try { Directory.CreateDirectory(ChunkDir); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyEditor] Не удалось создать папку чанков: {ex.Message}");
                return;
            }

            foreach (var cc in dirtyChunks)
                SaveChunk(cc.x, cc.y);

            Debug.Log($"[LobbyEditor] Сброшено {dirtyChunks.Count} чанк(ов) на диск.");
            dirtyChunks.Clear();
        }

        /// <summary>
        /// Принудительно сохраняет ВСЕ чанки острова (например, по кнопке «Сохранить»).
        /// </summary>
        public void SaveLayoutFull()
        {
            if (island == null) return;
            // Помечаем все чанки грязными
            int chunkCountX = Mathf.CeilToInt((float)island.TotalX / ChunkSize);
            int chunkCountZ = Mathf.CeilToInt((float)island.TotalZ / ChunkSize);
            for (int cx = 0; cx < chunkCountX; cx++)
            for (int cz = 0; cz < chunkCountZ; cz++)
                dirtyChunks.Add(new Vector2Int(cx, cz));
            SaveLayout();
        }

        private void SaveChunk(int cx, int cz)
        {
            var data = new ChunkSaveData { chunkX = cx, chunkZ = cz };

            int x0 = cx * ChunkSize, x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
            int z0 = cz * ChunkSize, z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);

            for (int x = x0; x < x1; x++)
            for (int y = 0; y < island.TotalY; y++)
            for (int z = z0; z < z1; z++)
            {
                if (island.TryGetBlockType(x, y, z, out BlockType bt))
                {
                    int id = (int)bt;
                    // В старых чанках "земля" могла быть сохранена как 0. Пишем 1 (Dirt), чтобы избежать Air.
                    if (id == 0) id = (int)BlockType.Dirt;
                    data.entries.Add(new LobbyVoxelEntry { x = x, y = y, z = z, blockTypeId = id });
                }
            }

            try { File.WriteAllText(ChunkFilePath(cx, cz), JsonUtility.ToJson(data, true)); }
            catch (System.Exception ex)
            { Debug.LogError($"[LobbyEditor] Ошибка записи чанка {cx},{cz}: {ex.Message}"); }
        }

        /// <summary>
        /// Загружает все сохранённые чанки и накладывает их поверх базового пола.
        /// Чанки без файла НЕ трогаются — базовый пол из GenerateFlatPlot остаётся.
        /// </summary>
        public void LoadAndApplyLayout()
        {
            if (island == null) return;
            if (!Directory.Exists(ChunkDir)) return;

            string[] files;
            try { files = Directory.GetFiles(ChunkDir, "chunk_*.json"); }
            catch { return; }

            if (files.Length == 0) return;

            int loaded = 0;
            foreach (string file in files)
            {
                ChunkSaveData data;
                try { data = JsonUtility.FromJson<ChunkSaveData>(File.ReadAllText(file)); }
                catch { continue; }
                if (data == null) continue;

                // Очищаем только область этого чанка
                int x0 = data.chunkX * ChunkSize, x1 = Mathf.Min(x0 + ChunkSize, island.TotalX);
                int z0 = data.chunkZ * ChunkSize, z1 = Mathf.Min(z0 + ChunkSize, island.TotalZ);

                for (int x = x0; x < x1; x++)
                for (int y = 0; y < island.TotalY; y++)
                for (int z = z0; z < z1; z++)
                    island.RemoveVoxel(x, y, z, false);

                // Восстанавливаем из файла
                if (data.entries != null)
                    foreach (var e in data.entries)
                    {
                        int raw = e.blockTypeId;
                        // Совместимость со старыми сохранениями лобби: 0 трактуем как Dirt.
                        if (raw <= 0) raw = (int)BlockType.Dirt;
                        if (raw > (int)BlockType.Grass) raw = (int)BlockType.Dirt;
                        island.SetVoxel(e.x, e.y, e.z, (BlockType)raw);
                    }

                loaded++;
            }

            if (loaded > 0)
            {
                island.RebuildMesh();
                Debug.Log($"[LobbyEditor] Загружено {loaded} чанк(ов).");
            }
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
                SpawnShopZone(new Vector3(e.worldX, e.worldY, e.worldZ), e.sizeX, e.sizeY, e.sizeZ, e.zoneType);

            Debug.Log($"[LobbyEditor] Загружено {spawnedZones.Count} зон магазина.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Preview cube
        // ══════════════════════════════════════════════════════════════════════

        void ShowPreview(VoxelIsland targetIsland, Vector3 gridLocalOrigin, Color color)
        {
            EnsurePreview();
            previewCube.SetActive(true);

            // world position. gridLocalOrigin.y - инвертирован, поэтому (x, y, z)
            Vector3 worldPos = targetIsland.transform.TransformPoint(gridLocalOrigin + new Vector3(0.5f, 0.5f, 0.5f));
            previewCube.transform.position = worldPos;
            previewCube.transform.rotation = targetIsland.transform.rotation;
            previewCube.transform.localScale = targetIsland.transform.lossyScale;

            var mr = previewCube.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = color;
        }

        void HidePreview() { if (previewCube != null) previewCube.SetActive(false); }

        void EnsurePreview()
        {
            if (previewCube != null) return;
            previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewCube.name = "LobbyEditorPreview";
            previewCube.layer = 2; // Ignore Raycast
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
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            // Кнопка-тоггл
            toggleBtn = MakeBtn(rootCanvas.transform, "LobbyEditToggle",
                "✏️ Редактор [F2]",
                new Color(0.25f, 0.65f, 0.25f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -110f), new Vector2(168f, 36f));
            toggleBtn.onClick.AddListener(ToggleEditMode);

            // Панель инструментов (высота 450 — учитывает и Shop-кнопку)
            editorPanel = MakePanel("LobbyEditorPanel", rootCanvas.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(168f, 450f),
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

            // Кнопка инструмента «Магазин Шахт»
            shopToolBtn = MakeBtn(editorPanel.transform, "ShopTool",
                "🛒 Зона шахт",
                new Color(0.15f, 0.35f, 0.80f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + BtnTypes.Length * 46f)), new Vector2(148f, 38f));
            shopToolBtn.onClick.AddListener(() => SetToolMode(EditorToolMode.Shop));

            // Кнопка инструмента «Магазин Кирок»
            pickaxeShopToolBtn = MakeBtn(editorPanel.transform, "PickaxeShopTool",
                "⚒️ Зона кирок",
                new Color(0.25f, 0.45f, 0.25f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(86f + (BtnTypes.Length + 1) * 46f)), new Vector2(148f, 38f));
            pickaxeShopToolBtn.onClick.AddListener(() => SetToolMode(EditorToolMode.PickaxeShop));

            // Сохранить
            Button saveBtn = MakeBtn(editorPanel.transform, "ManualSaveBtn",
                "💾 Сохранить",
                new Color(0.2f, 0.45f, 0.9f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(148f, 38f));
            saveBtn.onClick.AddListener(() => { SaveLayoutFull(); SaveShopZones(); });

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
                bool sel = ToolMode == EditorToolMode.Shop;
                var img = shopToolBtn.GetComponent<Image>();
                if (img != null) img.color = sel ? Color.white : new Color(0.15f, 0.35f, 0.80f, 1f);
                var txt = shopToolBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }

            // Выделяем кнопку PickaxeShop
            if (pickaxeShopToolBtn != null)
            {
                bool sel = ToolMode == EditorToolMode.PickaxeShop;
                var img = pickaxeShopToolBtn.GetComponent<Image>();
                if (img != null) img.color = sel ? Color.white : new Color(0.25f, 0.45f, 0.25f, 1f);
                var txt = pickaxeShopToolBtn.GetComponentInChildren<Text>();
                if (txt != null) txt.color = sel ? Color.black : Color.white;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Input
        // ══════════════════════════════════════════════════════════════════════

        bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            // Более надежная проверка для редактора и WebGL
            return EventSystem.current.IsPointerOverGameObject();
        }

        Camera ResolveEditorCamera()
        {
            if (editorCamera != null && editorCamera.isActiveAndEnabled)
                return editorCamera;

            // В мультиплеере локальный аватар помечается тегом Player.
            GameObject localPlayer = GameObject.FindGameObjectWithTag("Player");
            if (localPlayer != null)
            {
                Camera localCam = localPlayer.GetComponentInChildren<Camera>(true);
                if (localCam != null && localCam.isActiveAndEnabled)
                {
                    editorCamera = localCam;
                    return editorCamera;
                }
            }

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
            {
                editorCamera = Camera.main;
                return editorCamera;
            }

            Camera[] allCameras = Camera.allCameras;
            for (int i = 0; i < allCameras.Length; i++)
            {
                Camera cam = allCameras[i];
                if (cam != null && cam.isActiveAndEnabled)
                {
                    editorCamera = cam;
                    return editorCamera;
                }
            }

            editorCamera = null;
            return null;
        }

        Vector2 GetPointerPos()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            // Для луча предпочитаем координаты legacy Input, когда он доступен.
            Vector2 legacyPos = Input.mousePosition;
            if (legacyPos.x >= 0f && legacyPos.x <= Screen.width &&
                legacyPos.y >= 0f && legacyPos.y <= Screen.height)
                return legacyPos;
#endif

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Vector2.zero;
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
