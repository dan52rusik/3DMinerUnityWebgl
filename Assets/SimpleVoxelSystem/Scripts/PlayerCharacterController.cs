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

        [Header("View")]
        public Transform cameraHolder;
        public float lookSensitivity = 2.2f;
        public float minPitch = -80f;
        public float maxPitch = 80f;
        public bool lockCursorOnStart = false;

        [Header("Zoom")]
        public bool enableZoomWhileLooking = true;
        public float zoomSpeed = 2.0f;
        public float minZoomDistance = 2.5f;
        public float maxZoomDistance = 20f;

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;
        private Vector3 zoomDirectionLocal;
        private float currentZoomDistance;

        private bool autoMoveActive;
        private Vector3 autoMoveTarget;
        private float autoMoveStopDistance = 1.5f;

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
            if (WasLookButtonPressedDown())
                SetCursorLocked(true);
            else if (WasLookButtonReleased())
                SetCursorLocked(false);

            HandleLook();
            HandleZoom();
            HandleMove();
        }

        void HandleMove()
        {
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
                    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, autoMoveTurnSpeed * Time.deltaTime);
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

            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
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
            float magnitude = localPos.magnitude;
            if (magnitude < 0.001f)
            {
                zoomDirectionLocal = new Vector3(0f, 0.4f, -1f).normalized;
                currentZoomDistance = Mathf.Clamp(8f, minZoomDistance, maxZoomDistance);
                cameraHolder.localPosition = zoomDirectionLocal * currentZoomDistance;
                return;
            }

            zoomDirectionLocal = localPos / magnitude;
            currentZoomDistance = Mathf.Clamp(magnitude, minZoomDistance, maxZoomDistance);
            cameraHolder.localPosition = zoomDirectionLocal * currentZoomDistance;
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
            cameraHolder.localPosition = zoomDirectionLocal * currentZoomDistance;
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

        bool WasLookButtonPressedDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(2);
#else
            return false;
#endif
        }

        bool WasLookButtonReleased()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(2);
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
