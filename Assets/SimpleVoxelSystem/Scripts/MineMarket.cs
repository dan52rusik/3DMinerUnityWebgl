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

        private WellGenerator   wellGenerator;
        private MineInstance    pendingMine;       // шахта, ожидающая размещения
        private GameObject      previewInstance;   // призрак-превью

        // Событие для UI
        public event System.Action<MineInstance> OnMinePlaced;
        public event System.Action<MineInstance> OnMineSold;
        public event System.Action              OnPlacementCancelled;

        // ────────────────────────────────────────────────────────────────────

        void Awake()
        {
            wellGenerator = GetComponent<WellGenerator>();
            if (wellGenerator == null)
                wellGenerator = GetComponentInParent<WellGenerator>();
        }

        void Update()
        {
            if (!IsPlacementMode) return;

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

            if (wellGenerator != null && wellGenerator.IsMineGenerated)
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
                      $"Выберите место для установки. Остаток: {GlobalEconomy.Money}₽.");

            EnterPlacementMode();
            return true;
        }

        /// <summary>
        /// Продать истощённую (или любую) шахту обратно.
        /// </summary>
        public void SellCurrentMine()
        {
            if (wellGenerator == null || !wellGenerator.IsMineGenerated || wellGenerator.ActiveMine == null)
            {
                Debug.Log("[MineMarket] Нет активной шахты для продажи.");
                return;
            }

            MineInstance mine = wellGenerator.ActiveMine;
            int price = mine.SellPrice;

            GlobalEconomy.Money += price;
            Debug.Log($"[MineMarket] Шахта '{mine.shopData.displayName}' продана за {price}₽. " +
                      $"Добыто: {mine.minedBlocks}/{mine.totalBlocks} блоков.");

            OnMineSold?.Invoke(mine);
            wellGenerator.DemolishMine();
        }

        /// <summary>Публичная отмена размещения (для UI-кнопки).</summary>
        public void CancelPlacementPublic() => CancelPlacement();

        /// <summary>Проверить, стоит ли сейчас шахта на участке.</summary>
        public bool IsMineGenerated() => wellGenerator != null && wellGenerator.IsMineGenerated;

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

            IsPlacementMode = false;
            ShowPreview(false);

            if (wellGenerator == null)
            {
                Debug.LogError("[MineMarket] WellGenerator не найден!");
                return;
            }

            wellGenerator.GenerateMine(pendingMine);
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
                float w = pendingMine.shopData.wellWidth;
                float d = pendingMine.rolledDepth;
                float l = pendingMine.shopData.wellLength;
                previewInstance.transform.localScale = new Vector3(w, d, l);
            }

            // Позиционируем превью над WellGenerator
            if (wellGenerator != null)
                previewInstance.transform.position = wellGenerator.transform.position + new Vector3(2f, 1f, 0f);

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
