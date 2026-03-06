using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static YG.InfoYG;

namespace YG
{
    public static partial class YG2
    {
        public static class MultiplayerSessions
        {
#if UNITY_EDITOR
            public static InfoYG.MultiplayerSessionsSettings info => YG2.infoYG.MultiplayerSessions;
#endif

            public static event Action<List<Session>> onSessionsLoaded;

            public static void Init(InitConfig config)
            {
#if UNITY_EDITOR
                configStatic = new SimulatedSession();
                var allSim = info.simulatedSessions;
                var filtered = new List<Session>();
                var filteredResult = new List<Session>();
                int targetCount = config.count;

                foreach (var sim in allSim)
                {
                    bool matches = true;

                    if (config.meta != null)
                    {
                        if (config.meta.meta1 != null && config.meta.meta1.max == 0 && config.meta.meta1.min == 0) config.meta.meta1 = null;
                        if (config.meta.meta2 != null && config.meta.meta2.max == 0 && config.meta.meta2.min == 0) config.meta.meta2 = null;
                        if (config.meta.meta3 != null && config.meta.meta3.max == 0 && config.meta.meta3.min == 0) config.meta.meta3 = null;

                        if (config.meta.meta1 != null && !(sim.meta.meta1 > config.meta.meta1.min && sim.meta.meta1 < config.meta.meta1.max)) matches = false;
                        if (config.meta.meta2 != null && !(sim.meta.meta2 > config.meta.meta2.min && sim.meta.meta2 < config.meta.meta2.max)) matches = false;
                        if (config.meta.meta3 != null && !(sim.meta.meta3 > config.meta.meta3.min && sim.meta.meta3 < config.meta.meta3.max)) matches = false;
                    }

                    if (matches)
                    {
                        filtered.Add(new Session
                        {
                            id = sim.id,
                            meta = sim.meta,
                            player = sim.player,
                            timeline = new List<Frame>(sim.timeline)
                        });
                    }
                }
                var rnd = new System.Random();
                while (filtered.Count != 0 && targetCount > 0)
                {
                    int rndint = rnd.Next(0, filtered.Count);
                    {
                        filteredResult.Add(filtered[rndint]);
                        filtered.Remove(filtered[rndint]);
                        targetCount--;
                    }
                }

                InvokeSessionsLoaded(filteredResult);
                Debug.Log($"[Sim] Init: {config}");
#else
                string configJson = JsonUtility.ToJson(config);
                iPlatform.Init(configJson);
#endif
            }
#if UNITY_EDITOR
            public static SimulatedSession configStatic = null;
#endif
            public static void Commit(Payload payload)
            {
                string payloadJson = JsonUtility.ToJson(payload);
#if UNITY_EDITOR
                var frame = new Frame();
                frame.payload = payload;
                if(configStatic != null) configStatic.timeline.Add(frame);
                Debug.Log($"[Sim] Commit: {payloadJson}");
#else
                iPlatform.Commit(payloadJson);
#endif
            }

            public static void Push(Meta meta)
            {
                string metaJson = JsonUtility.ToJson(meta);
#if UNITY_EDITOR
                if(configStatic != null) configStatic.meta = meta;
                if(configStatic != null) info.simulatedSessions.Add(configStatic);
                configStatic = null;
                Debug.Log($"[Sim] Push: {metaJson}");
#else
                iPlatform.Push(metaJson);
#endif
            }

            // Метод для Invoke события из других классов (e.g., YGSendMessage)
            public static void InvokeSessionsLoaded(List<Session> sessions)
            {
                onSessionsLoaded?.Invoke(sessions ?? new List<Session>());
            }
        }

        [InitYG]
        private static void InitMultiplayerSessions()
        {
            // Авто-инициализация: ничего, т.к. Init вызывается вручную
        }
    }

    [System.Serializable]
    public class Frame
    {
        public string id;
        public long time;
        public Payload payload;
    }

    [System.Serializable]
    public class Player
    {
        public string name;
        public string avatar;
    }

    [System.Serializable]
    public class Meta
    {
        public long meta1;
        public long meta2;
        public long meta3;
    }

    [System.Serializable]
    public class Session
    {
        public string id;
        public Meta meta;
        public Player player;
        public List<Frame> timeline;
    }

    [System.Serializable]
    public class Range
    {
        public Range(long min = 0, long max = 0)
        {
            this.min = min; 
            this.max = max;
        }
        public long max;
        public long min;
    }

    [System.Serializable]
    public class MetaFilter
    {
        public Range meta1;
        public Range meta2;
        public Range meta3;
    }

    [System.Serializable]
    public class InitConfig
    {
        public int count = 1;
        public bool isEventBased = false;
        public int maxOpponentTurnTime = 200;
        public MetaFilter meta;
    }

    // Partial класс для прозрачной сериализации payload (добавляйте поля по мере нужды)
    [System.Serializable]
    public partial class Payload
    {
        // Здесь ваши поля, e.g., public string data; public int score;
    }
}

namespace YG.Insides
{
    [System.Serializable]
    public class ListWrapper
    {
        public List<Session> sessions;
    }
}
namespace YG.Insides
{
    public partial class YGSendMessage
    {
        public void OnSessionsLoaded(string data)
        {
            if (string.IsNullOrEmpty(data) || data == InfoYG.NO_DATA)
            {
                Debug.LogError("Sessions load error!");
                YG2.MultiplayerSessions.InvokeSessionsLoaded(new List<Session>());
                return;
            }

            var wrapper = JsonUtility.FromJson<ListWrapper>(data);
            YG2.MultiplayerSessions.InvokeSessionsLoaded(wrapper.sessions ?? new List<Session>());
        }
    }
}