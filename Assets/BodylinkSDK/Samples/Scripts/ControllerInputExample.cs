using UnityEngine;
using UnityEngine.UI;
using BodylinkSDK;
using System.Text;
using UnityEngine.UIElements;

public class ControllerInputExample : MonoBehaviour
{
    public Text buttonPressText, joyStickRightValueText, joyStickLeftValueText;
    public ScrollRect scrollView;

    void Start()
    {
        buttonPressText.text = "No buttons pressed";
    }

    // Update is called once per frame
    void Update()
    {
        UpdateButtonStates();
        UpdateJoystickValueText();
    }

    private void UpdateButtonStates()
    {
        if (buttonPressText == null) return;

        bool MenuPressed = BodylinkInput.GetKeyDown(Key.Menu);
        //bool rightMenuPressed = BodylinkInput.GetKeyDown(Key.Right_Menu);

        bool OPressed = BodylinkInput.GetKeyDown(Key.O);
        //bool rightOPressed = BodylinkInput.GetKeyDown(Key.Right_O);

        bool XPressed = BodylinkInput.GetKeyDown(Key.X);
        //bool rightXPressed = BodylinkInput.GetKeyDown(Key.Right_X);

        bool leftTriggerPressed = BodylinkInput.GetKeyDown(Key.Left_Trigger);
        bool rightTriggerPressed = BodylinkInput.GetKeyDown(Key.Right_Trigger);

        bool leftStickButtonPressed = BodylinkInput.GetKeyDown(Key.Left_JoyStickButton);
        bool rightStickButtonPressed = BodylinkInput.GetKeyDown(Key.Right_JoyStickButton);

        bool anyPressed = MenuPressed ||
                          OPressed ||
                          XPressed ||
                          leftTriggerPressed || rightTriggerPressed ||
                          leftStickButtonPressed || rightStickButtonPressed;
        if (!anyPressed) return;


        var builder = new StringBuilder();
        AppendState(builder, "Menu button", MenuPressed, MenuPressed, true);
        AppendState(builder, "O button", OPressed, OPressed, true);
        AppendState(builder, "X button", XPressed, XPressed, true);
        AppendState(builder, "Trigger", leftTriggerPressed, rightTriggerPressed);
        AppendState(builder, "Joystick button", leftStickButtonPressed, rightStickButtonPressed);

        buttonPressText.text = buttonPressText.text + "\n" + builder.ToString().TrimEnd();
        // Force UI update
        Canvas.ForceUpdateCanvases();
        // Scroll to bottom
        scrollView.verticalNormalizedPosition = 0f;
    }

    private void AppendState(StringBuilder builder, string label, bool leftPressed, bool rightPressed, bool overRide = false)
    {
        if (!leftPressed && !rightPressed) return;

        string state = leftPressed && rightPressed
            ? "Both pressed"
            : leftPressed
                ? "Left pressed"
                : "Right pressed";

        if (overRide)
            state = "pressed";

        builder.AppendLine($"{label}: {state}");

    }

    private void UpdateJoystickValueText()
    {
        Vector2 leftStick = BodylinkInput.GetJoyStickValue(0);
        Vector2 rightStick = BodylinkInput.GetJoyStickValue(1);

        if (joyStickLeftValueText != null)
        {
            joyStickLeftValueText.text = $"Left Joystick: {leftStick.x:F2}, {leftStick.y:F2}";
        }

        if (joyStickRightValueText != null)
        {
            joyStickRightValueText.text = $"Right Joystick: {rightStick.x:F2}, {rightStick.y:F2}";
        }
    }
}
