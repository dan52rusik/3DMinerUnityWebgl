using UnityEngine;
using YG;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class AdsManager : MonoBehaviour
    {
        public const string RewardCoinsId = "coins_bonus";
        public const int RewardCoinsAmount = 250;
        private const float MinSecondsBeforeFirstInterstitial = 90f;
        private const float InterstitialCooldownSeconds = 180f;

        public static AdsManager Instance { get; private set; }

        private WellGenerator wellGenerator;
        private bool bannerShown;
        private float sessionStartTime;
        private float nextInterstitialAllowedTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            sessionStartTime = Time.unscaledTime;
            nextInterstitialAllowedTime = sessionStartTime + MinSecondsBeforeFirstInterstitial;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += OnSdkReady;
            YG2.onRewardAdv += OnRewardAdv;
        }

        private void Start()
        {
            TryBindWellGenerator();
            TrySetupBanner();
        }

        private void Update()
        {
            TryBindWellGenerator();
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= OnSdkReady;
            YG2.onRewardAdv -= OnRewardAdv;

            if (wellGenerator != null)
                wellGenerator.OnWorldSwitch -= OnWorldSwitch;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void ShowRewardedCoins()
        {
            YG2.RewardedAdvShow(RewardCoinsId);
        }

        private void OnSdkReady()
        {
            TrySetupBanner();
        }

        private void TrySetupBanner()
        {
            if (bannerShown || !YG2.isSDKEnabled)
                return;

            YG2.SetBannerPosition(YG2.BannerPosition.Top);
            YG2.LoadBanner();
            YG2.ShowBanner();
            bannerShown = true;
        }

        private void TryBindWellGenerator()
        {
            if (wellGenerator != null)
                return;

            wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (wellGenerator != null)
                wellGenerator.OnWorldSwitch += OnWorldSwitch;
        }

        private void OnWorldSwitch(bool enteredLobby)
        {
            if (!enteredLobby || !YG2.isSDKEnabled)
                return;

            if (Time.unscaledTime < nextInterstitialAllowedTime)
                return;

            YG2.InterstitialAdvShow();
            nextInterstitialAllowedTime = Time.unscaledTime + InterstitialCooldownSeconds;
        }

        private void OnRewardAdv(string rewardId)
        {
            if (rewardId != RewardCoinsId)
                return;

            GlobalEconomy.Money += RewardCoinsAmount;
            FindFirstObjectByType<PlayerProgressPersistence>()?.NotifyGameplayStateChanged();
        }
    }
}
