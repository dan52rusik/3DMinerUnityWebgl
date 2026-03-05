using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// An island plot that can be purchased.
    /// Once purchased, it triggers a VoxelIsland expansion rather than just SetActive.
    /// </summary>
    public class LandPlot : MonoBehaviour
    {
        [Header("Price")]
        public int purchasePrice = EconomyTuning.LandPlotPurchasePrice;
        public bool isPurchased  = false;

        [Header("Scene")]
        public GameObject buyVisuals;      // Price tag visuals (disabled upon purchase)
        public WellGenerator wellGenerator; // Generator instance to expand

        [Header("Voxel Offset")]
        public int offsetX = 15;
        public int offsetZ = 0;
        public int width   = 5;
        public int length  = 5;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (purchasePrice <= 0)
                purchasePrice = EconomyTuning.LandPlotPurchasePrice;
        }

        public void Purchase()
        {
            if (isPurchased)
            {
                Debug.Log("[LandPlot] Plot already purchased.");
                return;
            }

            if (GlobalEconomy.Money < purchasePrice)
            {
                Debug.Log($"[LandPlot] Need ${purchasePrice}, have ${GlobalEconomy.Money}.");
                return;
            }

            GlobalEconomy.Money -= purchasePrice;
            isPurchased = true;

            // Hide visuals
            if (buyVisuals != null)
                buyVisuals.SetActive(false);

            // Expand voxel island (instead of creating new GameObjects)
            if (wellGenerator != null)
                wellGenerator.GeneratePlotExtension(offsetX, offsetZ, width, length);

            Debug.Log($"[LandPlot] Purchased! Remaining: ${GlobalEconomy.Money}");
        }
    }
}
