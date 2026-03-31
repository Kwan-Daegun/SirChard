using UnityEngine;
using System;
using System.Collections;

public class CutsceneAudioManager : MonoBehaviour
{
    public static CutsceneAudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicSource;     // Intro music
    public AudioSource sfxSource;       // SFX
    public AudioSource layerSource;     // Gameplay layer

    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
    }

    [Header("Audio Library")]
    public Sound[] musicSounds;
    public Sound[] sfxSounds;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 🎵 Play intro music
    public void PlayMusic(string name)
    {
        Sound s = Array.Find(musicSounds, x => x.name == name);
        if (s == null) return;

        musicSource.clip = s.clip;
        musicSource.Play();
    }

    // 🎵 Start gameplay layer ONLY after intro finishes
    public void PlayLayerAfterIntro(string layerName)
    {
        StartCoroutine(PlayLayerAfterIntroRoutine(layerName));
    }

    IEnumerator PlayLayerAfterIntroRoutine(string layerName)
    {
        // Wait until intro music finishes
        while (musicSource.isPlaying)
            yield return null;

        Sound s = Array.Find(musicSounds, x => x.name == layerName);
        if (s == null) yield break;

        layerSource.clip = s.clip;
        layerSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying)
            musicSource.Stop();
    }

    public void StopLayer()
    {
        if (layerSource.isPlaying)
            layerSource.Stop();
    }

    // 🔊 Play SFX
    public void PlaySFX(string name)
    {
        Sound s = Array.Find(sfxSounds, x => x.name == name);
        if (s == null) return;

        sfxSource.PlayOneShot(s.clip);
    }
}