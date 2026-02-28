#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace SimpleVoxelSystem.Editor
{
    /// <summary>
    /// Одно меню: Tools → Mine System → Setup Scene
    /// Делает всё сам:
    ///   1. Создаёт папку Assets/SimpleVoxelSystem/Mines/
    ///   2. Создаёт три ScriptableObject-шахты (если их ещё нет)
    ///   3. Добавляет MineMarket на GameObject с WellGenerator
    ///   4. Подключает созданные шахты в MineMarket.availableMines
    /// </summary>
    public static class MineSystemSetup
    {
        private const string MinesFolder = "Assets/SimpleVoxelSystem/Mines";

        [MenuItem("Tools/Mine System/Setup Scene")]
        public static void SetupScene()
        {
            // 1 — Папка Assets/SimpleVoxelSystem/Mines
            EnsureFolder("Assets/SimpleVoxelSystem");
            EnsureFolder(MinesFolder);

            // 2 — ScriptableObject'ы шахт
            MineShopData bronze  = EnsureMineAsset("Mine_Bronze",  "Бронзовая шахта",
                buyPrice: 300, depthMin: 3, depthMax: 5,
                labelColor: new Color(0.80f, 0.50f, 0.20f),
                layers: new BlockLayer[]
                {
                    new BlockLayer { maxDepth=2,  dirtWeight=90, stoneWeight=10, ironWeight=0,  goldWeight=0 },
                    new BlockLayer { maxDepth=30, dirtWeight=40, stoneWeight=55, ironWeight=5,  goldWeight=0 },
                });

            MineShopData silver  = EnsureMineAsset("Mine_Silver", "Серебряная шахта",
                buyPrice: 800, depthMin: 5, depthMax: 9,
                labelColor: new Color(0.70f, 0.70f, 0.75f),
                layers: new BlockLayer[]
                {
                    new BlockLayer { maxDepth=2,  dirtWeight=70, stoneWeight=30, ironWeight=0,  goldWeight=0 },
                    new BlockLayer { maxDepth=6,  dirtWeight=20, stoneWeight=55, ironWeight=25, goldWeight=0 },
                    new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=60, ironWeight=30, goldWeight=5 },
                });

            MineShopData gold    = EnsureMineAsset("Mine_Gold", "Золотая шахта",
                buyPrice: 2000, depthMin: 10, depthMax: 15,
                labelColor: new Color(1.00f, 0.84f, 0.10f),
                layers: new BlockLayer[]
                {
                    new BlockLayer { maxDepth=2,  dirtWeight=50, stoneWeight=50, ironWeight=0,  goldWeight=0  },
                    new BlockLayer { maxDepth=6,  dirtWeight=10, stoneWeight=50, ironWeight=35, goldWeight=5  },
                    new BlockLayer { maxDepth=30, dirtWeight=5,  stoneWeight=40, ironWeight=35, goldWeight=20 },
                });

            AssetDatabase.SaveAssets();

            // 3 — MineMarket на WellGenerator
            WellGenerator wg = Object.FindFirstObjectByType<WellGenerator>();
            if (wg == null)
            {
                Debug.LogWarning("[MineSystemSetup] WellGenerator не найден в сцене. Добавьте его вручную.");
                EditorUtility.DisplayDialog("Mine System Setup",
                    "WellGenerator не найден в сцене.\nДобавьте GameObject с WellGenerator и запустите Setup ещё раз.", "OK");
                return;
            }

            MineMarket market = wg.GetComponent<MineMarket>();
            if (market == null)
                market = wg.gameObject.AddComponent<MineMarket>();

            // 4 — Подключаем шахты
            market.availableMines = new List<MineShopData> { bronze, silver, gold };
            EditorUtility.SetDirty(market);

            // 5 — MineShopUI (добавляем на тот же GameObject если нет)
            MineShopUI shopUI = Object.FindFirstObjectByType<MineShopUI>();
            if (shopUI == null)
            {
                // Ищем Canvas или создаём пустой GO
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
                Debug.Log("[MineSystemSetup] MineShopUI добавлен на " + uiHost.name);
            }

            shopUI.mineMarket = market;
            EditorUtility.SetDirty(shopUI);

            // 6 — Стартовые деньги (только если 0)
            if (GlobalEconomy.Money == 0)
                GlobalEconomy.Money = 500;

            Debug.Log("[MineSystemSetup] ✅ Готово! MineMarket настроен, 3 шахты созданы в " + MinesFolder);
            EditorUtility.DisplayDialog("Mine System Setup",
                "✅ Настройка завершена!\n\n" +
                "• 3 класса шахт созданы в Assets/SimpleVoxelSystem/Mines/\n" +
                "• MineMarket добавлен на WellGenerator\n" +
                "• MineShopUI добавлен — весь UI строится автоматически\n" +
                "• Стартовые деньги: 500₽\n\n" +
                "▶ Просто нажмите Play!", "OK");
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
                Debug.Log($"[MineSystemSetup] '{assetName}' уже существует, пропускаем.");
                return existing;
            }

            MineShopData data = ScriptableObject.CreateInstance<MineShopData>();
            data.displayName    = displayName;
            data.buyPrice       = buyPrice;
            data.sellBackRatio  = 0.5f;
            data.depthMin       = depthMin;
            data.depthMax       = depthMax;
            data.labelColor     = labelColor;
            data.layers         = layers;
            data.wellWidth      = 5;
            data.wellLength     = 5;
            data.padding        = 3;

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[MineSystemSetup] Создан '{path}'");
            return data;
        }
    }
}
#endif
