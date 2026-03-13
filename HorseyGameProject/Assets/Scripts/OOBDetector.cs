using UnityEngine;

namespace HorseyGame
{
    [RequireComponent(typeof(SphereCollider))]
    public class OOBDetector : MonoBehaviour
    {
        private SphereCollider sphereCollider;
        private RacerId racerId;
        private bool wasInOOB;

        private readonly Collider[] overlapResults = new Collider[8];

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            racerId = GetComponentInParent<RacerId>();
        }

        private void FixedUpdate()
        {
            if (racerId == null || OutOfBoundsManager.Instance == null) return;

            Vector3 worldCenter = transform.TransformPoint(sphereCollider.center);
            float worldRadius = sphereCollider.radius * transform.lossyScale.x;

            int count = Physics.OverlapSphereNonAlloc(
                worldCenter, worldRadius, overlapResults,
                Physics.AllLayers, QueryTriggerInteraction.Collide);

            bool inOOB = false;
            for (int i = 0; i < count; i++)
            {
                if (overlapResults[i] == sphereCollider) continue;
                if (overlapResults[i].GetComponent<OutOfBoundsZone>() != null)
                {
                    inOOB = true;
                    break;
                }
            }

            if (inOOB && !wasInOOB)
                OutOfBoundsManager.Instance.OnRacerEnteredOOB(racerId.Id);
            else if (!inOOB && wasInOOB)
                OutOfBoundsManager.Instance.OnRacerExitedOOB(racerId.Id);

            wasInOOB = inOOB;
        }
    }
}
