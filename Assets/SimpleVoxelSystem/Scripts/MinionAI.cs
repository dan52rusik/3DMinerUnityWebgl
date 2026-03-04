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

        private void Start()
        {
            CreateVisuals();
            CreateFloatingLabel();
            StartCoroutine(BehaviorRoot());
        }

        private void CreateVisuals()
        {
            // Root group for visuals (to allow scaling/rotating independently of AI)
            GameObject group = new GameObject("VisualGroup");
            group.transform.SetParent(transform, false);
            visualGroup = group.transform;
            visualGroup.localScale = Vector3.one * 0.5f;

            // Main Body (Capsule)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visualGroup, false);
            Destroy(body.GetComponent<Collider>()); // Colliders are on the root or not needed for visual
            
            var mr = body.GetComponent<MeshRenderer>();
            mr.material.color = new Color(1f, 0.5f, 0f); // Worker Orange

            // Simple Pickaxe
            GameObject pPivot = new GameObject("PickaxePivot");
            pPivot.transform.SetParent(visualGroup, false);
            pPivot.transform.localPosition = new Vector3(0.6f, 0.2f, 0.5f);
            pickaxePivot = pPivot.transform;

            // Pickaxe Handle
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.transform.SetParent(pickaxePivot, false);
            handle.transform.localScale = new Vector3(0.1f, 0.1f, 0.8f);
            handle.transform.localPosition = new Vector3(0, 0, 0);
            handle.GetComponent<MeshRenderer>().material.color = new Color(0.4f, 0.2f, 0f);
            Destroy(handle.GetComponent<Collider>());

            // Pickaxe Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(pickaxePivot, false);
            head.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            head.transform.localPosition = new Vector3(0, 0, 0.4f);
            head.GetComponent<MeshRenderer>().material.color = Color.gray;
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

            floatingText = RuntimeUIFactory.MakeLabel(floatingLabel.transform, "Text", "MINION\n[Click to Manage]", 14, TextAnchor.MiddleCenter);
            floatingText.color = Color.yellow;
        }

        private void Update()
        {
            if (floatingLabel != null)
            {
                floatingLabel.transform.LookAt(Camera.main.transform);
                floatingLabel.transform.Rotate(0, 180, 0);
                
                // Proximity check
                float distToPlayer = Vector3.Distance(transform.position, Camera.main.transform.position);
                bool isClose = distToPlayer < 5f;

                if (isClose)
                {
                    floatingText.text = $"MINION ({currentLoad}/{capacity})\n<color=lime>Press [E]</color>";
                    
                    bool ePressed = false;
#if ENABLE_INPUT_SYSTEM
                    if (Keyboard.current != null) ePressed = Keyboard.current.eKey.wasPressedThisFrame;
#else
                    ePressed = Input.GetKeyDown(KeyCode.E);
#endif

                    if (ePressed)
                    {
                        MinionManagementUI.Show(this);
                    }
                }
                else
                {
                    floatingText.text = $"MINION ({currentLoad}/{capacity})\n[Manage]";
                }
            }
        }

        private IEnumerator BehaviorRoot()
        {
            while (true)
            {
                if (currentLoad >= capacity)
                {
                    // Full, wait for player
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                if (targetMine == null || targetMine.IsExhausted)
                {
                    FindNewMine();
                    if (targetMine == null)
                    {
                        yield return new WaitForSeconds(2.0f);
                        continue;
                    }
                }

                var wg = FindFirstObjectByType<WellGenerator>();
                if (wg == null) { yield return new WaitForSeconds(2f); continue; }
                
                VoxelIsland island = wg.ActiveIsland;
                if (island == null) { yield return new WaitForSeconds(2f); continue; }

                // Move toward mine
                Vector3 targetPos = island.transform.TransformPoint(new Vector3(targetMine.originX + 2.5f, 1f, targetMine.originZ + 2.5f));
                float dist = Vector3.Distance(transform.position, targetPos);
                
                if (dist > 3f)
                {
                    Vector3 dir = (targetPos - transform.position).normalized;
                    transform.position += dir * moveSpeed * Time.deltaTime;
                    transform.LookAt(targetPos);
                    yield return null;
                }
                else
                {
                    // At mine, choose a block and mine it
                    yield return StartCoroutine(MiningRoutine(island));
                }
            }
        }

        private IEnumerator MiningRoutine(VoxelIsland island)
        {
            if (targetMine == null) yield break;
            
            Vector3Int? blockPos = FindSolidBlockInMine(island, targetMine);
            
            if (blockPos.HasValue)
            {
                Vector3 worldTarget = island.transform.TransformPoint(new Vector3(blockPos.Value.x + 0.5f, -blockPos.Value.y + 0.5f, blockPos.Value.z + 0.5f));
                transform.LookAt(worldTarget);
                
                // Attack animation (tilt pickaxe)
                float tiltTime = 0.5f / mineSpeed;
                
                // Swing Forward
                float elapsed = 0f;
                while (elapsed < tiltTime)
                {
                    elapsed += Time.deltaTime;
                    if (pickaxePivot != null) pickaxePivot.localRotation = Quaternion.Euler(Mathf.Lerp(0, 60, elapsed / tiltTime), 0, 0);
                    yield return null;
                }

                MineOneBlockAt(island, blockPos.Value);

                // Swing Back
                elapsed = 0f;
                while (elapsed < tiltTime)
                {
                    elapsed += Time.deltaTime;
                    if (pickaxePivot != null) pickaxePivot.localRotation = Quaternion.Euler(Mathf.Lerp(60, 0, elapsed / tiltTime), 0, 0);
                    yield return null;
                }
            }
            else
            {
                targetMine = null; 
                yield return new WaitForSeconds(1.0f);
            }
        }

        private Vector3Int? FindSolidBlockInMine(VoxelIsland island, MineInstance mine)
        {
            for (int y = 0; y < 15; y++) 
            {
                for (int x = 0; x < 5; x++)
                {
                    for (int z = 0; z < 5; z++)
                    {
                        int gx = mine.originX + x;
                        int gy = y;
                        int gz = mine.originZ + z;
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

        private void FindNewMine()
        {
            var wg = FindFirstObjectByType<WellGenerator>();
            if (wg != null && wg.PlacedMines != null && wg.PlacedMines.Count > 0)
            {
                targetMine = wg.PlacedMines[Random.Range(0, wg.PlacedMines.Count)];
            }
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
