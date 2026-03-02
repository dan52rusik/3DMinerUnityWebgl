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

        [Serializable]
        private class ProgressSaveData
        {
            public int version = 1;
            public int money;
            public int miningXP;
            public int miningLevel;
            public bool hasPrivateIsland;
            public SerializableVector3 privateIslandOffset;
            public bool hasMine;
            public MineSaveData mine;
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

        private int cachedMoney;
        private int cachedXP;
        private int cachedLevel;
        private int cachedMinedBlocks;
        private bool cachedHasMine;
        private bool cachedHasIsland;

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
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= OnSdkReady;
        }

        private void OnDestroy()
        {
            if (mineMarket == null)
                return;

            mineMarket.OnMinePlaced -= OnMinePlaced;
            mineMarket.OnMineSold -= OnMineSold;
            mineMarket.OnPlacementCancelled -= OnPlacementCancelled;
        }

        private IEnumerator Start()
        {
            yield return BindSceneRefs();
            sdkWaitStartTime = Time.unscaledTime;
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

            GlobalEconomy.Money = save.money;
            GlobalEconomy.MiningXP = save.miningXP;
            GlobalEconomy.MiningLevel = Mathf.Max(1, save.miningLevel);

            if (save.hasPrivateIsland)
                wellGenerator.EnsurePrivateIslandAtOffset(save.privateIslandOffset.ToVector3());

            if (save.hasMine && save.mine != null)
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
                wellGenerator.RestoreMineFromSave(null);
            }

            isLoaded = true;
            CaptureStateCache();
        }

        private MineShopData ResolveMineData(MineSaveData saveMine)
        {
            if (mineMarket == null || mineMarket.availableMines == null || mineMarket.availableMines.Count == 0)
                return null;

            if (saveMine.mineIndex >= 0 && saveMine.mineIndex < mineMarket.availableMines.Count)
                return mineMarket.availableMines[saveMine.mineIndex];

            if (!string.IsNullOrWhiteSpace(saveMine.mineDisplayName))
            {
                foreach (MineShopData data in mineMarket.availableMines)
                {
                    if (data != null && data.displayName == saveMine.mineDisplayName)
                        return data;
                }
            }

            return null;
        }

        private void DetectStateChanges()
        {
            int currentMoney = GlobalEconomy.Money;
            int currentXP = GlobalEconomy.MiningXP;
            int currentLevel = GlobalEconomy.MiningLevel;
            bool hasMine = wellGenerator != null && wellGenerator.ActiveMine != null;
            bool hasIsland = wellGenerator != null && wellGenerator.IsIslandGenerated;
            int minedBlocks = hasMine ? wellGenerator.ActiveMine.minedBlocks : 0;

            if (currentMoney != cachedMoney ||
                currentXP != cachedXP ||
                currentLevel != cachedLevel ||
                hasMine != cachedHasMine ||
                hasIsland != cachedHasIsland ||
                minedBlocks != cachedMinedBlocks)
            {
                MarkDirty();
                CaptureStateCache();
            }
        }

        private void CaptureStateCache()
        {
            cachedMoney = GlobalEconomy.Money;
            cachedXP = GlobalEconomy.MiningXP;
            cachedLevel = GlobalEconomy.MiningLevel;
            cachedHasMine = wellGenerator != null && wellGenerator.ActiveMine != null;
            cachedHasIsland = wellGenerator != null && wellGenerator.IsIslandGenerated;
            cachedMinedBlocks = cachedHasMine ? wellGenerator.ActiveMine.minedBlocks : 0;
        }

        private void MarkDirty()
        {
            dirty = true;
            nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
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

        private ProgressSaveData BuildSaveData()
        {
            ProgressSaveData save = new ProgressSaveData
            {
                money = GlobalEconomy.Money,
                miningXP = GlobalEconomy.MiningXP,
                miningLevel = GlobalEconomy.MiningLevel,
                hasPrivateIsland = wellGenerator.IsIslandGenerated,
                privateIslandOffset = new SerializableVector3(wellGenerator.privateIslandOffset)
            };

            MineInstance mine = wellGenerator.ActiveMine;
            if (mine == null)
                return save;

            save.hasMine = true;
            save.mine = new MineSaveData
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
                    save.mine.minedLocalPositions.Add(new SerializableVector3Int(local));
            }

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
