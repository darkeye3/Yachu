using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("오디오 소스")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM")]
    [SerializeField] private AudioClip bgmGame;
    [SerializeField] private AudioClip bgmLobby;
    [SerializeField] private AudioClip bgmResult;

    [Header("SFX")]
    [SerializeField] private AudioClip sfxDiceRoll;
    [SerializeField] private AudioClip sfxDiceKeep;
    [SerializeField] private AudioClip sfxCupShakeLight;
    [SerializeField] private AudioClip sfxCupShakeMid;
    [SerializeField] private AudioClip sfxCupShakeHeavy;
    [SerializeField] private AudioClip sfxCupPour;
    [SerializeField] private AudioClip sfxScoreRegister;
    [SerializeField] private AudioClip sfxTimerWarning;
    [SerializeField] private AudioClip sfxButtonClick;
    [SerializeField] private AudioClip sfxEmotion;

    private Dictionary<string, AudioClip> sfxMap;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildSFXMap();
    }

    void BuildSFXMap()
    {
        sfxMap = new Dictionary<string, AudioClip>
        {
            { "dice_roll",         sfxDiceRoll        },
            { "dice_keep",         sfxDiceKeep        },
            { "cup_shake_light",   sfxCupShakeLight   },
            { "cup_shake_mid",     sfxCupShakeMid     },
            { "cup_shake_heavy",   sfxCupShakeHeavy   },
            { "cup_pour",          sfxCupPour         },
            { "score_register",    sfxScoreRegister   },
            { "timer_warning",     sfxTimerWarning    },
            { "button_click",      sfxButtonClick     },
            { "emotion",           sfxEmotion         },
        };
    }

    // ─── BGM ─────────────────────────────────────────────────────────
    public void PlayBGM(AudioClip clip, float volume = 0.6f)
    {
        if (clip == null || bgmSource == null) return;
        bgmSource.clip   = clip;
        bgmSource.volume = volume;
        bgmSource.loop   = true;
        bgmSource.Play();
    }

    public void PlayGameBGM()   => PlayBGM(bgmGame);
    public void PlayLobbyBGM()  => PlayBGM(bgmLobby);
    public void PlayResultBGM() => PlayBGM(bgmResult);
    public void StopBGM()       { if (bgmSource) bgmSource.Stop(); }

    // ─── SFX ─────────────────────────────────────────────────────────
    public static void Play(string key, float volume = 1f)
    {
        if (Instance == null) return;
        Instance.PlaySFX(key, volume);
    }

    public void PlaySFX(string key, float volume = 1f)
    {
        if (sfxSource == null) return;
        if (sfxMap.TryGetValue(key, out AudioClip clip) && clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    public void SetBGMVolume(float v) { if (bgmSource) bgmSource.volume = v; }
    public void SetSFXVolume(float v) { if (sfxSource) sfxSource.volume = v; }
}
