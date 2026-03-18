using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

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

    Gamepad p1Pad;
    Gamepad p2Pad;
    Gamepad p3Pad;
    Gamepad p4Pad;

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
        var gamepads = Gamepad.all;

        if (playerCount < 3 && player3Input != null)
        {
            Destroy(player3Input.gameObject);
            player3Input = null;
            p3Pad = null;
        }

        if (playerCount < 4 && player4Input != null)
        {
            Destroy(player4Input.gameObject);
            player4Input = null;
            p4Pad = null;
        }

        if (p1Pad != null && !ContainsGamepad(gamepads, p1Pad)) p1Pad = null;
        if (p2Pad != null && !ContainsGamepad(gamepads, p2Pad)) p2Pad = null;
        if (p3Pad != null && !ContainsGamepad(gamepads, p3Pad)) p3Pad = null;
        if (p4Pad != null && !ContainsGamepad(gamepads, p4Pad)) p4Pad = null;

        foreach (var pad in gamepads)
        {
            if (pad == p1Pad || pad == p2Pad || pad == p3Pad || pad == p4Pad)
                continue;

            if (p1Pad == null && player1Input != null)
            {
                SetGamepad(player1Input, pad);
                p1Pad = pad;
            }
            else if (p2Pad == null && player2Input != null)
            {
                SetGamepad(player2Input, pad);
                p2Pad = pad;
            }
            else if (p3Pad == null && player3Input != null)
            {
                SetGamepad(player3Input, pad);
                p3Pad = pad;
            }
            else if (p4Pad == null && player4Input != null)
            {
                SetGamepad(player4Input, pad);
                p4Pad = pad;
            }
        }
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad pad) return;

        if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
        {
            if (pad == p1Pad) p1Pad = null;
            if (pad == p2Pad) p2Pad = null;
            if (pad == p3Pad) p3Pad = null;
            if (pad == p4Pad) p4Pad = null;

            AssignControllers();
        }

        if (change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added)
        {
            RestoreController(pad);
        }
    }

    void RestoreController(Gamepad pad)
    {
        if (pad == p1Pad && player1Input != null)
        {
            SetGamepad(player1Input, pad);
            return;
        }

        if (pad == p2Pad && player2Input != null)
        {
            SetGamepad(player2Input, pad);
            return;
        }

        if (pad == p3Pad && player3Input != null)
        {
            SetGamepad(player3Input, pad);
            return;
        }

        if (pad == p4Pad && player4Input != null)
        {
            SetGamepad(player4Input, pad);
            return;
        }

        AssignControllers();
    }

    void SetGamepad(PlayerInput input, Gamepad pad)
    {
        input.user.UnpairDevices();
        input.SwitchCurrentControlScheme("Gamepad", pad);
    }

    bool ContainsGamepad(ReadOnlyArray<Gamepad> list, Gamepad target)
    {
        foreach (var pad in list)
        {
            if (pad == target)
                return true;
        }
        return false;
    }

    void Update()
    {
        bool p1HasController = player1Input != null && player1Input.devices.Count > 0;
        bool p2HasController = player2Input != null && player2Input.devices.Count > 0;

        if (!p1HasController)
            HandlePlayer1();

        if (!p2HasController)
            HandlePlayer2();
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
            tackle1.TryTackle();
    }

    void HandlePlayer2()
    {
        if (player2 == null || tackle2 == null) return;

        Vector2 move = new Vector2(
            (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
            (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0)
        );

        bool jump = Input.GetKey(KeyCode.Keypad0);

        player2.SetInput(move, jump);

        if (Input.GetKeyDown(KeyCode.KeypadPeriod))
            tackle2.TryTackle();
    }
}