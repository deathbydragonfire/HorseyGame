using UnityEngine;
using MalbersAnimations.Controller;
using MalbersAnimations;
using MalbersAnimations.PathCreation;

namespace HorseyGame
{
    /// <summary>
    /// Waypoint-based AI that drives an MAnimal along a race course.
    /// Mario Kart-style: curvature-based speed (slow in turns, sprint on straights), optional rubber-banding.
    /// Uses sequential waypoints to avoid wrong-section snapping on closed tracks.
    /// Can be on the Opponent or on RaceManager; if on RaceManager, assign Racer to the Opponent.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class HorseRacerAI : MonoBehaviour
    {
        [Header("Racer (required)")]
        [Tooltip("The Opponent GameObject (the horse to drive). Assign this if HorseRacerAI is on RaceManager. If null, uses this GameObject or finds 'Opponent' by name.")]
        public GameObject racer;

        [Header("Path")]
        [Tooltip("Spline path (Raceline). Use PathLink_Spline on Raceline, or assign Spline Container for direct use.")]
        public PathLink_Spline pathLink;
        [Tooltip("If Path Link is not set, use this Spline Container directly.")]
        public UnityEngine.Splines.SplineContainer splineContainer;

        [Header("Waypoints")]
        [Tooltip("Number of waypoints to sample from the spline.")]
        public int waypointCount = 300;
        [Tooltip("Distance to current waypoint before advancing to next.")]
        public float waypointReachDist = 3f;
        [Tooltip("How many waypoints ahead to steer toward.")]
        public int lookAheadWaypoints = 5;

        [Header("Jump zones")]
        [Tooltip("Transforms marking jump/ramp areas (e.g. Frog Jump). AI will trigger jump when approaching.")]
        public Transform[] jumpZones;
        [Tooltip("Distance to jump zone before triggering jump.")]
        public float jumpTriggerDistance = 12f;

        [Header("Speed (Mario Kart)")]
        [Tooltip("Use curvature to slow in turns and sprint on straights.")]
        public bool useCurvatureSpeed = true;
        [Tooltip("Angle change (degrees) above which we slow down and stop sprinting.")]
        public float sharpTurnAngle = 45f;
        [Tooltip("Angle change (degrees) below which we sprint at full speed.")]
        public float straightAngle = 20f;

        [Header("Rubber-banding")]
        [Tooltip("Scale AI speed when far behind/ahead of player (optional).")]
        public bool useRubberBanding = true;
        [Tooltip("Max speed multiplier when AI is behind.")]
        [Range(1f, 1.5f)]
        public float rubberBandBoost = 1.2f;
        [Tooltip("Min speed multiplier when AI is ahead.")]
        [Range(0.6f, 1f)]
        public float rubberBandSlow = 0.85f;

        [Header("Input disabling")]
        [Tooltip("Disable MalbersInput on this GameObject when AI is active (recommended).")]
        public bool disableInputWhenActive = true;

        private const int JumpStateId = 2;

        private MAnimal animal;
        private Vector3[] waypoints;
        private int currentWaypointIndex;
        private bool hasPath;
        private bool splineClosed;
        private MalbersAnimations.MInput[] inputsToDisable;
        private Transform racerRoot;
        private float lastJumpTime;

        public float CurrentProgress => waypoints != null && waypoints.Length > 0
            ? currentWaypointIndex / (float)waypoints.Length
            : 0f;
        public bool HasPath => hasPath;

        private void Awake()
        {
            ResolveRacerRoot();
            if (racerRoot == null) return;

            var mount = racerRoot.GetComponentInChildren<MalbersAnimations.HAP.Mount>(true);
            if (mount != null && mount.Animal != null)
                animal = mount.Animal;
            else
            {
                animal = racerRoot.GetComponentInChildren<MAnimal>(true);
                if (animal == null)
                    animal = racerRoot.GetComponent<MAnimal>();
            }

            if (pathLink != null && pathLink.spline != null)
            {
                hasPath = true;
                splineClosed = pathLink.IsClosed;
            }
            else if (splineContainer != null && splineContainer.Spline != null)
            {
                hasPath = true;
                splineClosed = splineContainer.Spline.Closed;
            }
            else
            {
                hasPath = false;
                splineClosed = false;
            }

            if (animal != null)
                animal.UseCameraInput = false;

            if (disableInputWhenActive)
            {
                inputsToDisable = racerRoot.GetComponentsInChildren<MalbersAnimations.MInput>(true);
                for (int i = 0; i < inputsToDisable.Length; i++)
                    inputsToDisable[i].enabled = false;
            }
            DisableAnimalInputSource();
        }

        private void ResolveRacerRoot()
        {
            if (racer != null)
            {
                racerRoot = racer.transform;
                return;
            }
            var byName = GameObject.Find("Opponent");
            if (byName != null)
            {
                racer = byName;
                racerRoot = byName.transform;
                return;
            }
            racerRoot = transform;
        }

        private void Start()
        {
            if (!hasPath || animal == null) return;

            SampleWaypoints();
            if (waypoints == null || waypoints.Length == 0) return;

            Vector3 pos = animal.transform.position;
            Vector3 posFlat = new Vector3(pos.x, 0f, pos.z);
            float bestDist = float.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector3 wpFlat = new Vector3(waypoints[i].x, 0f, waypoints[i].z);
                float d = (wpFlat - posFlat).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }
            currentWaypointIndex = bestIdx;
        }

        private void SampleWaypoints()
        {
            waypoints = new Vector3[waypointCount];

            for (int i = 0; i < waypointCount; i++)
            {
                float t = i / (float)(waypointCount - 1);
                if (i == waypointCount - 1 && splineClosed)
                    t = 1f;

                if (pathLink != null)
                    waypoints[i] = pathLink.GetPointAtTime(t);
                else if (splineContainer != null)
                    waypoints[i] = splineContainer.EvaluatePosition(t);
                else
                {
                    waypoints = null;
                    return;
                }
            }
        }

        private void OnEnable()
        {
            DisableAllInputs();
        }

        private void OnDisable()
        {
            if (animal != null)
            {
                animal.StopMoving();
                animal.SetSprint(false);
                EnableAnimalInputSource();
            }
            if (disableInputWhenActive && inputsToDisable != null)
            {
                for (int i = 0; i < inputsToDisable.Length; i++)
                    if (inputsToDisable[i] != null)
                        inputsToDisable[i].enabled = true;
            }
        }

        private void DisableAllInputs()
        {
            if (!disableInputWhenActive || racerRoot == null) return;
            if (inputsToDisable != null)
            {
                for (int i = 0; i < inputsToDisable.Length; i++)
                    if (inputsToDisable[i] != null)
                        inputsToDisable[i].enabled = false;
            }
            else
            {
                var list = racerRoot.GetComponentsInChildren<MalbersAnimations.MInput>(true);
                for (int i = 0; i < list.Length; i++)
                    list[i].enabled = false;
            }
        }

        private void DisableAnimalInputSource()
        {
            if (animal == null) return;
            if (animal.InputSource != null)
                animal.InputSource.Enable(false);
            var mount = animal.GetComponentInParent<MalbersAnimations.HAP.Mount>();
            if (mount != null)
                mount.EnableInput(false);
        }

        private void EnableAnimalInputSource()
        {
            if (animal == null) return;
            if (animal.InputSource != null)
                animal.InputSource.Enable(true);
            var mount = animal.GetComponentInParent<MalbersAnimations.HAP.Mount>();
            if (mount != null)
                mount.EnableInput(true);
        }

        private void Update()
        {
            if (!hasPath || animal == null || !enabled) return;
            if (waypoints == null || waypoints.Length == 0)
            {
                SampleWaypoints();
                if (waypoints == null || waypoints.Length == 0) return;
                Vector3 startPos = animal.transform.position;
                Vector3 startFlat = new Vector3(startPos.x, 0f, startPos.z);
                float bestDist = float.MaxValue;
                int bestIdx = 0;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    Vector3 wpFlat = new Vector3(waypoints[i].x, 0f, waypoints[i].z);
                    float d = (wpFlat - startFlat).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }
                currentWaypointIndex = bestIdx;
            }

            Vector3 pos = animal.transform.position;
            Vector3 posFlat = new Vector3(pos.x, 0f, pos.z);

            {
                int safetyCounter = 0;
                while (safetyCounter < waypoints.Length)
                {
                    Vector3 wpFlat = new Vector3(waypoints[currentWaypointIndex].x, 0f, waypoints[currentWaypointIndex].z);
                    float distXZ = Vector3.Distance(posFlat, wpFlat);

                    bool reached = distXZ <= waypointReachDist;

                    if (!reached)
                    {
                        int nextWp = currentWaypointIndex + 1;
                        if (nextWp >= waypoints.Length)
                            nextWp = splineClosed ? 0 : currentWaypointIndex;

                        Vector3 nextFlat = new Vector3(waypoints[nextWp].x, 0f, waypoints[nextWp].z);
                        Vector3 splineForward = (nextFlat - wpFlat);
                        Vector3 toHorse = (posFlat - wpFlat);

                        if (splineForward.sqrMagnitude > 0.001f && toHorse.sqrMagnitude > 0.001f)
                        {
                            float dot = Vector3.Dot(splineForward.normalized, toHorse.normalized);
                            reached = dot > 0.3f;
                        }
                    }

                    if (!reached) break;

                    currentWaypointIndex++;
                    if (currentWaypointIndex >= waypoints.Length)
                        currentWaypointIndex = splineClosed ? 0 : waypoints.Length - 1;
                    safetyCounter++;
                }
            }

            int effectiveLookAhead = lookAheadWaypoints;
            if (useCurvatureSpeed)
            {
                float previewAngle = GetCurvatureAngleAtWaypoint(currentWaypointIndex);
                if (previewAngle > sharpTurnAngle)
                    effectiveLookAhead = Mathf.Max(1, lookAheadWaypoints / 3);
                else if (previewAngle > straightAngle)
                    effectiveLookAhead = Mathf.Max(2, lookAheadWaypoints / 2);
            }

            int lookIdx = currentWaypointIndex + effectiveLookAhead;
            if (lookIdx >= waypoints.Length)
                lookIdx = splineClosed ? lookIdx % waypoints.Length : waypoints.Length - 1;

            Vector3 targetPos = waypoints[lookIdx];
            Vector3 dir = new Vector3(targetPos.x - pos.x, 0f, targetPos.z - pos.z);
            if (dir.sqrMagnitude < 0.01f)
            {
                int nextIdx = currentWaypointIndex + 1;
                if (nextIdx >= waypoints.Length) nextIdx = splineClosed ? 0 : currentWaypointIndex;
                dir = new Vector3(
                    waypoints[nextIdx].x - waypoints[currentWaypointIndex].x,
                    0f,
                    waypoints[nextIdx].z - waypoints[currentWaypointIndex].z);
                if (dir.sqrMagnitude < 0.01f) dir = animal.transform.forward;
            }
            dir.Normalize();

            float moveMultiplier = 1f;
            bool sprint = true;
            if (useCurvatureSpeed)
            {
                float angle = GetCurvatureAngleAtWaypoint(currentWaypointIndex);
                if (angle > sharpTurnAngle)
                {
                    sprint = false;
                    moveMultiplier = 0.5f + 0.5f * (straightAngle / Mathf.Max(angle, 0.1f));
                }
                else if (angle < straightAngle)
                    sprint = true;
            }

            if (useRubberBanding && RaceManager.Instance != null)
            {
                float playerProg = RaceManager.Instance.GetPlayerProgress();
                float aiProg = RaceManager.Instance.GetOpponentProgress();
                float diff = playerProg - aiProg;
                if (diff > 0.05f) moveMultiplier *= Mathf.Min(rubberBandBoost, 1f + diff * 2f);
                else if (diff < -0.05f)
                {
                    moveMultiplier *= Mathf.Max(rubberBandSlow, 1f + diff * 2f);
                }
            }

            if (jumpZones != null && jumpZones.Length > 0 && Time.time - lastJumpTime > 1.5f)
            {
                for (int i = 0; i < jumpZones.Length; i++)
                {
                    if (jumpZones[i] == null) continue;
                    float d = Vector3.Distance(pos, jumpZones[i].position);
                    if (d <= jumpTriggerDistance)
                    {
                        if (animal.State_TryActivate(JumpStateId))
                            lastJumpTime = Time.time;
                        break;
                    }
                }
            }

            animal.SetSprint(sprint);
            animal.Move(dir * moveMultiplier);
        }

        private float GetCurvatureAngleAtWaypoint(int idx)
        {
            if (waypoints == null || waypoints.Length < 3) return 0f;

            int prev = idx - 1;
            int next = idx + 1;
            if (splineClosed)
            {
                prev = (prev + waypoints.Length) % waypoints.Length;
                next = next % waypoints.Length;
            }
            else
            {
                prev = Mathf.Max(0, prev);
                next = Mathf.Min(waypoints.Length - 1, next);
            }

            Vector3 p1 = waypoints[prev];
            Vector3 p2 = waypoints[idx];
            Vector3 p3 = waypoints[next];
            p1.y = p2.y = p3.y = 0f;
            Vector3 v1 = (p2 - p1).normalized;
            Vector3 v2 = (p3 - p2).normalized;
            return Vector3.Angle(v1, v2);
        }

        public void SetProgress(float normalizedT)
        {
            if (waypoints == null || waypoints.Length == 0) return;
            currentWaypointIndex = Mathf.Clamp(
                Mathf.RoundToInt(normalizedT * waypoints.Length),
                0, waypoints.Length - 1);
        }

        public void ResetToStart()
        {
            currentWaypointIndex = 0;
        }
    }
}
