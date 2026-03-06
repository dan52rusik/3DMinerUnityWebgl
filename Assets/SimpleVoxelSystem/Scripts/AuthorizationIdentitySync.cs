using UnityEngine;
using YG;

namespace SimpleVoxelSystem
{
    [DisallowMultipleComponent]
    public class AuthorizationIdentitySync : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<AuthorizationIdentitySync>() != null)
                return;

            GameObject go = new GameObject("AuthorizationIdentitySync");
            DontDestroyOnLoad(go);
            go.AddComponent<AuthorizationIdentitySync>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SyncFromSdk();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += SyncFromSdk;
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= SyncFromSdk;
        }

        public static void TryOpenAuthDialog()
        {
#if Authorization_yg
            if (!YG2.player.auth)
                YG2.OpenAuthDialog();
#endif
        }

        private static void SyncFromSdk()
        {
#if Authorization_yg
            string playerId = YG2.player.id;
            string playerName = ResolveDisplayName();

            if (!string.IsNullOrWhiteSpace(playerId) && !string.IsNullOrWhiteSpace(playerName))
                PlayerIdentity.UpdateFromSdk(playerId, playerName);
#endif
        }

#if Authorization_yg
        private static string ResolveDisplayName()
        {
            if (YG2.player == null)
                return "Player";

            string rawName = YG2.player.name;
            if (string.IsNullOrWhiteSpace(rawName))
                return "Player";

            rawName = rawName.Trim();

            if (string.Equals(rawName, "unauthorized", System.StringComparison.OrdinalIgnoreCase))
                return "Player";

            return rawName;
        }
#endif
    }
}
