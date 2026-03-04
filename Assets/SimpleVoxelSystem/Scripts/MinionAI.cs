using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using SimpleVoxelSystem.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        private GameObject floatingLabel;
        private Text floatingText;
        private Transform visualGroup;
        private Transform pickaxePivot;

        [Header("State")]
        public MineInstance targetMine;

        // Блеклист истощённых шахт (чтобы не скакать к ним снова и снова)
        private HashSet<MineInstance> exhaustedMines = new HashSet<MineInstance>();

        private void Start()
        {
            CreateVisuals();
            CreateFloatingLabel();
            StartCoroutine(BehaviorRoot());
        }

        private void CreateVisuals()
        {
            // Root group for visuals
            GameObject group = new GameObject("VisualGroup");
            group.transform.SetParent(transform, false);
            visualGroup = group.transform;
            visualGroup.localScale = Vector3.one * 0.5f;

            // Main Body (Capsule) — используем встроенный материал примитива, просто меняем цвет
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visualGroup, false);
            Destroy(body.GetComponent<Collider>());

            // Используем sharedMaterial → создаём instance чтобы не трогать общий
            var mr = body.GetComponent<MeshRenderer>();
            var mat = new Material(mr.sharedMaterial); // наследуем рабочий шейдер примитива
            mat.color = new Color(1f, 0.55f, 0f); // оранжевый рабочий
            mr.material = mat;

            // Pickaxe Pivot
            GameObject pPivot = new GameObject("PickaxePivot");
            pPivot.transform.SetParent(visualGroup, false);
            pPivot.transform.localPosition = new Vector3(0.6f, 0.2f, 0.5f);
            pickaxePivot = pPivot.transform;

            // Pickaxe Handle
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.transform.SetParent(pickaxePivot, false);
            handle.transform.localScale = new Vector3(0.1f, 0.1f, 0.8f);
            var handleMat = new Material(handle.GetComponent<MeshRenderer>().sharedMaterial);
            handleMat.color = new Color(0.4f, 0.2f, 0f);
            handle.GetComponent<MeshRenderer>().material = handleMat;
            Destroy(handle.GetComponent<Collider>());

            // Pickaxe Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(pickaxePivot, false);
            head.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            head.transform.localPosition = new Vector3(0, 0, 0.4f);
            var headMat = new Material(head.GetComponent<MeshRenderer>().sharedMaterial);
            headMat.color = new Color(0.65f, 0.65f, 0.65f);
            head.GetComponent<MeshRenderer>().material = headMat;
            Destroy(head.GetComponent<Collider>());
        }

        private void CreateFloatingLabel()
        {
            floatingLabel = new GameObject("MinionLabel");
            floatingLabel.transform.SetParent(transform, false);
            floatingLabel.transform.localPosition = Vector3.up * 2.5f;

            var nestedCanvas = floatingLabel.AddComponent<Canvas>();
            nestedCanvas.renderMode = RenderMode.WorldSpace;
            var rt = floatingLabel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 50);
            rt.localScale = Vector3.one * 0.01f;

            floatingText = RuntimeUIFactory.MakeLabel(floatingLabel.transform, "Text", "MINION\n[M to Manage]", 14, TextAnchor.MiddleCenter);
            floatingText.color = Color.yellow;
        }

        private void Update()
        {
            if (floatingLabel != null && Camera.main != null)
            {
                floatingLabel.transform.LookAt(Camera.main.transform);
                floatingLabel.transform.Rotate(0, 180, 0);

                string status = currentLoad >= capacity ? "<color=orange>FULL! Collect via [M]</color>" : $"({currentLoad}/{capacity}) [M]";
                floatingText.text = $"MINION\n{status}";
            }
        }

        private IEnumerator BehaviorRoot()
        {
            // Кэшируем WellGenerator раз в корутине
            WellGenerator wg = null;
            VoxelIsland island = null;

            while (true)
            {
                if (currentLoad >= capacity)
                {
                    // Full — ждём пока игрок заберёт добычу
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                // Найди/обнови WellGenerator если нет
                if (wg == null) wg = FindFirstObjectByType<WellGenerator>();
                if (wg == null) { yield return new WaitForSeconds(2f); continue; }

                island = wg.ActiveIsland;
                if (island == null) { yield return new WaitForSeconds(2f); continue; }

                // Нужна шахта?
                if (targetMine == null || targetMine.IsExhausted || exhaustedMines.Contains(targetMine))
                {
                    targetMine = null;
                    FindNewMine(wg);

                    if (targetMine == null)
                    {
                        // Нет доступных шахт — отдыхаем
                        yield return new WaitForSeconds(3.0f);
                        continue;
                    }
                }

                // Центр шахты
                int baseW = targetMine.shopData != null ? targetMine.shopData.wellWidth : 5;
                int baseL = targetMine.shopData != null ? targetMine.shopData.wellLength : 5;
                int pad = targetMine.shopData != null ? targetMine.shopData.padding : 0;
                int mineW = baseW + pad * 2;
                int mineL = baseL + pad * 2;
                int x0 = targetMine.originX - (baseW / 2) - pad;
                int z0 = targetMine.originZ - (baseL / 2) - pad;
                Vector3 targetPos = island.transform.TransformPoint(
                    new Vector3(x0 + mineW * 0.5f, -wg.LobbyFloorY, z0 + mineL * 0.5f));

                float dist = Vector3.Distance(transform.position, targetPos);

                if (dist > 3f)
                {
                    // Двигаемся к шахте
                    Vector3 dir = (targetPos - transform.position).normalized;
                    transform.position += dir * moveSpeed * Time.deltaTime;
                    transform.LookAt(targetPos);
                    yield return null;
                }
                else
                {
                    // Копаем блок
                    yield return StartCoroutine(MiningRoutine(island, wg));
                }
            }
        }

        private IEnumerator MiningRoutine(VoxelIsland island, WellGenerator wg)
        {
            if (targetMine == null) yield break;

            int baseW = targetMine.shopData != null ? targetMine.shopData.wellWidth : 5;
            int baseL = targetMine.shopData != null ? targetMine.shopData.wellLength : 5;
            int pad = targetMine.shopData != null ? targetMine.shopData.padding : 0;
            int mineW = baseW + pad * 2;
            int mineL = baseL + pad * 2;
            int mineD = targetMine.rolledDepth > 0 ? targetMine.rolledDepth : 20;
            int x0 = targetMine.originX - (baseW / 2) - pad;
            int z0 = targetMine.originZ - (baseL / 2) - pad;
            int floorY = wg != null ? wg.LobbyFloorY : 0;

            Vector3Int? blockPos = FindSolidBlockInMine(island, x0, z0, floorY, mineW, mineL, mineD);

            if (blockPos.HasValue)
            {
                Vector3 worldTarget = island.transform.TransformPoint(
                    new Vector3(blockPos.Value.x + 0.5f, -blockPos.Value.y + 0.5f, blockPos.Value.z + 0.5f));
                transform.LookAt(worldTarget);

                // Анимация удара
                float tiltTime = 0.5f / mineSpeed;

                float elapsed = 0f;
                while (elapsed < tiltTime)
                {
                    elapsed += Time.deltaTime;
                    if (pickaxePivot != null)
                        pickaxePivot.localRotation = Quaternion.Euler(Mathf.Lerp(0, 60, elapsed / tiltTime), 0, 0);
                    yield return null;
                }

                MineOneBlockAt(island, blockPos.Value);

                elapsed = 0f;
                while (elapsed < tiltTime)
                {
                    elapsed += Time.deltaTime;
                    if (pickaxePivot != null)
                        pickaxePivot.localRotation = Quaternion.Euler(Mathf.Lerp(60, 0, elapsed / tiltTime), 0, 0);
                    yield return null;
                }
            }
            else
            {
                // В шахте нет видимых блоков — добавляем в блэклист и ищем другую
                Debug.Log($"[MinionAI] No solid blocks found in mine at ({targetMine.originX},{targetMine.originZ}). Blacklisting.");
                exhaustedMines.Add(targetMine);
                targetMine = null;
                yield return new WaitForSeconds(2.0f);
            }
        }

        private Vector3Int? FindSolidBlockInMine(VoxelIsland island, int startX, int startZ, int floorY, int mineW, int mineL, int mineD)
        {
            for (int y = 0; y < mineD; y++)
            {
                for (int x = 0; x < mineW; x++)
                {
                    for (int z = 0; z < mineL; z++)
                    {
                        int gx = startX + x;
                        int gy = floorY + y;
                        int gz = startZ + z;
                        if (island.IsSolid(gx, gy, gz))
                            return new Vector3Int(gx, gy, gz);
                    }
                }
            }
            return null;
        }

        private void MineOneBlockAt(VoxelIsland island, Vector3Int pos)
        {
            if (island.TryGetBlockType(pos.x, pos.y, pos.z, out BlockType bt))
            {
                island.RemoveVoxel(pos.x, pos.y, pos.z);
                targetMine?.RegisterMinedBlock(pos.x, pos.y, pos.z);
                storedValue += 5;
                currentLoad++;
            }
        }

        private void FindNewMine(WellGenerator wg)
        {
            if (wg.PlacedMines == null || wg.PlacedMines.Count == 0) return;

            // Ищем ближайшую незаблокированную, неистощённую шахту
            MineInstance best = null;
            float bestDist = float.MaxValue;

            foreach (var mine in wg.PlacedMines)
            {
                if (mine == null || mine.IsExhausted || exhaustedMines.Contains(mine)) continue;
                int baseW = mine.shopData != null ? mine.shopData.wellWidth : 5;
                int baseL = mine.shopData != null ? mine.shopData.wellLength : 5;
                int pad = mine.shopData != null ? mine.shopData.padding : 0;
                int mineW = baseW + pad * 2;
                int mineL = baseL + pad * 2;
                int x0 = mine.originX - (baseW / 2) - pad;
                int z0 = mine.originZ - (baseL / 2) - pad;
                float d = Vector3.Distance(transform.position,
                    new Vector3(x0 + mineW * 0.5f, transform.position.y, z0 + mineL * 0.5f));
                if (d < bestDist) { bestDist = d; best = mine; }
            }

            // Если все в блэклисте — сбрасываем блэклист и пробуем снова
            if (best == null && exhaustedMines.Count > 0)
            {
                Debug.Log("[MinionAI] All mines blacklisted, resetting blacklist.");
                exhaustedMines.Clear();
                FindNewMine(wg);
                return;
            }

            targetMine = best;
        }

        public void EmptyInventory()
        {
            if (currentLoad > 0)
            {
                GlobalEconomy.Money += storedValue;
                currentLoad = 0;
                storedValue = 0;
            }
        }

        private void OnMouseDown()
        {
            MinionManagementUI.Show(this);
        }
    }
}
