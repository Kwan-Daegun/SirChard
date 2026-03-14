using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public PlayerMovement player1;
    public PlayerMovement player2;
    public PlayerTackle tackle1;
    public PlayerTackle tackle2;

    public PlayerInput player1Input;
    public PlayerInput player2Input;
    public PlayerInput player3Input;
    public PlayerInput player4Input;

    int p1Device = -1;
    int p2Device = -1;
    int p3Device = -1;
    int p4Device = -1;

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    void Start()
    {
        AssignControllers();
    }

    void AssignControllers()
    {
        int playerCount = PlayerPrefs.GetInt("PlayerCount", 2);
        int gamepadIndex = 0;

        var gamepads = Gamepad.all;
        int gamepadCount = gamepads.Count;

        if (player1Input != null && gamepadCount > gamepadIndex)
        {
            var pad = gamepads[gamepadIndex];
            player1Input.SwitchCurrentControlScheme("Gamepad", pad);
            p1Device = pad.deviceId;
            gamepadIndex++;
        }

        if (player2Input != null && gamepadCount > gamepadIndex)
        {
            var pad = gamepads[gamepadIndex];
            player2Input.SwitchCurrentControlScheme("Gamepad", pad);
            p2Device = pad.deviceId;
            gamepadIndex++;
        }

        if (playerCount >= 3 && player3Input != null)
        {
            if (gamepadCount > gamepadIndex)
            {
                var pad = gamepads[gamepadIndex];
                player3Input.SwitchCurrentControlScheme("Gamepad", pad);
                p3Device = pad.deviceId;
                gamepadIndex++;
            }
        }
        else if (player3Input != null)
        {
            Destroy(player3Input.gameObject);
        }

        if (playerCount >= 4 && player4Input != null)
        {
            if (gamepadCount > gamepadIndex)
            {
                var pad = gamepads[gamepadIndex];
                player4Input.SwitchCurrentControlScheme("Gamepad", pad);
                p4Device = pad.deviceId;
                gamepadIndex++;
            }
        }
        else if (player4Input != null)
        {
            Destroy(player4Input.gameObject);
        }
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad pad) return;

        if (change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added)
        {
            RestoreController(pad);
        }

        if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
        {
            AssignControllers();
        }
    }

    void RestoreController(Gamepad pad)
    {
        if (pad.deviceId == p1Device && player1Input != null)
            player1Input.SwitchCurrentControlScheme("Gamepad", pad);

        if (pad.deviceId == p2Device && player2Input != null)
            player2Input.SwitchCurrentControlScheme("Gamepad", pad);

        if (pad.deviceId == p3Device && player3Input != null)
            player3Input.SwitchCurrentControlScheme("Gamepad", pad);

        if (pad.deviceId == p4Device && player4Input != null)
            player4Input.SwitchCurrentControlScheme("Gamepad", pad);
    }

    void Update()
    {
        if (player1Input == null) HandlePlayer1();
        if (player2Input == null) HandlePlayer2();
    }

    void HandlePlayer1()
    {
        if (player1 == null || tackle1 == null) return;

        Vector2 move = new Vector2(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
        );

        bool jump = Input.GetKey(KeyCode.Space);

        player1.SetInput(move, jump);

        if (Input.GetKeyDown(KeyCode.F))
        {
            tackle1.TryTackle();
        }
    }

    void HandlePlayer2()
    {
        if (player2 == null || tackle2 == null) return;

        Vector2 move = new Vector2(
            (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
            (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0)
        );

        bool jump = Input.GetKey(KeyCode.RightControl);

        player2.SetInput(move, jump);

        if (Input.GetKeyDown(KeyCode.RightShift))
        {
            tackle2.TryTackle();
        }
    }
}