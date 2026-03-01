using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SimpleVoxelSystem;

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

        [ServerRpc]
        public void RequestSpawnShopZoneServerRpc(Vector3 worldPos, int sx, int sy, int sz, int zoneType)
        {
            SpawnShopZoneClientRpc(worldPos, sx, sy, sz, zoneType);
        }

        [ServerRpc]
        public void RequestDeleteShopZoneServerRpc(Vector3 worldPos, int zoneType)
        {
            DeleteShopZoneClientRpc(worldPos, zoneType);
        }

        [ClientRpc]
        private void SpawnShopZoneClientRpc(Vector3 worldPos, int sx, int sy, int sz, int zoneType)
        {
            LobbyEditor editor = FindLobbyEditor();
            if (editor == null) return;
            editor.ApplyNetworkSpawnShopZone(worldPos, sx, sy, sz, (ShopZoneType)zoneType);
        }

        [ClientRpc]
        private void DeleteShopZoneClientRpc(Vector3 worldPos, int zoneType)
        {
            LobbyEditor editor = FindLobbyEditor();
            if (editor == null) return;
            editor.ApplyNetworkDeleteShopZone(worldPos, (ShopZoneType)zoneType);
        }

        private void CacheRefs()
        {
            controller = GetComponent<PlayerCharacterController>();
            pickaxe = GetComponent<PlayerPickaxe>();
            smartMiner = GetComponent<SmartMiner>();
            lobbyEditor = FindLobbyEditor();

            cameras = GetComponentsInChildren<Camera>(true);
            listeners = GetComponentsInChildren<AudioListener>(true);
        }

        private void ApplyOwnershipState()
        {
            bool local = IsOwner;

            if (controller != null) controller.enabled = local;
            if (pickaxe != null) pickaxe.enabled = local;
            if (smartMiner != null) smartMiner.enabled = local;
            // LobbyEditor is a shared scene object. Remote avatars must not disable it.
            if (lobbyEditor != null && local) lobbyEditor.enabled = true;

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

        private static LobbyEditor FindLobbyEditor()
        {
            var editors = FindObjectsByType<LobbyEditor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var e in editors)
            {
                if (e != null) return e;
            }
            return null;
        }
    }
}
