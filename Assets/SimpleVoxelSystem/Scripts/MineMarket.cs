using UnityEngine;
using System.Collections.Generic;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Управляет магазином шахт:
    ///   — список доступных классов шахт (MineShopData)
    ///   — покупка → создаёт MineInstance и запускает режим размещения
    ///   — размещение → вызывает WellGenerator.GenerateMine()
    ///   — продажа истощённой шахты → WellGenerator.DemolishMine() + деньги
    ///
    /// Подключите этот компонент к тому же GameObject, что и WellGenerator.
    /// В инспекторе назначьте список availableMines.
    /// </summary>
    public class MineMarket : MonoBehaviour
    {
        [Header("Магазин шахт")]
        public List<MineShopData> availableMines;

        [Header("Режим размещения")]
        [Tooltip("Полупрозрачный куб-превью (опциональный)")]
        public GameObject placementPreviewPrefab;
        public Color previewColor = new Color(0f, 1f, 0.5f, 0.3f);

        // ─── Runtime ────────────────────────────────────────────────────────
        public bool IsPlacementMode { get; private set; }

        [Header("Ссылки")]
        public WellGenerator WellGen { get; private set; }
        private MineInstance    pendingMine;       // шахта, ожидающая размещения
        private GameObject      previewInstance;   // призрак-превью

        // Событие для UI
        public event System.Action<MineInstance> OnMinePlaced;
        public event System.Action<MineInstance> OnMineSold;
        public event System.Action              OnPlacementCancelled;

        // ────────────────────────────────────────────────────────────────────

        void Awake()
        {
            WellGen = GetComponent<WellGenerator>();
            if (WellGen == null)
                WellGen = GetComponentInParent<WellGenerator>();

            CreateDefaultMinesIfEmpty();
            EnsureShopUI();
        }

        /// <summary>
        /// Создаёт MineShopUI в сцене если его ещё нет.
        /// Ищет существующий Canvas — добавляет туда; если Canvas нет — создаёт пустой GO.
        /// </summary>
        void EnsureShopUI()
        {
            if (FindFirstObjectByType<MineShopUI>() != null) return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            GameObject host;
            if (canvas != null)
                host = canvas.gameObject;
            else
            {
                host = new GameObject("MineShopUI_Host");
                Debug.Log("[MineMarket] Canvas не найден, создан отдельный GO для MineShopUI.");
            }

            var shopUI = host.AddComponent<MineShopUI>();
            shopUI.mineMarket = this;
            Debug.Log("[MineMarket] MineShopUI автоздан на «" + host.name + "».");
        }

        /// <summary>
        /// Создаёт дефолтный набор шахт в рантайме если список пуст или не назначен.
        /// Позволяет обойтись без редакторского сетапа (Tools → Mine System → Setup Scene).
        /// </summary>
        void CreateDefaultMinesIfEmpty()
        {
            if (availableMines != null && availableMines.Count > 0) return;

            availableMines = new List<MineShopData>();

            // Бронзовая
            var bronze = ScriptableObject.CreateInstance<MineShopData>();
            bronze.displayName  = "Бронзовая шахта";
            bronze.description  = "Небольшая, преимущественно земля и камень.";
            bronze.labelColor   = new Color(0.80f, 0.50f, 0.20f);
            bronze.buyPrice     = 300;
            bronze.sellBackRatio= 0.5f;
            bronze.depthMin     = 3;  bronze.depthMax = 5;
            bronze.wellWidth    = 5;  bronze.wellLength = 5; bronze.padding = 3;
            bronze.layers       = new BlockLayer[]
            {
                new BlockLayer { maxDepth=2,  dirtWeight=90, stoneWeight=10, ironWeight=0,  goldWeight=0 },
                new BlockLayer { maxDepth=30, dirtWeight=40, stoneWeight=55, ironWeight=5,  goldWeight=0 },
            };
            availableMines.Add(bronze);

            // Серебряная
            var silver = ScriptableObject.CreateInstance<MineShopData>();
            silver.displayName  = "Серебряная шахта";
            silver.description  = "Средняя. Железо и немного золота в глубине.";
            silver.labelColor   = new Color(0.70f, 0.70f, 0.80f);
            silver.buyPrice     = 800;
            silver.sellBackRatio= 0.5f;
            silver.depthMin     = 5;  silver.depthMax = 9;
            silver.wellWidth    = 5;  silver.wellLength = 5; silver.padding = 3;
            silver.layers       = new BlockLayer[]
            {
                new BlockLayer { maxDepth=2,  dirtWeight=70, stoneWeight=30, ironWeight=0,  goldWeight=0 },
                new BlockLayer { maxDepth=6,  dirtWeight=20, stoneWeight=55, ironWeight=25, goldWeight=0 },
                new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=55, ironWeight=35, goldWeight=5 },
            };
            availableMines.Add(silver);

            // Золотая
            var gold = ScriptableObject.CreateInstance<MineShopData>();
            gold.displayName  = "Золотая шахта";
            gold.description  = "Глубокая. Много железа и золота.";
            gold.labelColor   = new Color(1.00f, 0.84f, 0.10f);
            gold.buyPrice     = 2000;
            gold.sellBackRatio= 0.5f;
            gold.depthMin     = 10; gold.depthMax = 15;
            gold.wellWidth    = 5;  gold.wellLength = 5; gold.padding = 3;
            gold.layers       = new BlockLayer[]
            {
                new BlockLayer { maxDepth=2,  dirtWeight=50, stoneWeight=50, ironWeight=0,  goldWeight=0  },
                new BlockLayer { maxDepth=6,  dirtWeight=10, stoneWeight=50, ironWeight=35, goldWeight=5  },
                new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=35, ironWeight=35, goldWeight=25 },
            };
            availableMines.Add(gold);

            Debug.Log("[MineMarket] Созданы дефолтные шахты (3 вида).");
        }

        void Update()
        {
            if (pendingMine == null) 
            {
                IsPlacementMode = false;
                ShowPreview(false);
                return;
            }

            // В лобби превью скрыто, на острове — активно
            bool shouldShowPreview = WellGen != null && !WellGen.IsInLobbyMode;
            IsPlacementMode = shouldShowPreview;
            ShowPreview(shouldShowPreview);

            if (!IsPlacementMode) return;

            UpdatePlacementPreview();

            // Нажали Escape — отмена
            if (IsCancelPressed())
            {
                CancelPlacement();
                return;
            }

            // Левый клик = подтвердить размещение
            if (IsConfirmPressed())
            {
                ConfirmPlacement();
            }
        }

        private Vector3 lastValidPlacementPos;

        void UpdatePlacementPreview()
        {
            if (previewInstance == null) return;

            // Рейкаст от камеры в мир
            Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Находим точку на сетке (Snap)
                Vector3 p = hit.point;
                // Сдвигаем на полблока если нужно, но для 5x5 шахты лучше на целые числа
                float x = Mathf.Round(p.x);
                float z = Mathf.Round(p.z);
                
                // Высота: чуть выше поверхности острова (LobbyFloorY)
                float y = (WellGen != null) ? WellGen.LobbyFloorY : p.y;
                
                previewInstance.transform.position = new Vector3(x, y + 0.5f, z);
                lastValidPlacementPos = previewInstance.transform.position;
            }
        }

        Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        // ════════════════════════════════════════════════════════════════════
        // Публичное API (вызывается из UI)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Пытается купить шахту указанного класса.
        /// Если денег достаточно — списывает цену и входит в режим размещения.
        /// </summary>
        public bool TryBuyMine(MineShopData data)
        {
            if (data == null) return false;

            if (WellGen != null && WellGen.IsMineGenerated)
            {
                Debug.Log("[MineMarket] На участке уже стоит шахта. Сначала продайте текущую.");
                return false;
            }

            if (GlobalEconomy.Money < data.buyPrice)
            {
                Debug.Log($"[MineMarket] Не хватает денег. Нужно {data.buyPrice}₽, есть {GlobalEconomy.Money}₽.");
                return false;
            }

            GlobalEconomy.Money -= data.buyPrice;

            // Бросаем кубик — реальная глубина известна покупателю только сейчас
            int depth = data.RollDepth();
            pendingMine = new MineInstance(data, depth, 0);

            Debug.Log($"[MineMarket] Куплена '{data.displayName}' (глубина {depth}). " +
                      $"Она добавлена в инвентарь. Отправляйтесь на свой Остров, чтобы разместить её.");

            // Включаем режим, но Update() сам решит когда показать превью
            IsPlacementMode = true; 
            return true;
        }

        /// <summary>
        /// Продать истощённую (или любую) шахту обратно.
        /// </summary>
        public void SellCurrentMine()
        {
            if (WellGen == null || !WellGen.IsMineGenerated || WellGen.ActiveMine == null)
            {
                Debug.Log("[MineMarket] Нет активной шахты для продажи.");
                return;
            }

            MineInstance mine = WellGen.ActiveMine;
            int price = mine.SellPrice;

            GlobalEconomy.Money += price;
            Debug.Log($"[MineMarket] Шахта '{mine.shopData.displayName}' продана за {price}₽. " +
                      $"Добыто: {mine.minedBlocks}/{mine.totalBlocks} блоков.");

            OnMineSold?.Invoke(mine);
            WellGen.DemolishMine();
        }

        /// <summary>Публичная отмена размещения (для UI-кнопки).</summary>
        public void CancelPlacementPublic() => CancelPlacement();

        /// <summary>Проверить, стоит ли сейчас шахта на участке.</summary>
        public bool IsMineGenerated() => WellGen != null && WellGen.IsMineGenerated;

        // ════════════════════════════════════════════════════════════════════
        // Внутренняя логика размещения
        // ════════════════════════════════════════════════════════════════════

        void EnterPlacementMode()
        {
            IsPlacementMode = true;
            ShowPreview(true);
        }

        void ConfirmPlacement()
        {
            if (pendingMine == null) return;
            if (WellGen != null && WellGen.IsInLobbyMode)
            {
                Debug.Log("[MineMarket] Нельзя разместить шахту в Лобби. Отправляйтесь на свой Остров!");
                return;
            }

            IsPlacementMode = false;
            ShowPreview(false);

            if (WellGen == null)
            {
                Debug.LogError("[MineMarket] WellGen не найден!");
                return;
            }

            // Находим локальные координаты в острове из позиции превью
            Vector3 worldPos = previewInstance.transform.position;
            Vector3 localPos = WellGen.transform.InverseTransformPoint(worldPos);
            
            // Округляем до индекса вокселя
            int gx = Mathf.RoundToInt(localPos.x);
            int gz = Mathf.RoundToInt(localPos.z);

            WellGen.GenerateMineAt(pendingMine, gx, gz);
            OnMinePlaced?.Invoke(pendingMine);
            pendingMine = null;
        }

        void CancelPlacement()
        {
            // Возвращаем деньги
            if (pendingMine != null)
            {
                GlobalEconomy.Money += pendingMine.shopData.buyPrice;
                Debug.Log($"[MineMarket] Размещение отменено. Возврат {pendingMine.shopData.buyPrice}₽.");
            }

            pendingMine = null;
            IsPlacementMode = false;
            ShowPreview(false);
            OnPlacementCancelled?.Invoke();
        }

        void ShowPreview(bool show)
        {
            if (!show)
            {
                if (previewInstance != null)
                    previewInstance.SetActive(false);
                return;
            }

            // Создаём превью при первом показе
            if (previewInstance == null)
            {
                if (placementPreviewPrefab != null)
                {
                    previewInstance = Instantiate(placementPreviewPrefab);
                }
                else
                {
                    previewInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    previewInstance.name = "MinePlacementPreview";

                    // Убираем коллайдер
                    Collider[] cols = previewInstance.GetComponentsInChildren<Collider>();
                    foreach (Collider c in cols)
                        c.enabled = false;

                    // Полупрозрачный материал
                    MeshRenderer mr = previewInstance.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (shader == null) shader = Shader.Find("Standard");
                        Material mat = new Material(shader);
                        mat.color = previewColor;

                        if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface", 1f);
                        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite", 0f);
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                        mr.material = mat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }

            if (pendingMine != null)
            {
                // Для превью используем размер шахты + паддинг (чтобы видеть зону влияния)
                float w = pendingMine.shopData.wellWidth  + pendingMine.shopData.padding * 2;
                float d = 0.5f; // Плоское превью для выбора места
                float l = pendingMine.shopData.wellLength + pendingMine.shopData.padding * 2;
                previewInstance.transform.localScale = new Vector3(w, d, l);
            }

            previewInstance.SetActive(true);
        }

        bool IsConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Mouse.current != null &&
                   UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        bool IsCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null &&
                   UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
