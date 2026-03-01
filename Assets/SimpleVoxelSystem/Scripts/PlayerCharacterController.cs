using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 6f;
        public float runMultiplier = 1.7f;
        public float jumpHeight = 1.2f;
        public float gravity = -25f;
        public float autoMoveTurnSpeed = 480f;
        public bool rotateCharacterDuringAutoMove = false;

        [Header("View")]
        public Transform cameraHolder;
        public float lookSensitivity = 2.2f;
        public float minPitch = -80f;
        public float maxPitch = 80f;
        public bool lockCursorOnStart = false;
        public bool keepPlayerAlwaysInFrame = true;
        public float playerFocusHeight = 1.1f;

        [Header("Zoom")]
        public bool enableZoomWhileLooking = true;
        public float zoomSpeed = 2.0f;
        public float minZoomDistance = 2.5f;
        public float maxZoomDistance = 20f;
        public float zoomVerticalRatio = 0.55f;
        public float minCameraHeight = 1.6f;
        public float closeZoomMinPitch = 8f;
        public float closeZoomMaxPitch = 68f;
        public float farZoomMinPitch = -30f;
        public float farZoomMaxPitch = 78f;

        [Header("Obstruction")]
        public bool avoidCameraObstruction = true;
        public LayerMask cameraObstructionMask = Physics.DefaultRaycastLayers;
        public float cameraCollisionRadius = 0.2f;
        public float cameraCollisionBuffer = 0.12f;
        public float cameraPositionSmooth = 18f;

        [Header("Vertical Offset")]
        public float cameraLiftSpeed = 1.6f;
        public float minCameraLift = -0.5f;
        public float maxCameraLift = 6f;
        public bool autoRaiseWhenObstructed = true;
        public float obstructionLiftMax = 2.0f;
        public float obstructionLiftSmooth = 8f;

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;
        private float currentZoomDistance;

        private bool autoMoveActive;
        private Vector3 autoMoveTarget;
        private float autoMoveStopDistance = 1.5f;
        private float cameraLiftOffset;
        private float obstructionLiftOffset;

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraHolder == null && Camera.main != null)
                cameraHolder = Camera.main.transform;

            if (cameraHolder != null && cameraHolder.parent != transform)
                cameraHolder.SetParent(transform, true);

            if (cameraHolder != null)
            {
                pitch = NormalizeAngle(cameraHolder.localEulerAngles.x);
                InitializeZoomState();
            }

            if (lockCursorOnStart)
                SetCursorLocked(true);
            else
                SetCursorLocked(false);
        }

        void Update()
        {
            SetCursorLocked(IsLookButtonHeld());

            HandleLook();
            HandleZoom();
            HandleMove();
            UpdateCameraRig();
        }

        void HandleMove()
        {
            if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
                return;

            Vector2 moveInput = ReadMoveInput();
            bool hasManualMove = moveInput.sqrMagnitude > 0.0001f;
            bool isRunning = IsRunPressed();

            if (hasManualMove)
                CancelAutoMove();

            Vector3 horizontal;
            if (autoMoveActive && !hasManualMove)
            {
                Vector3 toTarget = autoMoveTarget - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist <= autoMoveStopDistance)
                {
                    CancelAutoMove();
                    horizontal = Vector3.zero;
                }
                else
                {
                    Vector3 dir = toTarget / Mathf.Max(dist, 0.0001f);
                    if (rotateCharacterDuringAutoMove)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, autoMoveTurnSpeed * Time.deltaTime);
                    }
                    horizontal = dir * walkSpeed;
                }
            }
            else
            {
                float speed = walkSpeed * (isRunning ? runMultiplier : 1f);
                horizontal = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (controller.isGrounded && WasJumpPressed())
            {
                CancelAutoMove();
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontal + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }

        void HandleLook()
        {
            if (!IsLookButtonHeld())
                return;

            Vector2 look = ReadLookDelta();
            transform.Rotate(0f, look.x * lookSensitivity, 0f);

            if (keepPlayerAlwaysInFrame)
            {
                cameraLiftOffset = Mathf.Clamp(cameraLiftOffset + look.y * cameraLiftSpeed, minCameraLift, maxCameraLift);
                return;
            }

            float t = Mathf.InverseLerp(minZoomDistance, maxZoomDistance, currentZoomDistance);
            float dynamicMinPitch = Mathf.Lerp(closeZoomMinPitch, farZoomMinPitch, t);
            float dynamicMaxPitch = Mathf.Lerp(closeZoomMaxPitch, farZoomMaxPitch, t);
            float clampedMin = Mathf.Max(minPitch, dynamicMinPitch);
            float clampedMax = Mathf.Min(maxPitch, dynamicMaxPitch);

            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, clampedMin, clampedMax);
            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void InitializeZoomState()
        {
            Vector3 localPos = cameraHolder.localPosition;
            float forwardDist = Mathf.Abs(localPos.z);
            if (forwardDist < 0.001f)
            {
                currentZoomDistance = Mathf.Clamp(8f, minZoomDistance, maxZoomDistance);
                cameraHolder.localPosition = BuildZoomLocalPosition(currentZoomDistance);
                return;
            }

            zoomVerticalRatio = Mathf.Clamp(localPos.y / forwardDist, 0.15f, 2f);
            currentZoomDistance = Mathf.Clamp(forwardDist, minZoomDistance, maxZoomDistance);
            cameraHolder.localPosition = BuildZoomLocalPosition(currentZoomDistance);
        }

        void HandleZoom()
        {
            if (!enableZoomWhileLooking || cameraHolder == null)
                return;
            if (!IsLookButtonHeld())
                return;

            float zoomInput = ReadZoomInput();
            if (Mathf.Abs(zoomInput) < 0.001f)
                return;

            currentZoomDistance = Mathf.Clamp(currentZoomDistance - zoomInput * zoomSpeed, minZoomDistance, maxZoomDistance);
        }

        Vector3 BuildZoomLocalPosition(float distance)
        {
            float y = Mathf.Max(minCameraHeight, distance * zoomVerticalRatio);
            return new Vector3(0f, y, -distance);
        }

        void UpdateCameraRig()
        {
            if (cameraHolder == null)
                return;

            Vector3 focus = transform.position + Vector3.up * playerFocusHeight;
            Vector3 desiredWorldPos = transform.TransformPoint(BuildZoomLocalPosition(currentZoomDistance));
            float desiredObstructionLift = 0f;

            if (avoidCameraObstruction)
                desiredWorldPos = ResolveObstructionAdjustedPosition(focus, desiredWorldPos, out desiredObstructionLift);

            if (!autoRaiseWhenObstructed)
                desiredObstructionLift = 0f;

            float smoothLiftT = 1f - Mathf.Exp(-obstructionLiftSmooth * Time.deltaTime);
            obstructionLiftOffset = Mathf.Lerp(obstructionLiftOffset, desiredObstructionLift, smoothLiftT);
            float totalLift = cameraLiftOffset + obstructionLiftOffset;
            desiredWorldPos += Vector3.up * totalLift;

            float smoothT = 1f - Mathf.Exp(-cameraPositionSmooth * Time.deltaTime);
            cameraHolder.position = Vector3.Lerp(cameraHolder.position, desiredWorldPos, smoothT);

            if (!keepPlayerAlwaysInFrame)
                return;

            Vector3 dir = focus - cameraHolder.position;
            if (dir.sqrMagnitude > 0.0001f)
                cameraHolder.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        Vector3 ResolveObstructionAdjustedPosition(Vector3 focus, Vector3 desiredWorldPos, out float obstructionLift)
        {
            obstructionLift = 0f;
            Vector3 toCam = desiredWorldPos - focus;
            float dist = toCam.magnitude;
            if (dist < 0.001f)
                return desiredWorldPos;

            Vector3 dir = toCam / dist;
            RaycastHit[] hits = Physics.SphereCastAll(
                focus,
                cameraCollisionRadius,
                dir,
                dist,
                cameraObstructionMask,
                QueryTriggerInteraction.Ignore
            );

            float nearest = float.MaxValue;
            bool blocked = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || IsSelfCollider(h.collider))
                    continue;

                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    blocked = true;
                }
            }

            if (!blocked)
                return desiredWorldPos;

            float safeDist = Mathf.Max(0.6f, nearest - cameraCollisionBuffer);
            float blockedFactor = Mathf.Clamp01(1f - (safeDist / Mathf.Max(dist, 0.001f)));
            obstructionLift = blockedFactor * obstructionLiftMax;
            return focus + dir * safeDist;
        }

        bool IsSelfCollider(Collider c)
        {
            return c.transform == transform || c.transform.IsChildOf(transform);
        }


        public void SetAutoMoveTarget(Vector3 worldTarget, float stopDistance = 1.5f)
        {
            autoMoveTarget = worldTarget;
            autoMoveStopDistance = Mathf.Max(0.5f, stopDistance);
            autoMoveActive = true;
        }

        public void CancelAutoMove()
        {
            autoMoveActive = false;
        }

        public bool IsAutoMoving()
        {
            return autoMoveActive;
        }

        static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
                angle -= 360f;
            return angle;
        }

        Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
                return Vector2.zero;

            float x = 0f;
            float y = 0f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            return new Vector2(x, y).normalized;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
#else
            return Vector2.zero;
#endif
        }

        Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.02f : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        bool IsRunPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift);
#else
            return false;
#endif
        }

        bool WasJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        bool IsLookButtonHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(2);
#else
            return false;
#endif
        }

        float ReadZoomInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
                return 0f;
            float y = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(y) < 0.01f)
                return 0f;
            return Mathf.Sign(y);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

    }
}
