#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace SimpleVoxelSystem.Editor
{
    /// <summary>
    /// One menu: Tools → Mine System → Setup Scene
    /// Performs automation:
    ///   1. Creates folder Assets/SimpleVoxelSystem/Mines/
    ///   2. Creates three mine ScriptableObjects (if they don't exist yet)
    ///   3. Adds MineMarket to the WellGenerator GameObject
    ///   4. Links created mines in MineMarket.availableMines
    /// </summary>
    public static class MineSystemSetup
    {
        private const string MinesFolder = "Assets/SimpleVoxelSystem/Mines";

        [MenuItem("Tools/Mine System/Setup Scene")]
        public static void SetupScene()
        {
            // 1 — Folder Assets/SimpleVoxelSystem/Mines
            EnsureFolder("Assets/SimpleVoxelSystem");
            EnsureFolder(MinesFolder);

            // 2 — Mine ScriptableObjects
            MineShopData bronze  = EnsureMineAsset("Mine_Bronze",  "Bronze Mine",
                buyPrice: EconomyTuning.BronzeMinePrice, depthMin: EconomyTuning.BronzeMineDepthMin, depthMax: EconomyTuning.BronzeMineDepthMax,
                labelColor: new Color(0.80f, 0.50f, 0.20f),
                layers: new BlockLayer[]
                {
                    new BlockLayer { maxDepth=2,  dirtWeight=90, stoneWeight=10, ironWeight=0,  goldWeight=0 },
                    new BlockLayer { maxDepth=30, dirtWeight=40, stoneWeight=55, ironWeight=5,  goldWeight=0 },
                });

            MineShopData silver  = EnsureMineAsset("Mine_Silver", "Silver Mine",
                buyPrice: EconomyTuning.SilverMinePrice, depthMin: EconomyTuning.SilverMineDepthMin, depthMax: EconomyTuning.SilverMineDepthMax,
                labelColor: new Color(0.70f, 0.70f, 0.75f),
                layers: new BlockLayer[]
                {
                    new BlockLayer { maxDepth=2,  dirtWeight=70, stoneWeight=30, ironWeight=0,  goldWeight=0 },
                    new BlockLayer { maxDepth=6,  dirtWeight=20, stoneWeight=55, ironWeight=25, goldWeight=0 },
                    new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=60, ironWeight=30, goldWeight=5 },
                });

            MineShopData gold    = EnsureMineAsset("Mine_Gold", "Gold Mine",
                buyPrice: EconomyTuning.GoldMinePrice, depthMin: EconomyTuning.GoldMineDepthMin, depthMax: EconomyTuning.GoldMineDepthMax,
                labelColor: new Color(1.00f, 0.84f, 0.10f),
                layers: new BlockLayer[]
                {
                    new BlockLayer { maxDepth=2,  dirtWeight=50, stoneWeight=50, ironWeight=0,  goldWeight=0  },
                    new BlockLayer { maxDepth=6,  dirtWeight=10, stoneWeight=50, ironWeight=35, goldWeight=5  },
                    new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=40, ironWeight=35, goldWeight=20 },
                });

            AssetDatabase.SaveAssets();

            // 3 — MineMarket on WellGenerator
            WellGenerator wg = Object.FindFirstObjectByType<WellGenerator>();
            if (wg == null)
            {
                Debug.LogWarning("[MineSystemSetup] WellGenerator not found in the scene. Add it manually.");
                EditorUtility.DisplayDialog("Mine System Setup",
                    "WellGenerator not found in the scene.\nAdd a GameObject with WellGenerator and run Setup again.", "OK");
                return;
            }

            MineMarket market = wg.GetComponent<MineMarket>();
            if (market == null)
                market = wg.gameObject.AddComponent<MineMarket>();

            // 4 — Link mines
            market.availableMines = new List<MineShopData> { bronze, silver, gold };
            EditorUtility.SetDirty(market);

            // 5 — MineShopUI (add to the same GameObject if missing)
            MineShopUI shopUI = Object.FindFirstObjectByType<MineShopUI>();
            if (shopUI == null)
            {
                // Search for Canvas or create a new GO
                Canvas canvas = Object.FindFirstObjectByType<Canvas>();
                GameObject uiHost;
                if (canvas != null)
                {
                    uiHost = canvas.gameObject;
                }
                else
                {
                    uiHost = new GameObject("MineShopUI");
                }
                shopUI = uiHost.AddComponent<MineShopUI>();
                Debug.Log("[MineSystemSetup] MineShopUI added to " + uiHost.name);
            }

            shopUI.mineMarket = market;
            EditorUtility.SetDirty(shopUI);

            // 6 — Starting money (only if 0)
            if (GlobalEconomy.Money == 0)
                GlobalEconomy.Money = EconomyTuning.StartMoney;

            Debug.Log("[MineSystemSetup] ✅ Done! MineMarket configured, 3 mines created in " + MinesFolder);
            EditorUtility.DisplayDialog("Mine System Setup",
                "✅ Setup completed!\n\n" +
                "• 3 mine classes created in Assets/SimpleVoxelSystem/Mines/\n" +
                "• MineMarket added to WellGenerator\n" +
                "• MineShopUI added — all UI is built automatically\n" +
                $"• Starting money: ${EconomyTuning.StartMoney}\n\n" +
                "▶ Just press Play!", "OK");
        }

        // ─────────────────────────────────────────────────────────────────────

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        static MineShopData EnsureMineAsset(string assetName, string displayName,
            int buyPrice, int depthMin, int depthMax,
            Color labelColor, BlockLayer[] layers)
        {
            string path = $"{MinesFolder}/{assetName}.asset";
            MineShopData existing = AssetDatabase.LoadAssetAtPath<MineShopData>(path);
            if (existing != null)
            {
                Debug.Log($"[MineSystemSetup] '{assetName}' already exists, skipping.");
                return existing;
            }

            MineShopData data = ScriptableObject.CreateInstance<MineShopData>();
            data.displayName    = displayName;
            data.buyPrice       = buyPrice;
            data.sellBackRatio  = EconomyTuning.BronzeMineSellBackRatio;
            data.depthMin       = depthMin;
            data.depthMax       = depthMax;
            data.labelColor     = labelColor;
            data.layers         = layers;
            data.wellWidth      = EconomyTuning.DefaultMineWellWidth;
            data.wellLength     = EconomyTuning.DefaultMineWellLength;
            data.padding        = EconomyTuning.DefaultMinePadding;

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[MineSystemSetup] Created '{path}'");
            return data;
        }
    }
}
#endif
