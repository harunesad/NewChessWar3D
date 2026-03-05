using UnityEngine;
using UnityEngine.InputSystem;

namespace BodylinkSDK
{
    public class BodylinkInput : MonoBehaviour
    {

        private static bool menuPressed;
        private static bool xButtonPressed;
        private static bool oButtonPressed;


        private static bool leftTriggerPressed, rightTriggerPressed;
        private static bool leftStickButtonPressed, rightStickButtonPressed;

        private static Vector2 leftStickValue, rightStickValue;

        void Update()
        {

            ResetFrameState();
            HandleGamepads();
            HandleKeyboardFallbacks();
        }

        private static void ResetFrameState()
        {
            menuPressed = false;
            xButtonPressed = false;
            oButtonPressed = false;

            leftTriggerPressed = false;
            rightTriggerPressed = false;
            leftStickButtonPressed = false;
            rightStickButtonPressed = false;

            leftStickValue = Vector2.zero;
            rightStickValue = Vector2.zero;
        }

        private void HandleGamepads()
        {
            if (Gamepad.all.Count == 0) return;

            ReadController(Gamepad.all[0], true);

            if (Gamepad.all.Count > 1)
            {
                ReadController(Gamepad.all[1], false);
            }
            else
            {
                ReadSingleGamepadRightInputs(Gamepad.all[0]);
            }
        }

        private void ReadController(Gamepad pad, bool isLeftController)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            if (!isLeftController)
            {
                Vector2 fallbackRightStick = pad.rightStick.ReadValue();
                if (fallbackRightStick.sqrMagnitude > stick.sqrMagnitude)
                {
                    stick = fallbackRightStick;
                }
            }

            if (stick.sqrMagnitude > 0.05f)
            {
                if (isLeftController)
                {
                    leftStickValue = stick;
                }
                else
                {
                    rightStickValue = stick;
                }
            }

            if (isLeftController)
            {
                leftStickButtonPressed |= pad.leftStickButton.wasPressedThisFrame;
                leftTriggerPressed |= pad.leftTrigger.wasPressedThisFrame;
            }
            else
            {
                // A split right-hand controller may map to left-side gamepad controls.
                rightStickButtonPressed |= pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame;
                rightTriggerPressed |= pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
            }

            if (pad.aButton.wasPressedThisFrame)
            {
                Debug.Log("A button pressed");
            }
            if (pad.bButton.wasPressedThisFrame)
            {
                Debug.Log("B button pressed");
            }
            if (pad.xButton.wasPressedThisFrame)
            {
                Debug.Log("X button pressed");
            }
            if (pad.yButton.wasPressedThisFrame)
            {
                Debug.Log("Y button pressed");
            }
            if (pad.leftStickButton.wasPressedThisFrame)
            {
                Debug.Log("Left Stick button pressed");
            }
            if (pad.rightStickButton.wasPressedThisFrame)
            {
                Debug.Log("Right Stick button pressed");
            }
            if (pad.startButton.wasPressedThisFrame)
            {
                Debug.Log("Start button pressed");
            }
            if (pad.selectButton.wasPressedThisFrame)
            {
                Debug.Log("Select button pressed");
            }
            if (pad.leftTrigger.wasPressedThisFrame)
            {
                Debug.Log("Left Trigger pressed");
            }
            if (pad.rightTrigger.wasPressedThisFrame)
            {
                Debug.Log("Right Trigger pressed");
            }
            if (pad.leftShoulder.wasPressedThisFrame)
            {
                Debug.Log("Left Shoulder pressed");
            }
            if (pad.rightShoulder.wasPressedThisFrame)
            {
                Debug.Log("Right Shoulder pressed");
            }
            if (pad.circleButton.wasPressedThisFrame)
            {
                Debug.Log("Circle button pressed");
            }
            if (pad.crossButton.wasPressedThisFrame)
            {
                Debug.Log("Cross button pressed");
            }



        }

        private static void ReadSingleGamepadRightInputs(Gamepad pad)
        {
            Vector2 rightStick = pad.rightStick.ReadValue();
            if (rightStick.sqrMagnitude > 0.05f)
            {
                rightStickValue = rightStick;
            }

            rightStickButtonPressed |= pad.rightStickButton.wasPressedThisFrame;
            rightTriggerPressed |= pad.rightTrigger.wasPressedThisFrame;
        }


        private void HandleKeyboardFallbacks()
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                oButtonPressed = true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                xButtonPressed = true;
            }

            if (Input.GetKeyDown(KeyCode.Menu))
            {
                menuPressed = true;
            }
        }

        public static bool GetKeyDown(Key bodylinkKeyCode)
        {
            switch (bodylinkKeyCode)
            {
                case Key.Menu:
                    return menuPressed;
                case Key.O:
                    return oButtonPressed;
                case Key.X:
                    return xButtonPressed;
                case Key.Trigger:
                    return leftTriggerPressed || rightTriggerPressed;
                case Key.Left_Trigger:
                    return leftTriggerPressed;
                case Key.Right_Trigger:
                    return rightTriggerPressed;
                case Key.Joystick_Button:
                    return leftStickButtonPressed || rightStickButtonPressed;
                case Key.Left_JoyStickButton:
                    return leftStickButtonPressed;
                case Key.Right_JoyStickButton:
                    return rightStickButtonPressed;
                default:
                    return false;
            }
        }

        public static Vector2 GetJoyStickValue(int index)
        {
            switch (index)
            {
                case 0:
                    return leftStickValue;
                case 1:
                    return rightStickValue;
                default:
                    return Vector2.zero;
            }
        }
    }

    public enum Key
    {
        Menu,
        X,
        O,
        Trigger,
        Joystick_Button,
        Left_Trigger,
        Left_JoyStickButton,
        Right_Trigger,
        Right_JoyStickButton
    }
}
