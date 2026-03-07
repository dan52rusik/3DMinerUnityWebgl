using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using YG;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class PlayerProgressPersistence : MonoBehaviour
    {
        private const string LocalSaveKey = "svs_progress_v1";
        private const float AutosaveIntervalSeconds = 8f;
        private const int ResetMoneyValue = EconomyTuning.StartMoney;
        private const int ResetXpValue = EconomyTuning.StartMiningXP;
        private const int ResetLevelValue = EconomyTuning.StartMiningLevel;

        [Serializable]
        private class ProgressSaveData
        {
            public int version = 1;
            public int money;
            public int bestMoneyBalance;
            public int miningXP;
            public int miningLevel;

            // Upgrades
            public int playerStrength = 0;
            public int maxBackpackCapacity = 10;
            public int upgStrengthCost = 100;
            public int upgBackpackCost = 150;
            public int dirtCount;
            public int stoneCount;
            public int ironCount;
            public int goldCount;

            public bool hasPrivateIsland;
            public SerializableVector3 privateIslandOffset;
            public bool hasCustomIslandSpawnPoint;
            public SerializableVector3 customIslandSpawnPoint;
            public bool hasMine;
            public MineSaveData mine;
            public List<MineSaveData> mines = new List<MineSaveData>();
            public List<PlayerBuildingSystem.SavedBlockState> builtBlocks = new List<PlayerBuildingSystem.SavedBlockState>();
        }

        [Serializable]
        private class MineSaveData
        {
            public int mineIndex = -1;
            public string mineDisplayName;
            public int rolledDepth;
            public int originX;
            public int originZ;
            public List<SerializableVector3Int> minedLocalPositions = new List<SerializableVector3Int>();
        }

        [Serializable]
        private struct SerializableVector3
        {
            public float x;
            public float y;
            public float z;

            public SerializableVector3(Vector3 value)
            {
                x = value.x;
                y = value.y;
                z = value.z;
            }

            public Vector3 ToVector3() => new Vector3(x, y, z);
        }

        [Serializable]
        private struct SerializableVector3Int
        {
            public int x;
            public int y;
            public int z;

            public SerializableVector3Int(Vector3Int value)
            {
                x = value.x;
                y = value.y;
                z = value.z;
            }

            public Vector3Int ToVector3Int() => new Vector3Int(x, y, z);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int YandexCloud_IsReady();

        [DllImport("__Internal")]
        private static extern void YandexCloud_Load(string gameObjectName, string callbackMethod);

        [DllImport("__Internal")]
        private static extern void YandexCloud_Save(string json);
#endif

        private WellGenerator wellGenerator;
        private MineMarket mineMarket;

        private bool isLoaded;
        private bool loadRequested;
        private bool dirty;
        private float nextAutosaveTime;
        private float sdkWaitStartTime;

        // Tracks the money value on the frame BEFORE any load completes.
        // If GlobalEconomy.Money differs from StartMoney when ApplyLoadedState
        // fires, it means a purchase happened during the load window and we
        // must carry the delta forward instead of blindly overwriting.
        private bool economyTouchedBeforeLoad;

        private int cachedMoney;
        private int cachedXP;
        private int cachedLevel;
        private int cachedMinedBlocks;
        private bool cachedHasMine;
        private bool cachedHasIsland;
        private bool cachedHasCustomSpawn;
        private Vector3 cachedCustomSpawn;

        // FIX #6: кешируем компоненты, чтобы не искать их каждый кадр
        private PlayerPickaxe cachedPickaxe;
        private UpgradeManager cachedUpgradeManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<PlayerProgressPersistence>() != null)
                return;

            GameObject go = new GameObject("PlayerProgressPersistence");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerProgressPersistence>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += OnSdkReady;
            // FIX #15: подписываемся на события Экономики — больше не нужно поллить каждый кадр
            GlobalEconomy.OnMoneyChanged  += OnEconomyChanged;
            GlobalEconomy.OnXPChanged     += OnEconomyChanged;
            GlobalEconomy.OnLevelChanged  += OnEconomyChanged;
            PlayerPickaxe.OnInventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= OnSdkReady;
            GlobalEconomy.OnMoneyChanged  -= OnEconomyChanged;
            GlobalEconomy.OnXPChanged     -= OnEconomyChanged;
            GlobalEconomy.OnLevelChanged  -= OnEconomyChanged;
            PlayerPickaxe.OnInventoryChanged -= OnInventoryChanged;
        }

        private void OnEconomyChanged(int _)
        {
            // срабатывает при любом изменении денег/XP/уровня — один центральный обработчик
            if (isLoaded)
                MarkDirty();
        }

        private void OnInventoryChanged()
        {
            if (isLoaded)
                MarkDirty();
        }

        private void OnDestroy()
        {
            if (mineMarket == null)
                return;

            mineMarket.OnMinePlaced -= OnMinePlaced;
            mineMarket.OnMineSold -= OnMineSold;
            mineMarket.OnPlacementCancelled -= OnPlacementCancelled;
        }

        /// <summary>
        /// Call this before any in-session money change (purchase, reward, etc.)
        /// so that a late-arriving save-load cannot overwrite the change.
        /// </summary>
        public void NotifyEconomyTouched()
        {
            economyTouchedBeforeLoad = true;
        }

        private IEnumerator Start()
        {
            yield return BindSceneRefs();
            sdkWaitStartTime = Time.unscaledTime;
            // Snapshot economy at the moment we're about to request a load.
            // Any deviation from StartMoney means the player already spent/earned
            // money in the current session (e.g. bought a mine while SDK was still
            // initialising). We'll apply that delta on top of the saved value.
            economyTouchedBeforeLoad = (GlobalEconomy.Money != EconomyTuning.StartMoney);
            RequestLoadIfPossible();
        }

        private IEnumerator BindSceneRefs()
        {
            while (wellGenerator == null)
            {
                wellGenerator = FindFirstObjectByType<WellGenerator>();
                if (wellGenerator == null)
                    yield return null;
            }

            while (mineMarket == null)
            {
                mineMarket = FindFirstObjectByType<MineMarket>();
                if (mineMarket == null)
                    yield return null;
            }

            mineMarket.OnMinePlaced += OnMinePlaced;
            mineMarket.OnMineSold += OnMineSold;
            mineMarket.OnPlacementCancelled += OnPlacementCancelled;
        }

        private void OnMinePlaced(MineInstance _) => MarkDirty();
        private void OnMineSold(MineInstance _) => MarkDirty();
        private void OnPlacementCancelled() => MarkDirty();

        private void Update()
        {
            TryFallbackLocalLoadOnSdkTimeout();

            if (!isLoaded || wellGenerator == null)
                return;

            DetectStateChanges();

            if (dirty && Time.unscaledTime >= nextAutosaveTime)
                SaveNow();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                SaveNow(force: true);
        }

        private void OnApplicationQuit()
        {
            SaveNow(force: true);
        }

        private void OnSdkReady()
        {
            RequestLoadIfPossible();
        }

        private void RequestLoadIfPossible()
        {
            if (loadRequested || wellGenerator == null)
                return;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (YG2.isSDKEnabled && YandexCloud_IsReady() == 1)
            {
                loadRequested = true;
                YandexCloud_Load(gameObject.name, nameof(OnCloudLoadResult));
                return;
            }
            return;
#endif
            loadRequested = true;
            string localJson = PlayerPrefs.GetString(LocalSaveKey, string.Empty);
            ApplyLoadedState(localJson);
        }

        private void TryFallbackLocalLoadOnSdkTimeout()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (isLoaded || loadRequested || wellGenerator == null)
                return;

            if (YG2.isSDKEnabled && YandexCloud_IsReady() == 1)
            {
                RequestLoadIfPossible();
                return;
            }

            // If SDK is unavailable for too long (e.g. local debug host), load local fallback.
            if (Time.unscaledTime - sdkWaitStartTime < 5f)
                return;

            loadRequested = true;
            string localJson = PlayerPrefs.GetString(LocalSaveKey, string.Empty);
            ApplyLoadedState(localJson);
#endif
        }

        public void OnCloudLoadResult(string json)
        {
            string payload = json;
            if (string.IsNullOrWhiteSpace(payload))
                payload = PlayerPrefs.GetString(LocalSaveKey, string.Empty);

            ApplyLoadedState(payload);
        }

        private void ApplyLoadedState(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                isLoaded = true;
                CaptureStateCache();
                return;
            }

            ProgressSaveData save;
            try
            {
                save = JsonUtility.FromJson<ProgressSaveData>(json);
            }
            catch
            {
                isLoaded = true;
                CaptureStateCache();
                return;
            }

            if (save == null)
            {
                isLoaded = true;
                CaptureStateCache();
                return;
            }

            GlobalEconomy.MiningLevel = Mathf.Max(1, save.miningLevel);

            // ── Money / XP: guard against the load racing with an in-session purchase ──
            // Scenario: player buys a mine → MineMarket deducts money from GlobalEconomy
            // → a moment later the cloud/local load fires and overwrites with the old value.
            // Fix: if money was already changed before this load completed, carry the delta
            // (spent / earned since session start) on top of the saved value.
            if (!economyTouchedBeforeLoad)
            {
                // Normal path – no purchases happened before the save loaded.
                GlobalEconomy.Money = save.money;
                GlobalEconomy.MiningXP = save.miningXP;
            }
            else
            {
                // Delta path – purchases raced ahead of the load.
                int deltaFromStart = GlobalEconomy.Money - EconomyTuning.StartMoney;
                // FIX #3: добавлен Mathf.Max(0, ...) — баланс не может уйти в минус
                GlobalEconomy.Money = Mathf.Max(0, save.money + deltaFromStart);
                // XP can only increase in a session; take the larger value.
                GlobalEconomy.MiningXP = Mathf.Max(GlobalEconomy.MiningXP, save.miningXP);
                Debug.Log($"[PlayerProgressPersistence] Load raced with session purchases. " +
                          $"Saved money={save.money}, delta={deltaFromStart}, result={GlobalEconomy.Money}");
            }

            GlobalEconomy.BestMoney = Mathf.Max(save.bestMoneyBalance, GlobalEconomy.Money);

            PlayerPickaxe pp = FindFirstObjectByType<PlayerPickaxe>();
            if (pp != null)
            {
                pp.playerStrength = Mathf.Max(0, save.playerStrength);
                pp.maxBackpackCapacity = Mathf.Max(5, save.maxBackpackCapacity);
                pp.SetInventoryCounts(save.dirtCount, save.stoneCount, save.ironCount, save.goldCount);
            }
            // FIX #6: кешируем после первого поиска
            cachedPickaxe = pp;

            UpgradeManager um = FindFirstObjectByType<UpgradeManager>();
            if (um != null)
            {
                um.playerStrengthCost = Mathf.Max(10, save.upgStrengthCost);
                um.backpackCapacityCost = Mathf.Max(10, save.upgBackpackCost);
            }
            cachedUpgradeManager = um;

            if (save.hasPrivateIsland)
                wellGenerator.EnsurePrivateIslandAtOffset(save.privateIslandOffset.ToVector3());

            if (save.hasCustomIslandSpawnPoint)
                wellGenerator.SetCustomIslandSpawnPointFromSave(save.customIslandSpawnPoint.ToVector3());
            else
                wellGenerator.ClearCustomIslandSpawnPoint();

            if (save.mines != null && save.mines.Count > 0)
            {
                List<MineInstance> restored = new List<MineInstance>();
                for (int i = 0; i < save.mines.Count; i++)
                {
                    MineSaveData mineSave = save.mines[i];
                    if (mineSave == null) continue;
                    MineShopData mineData = ResolveMineData(mineSave);
                    if (mineData == null) continue;

                    MineInstance restoredMine = new MineInstance(mineData, mineSave.rolledDepth, 0)
                    {
                        originX = mineSave.originX,
                        originZ = mineSave.originZ
                    };

                    if (mineSave.minedLocalPositions != null)
                    {
                        foreach (SerializableVector3Int local in mineSave.minedLocalPositions)
                            restoredMine.minedPositions.Add(local.ToVector3Int());

                        restoredMine.minedBlocks = restoredMine.minedPositions.Count;
                    }

                    restored.Add(restoredMine);
                }

                wellGenerator.RestoreMinesFromSave(restored);
            }
            else if (save.hasMine && save.mine != null)
            {
                MineShopData mineData = ResolveMineData(save.mine);
                if (mineData != null)
                {
                    MineInstance restoredMine = new MineInstance(mineData, save.mine.rolledDepth, 0)
                    {
                        originX = save.mine.originX,
                        originZ = save.mine.originZ
                    };

                    if (save.mine.minedLocalPositions != null)
                    {
                        foreach (SerializableVector3Int local in save.mine.minedLocalPositions)
                            restoredMine.minedPositions.Add(local.ToVector3Int());

                        restoredMine.minedBlocks = restoredMine.minedPositions.Count;
                    }

                    wellGenerator.RestoreMineFromSave(restoredMine);
                }
            }
            else
            {
                wellGenerator.RestoreMinesFromSave(null);
            }

            PlayerBuildingSystem buildingSystem = FindFirstObjectByType<PlayerBuildingSystem>();
            if (buildingSystem != null)
                buildingSystem.RestorePlacedBlocks(save.builtBlocks);

            isLoaded = true;
            CaptureStateCache();
        }

        private MineShopData ResolveMineData(MineSaveData saveMine)
        {
            if (mineMarket == null || mineMarket.availableMines == null || mineMarket.availableMines.Count == 0)
                return null;

            // FIX #8: сначала ищем по имени — устойчиво к изменению порядка в списке шахт
            if (!string.IsNullOrWhiteSpace(saveMine.mineDisplayName))
            {
                foreach (MineShopData data in mineMarket.availableMines)
                {
                    if (data != null && data.displayName == saveMine.mineDisplayName)
                        return data;
                }
            }

            // Fallback: индекс как запасной вариант (старые сохранения без имени)
            if (saveMine.mineIndex >= 0 && saveMine.mineIndex < mineMarket.availableMines.Count)
                return mineMarket.availableMines[saveMine.mineIndex];

            return null;
        }

        private void DetectStateChanges()
        {
            // FIX #15: Деньги/XP/уровень отслеживаются через события (OnEconomyChanged) — здесь только игровые объекты
            bool hasMine = wellGenerator != null && wellGenerator.PlacedMines != null && wellGenerator.PlacedMines.Count > 0;
            bool hasIsland = wellGenerator != null && wellGenerator.IsIslandGenerated;
            int minedBlocks = 0;
            if (hasMine)
            {
                for (int i = 0; i < wellGenerator.PlacedMines.Count; i++)
                {
                    MineInstance m = wellGenerator.PlacedMines[i];
                    if (m != null) minedBlocks += Mathf.Max(0, m.minedBlocks);
                }
            }

            if (cachedPickaxe == null) cachedPickaxe = FindFirstObjectByType<PlayerPickaxe>();
            if (cachedUpgradeManager == null) cachedUpgradeManager = FindFirstObjectByType<UpgradeManager>();

            bool hasCustomSpawn = wellGenerator != null && wellGenerator.HasCustomIslandSpawnPoint;
            Vector3 customSpawn = hasCustomSpawn ? wellGenerator.GetCustomIslandSpawnPoint() : Vector3.zero;

            if (hasMine != cachedHasMine ||
                hasIsland != cachedHasIsland ||
                minedBlocks != cachedMinedBlocks ||
                hasCustomSpawn != cachedHasCustomSpawn ||
                (hasCustomSpawn && (customSpawn - cachedCustomSpawn).sqrMagnitude > 0.0001f))
            {
                MarkDirty();
                CaptureStateCache();
            }
        }

        private void CaptureStateCache()
        {
            cachedHasMine = wellGenerator != null && wellGenerator.PlacedMines != null && wellGenerator.PlacedMines.Count > 0;
            cachedHasIsland = wellGenerator != null && wellGenerator.IsIslandGenerated;
            cachedMinedBlocks = 0;
            if (cachedHasMine)
            {
                for (int i = 0; i < wellGenerator.PlacedMines.Count; i++)
                {
                    MineInstance m = wellGenerator.PlacedMines[i];
                    if (m != null) cachedMinedBlocks += Mathf.Max(0, m.minedBlocks);
                }
            }
            cachedHasCustomSpawn = wellGenerator != null && wellGenerator.HasCustomIslandSpawnPoint;
            cachedCustomSpawn = cachedHasCustomSpawn ? wellGenerator.GetCustomIslandSpawnPoint() : Vector3.zero;
        }

        private void MarkDirty()
        {
            dirty = true;
            nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
        }

        public void NotifyGameplayStateChanged()
        {
            if (isLoaded)
                MarkDirty();
        }

        private void SaveNow(bool force = false)
        {
            if (!isLoaded || wellGenerator == null)
                return;

            if (!force && !dirty)
                return;

            ProgressSaveData save = BuildSaveData();
            string json = JsonUtility.ToJson(save);
            if (string.IsNullOrWhiteSpace(json))
                return;

            PlayerPrefs.SetString(LocalSaveKey, json);
            PlayerPrefs.Save();

#if UNITY_WEBGL && !UNITY_EDITOR
            if (YandexCloud_IsReady() == 1)
                YandexCloud_Save(json);
#endif

            dirty = false;
            nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
        }

        public void ResetProgressToNewPlayer()
        {
            ResetTutorialProgressFlags();

            if (wellGenerator == null)
                wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (mineMarket == null)
                mineMarket = FindFirstObjectByType<MineMarket>();

            if (mineMarket != null)
                mineMarket.CancelPlacementPublic();

            if (wellGenerator != null)
                wellGenerator.ResetPlayerWorldForNewProgress();

            GlobalEconomy.Money = ResetMoneyValue;
            GlobalEconomy.BestMoney = ResetMoneyValue;
            GlobalEconomy.MiningXP = ResetXpValue;
            GlobalEconomy.MiningLevel = ResetLevelValue;

            isLoaded = true;
            loadRequested = true;
            dirty = false;
            nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
            CaptureStateCache();

            SaveResetStateToStorage();
        }

        public static void ResetStoredProgressToNewPlayer()
        {
            PlayerProgressPersistence instance = FindFirstObjectByType<PlayerProgressPersistence>();
            if (instance != null)
            {
                instance.ResetProgressToNewPlayer();
                return;
            }

            ResetTutorialProgressFlags();

            string json = BuildDefaultResetJson();
            PlayerPrefs.SetString(LocalSaveKey, json);
            PlayerPrefs.Save();

#if UNITY_WEBGL && !UNITY_EDITOR
            if (YandexCloud_IsReady() == 1)
                YandexCloud_Save(json);
#endif
        }

        private void SaveResetStateToStorage()
        {
            ProgressSaveData resetSave = BuildSaveData();
            string json = JsonUtility.ToJson(resetSave);
            if (string.IsNullOrWhiteSpace(json))
                json = BuildDefaultResetJson();

            PlayerPrefs.SetString(LocalSaveKey, json);
            PlayerPrefs.Save();

#if UNITY_WEBGL && !UNITY_EDITOR
            if (YandexCloud_IsReady() == 1)
                YandexCloud_Save(json);
#endif
        }

        private static string BuildDefaultResetJson()
        {
            ProgressSaveData reset = new ProgressSaveData
            {
                money = ResetMoneyValue,
                bestMoneyBalance = ResetMoneyValue,
                miningXP = ResetXpValue,
                miningLevel = ResetLevelValue,
                hasPrivateIsland = false,
                hasMine = false
            };

            return JsonUtility.ToJson(reset);
        }

        private static void ResetTutorialProgressFlags()
        {
            OnboardingTutorial.ResetTutorialStatic();
        }

        private ProgressSaveData BuildSaveData()
        {
            ProgressSaveData save = new ProgressSaveData
            {
                money = GlobalEconomy.Money,
                bestMoneyBalance = GlobalEconomy.BestMoney,
                miningXP = GlobalEconomy.MiningXP,
                miningLevel = GlobalEconomy.MiningLevel,
                hasPrivateIsland = wellGenerator.IsIslandGenerated,
                privateIslandOffset = new SerializableVector3(wellGenerator.privateIslandOffset),
                hasCustomIslandSpawnPoint = wellGenerator.HasCustomIslandSpawnPoint,
                customIslandSpawnPoint = new SerializableVector3(wellGenerator.GetCustomIslandSpawnPoint())
            };

            // FIX #6: используем кешированные ссылки
            PlayerPickaxe pp = cachedPickaxe != null ? cachedPickaxe : FindFirstObjectByType<PlayerPickaxe>();
            if (pp != null)
            {
                save.playerStrength = pp.playerStrength;
                save.maxBackpackCapacity = pp.maxBackpackCapacity;
                save.dirtCount = pp.dirtCount;
                save.stoneCount = pp.stoneCount;
                save.ironCount = pp.ironCount;
                save.goldCount = pp.goldCount;
            }

            UpgradeManager um = cachedUpgradeManager != null ? cachedUpgradeManager : FindFirstObjectByType<UpgradeManager>();
            if (um != null)
            {
                save.upgStrengthCost = um.playerStrengthCost;
                save.upgBackpackCost = um.backpackCapacityCost;
            }

            if (wellGenerator.PlacedMines == null || wellGenerator.PlacedMines.Count == 0)
                return save;

            save.hasMine = true;
            save.mines = new List<MineSaveData>();
            for (int i = 0; i < wellGenerator.PlacedMines.Count; i++)
            {
                MineInstance mine = wellGenerator.PlacedMines[i];
                if (mine == null) continue;

                MineSaveData mineSave = new MineSaveData
                {
                    mineDisplayName = mine.shopData != null ? mine.shopData.displayName : string.Empty,
                    rolledDepth = mine.rolledDepth,
                    originX = mine.originX,
                    originZ = mine.originZ,
                    mineIndex = ResolveMineIndex(mine.shopData)
                };

                if (mine.minedPositions != null)
                {
                    foreach (Vector3Int local in mine.minedPositions)
                        mineSave.minedLocalPositions.Add(new SerializableVector3Int(local));
                }

                save.mines.Add(mineSave);
            }

            if (save.mines.Count > 0)
                save.mine = save.mines[save.mines.Count - 1];

            PlayerBuildingSystem buildingSystem = FindFirstObjectByType<PlayerBuildingSystem>();
            if (buildingSystem != null)
                save.builtBlocks = buildingSystem.CapturePlacedBlocks();

            return save;
        }

        private int ResolveMineIndex(MineShopData mineData)
        {
            if (mineMarket == null || mineMarket.availableMines == null || mineData == null)
                return -1;

            for (int i = 0; i < mineMarket.availableMines.Count; i++)
            {
                if (mineMarket.availableMines[i] == mineData)
                    return i;
            }

            return -1;
        }
    }
}
