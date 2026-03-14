using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Singleton instance so other scripts can access it easily
    public static GameManager Instance;

    [Header("UI Panels")]
    public GameObject pausePanel;
    public GameObject controlsPanel;
    public GameObject creditsPanel;
    public GameObject winPanel;
    public GameObject losePanel;

    private bool isPaused = false;

    private void Awake()
    {
        // Set up the Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Ensure all panels are hidden and time is running when the scene starts
        Time.timeScale = 1f;
        HideAllPanels();
    }

    private void Update()
    {
        // Press Escape to toggle pause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Don't allow pausing if we already won or lost
            if (winPanel.activeSelf || losePanel.activeSelf) return;

            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // --- Core Game States ---

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freezes game time
        HideAllPanels();
        pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Resumes game time
        HideAllPanels();
    }

    public void WinGame()
    {
        Time.timeScale = 0f; // Stop the game in the background
        HideAllPanels();
        winPanel.SetActive(true);
    }

    public void LoseGame()
    {
        Time.timeScale = 0f; // Stop the game in the background
        HideAllPanels();
        losePanel.SetActive(true);
    }

    // --- UI Navigation ---

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
        // Useful for a "Back" button on the Controls or Credits panels
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

    // --- Scene Management ---

    public void RestartGame()
    {
        Time.timeScale = 1f; // Always reset time scale before loading a scene!
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}