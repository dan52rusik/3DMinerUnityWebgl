using System;
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
        private const float RewardedAdCooldownSeconds = 300f; // 5 minutes

        public static AdsManager Instance { get; private set; }

        /// <summary>Fires whenever the rewarded-ad cooldown state changes (started, ticking, ended).</summary>
        public static event Action OnRewardCooldownChanged;

        private WellGenerator wellGenerator;
        private bool bannerShown;
        private float sessionStartTime;
        private float nextInterstitialAllowedTime;
        private float nextRewardedAllowedTime;

        /// <summary>True while the rewarded ad button should be disabled.</summary>
        public bool IsRewardedOnCooldown => Time.unscaledTime < nextRewardedAllowedTime;

        /// <summary>Remaining cooldown seconds (0 when ready).</summary>
        public int RewardedCooldownRemaining =>
            Mathf.Max(0, Mathf.CeilToInt(nextRewardedAllowedTime - Time.unscaledTime));

        private bool wasCooldownActive;

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
            UpdateRewardCooldown();
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
            if (Instance != null && Instance.IsRewardedOnCooldown)
                return;

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

            // Start cooldown
            nextRewardedAllowedTime = Time.unscaledTime + RewardedAdCooldownSeconds;
            wasCooldownActive = true;
            OnRewardCooldownChanged?.Invoke();
        }

        private void UpdateRewardCooldown()
        {
            if (!wasCooldownActive)
                return;

            if (IsRewardedOnCooldown)
            {
                // Fire every second so UI can update the countdown
                OnRewardCooldownChanged?.Invoke();
            }
            else
            {
                // Cooldown just ended
                wasCooldownActive = false;
                OnRewardCooldownChanged?.Invoke();
            }
        }
    }
}
