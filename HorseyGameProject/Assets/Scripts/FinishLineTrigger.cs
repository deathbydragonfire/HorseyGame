using UnityEngine;
using MalbersAnimations.Controller;

namespace HorseyGame
{
    /// <summary>
    /// Place on the Finish/Start GameObject (with trigger collider). Reports crossings to RaceManager.
    /// Detects racers by RacerId on the collider's hierarchy, or by MAnimal + tag/layer.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FinishLineTrigger : MonoBehaviour
    {
        [Tooltip("RaceManager in scene. If null, uses RaceManager.Instance.")]
        public RaceManager raceManager;
        [Tooltip("Only count crossing when coming from this direction (e.g. forward along track). Leave empty to always count.")]
        public bool useDirectionCheck;
        [Tooltip("Forward direction of the finish line (world space). Used to avoid double-count when useDirectionCheck is true.")]
        public Vector3 finishForward = Vector3.forward;

        private void Awake()
        {
            Collider c = GetComponent<Collider>();
            if (c != null && !c.isTrigger)
                c.isTrigger = true;
        }

        private void Start()
        {
            if (raceManager == null)
                raceManager = RaceManager.Instance != null ? RaceManager.Instance : FindObjectOfType<RaceManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (raceManager == null) raceManager = RaceManager.Instance;
            if (raceManager == null || raceManager.RaceFinished) return;

            RacerId racerId = other.GetComponentInParent<RacerId>();
            if (racerId != null)
            {
                raceManager.OnRacerCrossedFinish(racerId.Id);
                return;
            }

            MAnimal animal = other.GetComponentInParent<MAnimal>();
            if (animal != null)
            {
                GameObject root = animal.transform.root.gameObject;
                racerId = root.GetComponent<RacerId>();
                if (racerId != null)
                {
                    raceManager.OnRacerCrossedFinish(racerId.Id);
                    return;
                }
                if (root.CompareTag("Player"))
                {
                    raceManager.OnRacerCrossedFinish(0);
                    return;
                }
                if (root.name.Contains("Opponent"))
                {
                    raceManager.OnRacerCrossedFinish(1);
                    return;
                }
            }
        }
    }
}
