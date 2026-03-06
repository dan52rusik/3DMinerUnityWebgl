#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using YG.Insides;

namespace YG
{
    public partial class InfoYG
    {
        public MultiplayerSessionsSettings MultiplayerSessions;

        [System.Serializable]
        public partial class MultiplayerSessionsSettings
        {
#if RU_YG2
            [Tooltip("Список сессий для симуляции в Editor (фильтруется по InitConfig).")]
#else
            [Tooltip("List of sessions for Editor simulation (filtered by InitConfig).")]
#endif
            [HeaderYG("Simulation", 5)]
            public List<SimulatedSession> simulatedSessions = new List<SimulatedSession>();
        }

        [System.Serializable]
        public class SimulatedSession
        {
            public string id = "sim-id-1";
            public Meta meta = new Meta();
            public Player player = new Player();
            public List<Frame> timeline = new List<Frame>();
        }
    }
}
#endif