using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterSoundFXManager : MonoBehaviour
{
    CharacterManager character;

    [Header("Base Audio Source")]
    [SerializeField] private AudioSource templateSource;

    [Header("Pool")]
    [SerializeField] private int initialPoolSize = 5;

    [Header("Clips")]
    public AudioClip equipSound;
    public AudioClip unequipSound;
    public AudioClip damageSound;
    public AudioClip blockSound;
    public AudioClip parrySound;
    public AudioClip poiseBreakSound;
    public AudioClip swingAttackSound;
    public AudioClip deathSound;

    public List<AudioSource> audioSourcePool = new();

    protected virtual void Awake()
    {
        character = GetComponent<CharacterManager>();

        if (templateSource == null)
            templateSource = GetComponent<AudioSource>();

        if (templateSource == null)
            templateSource = gameObject.AddComponent<AudioSource>();

        for (int i = 0; i < initialPoolSize; i++)
        {
            audioSourcePool.Add(CreatePooledSource());
        }
    }

    private AudioSource CreatePooledSource()
    {
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(templateSource, newSource);
        return newSource;
    }

    private void CopyAudioSourceSettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.mute = from.mute;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.playOnAwake = false;
        to.loop = false;
        to.priority = from.priority;
        to.volume = from.volume;
        to.pitch = 1f;
        to.panStereo = from.panStereo;
        to.spatialBlend = from.spatialBlend;
        to.reverbZoneMix = from.reverbZoneMix;
        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
    }

    private AudioSource GetAvailableSource()
    {
        for (int i = 0; i < audioSourcePool.Count; i++)
        {
            if (!audioSourcePool[i].isPlaying)
                return audioSourcePool[i];
        }

        AudioSource extraSource = CreatePooledSource();
        audioSourcePool.Add(extraSource);
        return extraSource;
    }

    public void PlaySoundEffect(AudioClip clip, float volume = 1f, float pitch = 1f, float randomPitchRange = 0.05f)
    {
        if (clip == null)
            return;

        AudioSource source = GetAvailableSource();

        float randomPitch = Random.Range(1f - randomPitchRange, 1f + randomPitchRange);
        source.pitch = pitch * randomPitch;
        
        GameSettings settings = SettingsManager.Instance.CurrentSettings;
        float volumeSettings = settings.masterVolume * settings.sfxVolume;
        
        source.PlayOneShot(clip, volume * volumeSettings);
    }
}