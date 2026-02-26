using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Простой лифт-платформа. Заход игрока в триггер может запускать движение,
    /// а также есть ручной запуск кнопкой E пока игрок стоит на платформе.
    /// </summary>
    public class SimpleElevator : MonoBehaviour
    {
        public float speed = 5f;
        public float topY;
        public bool autoStartOnEnter = true;
        public bool allowKeyStart = true;
        public WellGenerator wellGenerator;
        public VoxelIsland island;
        public int shaftGridX;
        public int shaftGridZ;
        public float platformHalfHeight = 0.125f;

        private Vector3 targetPos;
        private bool isMoving;
        private Transform rider;
        private Collider[] selfColliders;
        private bool playerInsideTrigger;

        void Awake()
        {
            selfColliders = GetComponentsInChildren<Collider>(true);
        }

        void Update()
        {
            if (!isMoving)
            {
                if (rider == null && !playerInsideTrigger)
                    TryAutoLowerToClearedDepth();
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (Vector3.SqrMagnitude(transform.position - targetPos) <= 0.0001f)
            {
                isMoving = false;
                ReleaseRider();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            playerInsideTrigger = true;
            if (!autoStartOnEnter || isMoving)
                return;

            StartRideToTop(other.transform);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!allowKeyStart || !other.CompareTag("Player") || isMoving)
                return;

            if (!IsInteractPressedDown())
                return;

            StartRideToTop(other.transform);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
                playerInsideTrigger = false;

            if (rider != null && other.transform == rider)
                ReleaseRider();
        }

        private void StartRideToTop(Transform player)
        {
            rider = player;
            rider.SetParent(transform, true);
            MoveToTop();
        }

        private void MoveToTop()
        {
            targetPos = new Vector3(transform.position.x, topY, transform.position.z);
            isMoving = true;
        }

        private void FindBottomAndMove()
        {
            Vector3 rayOrigin = transform.position + Vector3.down * 0.3f;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 256f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return;

            float bestDist = float.MaxValue;
            bool found = false;
            Vector3 bestPoint = Vector3.zero;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsSelfCollider(hit.collider))
                    continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestPoint = hit.point;
                    found = true;
                }
            }

            if (!found)
                return;

            targetPos = new Vector3(transform.position.x, bestPoint.y + 0.125f, transform.position.z);
            isMoving = true;
        }

        private void TryAutoLowerToClearedDepth()
        {
            if (wellGenerator == null || island == null)
                return;

            int clearedDepth = wellGenerator.GetContiguousClearedDepth();
            float desiredY = GetWorldYForDepth(clearedDepth);

            if (Mathf.Abs(transform.position.y - desiredY) < 0.02f)
                return;

            targetPos = new Vector3(transform.position.x, desiredY, transform.position.z);
            isMoving = true;
        }

        private float GetWorldYForDepth(int depthIndex)
        {
            Vector3 local = island.GridToLocal(shaftGridX, depthIndex, shaftGridZ) + new Vector3(0.5f, platformHalfHeight, 0.5f);
            return island.transform.TransformPoint(local).y;
        }

        private bool IsSelfCollider(Collider c)
        {
            if (selfColliders == null)
                return false;

            for (int i = 0; i < selfColliders.Length; i++)
            {
                if (selfColliders[i] == c)
                    return true;
            }

            return false;
        }

        private void ReleaseRider()
        {
            if (rider == null)
                return;

            rider.SetParent(null, true);
            rider = null;
        }

        private bool IsInteractPressedDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }
    }
}
