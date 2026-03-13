using System.Collections;
using TMPro;
using UnityEngine;

namespace HorseyGame
{
    public class RaceUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI countdownText;
        public TextMeshProUGUI lapText;
        public TextMeshProUGUI finishText;
        public TextMeshProUGUI positionText;

        private const string LapFormat = "Lap {0} / {1}";

        private void Start()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnRacerLap.AddListener(OnLapCompleted);
                RaceManager.Instance.OnRaceFinished.AddListener(OnRaceFinished);
            }

            if (finishText != null) finishText.gameObject.SetActive(false);
            if (positionText != null) positionText.gameObject.SetActive(false);
            if (countdownText != null) countdownText.text = string.Empty;

            UpdateLapText(0);
        }

        /// <summary>Called by RaceManager to begin the countdown sequence.</summary>
        public void StartCountdown()
        {
            StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            if (countdownText != null) countdownText.text = string.Empty;

            yield return new WaitForSeconds(2f);

            string[] steps = { "5", "4", "3", "2", "1" };
            foreach (string step in steps)
            {
                if (countdownText != null) countdownText.text = step;
                yield return new WaitForSeconds(1f);
            }

            if (countdownText != null) countdownText.text = "GO!";

            if (RaceManager.Instance != null)
                RaceManager.Instance.StartRace();

            yield return new WaitForSeconds(1f);

            if (countdownText != null) countdownText.text = string.Empty;
        }

        private void OnLapCompleted(int racerId)
        {
            if (racerId != 0) return;
            int currentLap = RaceManager.Instance != null ? RaceManager.Instance.PlayerLaps : 0;
            UpdateLapText(currentLap);
        }

        private void OnRaceFinished(int winnerRacerId)
        {
            if (finishText != null)
            {
                finishText.gameObject.SetActive(true);
                finishText.text = "Finish!";
            }

            if (positionText != null && RaceManager.Instance != null)
            {
                int position = RaceManager.Instance.GetRacerPosition(0);
                positionText.gameObject.SetActive(true);
                positionText.text = GetOrdinal(position);
            }
        }

        private void UpdateLapText(int currentLap)
        {
            if (lapText == null) return;
            int lapsToWin = RaceManager.Instance != null ? RaceManager.Instance.LapsToWin : 3;
            lapText.text = string.Format(LapFormat, currentLap, lapsToWin);
        }

        private string GetOrdinal(int position)
        {
            switch (position)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                case 4: return "4th";
                case 5: return "5th";
                default: return position + "th";
            }
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnRacerLap.RemoveListener(OnLapCompleted);
                RaceManager.Instance.OnRaceFinished.RemoveListener(OnRaceFinished);
            }
        }
    }
}
