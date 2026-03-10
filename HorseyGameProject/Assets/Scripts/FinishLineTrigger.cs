using UnityEngine;
using MalbersAnimations.Controller;

namespace HorseyGame
{
    [RequireComponent(typeof(Collider))]
    public class FinishLineTrigger : MonoBehaviour
    {
        public RaceManager raceManager;
        public bool useDirectionCheck;
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
                }
            }
        }
    }
}
