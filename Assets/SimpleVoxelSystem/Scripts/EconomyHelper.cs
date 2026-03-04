using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// A helper component to expose GlobalEconomy values in the Inspector.
    /// Read-only display by default. To manually override a value during Play,
    /// enable the toggle next to the field, edit it — then disable the toggle.
    /// </summary>
    public class EconomyHelper : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Live Display (read-only during normal gameplay)")]
        public int currentMoney;
        public int currentXP;
        public int currentLevel;

        [Header("Manual Override (enable to push value → GlobalEconomy)")]
        public bool overrideMoney;
        public bool overrideXP;
        public bool overrideLevel;

        // Shadow values: what the Inspector showed last frame.
        // Used to detect a deliberate manual edit by the developer.
        private int lastMoney;
        private int lastXP;
        private int lastLevel;
#endif

        private void Start()
        {
            SyncToInspector();
#if UNITY_EDITOR
            lastMoney = currentMoney;
            lastXP    = currentXP;
            lastLevel = currentLevel;
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            // ── Write path: only push to GlobalEconomy when the developer
            //    has enabled the override toggle AND changed the value. ──────
            if (overrideMoney && currentMoney != lastMoney)
                GlobalEconomy.Money = currentMoney;

            if (overrideXP && currentXP != lastXP)
                GlobalEconomy.MiningXP = currentXP;

            if (overrideLevel && currentLevel != lastLevel)
                GlobalEconomy.MiningLevel = currentLevel;

            // ── Read path: always mirror GlobalEconomy → Inspector
            //    (unless override is active, so the user can type freely) ───
            if (!overrideMoney) currentMoney = GlobalEconomy.Money;
            if (!overrideXP)    currentXP    = GlobalEconomy.MiningXP;
            if (!overrideLevel) currentLevel = GlobalEconomy.MiningLevel;

            lastMoney = currentMoney;
            lastXP    = currentXP;
            lastLevel = currentLevel;
#endif
        }

        private void SyncToInspector()
        {
#if UNITY_EDITOR
            currentMoney = GlobalEconomy.Money;
            currentXP    = GlobalEconomy.MiningXP;
            currentLevel = GlobalEconomy.MiningLevel;
#endif
        }
    }
}