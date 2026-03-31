using UnityEngine;
using System;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicSource;   // Main BGM
    public AudioSource sfxSource;     // SFX
    public AudioSource layerSource;   // 🎵 NEW: Layered music

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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 🎵 MAIN MUSIC
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

    // 🔊 SFX
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

    // 🎵 NEW: PLAY LAYER MUSIC
    public void PlayMusicLayer(string name)
    {
        Sound s = Array.Find(musicSounds, x => x.name == name);
        if (s == null)
        {
            Debug.LogWarning("Layer Music not found: " + name);
            return;
        }

        layerSource.clip = s.clip;
        layerSource.Play();
    }

    // 🎵 STOP LAYER ONLY
    public void StopMusicLayer()
    {
        if (layerSource.isPlaying)
            layerSource.Stop();
    }

    // 🎵 OPTIONAL: STOP ALL MUSIC
    public void StopAllMusic()
    {
        musicSource.Stop();
        layerSource.Stop();
    }

    public void ToggleMusic()
    {
        musicSource.mute = !musicSource.mute;
        layerSource.mute = !layerSource.mute;
    }

    public void ToggleSFX()
    {
        sfxSource.mute = !sfxSource.mute;
    }
}