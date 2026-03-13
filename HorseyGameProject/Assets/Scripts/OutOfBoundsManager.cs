using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MalbersAnimations.PathCreation;

namespace HorseyGame
{
    public class OutOfBoundsManager : MonoBehaviour
    {
        public static OutOfBoundsManager Instance { get; private set; }

        [Header("Respawn Settings")]
        public float oobDelay = 3f;
        public float splineRewindAmount = 0.05f;

        [Header("Player HUD")]
        public CanvasGroup oobWarningGroup;
        public TextMeshProUGUI oobCountdownText;

        [Header("Pulse")]
        public float pulseSpeed = 2f;

        private readonly Dictionary<int, float> oobTimers = new Dictionary<int, float>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Called by OutOfBoundsZone when a racer enters an OOB trigger.</summary>
        public void OnRacerEnteredOOB(int racerId)
        {
            oobTimers[racerId] = 0f;

            if (racerId == 0)
                ShowPlayerHUD(true);
        }

        /// <summary>Called by OutOfBoundsZone when a racer exits an OOB trigger.</summary>
        public void OnRacerExitedOOB(int racerId)
        {
            oobTimers.Remove(racerId);

            if (racerId == 0)
                ShowPlayerHUD(false);
        }

        private void Update()
        {
            if (oobTimers.Count == 0) return;

            List<int> toRespawn = null;

            foreach (int racerId in new List<int>(oobTimers.Keys))
            {
                oobTimers[racerId] += Time.deltaTime;
                float elapsed = oobTimers[racerId];

                if (racerId == 0)
                    UpdateCountdownText(elapsed);

                if (elapsed >= oobDelay)
                {
                    if (toRespawn == null)
                        toRespawn = new List<int>();
                    toRespawn.Add(racerId);
                }
            }

            if (toRespawn == null) return;

            foreach (int racerId in toRespawn)
            {
                oobTimers.Remove(racerId);
                RespawnRacer(racerId);
            }
        }

        private void UpdateCountdownText(float elapsed)
        {
            if (oobWarningGroup != null)
                oobWarningGroup.alpha = Mathf.PingPong(Time.time * pulseSpeed, 1f);

            if (oobCountdownText != null)
            {
                int remaining = Mathf.Max(1, Mathf.CeilToInt(oobDelay - elapsed));
                oobCountdownText.text = remaining.ToString();
            }
        }

        private void ShowPlayerHUD(bool show)
        {
            if (oobWarningGroup != null)
                oobWarningGroup.alpha = show ? 1f : 0f;

            if (oobCountdownText != null)
                oobCountdownText.text = show ? oobDelay.ToString("0") : string.Empty;
        }

        private void RespawnRacer(int racerId)
        {
            if (RaceManager.Instance == null)
            {
                Debug.LogWarning("OutOfBoundsManager: RaceManager.Instance is null — skipping respawn.");
                return;
            }

            PathLink_Spline path = RaceManager.Instance.pathForProgress;
            if (path == null)
            {
                Debug.LogWarning("OutOfBoundsManager: pathForProgress is null — skipping respawn.");
                return;
            }

            if (RaceManager.Instance.RaceFinished) return;

            RaceManager.RacerData racerData = null;
            foreach (RaceManager.RacerData data in RaceManager.Instance.GetAllRacers())
            {
                if (data.racerIndex == racerId)
                {
                    racerData = data;
                    break;
                }
            }

            if (racerData == null || racerData.root == null) return;

            Transform racerTransform = racerData.root.transform;

            float currentT = path.GetClosestTimeOnPath(racerTransform.position);
            float respawnT = Mathf.Clamp01(currentT - splineRewindAmount);

            Vector3 respawnPos = path.GetPointAtTime(respawnT) + Vector3.up * 3f;
            Quaternion rawRot = path.GetPathRotation(respawnT);
            Quaternion respawnRot = Quaternion.Euler(0f, rawRot.eulerAngles.y, 0f);

            racerTransform.SetPositionAndRotation(respawnPos, respawnRot);

            Rigidbody rb = racerData.root.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (racerData.ai != null)
                racerData.ai.SetProgress(respawnT);

            if (racerId == 0)
                ShowPlayerHUD(false);
        }
    }
}
