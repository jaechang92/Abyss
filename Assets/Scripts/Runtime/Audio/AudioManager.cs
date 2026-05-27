using UnityEngine;
using UnityEngine.Audio;
using Singleton_Core;

namespace Abyss.Runtime.Audio
{
    /// <summary>
    /// 오디오 관리자. AudioMixer 기반 Master/BGM/SFX 채널별 볼륨 제어.
    /// PlayerPrefs로 볼륨 영속화. BGM 단일 트랙 재생 + SFX OneShot 헬퍼 제공.
    /// </summary>
    public sealed class AudioManager : SingletonManager<AudioManager>
    {
        [Header("AudioMixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("BGM")]
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private bool isAutoPlayBgm = true;

        private AudioSource bgmSource;
        private AudioSource sfxSource;

        private float masterVolume;
        private float bgmVolume;
        private float sfxVolume;

        // Mixer 노출 파라미터 이름 (GameAudioMixer Exposed Parameter)
        private const string PARAM_MASTER = "Master";
        private const string PARAM_BGM = "BGM";
        private const string PARAM_SFX = "SFX";

        // PlayerPrefs 키
        private const string PREF_MASTER_VOLUME = "Audio.MasterVolume";
        private const string PREF_BGM_VOLUME = "Audio.BgmVolume";
        private const string PREF_SFX_VOLUME = "Audio.SfxVolume";

        // 기본 볼륨 (선형 0~1)
        private const float DEFAULT_MASTER_VOLUME = 0.7f;
        private const float DEFAULT_BGM_VOLUME = 0.7f;
        private const float DEFAULT_SFX_VOLUME = 0.8f;

        protected override void OnSingletonAwake()
        {
            LoadResourcesIfNeeded();
            SetupAudioSources();
            LoadVolumes();
        }

        private void Start()
        {
            if (isAutoPlayBgm)
            {
                PlayBgm();
            }
        }

        /// <summary>
        /// SerializeField가 null이면 Resources에서 폴백 로드.
        /// (코드로 매니저를 동적 생성하는 경로 대응)
        /// </summary>
        private void LoadResourcesIfNeeded()
        {
            if (audioMixer == null)
            {
                audioMixer = Resources.Load<AudioMixer>("Audio/GameAudioMixer");
            }

            if (audioMixer != null)
            {
                if (bgmGroup == null)
                {
                    var groups = audioMixer.FindMatchingGroups("BGM");
                    if (groups.Length > 0) bgmGroup = groups[0];
                }

                if (sfxGroup == null)
                {
                    var groups = audioMixer.FindMatchingGroups("SFX");
                    if (groups.Length > 0) sfxGroup = groups[0];
                }
            }
        }

        /// <summary>
        /// BGM/SFX AudioSource 초기화. BGM은 루프, SFX는 OneShot 채널.
        /// </summary>
        private void SetupAudioSources()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = 1f;
            if (bgmGroup != null) bgmSource.outputAudioMixerGroup = bgmGroup;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 1f;
            if (sfxGroup != null) sfxSource.outputAudioMixerGroup = sfxGroup;
        }

        /// <summary>
        /// PlayerPrefs에서 볼륨 로드 후 Mixer에 적용.
        /// </summary>
        private void LoadVolumes()
        {
            masterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, DEFAULT_MASTER_VOLUME);
            bgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOLUME, DEFAULT_BGM_VOLUME);
            sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_SFX_VOLUME);

            ApplyVolumeToMixer(PARAM_MASTER, masterVolume);
            ApplyVolumeToMixer(PARAM_BGM, bgmVolume);
            ApplyVolumeToMixer(PARAM_SFX, sfxVolume);
        }

        /// <summary>
        /// BGM 재생. clip 미지정 시 인스펙터의 bgmClip 사용.
        /// </summary>
        public void PlayBgm(AudioClip clip = null)
        {
            var targetClip = clip != null ? clip : bgmClip;
            if (targetClip == null) return;

            bgmSource.clip = targetClip;
            bgmSource.Play();
        }

        /// <summary>
        /// BGM 정지.
        /// </summary>
        public void StopBgm()
        {
            bgmSource.Stop();
        }

        /// <summary>
        /// SFX OneShot 재생. 동시 다발 호출 지원 (PlayOneShot은 채널 중첩 가능).
        /// </summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            ApplyVolumeToMixer(PARAM_MASTER, masterVolume);
            PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, masterVolume);
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            ApplyVolumeToMixer(PARAM_BGM, bgmVolume);
            PlayerPrefs.SetFloat(PREF_BGM_VOLUME, bgmVolume);
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            ApplyVolumeToMixer(PARAM_SFX, sfxVolume);
            PlayerPrefs.SetFloat(PREF_SFX_VOLUME, sfxVolume);
        }

        public float GetMasterVolume() => masterVolume;
        public float GetBgmVolume() => bgmVolume;
        public float GetSfxVolume() => sfxVolume;

        /// <summary>
        /// 선형(0~1) 볼륨을 데시벨(-80~0dB)로 변환 후 Mixer 노출 파라미터에 적용.
        /// </summary>
        private void ApplyVolumeToMixer(string parameter, float linear)
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(parameter, LinearToDecibel(linear));
        }

        /// <summary>
        /// 선형 볼륨(0~1) → 데시벨(-80~0). 0이면 -80dB (사실상 음소거).
        /// </summary>
        private float LinearToDecibel(float linear)
        {
            return linear <= 0f ? -80f : Mathf.Log10(linear) * 20f;
        }
    }
}
