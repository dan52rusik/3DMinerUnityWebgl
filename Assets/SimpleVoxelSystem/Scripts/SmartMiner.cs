using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    public class SmartMiner : MonoBehaviour
    {
        [Header("Mining")]
        public float mineCooldown = 0.3f;
        public LayerMask voxelLayer = Physics.DefaultRaycastLayers;
        public bool autoMine = false;
        public bool disableLegacyRaycastMining = true;

        [Header("Mouse Targeting")]
        public float maxMineDistance = 7f;
        public float maxTargetDistance = 60f;
        public bool autoMoveToFarTargets = true;
        public float autoMoveStopDistance = 1.6f;

        [Header("Highlight")]
        public GameObject highlightPrefab;
        public Vector3 highlightScale = new Vector3(1.05f, 1.05f, 1.05f);
        public Color defaultHighlightColor = new Color(1f, 0.92f, 0.2f, 0.35f);

        private WellGenerator wellGenerator;
        private VoxelIsland island;
        private PlayerPickaxe pickaxe;
        private PlayerCharacterController playerController;

        private GameObject highlightInstance;
        private Vector3Int currentTargetGridPos;
        private Vector3 currentTargetWorldPos;
        private float currentTargetDistance;
        private bool hasTarget;
        private float lastMineTime;

        private bool queuedAutoMine;
        private Vector3Int queuedTargetGridPos;
        private Vector3 queuedTargetWorldPos;

        void Start()
        {
            pickaxe = GetComponent<PlayerPickaxe>();
            if (pickaxe == null)
                pickaxe = GetComponentInChildren<PlayerPickaxe>();

            playerController = GetComponent<PlayerCharacterController>();
            if (playerController == null)
                playerController = GetComponentInChildren<PlayerCharacterController>();

            wellGenerator = FindFirstObjectByType<WellGenerator>();
            if (wellGenerator != null)
                island = wellGenerator.GetComponent<VoxelIsland>();
            if (island == null)
                island = FindFirstObjectByType<VoxelIsland>();

            if (pickaxe != null && disableLegacyRaycastMining)
                pickaxe.enableManualRaycastMining = false;

            highlightInstance = CreateHighlightInstance();
            if (highlightInstance != null)
                highlightInstance.SetActive(false);
        }

        void Update()
        {
            if (island == null)
                return;

            FindTargetBlock();
            HandleQueuedAutoMine();

            if (!hasTarget)
                return;

            if (WasMinePressedDown())
                TryStartAutoMoveToTarget();

            bool mineInput = autoMine || IsMineHeld();
            if (!mineInput)
                return;

            if (currentTargetDistance > maxMineDistance)
            {
                if (autoMoveToFarTargets)
                    TryStartAutoMoveToTarget();
                return;
            }

            if (Time.time < lastMineTime + mineCooldown)
                return;

            if (MineTargetBlock(currentTargetGridPos))
                lastMineTime = Time.time;
        }

        void HandleQueuedAutoMine()
        {
            if (!queuedAutoMine)
                return;

            float dist = Vector3.Distance(transform.position, queuedTargetWorldPos);
            if (dist > maxMineDistance)
                return;

            if (Time.time < lastMineTime + mineCooldown)
                return;

            if (MineTargetBlock(queuedTargetGridPos))
            {
                lastMineTime = Time.time;
                queuedAutoMine = false;
                if (playerController != null)
                    playerController.CancelAutoMove();
            }
        }

        void TryStartAutoMoveToTarget()
        {
            if (!autoMoveToFarTargets || playerController == null || !hasTarget)
                return;

            queuedAutoMine = true;
            queuedTargetGridPos = currentTargetGridPos;
            queuedTargetWorldPos = currentTargetWorldPos;
            playerController.SetAutoMoveTarget(currentTargetWorldPos, autoMoveStopDistance);
        }

        void FindTargetBlock()
        {
            hasTarget = false;

            Camera cam = Camera.main;
            if (cam == null)
            {
                if (highlightInstance != null && highlightInstance.activeSelf)
                    highlightInstance.SetActive(false);
                return;
            }

            Vector2 pointerPos = ReadPointerPosition();
            Ray ray = cam.ScreenPointToRay(pointerPos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxTargetDistance, voxelLayer, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<VoxelIsland>() != null)
            {
                Vector3 localHit = island.transform.InverseTransformPoint(hit.point - hit.normal * 0.5f);

                int gx = Mathf.FloorToInt(localHit.x);
                int gy = -Mathf.FloorToInt(localHit.y);
                int gz = Mathf.FloorToInt(localHit.z);

                if (island.TryGetBlockType(gx, gy, gz, out _))
                {
                    if (wellGenerator != null && !wellGenerator.CanMineVoxel(gx, gy, gz))
                    {
                        if (highlightInstance != null && highlightInstance.activeSelf)
                            highlightInstance.SetActive(false);
                        return;
                    }

                    currentTargetGridPos = new Vector3Int(gx, gy, gz);
                    hasTarget = true;

                    Vector3 blockLocalPos = island.GridToLocal(gx, gy, gz) + new Vector3(0.5f, 0.5f, 0.5f);
                    currentTargetWorldPos = island.transform.TransformPoint(blockLocalPos);
                    currentTargetDistance = Vector3.Distance(transform.position, currentTargetWorldPos);

                    if (highlightInstance != null)
                    {
                        highlightInstance.transform.position = currentTargetWorldPos;
                        if (!highlightInstance.activeSelf)
                            highlightInstance.SetActive(true);
                    }

                    return;
                }
            }

            if (highlightInstance != null && highlightInstance.activeSelf)
                highlightInstance.SetActive(false);
        }

        bool MineTargetBlock(Vector3Int target)
        {
            if (pickaxe == null)
            {
                Debug.LogWarning("[SmartMiner] Не найден PlayerPickaxe на игроке.", this);
                return false;
            }

            return pickaxe.TryMineGridTarget(target.x, target.y, target.z, island);
        }

        GameObject CreateHighlightInstance()
        {
            GameObject obj;

            if (highlightPrefab != null)
            {
                obj = Instantiate(highlightPrefab);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "AutoBlockHighlight";
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null)
                        shader = Shader.Find("Standard");

                    Material mat = new Material(shader);
                    mat.color = defaultHighlightColor;

                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                    if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    renderer.material = mat;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            obj.transform.localScale = highlightScale;

            Collider[] cols = obj.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;

            return obj;
        }

        Vector2 ReadPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        bool WasMinePressedDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        bool IsMineHeld()
        {
#if ENABLE_INPUT_SYSTEM
            bool mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            return mouseHeld || spaceHeld;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
