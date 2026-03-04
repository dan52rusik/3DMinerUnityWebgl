using System;
using System.Collections.Generic;
using UnityEngine;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Компонент для управления логикой добычи ресурсов и шахтами.
    /// Отделен от WellGenerator для упрощения поддержки.
    /// </summary>
    public class MiningManager : MonoBehaviour
    {
        private WellGenerator wellGenerator;

        [Header("Mining Rules")]
        public bool lockDeeperLayersUntilCleared = false;
        public List<BlockData> blockDataConfig;

        [Header("Elevator")]
        public Color elevatorColor = new Color(0.6f, 0.3f, 0.1f, 1f);

        // ─── Runtime ────────────────────────────────────────────────────────
        public MineInstance ActiveMine { get; private set; }
        public IReadOnlyList<MineInstance> PlacedMines => placedMines;
        private readonly List<MineInstance> placedMines = new List<MineInstance>();
        private bool mineAppliedToIsland;

        public bool IsMineGenerated => placedMines.Count > 0;

        public void Initialize(WellGenerator generator)
        {
            wellGenerator = generator;
            EnsureBasicBlockConfig();
        }

        public void GenerateMine(MineInstance mine)
        {
            if (wellGenerator != null && wellGenerator.ActiveIsland != null)
                GenerateMineAt(mine, wellGenerator.ActiveIsland.TotalX / 2, wellGenerator.ActiveIsland.TotalZ / 2);
        }

        public void GenerateMineAt(MineInstance mine, int gx, int gz)
        {
            if (wellGenerator == null || wellGenerator.ActiveIsland == null) return;
            if (mine == null || mine.shopData == null) return;
            
            Transform player = wellGenerator.ResolveOrSpawnPlayer();
            CharacterController cc = (player != null) ? player.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            int minDepth = Mathf.Min(mine.shopData.depthMin, mine.shopData.depthMax);
            int maxDepth = Mathf.Max(mine.shopData.depthMin, mine.shopData.depthMax);
            mine.rolledDepth = Mathf.Clamp(mine.rolledDepth, minDepth, maxDepth);

            ActiveMine = mine;
            ActiveMine.originX = gx;
            ActiveMine.originZ = gz;
            if (!placedMines.Contains(ActiveMine))
                placedMines.Add(ActiveMine);

            ApplyMineVoxels(ActiveMine);
            mineAppliedToIsland = true;

            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int pad = mine.shopData.padding;
            int x0 = gx - (ww / 2) - pad;
            int z0 = gz - (wl / 2) - pad;
            
            CreateElevator(x0, z0);
            
            wellGenerator.RememberCurrentIslandSpawnPoint();
            if (cc != null) cc.enabled = true;

            wellGenerator.NotifyMinePlaced(mine);
        }

        public void ApplyMineVoxels(MineInstance mine)
        {
            if (mine == null || wellGenerator == null || wellGenerator.ActiveIsland == null) return;

            VoxelIsland activeIsland = wellGenerator.ActiveIsland;
            int gx = mine.originX;
            int gz = mine.originZ;
            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int wd = mine.rolledDepth;
            int pad = mine.shopData.padding;

            int x0 = gx - (ww / 2) - pad;
            int z0 = gz - (wl / 2) - pad;

            int shaftX = x0; 
            int shaftZ = z0; 

            if (!mine.HasVoxelsData)
                mine.InitializeVoxels(ww + pad * 2, wl + pad * 2, wd);

            int actualBlocksCount = 0;
            for (int ix = 0; ix < ww + pad * 2; ix++)
            for (int iz = 0; iz < wl + pad * 2; iz++)
            {
                int curX = x0 + ix;
                int curZ = z0 + iz;
                if (!activeIsland.IsInBounds(curX, 0, curZ)) continue;

                for (int iy = 0; iy < wd; iy++)
                {
                    int curY = wellGenerator.LobbyFloorY + iy;
                    if (curY >= activeIsland.TotalY) break;

                    if (curX == shaftX && curZ == shaftZ)
                    {
                        activeIsland.RemoveVoxel(curX, curY, curZ, false);
                        continue;
                    }

                    bool inWell = ix >= pad && ix < ww + pad && iz >= pad && iz < wl + pad;

                    if (mine.IsVoxelMined(curX, curY, curZ))
                    {
                        activeIsland.RemoveVoxel(curX, curY, curZ, false);
                        continue;
                    }

                    BlockType t;
                    if (!mine.HasVoxelValue(ix, iy, iz))
                    {
                        t = inWell ? mine.shopData.RollBlockType(iy) : BlockType.Dirt;
                        mine.SetVoxel(ix, iy, iz, t);
                    }
                    else
                    {
                        t = mine.GetVoxel(ix, iy, iz);
                    }

                    activeIsland.SetVoxel(curX, curY, curZ, t);
                    if (inWell) actualBlocksCount++;
                }
            }

            if (mine.totalBlocks <= 0) mine.totalBlocks = actualBlocksCount;
            activeIsland.RebuildMesh();
            mineAppliedToIsland = true;
        }

        public void MineVoxel(int gx, int gy, int gz)
        {
            if (wellGenerator == null || wellGenerator.ActiveIsland == null) return;
            VoxelIsland activeIsland = wellGenerator.ActiveIsland;
            
            activeIsland.RemoveVoxel(gx, gy, gz);

            foreach (SimpleElevator elev in GetComponentsInChildren<SimpleElevator>())
            {
                if (elev != null && elev.shaftGridX == gx && elev.shaftGridZ == gz)
                {
                    Vector3 localPos = activeIsland.transform.InverseTransformPoint(elev.transform.position);
                    int elevGridY = activeIsland.LocalToGrid(localPos).y;

                    if (gy == elevGridY)
                    {
                        int clearedDepth = GetContiguousClearedDepthAtShaft(elev.shaftGridX, elev.shaftGridZ);
                        MineInstance shaftMine = FindMineByShaft(elev.shaftGridX, elev.shaftGridZ);
                        int maxDepth = shaftMine != null ? shaftMine.rolledDepth : 0;
                        
                        if (clearedDepth >= maxDepth) 
                        {
                            Destroy(elev.gameObject);
                            Debug.Log($"[MiningManager] Лифт разрушен: удалена последняя опора.");
                        }
                    }
                }
            }

            if (TryGetMineForCell(gx, gz, out MineInstance mine))
            {
                mine.RegisterMinedBlock(gx, gy, gz);
            }
        }

        public void DemolishMine()
        {
            if (placedMines.Count == 0)
                return;

            MineInstance target = ActiveMine;
            if (target == null)
                target = placedMines[placedMines.Count - 1];
            if (target == null)
                return;

            int sx = target.originX - (target.shopData.wellWidth / 2) - target.shopData.padding;
            int sz = target.originZ - (target.shopData.wellLength / 2) - target.shopData.padding;
            
            SimpleElevator[] elevators = GetComponentsInChildren<SimpleElevator>();
            for (int i = 0; i < elevators.Length; i++)
            {
                SimpleElevator elev = elevators[i];
                if (elev == null) continue;
                if (elev.shaftGridX == sx && elev.shaftGridZ == sz)
                {
                    Destroy(elev.gameObject);
                    break;
                }
            }

            placedMines.Remove(target);
            ActiveMine = placedMines.Count > 0 ? placedMines[placedMines.Count - 1] : null;
            mineAppliedToIsland = placedMines.Count > 0;
        }

        public bool IsInsideWellArea(int gx, int gz)
        {
            return TryGetMineForCell(gx, gz, out _);
        }

        public bool CanMineVoxel(int gx, int gy, int gz)
        {
            if (wellGenerator == null || wellGenerator.IsInLobbyMode) return false;
            if (wellGenerator.ActiveIsland == null) return false;
            return gy >= wellGenerator.LobbyFloorY && gy < wellGenerator.ActiveIsland.TotalY;
        }

        public bool IsWellLayerCleared(int depthGridY)
        {
            if (ActiveMine == null) return false;
            return IsWellLayerClearedForMine(ActiveMine, depthGridY);
        }

        public bool IsWellLayerClearedForMine(MineInstance mine, int depthGridY)
        {
            if (mine == null || wellGenerator == null || wellGenerator.ActiveIsland == null) return false;
            VoxelIsland activeIsland = wellGenerator.ActiveIsland;
            
            int ww = mine.shopData.wellWidth;
            int wl = mine.shopData.wellLength;
            int ox = mine.originX;
            int oz = mine.originZ;
            int xMin = ox - (ww / 2);
            int zMin = oz - (wl / 2);

            for (int x = xMin; x < xMin + ww; x++)
            for (int z = zMin; z < zMin + wl; z++)
            {
                if (activeIsland.IsInBounds(x, depthGridY, z) && activeIsland.IsSolid(x, depthGridY, z))
                    return false;
            }
            return true;
        }

        public int GetContiguousClearedDepth()
        {
            if (ActiveMine == null) return 0;
            int cleared = 0;
            for (int y = 0; y < ActiveMine.rolledDepth; y++)
            {
                if (!IsWellLayerCleared(wellGenerator.LobbyFloorY + y)) break;
                cleared++;
            }
            return cleared;
        }

        public int GetContiguousClearedDepthAtShaft(int shaftGridX, int shaftGridZ)
        {
            MineInstance mine = FindMineByShaft(shaftGridX, shaftGridZ);
            if (mine == null) mine = ActiveMine;
            if (mine == null || wellGenerator == null) return 0;

            int cleared = 0;
            for (int y = 0; y < mine.rolledDepth; y++)
            {
                if (!IsWellLayerClearedForMine(mine, wellGenerator.LobbyFloorY + y)) break;
                cleared++;
            }
            return cleared;
        }

        public MineInstance FindMineByShaft(int shaftGridX, int shaftGridZ)
        {
            for (int i = 0; i < placedMines.Count; i++)
            {
                MineInstance m = placedMines[i];
                if (m == null || m.shopData == null) continue;
                int sx = m.originX - (m.shopData.wellWidth / 2) - m.shopData.padding;
                int sz = m.originZ - (m.shopData.wellLength / 2) - m.shopData.padding;
                if (sx == shaftGridX && sz == shaftGridZ)
                    return m;
            }
            return null;
        }

        public bool TryGetMineForCell(int gx, int gz, out MineInstance mine)
        {
            for (int i = placedMines.Count - 1; i >= 0; i--)
            {
                MineInstance candidate = placedMines[i];
                if (candidate == null || candidate.shopData == null)
                    continue;

                int ww = candidate.shopData.wellWidth;
                int wl = candidate.shopData.wellLength;
                int pad = candidate.shopData.padding;
                int ox = candidate.originX;
                int oz = candidate.originZ;

                int xMin = ox - (ww / 2) - pad;
                int zMin = oz - (wl / 2) - pad;
                int xMax = xMin + ww + pad * 2 - 1;
                int zMax = zMin + wl + pad * 2 - 1;

                if (gx >= xMin && gx <= xMax && gz >= zMin && gz <= zMax)
                {
                    mine = candidate;
                    return true;
                }
            }

            mine = null;
            return false;
        }

        public void RestoreMineFromSave(MineInstance mine)
        {
            placedMines.Clear();
            ActiveMine = mine;
            mineAppliedToIsland = false;

            if (mine != null) placedMines.Add(mine);
            if (wellGenerator != null && wellGenerator.IsInLobbyMode) return;

            ApplyMinesToIsland();
        }

        public void RestoreMinesFromSave(List<MineInstance> mines)
        {
            placedMines.Clear();
            ActiveMine = null;
            mineAppliedToIsland = false;

            if (mines != null)
            {
                foreach (var m in mines)
                {
                    if (m == null || m.shopData == null) continue;
                    placedMines.Add(m);
                    ActiveMine = m;
                }
            }

            if (wellGenerator != null && wellGenerator.IsInLobbyMode) return;

            ApplyMinesToIsland();
        }

        public void ApplyMinesToIsland()
        {
            if (wellGenerator == null || wellGenerator.IsInLobbyMode) return;
            
            foreach (var m in placedMines)
            {
                ApplyMineVoxels(m);
                CreateElevator(m.originX - (m.shopData.wellWidth / 2) - m.shopData.padding,
                               m.originZ - (m.shopData.wellLength / 2) - m.shopData.padding);
            }
            mineAppliedToIsland = placedMines.Count > 0;
        }

        public void ClearMines()
        {
            ActiveMine = null;
            placedMines.Clear();
            mineAppliedToIsland = false;
        }

        public void CreateElevator(int gx, int gz)
        {
            if (wellGenerator == null || wellGenerator.ActiveIsland == null) return;
            VoxelIsland activeIsland = wellGenerator.ActiveIsland;

            SimpleElevator[] existing = GetComponentsInChildren<SimpleElevator>();
            for (int i = 0; i < existing.Length; i++)
            {
                SimpleElevator elev = existing[i];
                if (elev != null && elev.shaftGridX == gx && elev.shaftGridZ == gz)
                    return;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "AutoElevator";
            go.transform.SetParent(this.transform);
            
            Vector3 localPos = activeIsland.GridToLocal(gx, wellGenerator.LobbyFloorY, gz);
            go.transform.position = activeIsland.transform.TransformPoint(localPos + new Vector3(0.5f, 0.125f, 0.5f));
            go.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);

            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 2f, 1f);
            trigger.center = new Vector3(0f, 1f, 0f);
            go.layer = 2; // Ignore Raycast

            SimpleElevator elevatorScript = go.AddComponent<SimpleElevator>();
            elevatorScript.topY = go.transform.position.y;
            elevatorScript.wellGenerator = wellGenerator;
            elevatorScript.island = activeIsland;
            elevatorScript.shaftGridX = gx; 
            elevatorScript.shaftGridZ = gz;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = elevatorColor;
                renderer.material = mat;
            }
        }

        public void EnsureBasicBlockConfig()
        {
            if (blockDataConfig == null) blockDataConfig = new List<BlockData>();
            
            ForceUpdateOrAdd(BlockType.Grass, new Color(0.2f, 0.8f, 0.2f), 1, 2, 0);
            ForceUpdateOrAdd(BlockType.Dirt,  new Color(0.5f, 0.3f, 0.1f), 1, 2, 0);
            ForceUpdateOrAdd(BlockType.Stone, new Color(0.5f, 0.5f, 0.5f), 2, 10, 5);
            ForceUpdateOrAdd(BlockType.Iron,  new Color(0.8f, 0.6f, 0.4f), 5, 50, 10);
            ForceUpdateOrAdd(BlockType.Gold,  new Color(1.0f, 0.9f, 0.0f), 10, 100, 15);
        }

        private void ForceUpdateOrAdd(BlockType type, Color color, int hp, int xp, int level)
        {
            BlockData existing = null;
            if (blockDataConfig != null)
            {
                foreach (var b in blockDataConfig) 
                {
                    if (b != null && b.type == type) 
                    {
                        existing = b;
                        break;
                    }
                }
            }

            if (existing != null)
            {
                existing.xpReward = xp;
                existing.requiredMiningLevel = level;
                existing.maxHealth = hp;
                existing.reward = xp * 2;
            }
            else
            {
                blockDataConfig.Add(new BlockData 
                { 
                    type = type, 
                    blockColor = color, 
                    maxHealth = hp, 
                    xpReward = xp, 
                    requiredMiningLevel = level,
                    reward = xp * 2
                });
            }
        }
    }
}
