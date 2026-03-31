using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class Intro : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public VideoClip[] logoVideos;
    public string nextSceneName = "MainMenu";

    private int currentIndex = 0;

    void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.loopPointReached += OnVideoFinished;
        PlayCurrentVideo();
    }

    void Update()
    {

        if (Input.anyKeyDown)
        {
            SkipCurrentVideo();
        }
    }

    void SkipCurrentVideo()
    {
        currentIndex++;

        if (currentIndex < logoVideos.Length)
        {
            PlayCurrentVideo();
        }
        else
        {
            LoadNextScene();
        }
    }

    void PlayCurrentVideo()
    {
        videoPlayer.Stop();
        videoPlayer.clip = logoVideos[currentIndex];
        videoPlayer.Play();
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        currentIndex++;

        if (currentIndex < logoVideos.Length)
        {
            PlayCurrentVideo();
        }
        else
        {
            LoadNextScene();
        }
    }

    void LoadNextScene()
    {
        videoPlayer.loopPointReached -= OnVideoFinished;
        SceneManager.LoadScene(nextSceneName);
    }
}
