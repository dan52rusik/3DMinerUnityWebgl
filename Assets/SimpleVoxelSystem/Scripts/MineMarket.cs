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
            // Если шахта не куплена — ничего не делаем
            if (pendingMine == null) 
            {
                if (IsPlacementMode) { IsPlacementMode = false; ShowPreview(false); }
                return;
            }

            IsPlacementMode = true;

            // Но призрака показываем только на Острове
            bool onIsland = WellGen != null && !WellGen.IsInLobbyMode;
            ShowPreview(onIsland);

            if (onIsland)
            {
                UpdatePlacementPreview();
                
                // Левый клик = подтвердить размещение
                if (IsConfirmPressed())
                {
                    ConfirmPlacement();
                }
            }

            // Отменить покупку (Escape)
            if (IsCancelPressed())
            {
                CancelPlacement();
            }
        }

        private Vector3 lastValidPlacementPos;

        void UpdatePlacementPreview()
        {
            if (previewInstance == null || !previewInstance.activeSelf) return;

            Ray ray = Camera.main.ScreenPointToRay(GetMousePosition());
            ray.origin += ray.direction * 0.1f; 

            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                VoxelIsland land = hit.collider.GetComponentInParent<VoxelIsland>();
                if (land != null)
                {
                    Vector3 localPos = land.transform.InverseTransformPoint(hit.point);
                    int gx = Mathf.RoundToInt(localPos.x);
                    int gz = Mathf.RoundToInt(localPos.z);
                    
                    Vector3 snappedLocal = new Vector3(gx, -WellGen.LobbyFloorY + 1.1f, gz);
                    previewInstance.transform.position = land.transform.TransformPoint(snappedLocal);
                    
                    lastValidPlacementPos = previewInstance.transform.position;
                    SetPreviewVisibility(true);
                }
                else
                {
                    // Если не попали в остров — скрываем превью (чтобы не висело в воздухе)
                    SetPreviewVisibility(false);
                }
            }
            else
            {
                SetPreviewVisibility(false);
            }
        }

        private void SetPreviewVisibility(bool visible)
        {
            if (previewInstance == null) return;
            MeshRenderer mr = previewInstance.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = visible;
        }

        Vector2 GetMousePosition()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                return new Vector2(Screen.width / 2f, Screen.height / 2f);
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        public bool TryBuyMine(MineShopData data)
        {
            if (data == null) return false;
            if (WellGen != null && WellGen.IsMineGenerated) { Debug.Log("[MineMarket] Участок занят."); return false; }
            if (GlobalEconomy.Money < data.buyPrice) { Debug.Log("[MineMarket] Мало денег."); return false; }

            GlobalEconomy.Money -= data.buyPrice;
            int depth = data.RollDepth();
            pendingMine = new MineInstance(data, depth, 0);

            Debug.Log($"[MineMarket] Куплена '{data.displayName}'. Режим установки ВКЛЮЧЕН.");
            IsPlacementMode = true; 
            return true;
        }

        public void SellCurrentMine()
        {
            if (WellGen == null || !WellGen.IsMineGenerated || WellGen.ActiveMine == null) return;
            MineInstance mine = WellGen.ActiveMine;
            GlobalEconomy.Money += mine.SellPrice;
            OnMineSold?.Invoke(mine);
            WellGen.DemolishMine();
        }

        public void CancelPlacementPublic() => CancelPlacement();
        public bool IsMineGenerated() => WellGen != null && WellGen.IsMineGenerated;

        void ConfirmPlacement()
        {
            if (pendingMine == null) return;
            Vector3 worldPos = previewInstance.transform.position;
            Vector3 localPos = WellGen.ActiveIsland.transform.InverseTransformPoint(worldPos);
            int gx = Mathf.RoundToInt(localPos.x);
            int gz = Mathf.RoundToInt(localPos.z);

            Debug.Log($"[MineMarket] Установка шахты в сетку: {gx}, {gz}");
            WellGen.GenerateMineAt(pendingMine, gx, gz);
            OnMinePlaced?.Invoke(pendingMine);
            pendingMine = null;
            IsPlacementMode = false;
            ShowPreview(false);
        }

        void CancelPlacement()
        {
            if (pendingMine != null) GlobalEconomy.Money += pendingMine.shopData.buyPrice;
            pendingMine = null;
            IsPlacementMode = false;
            ShowPreview(false);
            OnPlacementCancelled?.Invoke();
        }

        void ShowPreview(bool show)
        {
            if (!show) { if (previewInstance != null) previewInstance.SetActive(false); return; }
            if (previewInstance == null)
            {
                previewInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                previewInstance.name = "MinePlacementPreview";
                foreach (var c in previewInstance.GetComponentsInChildren<Collider>()) c.enabled = false;
                
                MeshRenderer mr = previewInstance.GetComponent<MeshRenderer>();
                // Пробуем несколько шейдеров, чтобы избежать розового цвета (Magenta)
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                if (s == null) s = Shader.Find("Sprites/Default");
                
                Material mat = new Material(s);
                mat.color = new Color(0, 1, 0.4f, 0.4f);
                
                // Настройка прозрачности для Standard/Lit
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
                mr.material = mat;
            }
            if (pendingMine != null)
            {
                float w = pendingMine.shopData.wellWidth + pendingMine.shopData.padding * 2;
                float l = pendingMine.shopData.wellLength + pendingMine.shopData.padding * 2;
                previewInstance.transform.localScale = new Vector3(w, 0.5f, l);
            }
            previewInstance.SetActive(true);
        }

        bool IsConfirmPressed()
        {
            bool triggered = false;
#if ENABLE_INPUT_SYSTEM
            triggered = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
            triggered = Input.GetMouseButtonDown(0);
#endif
            if (triggered)
            {
                bool overUI = (Cursor.lockState != CursorLockMode.Locked) && 
                              (UnityEngine.EventSystems.EventSystem.current != null && 
                               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());
                
                // Debug.Log($"[MineMarket] LMB Click. UI Blocking: {overUI}. MouseLocked: {Cursor.lockState == CursorLockMode.Locked}");
                return !overUI;
            }
            return false;
        }

        bool IsCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
