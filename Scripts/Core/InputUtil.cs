using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LaserLab
{
    /// <summary>
    /// Extra keys on top of the Starter Assets input (WASD / mouse / Shift / Space).
    /// Reads the keyboard directly, so you do not need to edit the Input Actions asset.
    /// </summary>
    public static class InputUtil
    {
#if ENABLE_INPUT_SYSTEM
        static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        public static bool FlashlightPressed() =>
            Pressed(Key.F) || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
        public static bool InteractPressed() => Pressed(Key.E);
        public static bool CrouchPressed() => Pressed(Key.C) || Pressed(Key.LeftCtrl);
        public static bool RestartPressed() => Pressed(Key.R);
#else
        public static bool FlashlightPressed() => Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1);
        public static bool InteractPressed() => Input.GetKeyDown(KeyCode.E);
        public static bool CrouchPressed() => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
        public static bool RestartPressed() => Input.GetKeyDown(KeyCode.R);
#endif
    }
}
