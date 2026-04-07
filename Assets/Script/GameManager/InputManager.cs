using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using TMPro;

public class InputManager : MonoBehaviour
{
    [Header("Player Panels")]
    public GameObject p1Object;
    public GameObject p2Object;
    public GameObject p3Object;
    public GameObject p4Object;
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

    // =========================
    // READY SYSTEM
    // =========================
    bool p1Ready = false;
    bool p2Ready = false;
    bool p3Ready = false;
    bool p4Ready = false;

    bool gameStarted = false;

    [Header("UI")]
    public GameObject readyPanel;

    [Header("Ready UI")]
    public Image p1Panel;
    public Image p2Panel;
    public Image p3Panel;
    public Image p4Panel;

    public TextMeshProUGUI p1Text;
    public TextMeshProUGUI p2Text;
    public TextMeshProUGUI p3Text;
    public TextMeshProUGUI p4Text;

    void SetupPlayerPanels()
    {
        int playerCount = PlayerPrefs.GetInt("PlayerCount", 2);

        if (p1Object) p1Object.SetActive(playerCount >= 1);
        if (p2Object) p2Object.SetActive(playerCount >= 2);
        if (p3Object) p3Object.SetActive(playerCount >= 3);
        if (p4Object) p4Object.SetActive(playerCount >= 4);
    }
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
        GameState.ResetPregame();

        AssignControllers();
        SetupPlayerPanels();

        // LOCK movement at start
        if (player1) player1.enabled = false;
        if (player2) player2.enabled = false;

        if (player3Input)
        {
            var m = player3Input.GetComponent<PlayerMovement>();
            if (m) m.enabled = false;
        }

        if (player4Input)
        {
            var m = player4Input.GetComponent<PlayerMovement>();
            if (m) m.enabled = false;
        }
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
        // =========================
        // READY PHASE
        // =========================
        if (!gameStarted)
        {
            HandleReadyInputs();
            return;
        }

        // =========================
        // NORMAL GAME INPUT
        // =========================
        bool p1HasController = player1Input != null && player1Input.devices.Count > 0;
        bool p2HasController = player2Input != null && player2Input.devices.Count > 0;

        if (!p1HasController)
            HandlePlayer1();

        if (!p2HasController)
            HandlePlayer2();
    }

    void HandleReadyInputs()
    {
        // PLAYER 1
        if (!p1Ready)
        {
            if ((p1Pad != null && p1Pad.startButton.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Space))
            {
                p1Ready = true;
                SetReadyVisual(1);
                Debug.Log("P1 READY");
            }
        }

        // PLAYER 2
        if (!p2Ready)
        {
            if ((p2Pad != null && p2Pad.startButton.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                p2Ready = true;
                SetReadyVisual(2);
                Debug.Log("P2 READY");
            }
        }

        // PLAYER 3
        if (player3Input != null && !p3Ready)
        {
            if (p3Pad != null && p3Pad.startButton.wasPressedThisFrame)
            {
                p3Ready = true;
                SetReadyVisual(3);
                Debug.Log("P3 READY");
            }
        }

        // PLAYER 4
        if (player4Input != null && !p4Ready)
        {
            if (p4Pad != null && p4Pad.startButton.wasPressedThisFrame)
            {
                p4Ready = true;
                SetReadyVisual(4);
                Debug.Log("P4 READY");
            }
        }

        CheckAllReady();
    }

    void SetReadyVisual(int playerIndex)
    {
        switch (playerIndex)
        {
            case 1:
                if (p1Panel) p1Panel.color = Color.green;
                if (p1Text) p1Text.text = "READY ✔";
                break;

            case 2:
                if (p2Panel) p2Panel.color = Color.green;
                if (p2Text) p2Text.text = "READY ✔";
                break;

            case 3:
                if (p3Panel) p3Panel.color = Color.green;
                if (p3Text) p3Text.text = "READY ✔";
                break;

            case 4:
                if (p4Panel) p4Panel.color = Color.green;
                if (p4Text) p4Text.text = "READY ✔";
                break;
        }
    }

    void CheckAllReady()
    {
        int playerCount = PlayerPrefs.GetInt("PlayerCount", 2);

        bool allReady =
            (playerCount < 1 || p1Ready) &&
            (playerCount < 2 || p2Ready) &&
            (playerCount < 3 || p3Ready) &&
            (playerCount < 4 || p4Ready);

        if (allReady)
        {
            StartGame();
        }
    }

    void StartGame()
    {
        gameStarted = true;
        GameState.ArePlayersReady = true;

        Debug.Log("GAME START");

        if (readyPanel != null)
            readyPanel.SetActive(false);

        if (player1) player1.enabled = true;
        if (player2) player2.enabled = true;

        if (player3Input)
        {
            var m = player3Input.GetComponent<PlayerMovement>();
            if (m) m.enabled = true;
        }

        if (player4Input)
        {
            var m = player4Input.GetComponent<PlayerMovement>();
            if (m) m.enabled = true;
        }
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
