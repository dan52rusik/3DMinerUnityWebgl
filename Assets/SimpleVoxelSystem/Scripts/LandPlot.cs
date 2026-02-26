using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Участок острова, который можно купить.
    /// После покупки — вызывает расширение VoxelIsland, а не просто SetActive.
    /// </summary>
    public class LandPlot : MonoBehaviour
    {
        [Header("Цена")]
        public int purchasePrice = 500;
        public bool isPurchased  = false;

        [Header("Сцена")]
        public GameObject buyVisuals;      // Табличка с ценой (отключается при покупке)
        public WellGenerator wellGenerator; // На кого расширяться

        [Header("Смещение в воксельных координатах")]
        public int offsetX = 15;
        public int offsetZ = 0;
        public int width   = 5;
        public int length  = 5;

        // ─────────────────────────────────────────────────────────────────────

        public void Purchase()
        {
            if (isPurchased)
            {
                Debug.Log("[LandPlot] Участок уже куплен.");
                return;
            }

            if (GlobalEconomy.Money < purchasePrice)
            {
                Debug.Log($"[LandPlot] Нужно {purchasePrice}₽, есть {GlobalEconomy.Money}₽.");
                return;
            }

            GlobalEconomy.Money -= purchasePrice;
            isPurchased = true;

            // Скрываем табличку
            if (buyVisuals != null)
                buyVisuals.SetActive(false);

            // Расширяем воксельный остров (не создаём новые GameObject-ы)
            if (wellGenerator != null)
                wellGenerator.GeneratePlotExtension(offsetX, offsetZ, width, length);

            Debug.Log($"[LandPlot] Куплен! Остаток: {GlobalEconomy.Money}₽");
        }
    }
}
