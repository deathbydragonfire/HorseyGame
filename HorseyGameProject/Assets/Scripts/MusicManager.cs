using UnityEngine;

namespace HorseyGame
{
    [RequireComponent(typeof(AudioSource))]
    public class MusicManager : MonoBehaviour
    {
        [Header("Music Clips")]
        public AudioClip raceMusic;

        [Header("Settings")]
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = true;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = loop;
            audioSource.volume = volume;
        }

        private void Start()
        {
            if (RaceManager.Instance != null)
                RaceManager.Instance.OnRaceFinished.AddListener(OnRaceFinished);
        }

        /// <summary>Called externally or by RaceManager to start playing race music.</summary>
        public void PlayRaceMusic()
        {
            if (raceMusic == null) return;

            audioSource.clip = raceMusic;
            audioSource.Play();
        }

        /// <summary>Stops music playback immediately.</summary>
        public void StopMusic()
        {
            audioSource.Stop();
        }

        private void OnRaceFinished(int winnerRacerId)
        {
            StopMusic();
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
                RaceManager.Instance.OnRaceFinished.RemoveListener(OnRaceFinished);
        }
    }
}
