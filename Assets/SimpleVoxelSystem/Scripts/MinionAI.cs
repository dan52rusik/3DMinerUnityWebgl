using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;

namespace SimpleVoxelSystem
{
    public class MinionAI : MonoBehaviour
    {
        [Header("Stats")]
        public int strength = 1;
        public int capacity = 10;
        public int currentLoad = 0;
        public float mineSpeed = 2.0f; // hits per second
        public float moveSpeed = 3.0f;
        public int storedValue = 0;

        [Header("Movement")]
        public float workReachDistance = 1.25f;
        public float stopDistance = 0.15f;
        public float standHeightOffset = 1.0f;
        public float maxMoveTime = 8.0f;

        [Header("State")]
        public MineInstance targetMine;

        private GameObject floatingLabel;
        private Text floatingText;

        private readonly HashSet<MineInstance> exhaustedMines = new HashSet<MineInstance>();
        private bool hasWorkTarget;
        private Vector3Int workTargetBlock;
        private Vector3 workTargetPos;
        private float workTargetExpireAt;

        private void Start()
        {
            CreateVisuals();
            CreateFloatingLabel();
            StartCoroutine(BehaviorRoot());
        }

        private void CreateVisuals()
        {
            // Replace primitive look with the same blocky miner skin used by player.
            BlockyMixCharacter skin = GetComponent<BlockyMixCharacter>();
            if (skin == null)
                skin = gameObject.AddComponent<BlockyMixCharacter>();

            skin.rebuildOnAwake = false;
            skin.fitToCharacterControllerHeight = false;
            skin.overallScale = 0.55f;
            skin.visualYOffset = 0.0f;
            skin.autoAddAnimator = true;
            CopyPaletteFromPlayer(skin);
            skin.Rebuild();
        }

        private void CopyPaletteFromPlayer(BlockyMixCharacter target)
        {
            if (target == null)
                return;

            BlockyMixCharacter playerSkin = null;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerSkin = player.GetComponent<BlockyMixCharacter>();
            if (playerSkin == null)
                playerSkin = FindFirstObjectByType<BlockyMixCharacter>();
            if (playerSkin == null || playerSkin == target)
                return;

            target.skinColor = playerSkin.skinColor;
            target.shirtColor = playerSkin.shirtColor;
            target.pantsColor = playerSkin.pantsColor;
            target.accentColor = playerSkin.accentColor;
            target.bootColor = playerSkin.bootColor;
            target.gloveColor = playerSkin.gloveColor;
            target.stripeColor = playerSkin.stripeColor;
        }

        private void CreateFloatingLabel()
        {
            floatingLabel = new GameObject("MinionLabel");
            floatingLabel.transform.SetParent(transform, false);
            floatingLabel.transform.localPosition = Vector3.up * 2.0f;

            Canvas nestedCanvas = floatingLabel.AddComponent<Canvas>();
            nestedCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = floatingLabel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 56f);
            rt.localScale = Vector3.one * 0.01f;

            floatingText = RuntimeUIFactory.MakeLabel(floatingLabel.transform, "Text", "MINION", 14, TextAnchor.MiddleCenter);
            floatingText.color = Color.yellow;
        }

        private void Update()
        {
            if (floatingLabel != null && Camera.main != null)
            {
                floatingLabel.transform.LookAt(Camera.main.transform);
                floatingLabel.transform.Rotate(0f, 180f, 0f);

                string status = currentLoad >= capacity
                    ? "<color=orange>FULL! Collect via [M]</color>"
                    : $"({currentLoad}/{capacity}) [M]";
                floatingText.text = $"MINION\n{status}";
            }
        }

        private IEnumerator BehaviorRoot()
        {
            WellGenerator wg = null;
            VoxelIsland island = null;

            while (true)
            {
                if (currentLoad >= capacity)
                {
                    yield return new WaitForSeconds(0.75f);
                    continue;
                }

                if (wg == null) wg = FindFirstObjectByType<WellGenerator>();
                if (wg == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                island = wg.ActiveIsland;
                if (island == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                if (targetMine == null || targetMine.IsExhausted || exhaustedMines.Contains(targetMine))
                {
                    targetMine = null;
                    FindNewMine(wg);
                    if (targetMine == null)
                    {
                        yield return new WaitForSeconds(1.5f);
                        continue;
                    }
                }

                GetMineBounds(targetMine, out int startX, out int startZ, out int mineW, out int mineL, out int mineD);
                int floorY = wg.LobbyFloorY;

                if (hasWorkTarget)
                {
                    bool expired = Time.time >= workTargetExpireAt;
                    bool invalid = !island.IsInBounds(workTargetBlock.x, workTargetBlock.y, workTargetBlock.z) ||
                                   !island.IsSolid(workTargetBlock.x, workTargetBlock.y, workTargetBlock.z);
                    if (expired || invalid)
                        hasWorkTarget = false;
                }

                if (!hasWorkTarget)
                {
                    Vector3Int? targetBlock = FindExposedBlockInMine(island, startX, startZ, floorY, mineW, mineL, mineD);
                    if (!targetBlock.HasValue)
                    {
                        exhaustedMines.Add(targetMine);
                        targetMine = null;
                        yield return new WaitForSeconds(0.75f);
                        continue;
                    }

                    Vector3 workWorldPos = ResolveWorkWorldPosition(island, targetBlock.Value, floorY, startX, startZ, mineW, mineL, mineD);
                    workWorldPos = SanitizeWorkPosition(island, workWorldPos, floorY, startX, startZ, mineW, mineL, mineD);

                    workTargetBlock = targetBlock.Value;
                    workTargetPos = workWorldPos;
                    workTargetExpireAt = Time.time + 4.0f;
                    hasWorkTarget = true;
                }
                
                yield return MoveTo(island, workTargetPos, floorY, mineD);

                if (currentLoad >= capacity)
                    continue;

                if (!island.IsSolid(workTargetBlock.x, workTargetBlock.y, workTargetBlock.z))
                {
                    hasWorkTarget = false;
                    continue;
                }

                float distToBlock = Vector3.Distance(transform.position, GetTopWorldPosition(island, workTargetBlock.x, workTargetBlock.y, workTargetBlock.z));
                if (distToBlock > workReachDistance)
                    continue;

                // Short hit pause creates "working" feeling instead of instant delete.
                yield return new WaitForSeconds(Mathf.Max(0.05f, 0.5f / Mathf.Max(0.1f, mineSpeed)));
                MineOneBlockAt(island, workTargetBlock);
                hasWorkTarget = false;
            }
        }

        private IEnumerator MoveTo(VoxelIsland island, Vector3 targetPos, int floorY, int mineD)
        {
            float startTime = Time.time;
            while (true)
            {
                Vector3 to = targetPos - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist <= Mathf.Max(0.3f, stopDistance))
                    yield break;
                if (Time.time - startTime > Mathf.Max(1f, maxMoveTime))
                    yield break;

                Vector3 dir = to / Mathf.Max(0.0001f, dist);
                transform.position += dir * moveSpeed * Time.deltaTime;
                Vector3 p = transform.position;
                p.y = Mathf.MoveTowards(p.y, targetPos.y, moveSpeed * 1.8f * Time.deltaTime);
                if (TrySampleSurfaceYAtWorldXZ(island, p.x, p.z, floorY, mineD, out float surfY))
                    p.y = Mathf.MoveTowards(p.y, surfY + standHeightOffset, moveSpeed * 2.5f * Time.deltaTime);
                transform.position = p;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 14f * Time.deltaTime);
                }

                if (!IsWorldPosNearIsland(island, transform.position, 2.5f))
                {
                    int startX = Mathf.Clamp(targetMine != null ? targetMine.originX - ((targetMine.shopData != null ? targetMine.shopData.wellWidth : 5) / 2) - (targetMine.shopData != null ? targetMine.shopData.padding : 0) : 0, 0, island.TotalX - 1);
                    int startZ = Mathf.Clamp(targetMine != null ? targetMine.originZ - ((targetMine.shopData != null ? targetMine.shopData.wellLength : 5) / 2) - (targetMine.shopData != null ? targetMine.shopData.padding : 0) : 0, 0, island.TotalZ - 1);
                    int mineW = targetMine != null ? (targetMine.shopData != null ? targetMine.shopData.wellWidth : 5) + (targetMine.shopData != null ? targetMine.shopData.padding : 0) * 2 : island.TotalX;
                    int mineL = targetMine != null ? (targetMine.shopData != null ? targetMine.shopData.wellLength : 5) + (targetMine.shopData != null ? targetMine.shopData.padding : 0) * 2 : island.TotalZ;
                    Vector3 safe = SanitizeWorkPosition(island, targetPos, floorY, startX, startZ, mineW, mineL, mineD);
                    transform.position = safe;
                    yield break;
                }

                yield return null;
            }
        }

        private void GetMineBounds(MineInstance mine, out int startX, out int startZ, out int mineW, out int mineL, out int mineD)
        {
            int baseW = mine.shopData != null ? mine.shopData.wellWidth : 5;
            int baseL = mine.shopData != null ? mine.shopData.wellLength : 5;
            int pad = mine.shopData != null ? mine.shopData.padding : 0;

            mineW = baseW + pad * 2;
            mineL = baseL + pad * 2;
            mineD = mine.rolledDepth > 0 ? mine.rolledDepth : 20;

            startX = mine.originX - (baseW / 2) - pad;
            startZ = mine.originZ - (baseL / 2) - pad;
        }

        private Vector3Int? FindExposedBlockInMine(VoxelIsland island, int startX, int startZ, int floorY, int mineW, int mineL, int mineD)
        {
            Vector3Int? best = null;
            float bestDist = float.MaxValue;

            for (int y = 0; y < mineD; y++)
            {
                int gy = floorY + y;
                for (int x = 0; x < mineW; x++)
                {
                    int gx = startX + x;
                    for (int z = 0; z < mineL; z++)
                    {
                        int gz = startZ + z;
                        if (!island.IsInBounds(gx, gy, gz) || !island.IsSolid(gx, gy, gz))
                            continue;

                        // Exposed top face: no solid above this block.
                        if (gy > 0 && island.IsSolid(gx, gy - 1, gz))
                            continue;

                        Vector3 top = GetTopWorldPosition(island, gx, gy, gz);
                        float d = (top - transform.position).sqrMagnitude;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = new Vector3Int(gx, gy, gz);
                        }
                    }
                }
            }

            return best;
        }

        private Vector3 ResolveWorkWorldPosition(
            VoxelIsland island,
            Vector3Int targetBlock,
            int floorY,
            int startX,
            int startZ,
            int mineW,
            int mineL,
            int mineD)
        {
            // Prefer standing on neighbor surface blocks so the minion looks like it mines nearby blocks.
            Vector2Int[] dirs =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            Vector3 bestPos = GetTopWorldPosition(island, targetBlock.x, targetBlock.y, targetBlock.z);
            float bestDist = float.MaxValue;

            for (int i = 0; i < dirs.Length; i++)
            {
                int sx = targetBlock.x + dirs[i].x;
                int sz = targetBlock.z + dirs[i].y;
                if (sx < startX || sx >= startX + mineW || sz < startZ || sz >= startZ + mineL)
                    continue;

                if (!TryFindTopSurfaceYInColumn(island, sx, sz, floorY, mineD, out int sy))
                    continue;

                Vector3 pos = GetTopWorldPosition(island, sx, sy, sz);
                float d = (pos - transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPos = pos;
                }
            }

            bestPos.y += standHeightOffset;
            return bestPos;
        }

        private Vector3 SanitizeWorkPosition(VoxelIsland island, Vector3 desiredWorldPos, int floorY, int startX, int startZ, int mineW, int mineL, int mineD)
        {
            if (island == null)
                return desiredWorldPos;

            if (!IsFinite(desiredWorldPos))
                desiredWorldPos = island.transform.TransformPoint(new Vector3(startX + 0.5f, -floorY, startZ + 0.5f));

            Vector3 local = island.transform.InverseTransformPoint(desiredWorldPos);
            int gx = Mathf.Clamp(Mathf.FloorToInt(local.x), Mathf.Max(0, startX), Mathf.Min(island.TotalX - 1, startX + mineW - 1));
            int gz = Mathf.Clamp(Mathf.FloorToInt(local.z), Mathf.Max(0, startZ), Mathf.Min(island.TotalZ - 1, startZ + mineL - 1));

            if (TryFindTopSurfaceYInColumn(island, gx, gz, floorY, mineD, out int gy))
            {
                Vector3 top = GetTopWorldPosition(island, gx, gy, gz);
                top.y += standHeightOffset;
                return top;
            }

            Vector3 fallback = island.transform.TransformPoint(new Vector3(gx + 0.5f, -floorY, gz + 0.5f));
            fallback.y += standHeightOffset;
            return fallback;
        }

        private bool TryFindTopSurfaceYInColumn(VoxelIsland island, int gx, int gz, int floorY, int mineD, out int gy)
        {
            int maxY = floorY + Mathf.Max(1, mineD) + 2;
            for (int y = floorY; y <= maxY; y++)
            {
                if (!island.IsInBounds(gx, y, gz) || !island.IsSolid(gx, y, gz))
                    continue;

                if (y > 0 && island.IsSolid(gx, y - 1, gz))
                    continue;

                gy = y;
                return true;
            }

            gy = 0;
            return false;
        }

        private Vector3 GetTopWorldPosition(VoxelIsland island, int gx, int gy, int gz)
        {
            Vector3 localCenter = island.GridToLocal(gx, gy, gz) + new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 worldCenter = island.transform.TransformPoint(localCenter);
            worldCenter.y += 0.5f;
            return worldCenter;
        }

        private bool TrySampleSurfaceYAtWorldXZ(VoxelIsland island, float worldX, float worldZ, int floorY, int mineD, out float worldY)
        {
            worldY = 0f;
            if (island == null)
                return false;

            Vector3 local = island.transform.InverseTransformPoint(new Vector3(worldX, island.transform.position.y, worldZ));
            int gx = Mathf.FloorToInt(local.x);
            int gz = Mathf.FloorToInt(local.z);
            if (gx < 0 || gz < 0 || gx >= island.TotalX || gz >= island.TotalZ)
                return false;

            if (!TryFindTopSurfaceYInColumn(island, gx, gz, floorY, mineD, out int gy))
                return false;

            worldY = GetTopWorldPosition(island, gx, gy, gz).y;
            return true;
        }

        private static bool IsFinite(Vector3 p)
        {
            return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                     float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
        }

        private bool IsWorldPosNearIsland(VoxelIsland island, Vector3 worldPos, float margin)
        {
            if (island == null)
                return false;

            Vector3 local = island.transform.InverseTransformPoint(worldPos);
            return local.x >= -margin && local.z >= -margin &&
                   local.x <= island.TotalX + margin && local.z <= island.TotalZ + margin;
        }

        private void MineOneBlockAt(VoxelIsland island, Vector3Int pos)
        {
            if (!island.TryGetBlockType(pos.x, pos.y, pos.z, out BlockType _))
                return;

            if (!island.IsSolid(pos.x, pos.y, pos.z))
                return;

            island.RemoveVoxel(pos.x, pos.y, pos.z);
            targetMine?.RegisterMinedBlock(pos.x, pos.y, pos.z);
            storedValue += 5;
            currentLoad++;
        }

        private void FindNewMine(WellGenerator wg)
        {
            if (wg.PlacedMines == null || wg.PlacedMines.Count == 0)
                return;

            MineInstance best = null;
            float bestDist = float.MaxValue;

            foreach (MineInstance mine in wg.PlacedMines)
            {
                if (mine == null || mine.IsExhausted || exhaustedMines.Contains(mine))
                    continue;

                GetMineBounds(mine, out int startX, out int startZ, out int mineW, out int mineL, out _);
                VoxelIsland island = wg.ActiveIsland;
                Vector3 mineCenter = island != null
                    ? island.transform.TransformPoint(new Vector3(startX + mineW * 0.5f, -wg.LobbyFloorY, startZ + mineL * 0.5f))
                    : new Vector3(startX + mineW * 0.5f, transform.position.y, startZ + mineL * 0.5f);
                float d = Vector3.Distance(transform.position, mineCenter);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = mine;
                }
            }

            if (best == null && exhaustedMines.Count > 0)
            {
                exhaustedMines.Clear();
                FindNewMine(wg);
                return;
            }

            targetMine = best;
        }

        public void EmptyInventory()
        {
            if (currentLoad <= 0)
                return;

            GlobalEconomy.Money += storedValue;
            currentLoad = 0;
            storedValue = 0;
        }

        private void OnMouseDown()
        {
            MinionManagementUI.Show(this);
        }
    }
}
