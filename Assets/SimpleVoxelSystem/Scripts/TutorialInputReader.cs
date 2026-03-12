// TutorialInputReader.cs — isolated input helpers for OnboardingTutorial
// No MonoBehaviour dependencies; static, zero-allocation, easily testable.

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Centralised input queries used by the tutorial system.
    /// Handles both the new Input System and the Legacy Input Manager transparently.
    /// </summary>
    internal static class TutorialInputReader
    {
        /// <summary>WASD pressed (held) — used to detect PC movement.</summary>
        public static bool IsMovePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.wKey.isPressed || kb.aKey.isPressed ||
                   kb.sKey.isPressed || kb.dKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f
                || Mathf.Abs(Input.GetAxisRaw("Vertical"))   > 0.01f;
#else
            return false;
#endif
        }

        /// <summary>Any movement key or space pressed THIS frame — dismisses PC tutorial card.</summary>
        public static bool IsContinuePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            var kb = Keyboard.current;
            return kb.wKey.wasPressedThisFrame
                || kb.aKey.wasPressedThisFrame
                || kb.sKey.wasPressedThisFrame
                || kb.dKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.W)
                || Input.GetKeyDown(KeyCode.A)
                || Input.GetKeyDown(KeyCode.S)
                || Input.GetKeyDown(KeyCode.D)
                || Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        /// <summary>Left mouse button pressed THIS frame — mining input on PC.</summary>
        public static bool IsMinePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        /// <summary>Touch began OR left mouse click THIS frame — used for "tap to continue".</summary>
        public static bool WasTapped()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame) return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
            if (Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }

        public static bool TryGetTapPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                position = Input.GetTouch(0).position;
                return true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
#endif

            position = Vector2.zero;
            return false;
        }
    }
}
