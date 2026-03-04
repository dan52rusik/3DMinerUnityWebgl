using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// A helper component to expose GlobalEconomy values in the Inspector.
    /// Changes in the inspector will reflect in the game.
    /// </summary>
    public class EconomyHelper : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Manual Adjustment (Editor Only)")]
        public int currentMoney;
        public int currentXP;
        public int currentLevel;
#endif

        private void Start()
        {
            SyncToInspector();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // If values in inspector don't match static economy, update them
            // This allows editing money during play mode
            if (currentMoney != GlobalEconomy.Money)
            {
                GlobalEconomy.Money = currentMoney;
            }
            
            if (currentXP != GlobalEconomy.MiningXP)
            {
                GlobalEconomy.MiningXP = currentXP;
            }

            if (currentLevel != GlobalEconomy.MiningLevel)
            {
                GlobalEconomy.MiningLevel = currentLevel;
            }

            SyncToInspector();
#endif
        }

        private void SyncToInspector()
        {
#if UNITY_EDITOR
            currentMoney = GlobalEconomy.Money;
            currentXP = GlobalEconomy.MiningXP;
            currentLevel = GlobalEconomy.MiningLevel;
#endif
        }
    }
}
