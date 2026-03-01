using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SimpleVoxelSystem.Net
{
    /// <summary>
    /// Network-side ownership gate for player avatar.
    /// Local owner controls movement/mining/camera, remote copies are read-only.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetPlayerAvatar : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> playerName =
            new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> inLobby =
            new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerCharacterController controller;
        private PlayerPickaxe pickaxe;
        private SmartMiner smartMiner;
        private LobbyEditor lobbyEditor;
        private Camera[] cameras;
        private AudioListener[] listeners;

        public override void OnNetworkSpawn()
        {
            CacheRefs();
            ApplyOwnershipState();

            if (IsOwner)
            {
                SetPlayerNameServerRpc((FixedString32Bytes)SystemInfo.deviceName);
                SetWorldStateServerRpc(true);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                gameObject.tag = "Player";
            }
        }

        [ServerRpc]
        public void SetWorldStateServerRpc(bool lobbyState)
        {
            inLobby.Value = lobbyState;
        }

        [ServerRpc]
        public void SetPlayerNameServerRpc(FixedString32Bytes newName)
        {
            playerName.Value = newName;
        }

        private void CacheRefs()
        {
            controller = GetComponent<PlayerCharacterController>();
            pickaxe = GetComponent<PlayerPickaxe>();
            smartMiner = GetComponent<SmartMiner>();
            lobbyEditor = FindFirstObjectByType<LobbyEditor>();

            cameras = GetComponentsInChildren<Camera>(true);
            listeners = GetComponentsInChildren<AudioListener>(true);
        }

        private void ApplyOwnershipState()
        {
            bool local = IsOwner;

            if (controller != null) controller.enabled = local;
            if (pickaxe != null) pickaxe.enabled = local;
            if (smartMiner != null) smartMiner.enabled = local;
            if (lobbyEditor != null) lobbyEditor.enabled = local;

            if (cameras != null)
            {
                foreach (Camera cam in cameras)
                    cam.enabled = local;
            }

            if (listeners != null)
            {
                foreach (AudioListener al in listeners)
                    al.enabled = local;
            }

            gameObject.tag = local ? "Player" : "Untagged";
        }
    }
}

