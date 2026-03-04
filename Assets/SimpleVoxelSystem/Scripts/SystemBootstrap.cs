using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Ensures all essential runtime systems are present in the scene.
    /// </summary>
    public class SystemBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            GameObject go = new GameObject("SystemBootstrap");
            go.AddComponent<SystemBootstrap>();
            DontDestroyOnLoad(go);
            
            // Add system managers that don't need a Canvas
            // (UpgradeManager etc should already be in scene or added)
        }

        private void Awake()
        {
            EnsureComponent<UpgradeManager>();
            EnsureComponent<PickaxeShopUI>();
            EnsureComponent<MinionShopUI>();
            EnsureComponent<UpgradeHUD>();
            EnsureComponent<EconomyHelper>();
            EnsureComponent<MinionManagementUI>();
            EnsureComponent<OnboardingTutorial>();
        }

        private void EnsureComponent<T>() where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
            {
                gameObject.AddComponent<T>();
            }
        }
    }
}
