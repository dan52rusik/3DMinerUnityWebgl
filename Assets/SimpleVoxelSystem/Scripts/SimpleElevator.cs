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
        public float moveCooldown = 1.0f; // Пауза между поездками

        private Vector3 targetPos;
        private bool isMoving;
        private Transform rider;
        private Collider[] selfColliders;
        private bool playerInsideTrigger;
        private float lastMoveTime; // Время завершения последнего движения

        void Awake()
        {
            selfColliders = GetComponentsInChildren<Collider>(true);
        }

        void Update()
        {
            if (!isMoving)
            {
                if (rider == null)
                    TryAutoLowerToClearedDepth();
                return;
            }

            Vector3 oldPos = transform.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            Vector3 delta = transform.position - oldPos;

            // Двигаем игрока за собой вручную
            if (rider != null)
            {
                CharacterController cc = rider.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                    rider.position += delta;
                    cc.enabled = true;
                }
                else
                {
                    rider.position += delta;
                }
            }

            if (Vector3.SqrMagnitude(transform.position - targetPos) <= 0.0001f)
            {
                isMoving = false;
                ReleaseRider();
                lastMoveTime = Time.time; // Ставим метку времени завершения
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            playerInsideTrigger = true;

            // Если только что приехали или уже в пути — не перезапускаем
            if (isMoving || (Time.time - lastMoveTime < moveCooldown))
                return;

            // Если мы и так на самом верху — ехать некуда
            if (Mathf.Abs(transform.position.y - topY) < 0.1f)
                return;

            if (autoStartOnEnter)
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
            // rider.SetParent(transform, true); // Вызывает ошибку в Netcode
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

            // Не опускаемся, если недавно двигались (защита от дерготни)
            if (Time.time - lastMoveTime < moveCooldown)
                return;

            int clearedDepth = wellGenerator.GetContiguousClearedDepth();
            float desiredY = GetWorldYForDepth(clearedDepth);

            // Если мы уже на месте
            if (Mathf.Abs(transform.position.y - desiredY) < 0.02f)
                return;

            // ВАЖНО: Если мы на самом верху и игрок внутри — НЕ ОПУСКАЕМСЯ.
            // Даем игроку возможность выйти из шахты.
            if (playerInsideTrigger && Mathf.Abs(transform.position.y - topY) < 0.1f)
                return;

            targetPos = new Vector3(transform.position.x, desiredY, transform.position.z);
            isMoving = true;
            
            // Если игрок на платформе — берем его с собой как rider, 
            // чтобы движение вниз было плавным и не вызывало OnTriggerEnter/Exit
            if (playerInsideTrigger)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) rider = p.transform;
            }
        }

        private float GetWorldYForDepth(int depthIndex)
        {
            // depthIndex 0 в шахте соответствует уровню LobbyFloorY в сетке вокселей.
            // Также добавляем платформу на 0.5f выше пола ячейки.
            int gridY = (wellGenerator != null) ? wellGenerator.LobbyFloorY + depthIndex : depthIndex;
            Vector3 local = island.GridToLocal(shaftGridX, gridY, shaftGridZ) + new Vector3(0.5f, platformHalfHeight, 0.5f);
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

            // rider.SetParent(null, true); // Не нужно, так как больше нет привязки
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
