using Unity.Netcode;
using UnityEngine;

namespace SimpleVoxelSystem.Net
{
    /// <summary>
    /// Syncs player world presence (Lobby/Island).
    /// Rule: lobby is shared; islands are private (remote players hidden while local player is on island).
    /// </summary>
    [RequireComponent(typeof(NetPlayerAvatar))]
    [DisallowMultipleComponent]
    public class NetWorldPresenceSync : NetworkBehaviour
    {
        private NetPlayerAvatar avatar;
        private WellGenerator wellGenerator;
        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        private bool localInLobby = true;

        void Awake()
        {
            avatar = GetComponent<NetPlayerAvatar>();
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        public override void OnNetworkSpawn()
        {
            wellGenerator = FindFirstObjectByType<WellGenerator>();

            if (wellGenerator != null)
            {
                localInLobby = wellGenerator.IsInLobbyMode;
                wellGenerator.OnWorldSwitch += OnWorldSwitch;
            }

            if (avatar != null)
                avatar.inLobby.OnValueChanged += OnRemoteWorldChanged;

            if (IsOwner && avatar != null)
                avatar.SetWorldStateServerRpc(localInLobby);

            ApplyVisibility();
        }

        public override void OnNetworkDespawn()
        {
            if (wellGenerator != null)
                wellGenerator.OnWorldSwitch -= OnWorldSwitch;

            if (avatar != null)
                avatar.inLobby.OnValueChanged -= OnRemoteWorldChanged;
        }

        private void OnWorldSwitch(bool inLobbyNow)
        {
            localInLobby = inLobbyNow;

            if (IsOwner && avatar != null)
                avatar.SetWorldStateServerRpc(inLobbyNow);

            ApplyVisibility();
        }

        private void OnRemoteWorldChanged(bool _, bool __)
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (avatar == null || IsOwner)
                return;

            // Shared lobby; private islands.
            bool shouldBeVisible = localInLobby && avatar.inLobby.Value;

            if (cachedRenderers != null)
            {
                foreach (Renderer r in cachedRenderers)
                    if (r != null) r.enabled = shouldBeVisible;
            }

            if (cachedColliders != null)
            {
                foreach (Collider c in cachedColliders)
                    if (c != null) c.enabled = shouldBeVisible;
            }
        }
    }
}
