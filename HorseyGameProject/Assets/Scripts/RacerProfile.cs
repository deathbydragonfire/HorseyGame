using UnityEngine;

namespace HorseyGame
{
    [CreateAssetMenu(fileName = "NewRacerProfile", menuName = "HorseyGame/Racer Profile")]
    public class RacerProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "AI Racer";

        [Header("Speed")]
        [Range(0.9f, 1.1f)]
        public float baseSpeedMultiplier = 1f;

        [Header("Personality")]
        [Range(0f, 1f)]
        public float aggressiveness = 0.5f;

        [Header("Lane Preference")]
        [Range(-1f, 1f)]
        public float preferredLane = 0f;
        public float laneWidth = 4f;
        [Range(0f, 1f)]
        public float laneWander = 0.1f;

        [Header("Rubber Banding")]
        [Range(1f, 1.5f)]
        public float rubberBandBoostMax = 1.2f;
        [Range(0.6f, 1f)]
        public float rubberBandSlowMin = 0.85f;

        [Header("Overtaking")]
        public float overtakeDistance = 15f;
        public float lateralOffsetStrength = 3f;

        [Header("Drafting")]
        public float draftingDistance = 12f;
        public float draftingSpeedBonus = 1.05f;

        [Header("Avoidance")]
        public float avoidanceRadius = 1.5f;
        public float avoidanceSteering = 2f;

        [Header("Waypoints")]
        public int lookAheadWaypoints = 5;
    }
}
