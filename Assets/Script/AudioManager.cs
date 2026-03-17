using UnityEngine;
using System;

public class AudioManager : MonoBehaviour
{
    // The Singleton instance
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("AudioSource dedicated to background music.")]
    public AudioSource musicSource;
    [Tooltip("AudioSource dedicated to sound effects.")]
    public AudioSource sfxSource;

    // A custom class to easily map string names to AudioClips in the Inspector
    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
    }

    [Header("Audio Library")]
    public Sound[] musicSounds;
    public Sound[] sfxSounds;

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps the audio manager alive between scenes
        }
        else
        {
            // Destroy any duplicates that might be created when reloading a scene
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Plays a sound effect once. Overlaps with currently playing SFX.
    /// </summary>
    public void PlaySFX(string soundName)
    {
        Sound s = Array.Find(sfxSounds, x => x.name == soundName);
        if (s == null)
        {
            Debug.LogWarning("SFX: " + soundName + " not found!");
            return;
        }
        sfxSource.PlayOneShot(s.clip);
    }

    /// <summary>
    /// Plays background music. Replaces any currently playing music.
    /// </summary>
    public void PlayMusic(string trackName)
    {
        Sound s = Array.Find(musicSounds, x => x.name == trackName);
        if (s == null)
        {
            Debug.LogWarning("Music: " + trackName + " not found!");
            return;
        }

        musicSource.clip = s.clip;
        musicSource.Play();
    }

    /// <summary>
    /// Toggles the background music on or off.
    /// </summary>
    public void ToggleMusic()
    {
        musicSource.mute = !musicSource.mute;
    }

    /// <summary>
    /// Toggles all sound effects on or off.
    /// </summary>
    public void ToggleSFX()
    {
        sfxSource.mute = !sfxSource.mute;
    }
}