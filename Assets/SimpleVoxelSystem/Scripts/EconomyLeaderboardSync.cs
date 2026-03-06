using UnityEngine;
using YG;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class EconomyLeaderboardSync : MonoBehaviour
    {
        private const string LeaderboardName = "maxmoneyearned";
        private const string SubmittedPrefKey = "svs_lb_max_money_earned_submitted";
        private const float SubmitCooldownSeconds = 1.1f;

        private float _nextSubmitTime;
        private int _pendingBestScore = -1;
        private int _lastSubmittedScore = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<EconomyLeaderboardSync>() != null)
                return;

            GameObject go = new GameObject("EconomyLeaderboardSync");
            DontDestroyOnLoad(go);
            go.AddComponent<EconomyLeaderboardSync>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _lastSubmittedScore = PlayerPrefs.GetInt(SubmittedPrefKey, -1);
            QueueBestMoney(GlobalEconomy.BestMoney);
        }

        private void OnEnable()
        {
            GlobalEconomy.OnBestMoneyChanged += OnBestMoneyChanged;
            YG2.onGetSDKData += OnSdkReady;
        }

        private void OnDisable()
        {
            GlobalEconomy.OnBestMoneyChanged -= OnBestMoneyChanged;
            YG2.onGetSDKData -= OnSdkReady;
        }

        private void Update()
        {
            if (_pendingBestScore < 0 || Time.unscaledTime < _nextSubmitTime)
                return;

            TrySubmitPendingScore();
        }

        private void OnSdkReady()
        {
            QueueBestMoney(GlobalEconomy.BestMoney);
            TrySubmitPendingScore();
        }

        private void OnBestMoneyChanged(int bestMoney)
        {
            QueueBestMoney(bestMoney);
            TrySubmitPendingScore();
        }

        private void QueueBestMoney(int bestMoney)
        {
            if (bestMoney <= _lastSubmittedScore)
                return;

            _pendingBestScore = Mathf.Max(_pendingBestScore, bestMoney);
        }

        private void TrySubmitPendingScore()
        {
#if Authorization_yg
            if (!YG2.player.auth)
                return;
#endif

            if (_pendingBestScore < 0)
                return;

            if (Time.unscaledTime < _nextSubmitTime)
                return;

            YG2.SetLeaderboard(LeaderboardName, _pendingBestScore);
            _lastSubmittedScore = Mathf.Max(_lastSubmittedScore, _pendingBestScore);
            PlayerPrefs.SetInt(SubmittedPrefKey, _lastSubmittedScore);
            PlayerPrefs.Save();
            _pendingBestScore = -1;
            _nextSubmitTime = Time.unscaledTime + SubmitCooldownSeconds;
        }
    }
}
