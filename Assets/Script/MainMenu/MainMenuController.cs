using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    public void StartTwoPlayer()
    {
        PlayerPrefs.SetInt("PlayerCount", 2);
        SceneManager.LoadScene(gameSceneName);
    }

    public void StartThreePlayer()
    {
        PlayerPrefs.SetInt("PlayerCount", 3);
        SceneManager.LoadScene(gameSceneName);
    }

    public void StartFourPlayer()
    {
        PlayerPrefs.SetInt("PlayerCount", 4);
        SceneManager.LoadScene(gameSceneName);
    }
}