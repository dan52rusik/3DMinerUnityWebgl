using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Простая флай-камера для теста сцены.
    /// Управление:
    /// - RMB зажать: вращение мышью
    /// - WASD: движение
    /// - Q/E: вниз/вверх
    /// - Shift: ускорение
    /// - Mouse Wheel: изменить базовую скорость
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        [Header("Move")]
        public float moveSpeed = 12f;
        public float fastMultiplier = 3f;
        public float wheelSpeedStep = 1f;
        public float minSpeed = 1f;
        public float maxSpeed = 80f;

        [Header("Look")]
        public float lookSensitivity = 2.5f;
        public bool invertY = false;

        private float yaw;
        private float pitch;

        void Start()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
        }

        void Update()
        {
            HandleSpeedByWheel();
            HandleLook();
            HandleMove();
        }

        void HandleSpeedByWheel()
        {
            float wheel = ReadScrollY();
            if (Mathf.Abs(wheel) < 0.001f)
                return;

            moveSpeed = Mathf.Clamp(moveSpeed + wheel * wheelSpeedStep, minSpeed, maxSpeed);
        }

        void HandleLook()
        {
            if (!IsLookPressed())
                return;

            Vector2 mouseDelta = ReadMouseDelta();
            float mouseX = mouseDelta.x * lookSensitivity;
            float mouseY = mouseDelta.y * lookSensitivity * (invertY ? 1f : -1f);

            yaw += mouseX;
            pitch = Mathf.Clamp(pitch + mouseY, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        void HandleMove()
        {
            float speed = IsFastMovePressed() ? moveSpeed * fastMultiplier : moveSpeed;

            Vector3 input = Vector3.zero;
            if (IsForwardPressed()) input += Vector3.forward;
            if (IsBackPressed()) input += Vector3.back;
            if (IsLeftPressed()) input += Vector3.left;
            if (IsRightPressed()) input += Vector3.right;
            if (IsUpPressed()) input += Vector3.up;
            if (IsDownPressed()) input += Vector3.down;

            if (input.sqrMagnitude < 0.0001f)
                return;

            transform.position += transform.TransformDirection(input.normalized) * speed * Time.deltaTime;
        }

        bool IsLookPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.02f : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        float ReadScrollY()
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

        bool IsForwardPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.wKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.W);
#else
            return false;
#endif
        }

        bool IsBackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.sKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.S);
#else
            return false;
#endif
        }

        bool IsLeftPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.aKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.A);
#else
            return false;
#endif
        }

        bool IsRightPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.dKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.D);
#else
            return false;
#endif
        }

        bool IsUpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.E);
#else
            return false;
#endif
        }

        bool IsDownPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Q);
#else
            return false;
#endif
        }

        bool IsFastMovePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift);
#else
            return false;
#endif
        }
    }
}
