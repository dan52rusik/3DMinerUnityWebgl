using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SimpleVoxelSystem;
using System.Collections.Generic;

using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem.Net
{
    /// <summary>
    /// Network-side ownership gate for player avatar.
    /// Local owner controls movement/mining/camera, remote copies are read-only.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetPlayerAvatar : NetworkBehaviour
    {
        [Header("Economy (Server Authoritative)")]
        public NetworkVariable<int> money = new NetworkVariable<int>(300, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> miningXP = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> miningLevel = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        private WellGenerator wellGenerator;

        public override void OnNetworkSpawn()
        {
            CacheRefs();
            ApplyOwnershipState();

            if (IsOwner)
            {
                SetPlayerNameServerRpc((FixedString32Bytes)SystemInfo.deviceName);
                SetWorldStateServerRpc(true);
                
                // Sync local state to network variables initially if needed
                // Or better: NetworkVariables are the source of truth.
                SyncLocalEconomyFromNetwork();
                
                money.OnValueChanged += (old, newVal) => GlobalEconomy.SyncFromNetwork(newVal, miningXP.Value, miningLevel.Value);
                miningXP.OnValueChanged += (old, newVal) => GlobalEconomy.SyncFromNetwork(money.Value, newVal, miningLevel.Value);
                miningLevel.OnValueChanged += (old, newVal) => GlobalEconomy.SyncFromNetwork(money.Value, miningXP.Value, newVal);
            }
        }

        private void SyncLocalEconomyFromNetwork()
        {
            GlobalEconomy.SyncFromNetwork(money.Value, miningXP.Value, miningLevel.Value);
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

        // ─── Mining Sync ───────────────────────────────────────────────────

        [ServerRpc]
        public void RequestMineBlockServerRpc(Vector3Int gridPos, bool isLobby, ServerRpcParams rpcParams = default)
        {
            if (isLobby)
            {
                MineBlockClientRpc(gridPos, true);
                return;
            }

            // Private island: apply only on caller's client to avoid mutating
            // same grid coordinates on other players' private islands.
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new List<ulong> { rpcParams.Receive.SenderClientId }
                }
            };
            MineBlockClientRpc(gridPos, false, target);
        }

        [ClientRpc]
        private void MineBlockClientRpc(Vector3Int gridPos, bool isLobby, ClientRpcParams clientRpcParams = default)
        {
            if (wellGenerator == null) wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (wellGenerator == null) return;
            wellGenerator.MineVoxel(gridPos.x, gridPos.y, gridPos.z);
        }

        [ServerRpc]
        public void RequestPlaceBlockServerRpc(Vector3Int gridPos, int blockType)
        {
            PlaceBlockClientRpc(gridPos, blockType);
        }

        [ClientRpc]
        private void PlaceBlockClientRpc(Vector3Int gridPos, int blockType)
        {
            LobbyEditor editor = FindLobbyEditor();
            if (editor == null) return;
            // Use SendMessage to bypass compile-time method checking
            editor.SendMessage("ApplyNetworkPlaceBlockManual", new object[] { gridPos, blockType }, SendMessageOptions.DontRequireReceiver);
        }

        [ServerRpc]
        public void RequestRemoveBlockServerRpc(Vector3Int gridPos)
        {
            RemoveBlockClientRpc(gridPos);
        }

        [ClientRpc]
        private void RemoveBlockClientRpc(Vector3Int gridPos)
        {
            LobbyEditor editor = FindLobbyEditor();
            if (editor == null) return;
            editor.SendMessage("ApplyNetworkRemoveBlockManual", gridPos, SendMessageOptions.DontRequireReceiver);
        }

        [ServerRpc]
        public void AddRewardsServerRpc(int moneyToAdd, int xpToAdd)
        {
            money.Value += moneyToAdd;
            int nextLevelThreshold = miningLevel.Value * 50;
            int newXP = miningXP.Value + xpToAdd;
            
            if (newXP >= nextLevelThreshold)
            {
                newXP -= nextLevelThreshold;
                miningLevel.Value++;
            }
            miningXP.Value = newXP;
        }

        // ────────────────────────────────────────────────────────────────────

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
