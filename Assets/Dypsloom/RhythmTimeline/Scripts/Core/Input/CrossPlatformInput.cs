namespace Dypsloom.RhythmTimeline.Core.Input
{
    using UnityEngine;

    public static class CrossPlatformInput
    {
        public static Vector3 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null) {
                    return mouse.position.ReadValue();
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
            return Vector3.zero;
#endif
            }
        }


        public static bool GetMouseButtonDown(int button = 0)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) {
                switch (button) {
                    case 0: return mouse.leftButton.wasPressedThisFrame;
                    case 1: return mouse.rightButton.wasPressedThisFrame;
                    case 2: return mouse.middleButton.wasPressedThisFrame;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
    return false;
#endif
        }

        public static bool GetMouseButtonUp(int button = 0)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) {
                switch (button) {
                    case 0: return mouse.leftButton.wasReleasedThisFrame;
                    case 1: return mouse.rightButton.wasReleasedThisFrame;
                    case 2: return mouse.middleButton.wasReleasedThisFrame;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(button);
#else
    return false;
#endif
        }

        public static bool GetMouseButton(int button = 0)
        {
           
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                switch (button)
                {
                    case 0: return mouse.leftButton.isPressed;
                    case 1: return mouse.rightButton.isPressed;
                    case 2: return mouse.middleButton.isPressed;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
    return false;
#endif
        }
    }
}