using UnityEngine;
using TMPro;

public class CommandConsole : MonoBehaviour
{
    public static bool IsOpen = false;

    [Header("References")]
    public GameModeController controller;
    public GameObject consolePanel;
    public TMP_InputField inputField;

    private void Start()
    {
        if (controller == null) controller = FindObjectOfType<GameModeController>();
        if (consolePanel != null) consolePanel.SetActive(false);
    }

    private void Update()
    {
        // Toggle with / or Enter
        if (Input.GetKeyDown(KeyCode.Slash) && !IsOpen)
        {
            OpenConsole();
            inputField.text = "/";
            inputField.MoveTextEnd(false);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!IsOpen) OpenConsole();
            else SubmitCommand();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && IsOpen)
        {
            CloseConsole();
        }
    }

    public void OpenConsole()
    {
        IsOpen = true;
        if (consolePanel != null) consolePanel.SetActive(true);
        if (inputField != null) inputField.ActivateInputField();

        // Lock Player Movement and Interaction
        if (controller != null && controller.movement != null)
        {
            controller.movement.InputLocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void CloseConsole()
    {
        IsOpen = false;
        if (consolePanel != null) consolePanel.SetActive(false);
        if (inputField != null) inputField.DeactivateInputField();

        // Unlock Player
        if (controller != null && controller.movement != null)
        {
            controller.movement.InputLocked = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SubmitCommand()
    {
        if (inputField != null && !string.IsNullOrEmpty(inputField.text))
        {
            CommandProcessor.Instance.Execute(inputField.text, controller);
        }
        if (inputField != null) inputField.text = "";
        CloseConsole();
    }
}