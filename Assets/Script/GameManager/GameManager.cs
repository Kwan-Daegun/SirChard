using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject pausePanel;
    public GameObject controlsPanel;
    public GameObject creditsPanel;
    public GameObject winPanel;
    public GameObject losePanel;

    public Transform player1;
    public Transform player2;
    public Transform player3;
    public Transform player4;

    public Transform spawn1;
    public Transform spawn2;
    public Transform spawn3;
    public Transform spawn4;

    private bool isPaused = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (player1 != null && spawn1 != null)
        {
            var rb = player1.GetComponent<Rigidbody>();
            if (rb != null) rb.position = spawn1.position;
            else player1.position = spawn1.position;
        }

        if (player2 != null && spawn2 != null)
        {
            var rb = player2.GetComponent<Rigidbody>();
            if (rb != null) rb.position = spawn2.position;
            else player2.position = spawn2.position;
        }

        if (player3 != null && spawn3 != null)
        {
            var rb = player3.GetComponent<Rigidbody>();
            if (rb != null) rb.position = spawn3.position;
            else player3.position = spawn3.position;
        }

        if (player4 != null && spawn4 != null)
        {
            var rb = player4.GetComponent<Rigidbody>();
            if (rb != null) rb.position = spawn4.position;
            else player4.position = spawn4.position;
        }

        Debug.Log("Spawning Player1 at " + spawn1.position);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        HideAllPanels();
        int playerCount = PlayerPrefs.GetInt("PlayerCount", 2);

        CameraControl cam = FindObjectOfType<CameraControl>();

        cam.SetTargets(player1, player2, player3, player4, playerCount);
        cam.SetStartPositionAndSize();
    }

    private void Update()
    {
        bool pausePressed = Input.GetKeyDown(KeyCode.Escape);

        foreach (var pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame)
                pausePressed = true;
        }

        if (pausePressed)
        {
            if (winPanel.activeSelf || losePanel.activeSelf) return;

            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        HideAllPanels();
        pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        HideAllPanels();
    }

    public void WinGame()
    {
        Time.timeScale = 0f;
        HideAllPanels();
        winPanel.SetActive(true);
    }

    public void LoseGame()
    {
        Time.timeScale = 0f;
        HideAllPanels();
        losePanel.SetActive(true);
    }

    public void ShowControls()
    {
        HideAllPanels();
        controlsPanel.SetActive(true);
    }

    public void ShowCredits()
    {
        HideAllPanels();
        creditsPanel.SetActive(true);
    }

    public void BackToPauseMenu()
    {
        HideAllPanels();
        pausePanel.SetActive(true);
    }

    private void HideAllPanels()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}