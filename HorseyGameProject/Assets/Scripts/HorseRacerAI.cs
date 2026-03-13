using System.Collections.Generic;
using UnityEngine;
using MalbersAnimations.Controller;
using MalbersAnimations;
using MalbersAnimations.PathCreation;

namespace HorseyGame
{
    [DefaultExecutionOrder(32000)]
    public class HorseRacerAI : MonoBehaviour
    {
        [Header("Racer")]
        public GameObject racer;
        public RacerProfile profile;
        public int racerIndex;

        [Header("Path")]
        public PathLink_Spline pathLink;
        public UnityEngine.Splines.SplineContainer splineContainer;

        [Header("Waypoints")]
        public int waypointCount = 300;
        public float waypointReachDist = 3f;
        public int lookAheadWaypoints = 5;

        [Header("Jump zones")]
        public Transform[] jumpZones;
        public float jumpTriggerDistance = 12f;

        [Header("Speed (Mario Kart)")]
        public bool useCurvatureSpeed = true;
        public float sharpTurnAngle = 45f;
        public float straightAngle = 20f;

        [Header("Rubber-banding")]
        public bool useRubberBanding = true;
        [Range(1f, 1.5f)]
        public float rubberBandBoost = 1.2f;
        [Range(0.6f, 1f)]
        public float rubberBandSlow = 0.85f;

        [Header("Input disabling")]
        public bool disableInputWhenActive = true;

        private const int JumpStateId = 2;
        private const float AvoidanceCastDistance = 10f;
        private const float OvertakeCheckInterval = 1f;
        private const float OvertakeTimeout = 3f;
        private const float LateralBlendSpeed = 3f;
        private const float DraftingDotThreshold = 0.8f;
        private const float OvertakeRayAngleDeg = 30f;
        private const float OvertakeRayDistance = 5f;

        private MAnimal animal;
        private Vector3[] waypoints;
        private Vector3[] rawSplinePoints;
        private int currentWaypointIndex;
        private bool hasPath;
        private bool splineClosed;
        private MInput[] inputsToDisable;
        private Transform racerRoot;
        private float lastJumpTime;

        private Vector3 currentAvoidanceOffset;
        private Vector3 currentOvertakeOffset;
        private float lastOvertakeCheckTime;
        private float overtakeStartTime;
        private bool isOvertaking;
        private int overtakeTargetIndex = -1;
        private int animalLayerMask;
        private float raceStartTime = -1f;

        private const float SprintRampUpDuration = 1.5f;

        public float CurrentProgress => waypoints != null && waypoints.Length > 0
            ? currentWaypointIndex / (float)waypoints.Length
            : 0f;
        public bool HasPath => hasPath;

        private void Awake()
        {
            if (racer != null)
                racerRoot = racer.transform;
            else
                racerRoot = transform;

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
                inputsToDisable = racerRoot.GetComponentsInChildren<MInput>(true);
                for (int i = 0; i < inputsToDisable.Length; i++)
                    inputsToDisable[i].enabled = false;
            }
            DisableAnimalInputSource();

            animalLayerMask = 1 << LayerMask.NameToLayer("Animal");

            ApplyProfile();
        }

        private void ApplyProfile()
        {
            if (profile == null) return;
            lookAheadWaypoints = profile.lookAheadWaypoints;
            rubberBandBoost = profile.rubberBandBoostMax;
            rubberBandSlow = profile.rubberBandSlowMin;
        }

        private void Start()
        {
            if (!hasPath || animal == null) return;

            SampleWaypoints();
            if (waypoints == null || waypoints.Length == 0) return;

            currentWaypointIndex = FindStartingWaypointIndex();
        }

        private int FindStartingWaypointIndex()
        {
            Vector3[] searchPoints = (rawSplinePoints != null && rawSplinePoints.Length > 0) ? rawSplinePoints : waypoints;
            if (searchPoints == null || searchPoints.Length == 0) return 0;

            Vector3 pos = animal.transform.position;
            Vector3 posFlat = new Vector3(pos.x, 0f, pos.z);
            Vector3 facingFlat = new Vector3(animal.transform.forward.x, 0f, animal.transform.forward.z);
            if (facingFlat.sqrMagnitude > 0.001f)
                facingFlat.Normalize();

            float bestScore = float.MaxValue;
            int bestIdx = 0;

            for (int i = 0; i < searchPoints.Length; i++)
            {
                Vector3 wpFlat = new Vector3(searchPoints[i].x, 0f, searchPoints[i].z);
                float distSq = (wpFlat - posFlat).sqrMagnitude;

                int nextI = (i + 1) < searchPoints.Length ? i + 1 : (splineClosed ? 0 : i);
                Vector3 nextFlat = new Vector3(searchPoints[nextI].x, 0f, searchPoints[nextI].z);
                Vector3 segDir = nextFlat - wpFlat;
                float alignment = segDir.sqrMagnitude > 0.001f
                    ? Vector3.Dot(segDir.normalized, facingFlat)
                    : 0f;

                if (alignment < 0f) continue;

                float score = distSq * (1f - Mathf.Clamp01(alignment) * 0.5f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }

            int advanced = bestIdx + lookAheadWaypoints;
            if (advanced >= waypoints.Length)
                advanced = splineClosed ? advanced % waypoints.Length : waypoints.Length - 1;

            return advanced;
        }

        private void SampleWaypoints()
        {
            Vector3[] rawPoints = new Vector3[waypointCount];

            for (int i = 0; i < waypointCount; i++)
            {
                float t = i / (float)(waypointCount - 1);
                if (i == waypointCount - 1 && splineClosed)
                    t = 1f;

                if (pathLink != null)
                    rawPoints[i] = pathLink.GetPointAtTime(t);
                else if (splineContainer != null)
                    rawPoints[i] = splineContainer.EvaluatePosition(t);
                else
                {
                    waypoints = null;
                    rawSplinePoints = null;
                    return;
                }
            }

            rawSplinePoints = rawPoints;

            waypoints = new Vector3[waypointCount];
            float laneOffset = profile != null ? profile.preferredLane * profile.laneWidth : 0f;
            float wander = profile != null ? profile.laneWander : 0f;

            for (int i = 0; i < waypointCount; i++)
            {
                if (Mathf.Approximately(laneOffset, 0f) && Mathf.Approximately(wander, 0f))
                {
                    waypoints[i] = rawPoints[i];
                    continue;
                }

                int prev = i - 1;
                int next = i + 1;
                if (splineClosed)
                {
                    prev = (prev + waypointCount) % waypointCount;
                    next = next % waypointCount;
                }
                else
                {
                    prev = Mathf.Max(0, prev);
                    next = Mathf.Min(waypointCount - 1, next);
                }

                Vector3 forward = rawPoints[next] - rawPoints[prev];
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f)
                {
                    waypoints[i] = rawPoints[i];
                    continue;
                }
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                float wanderNoise = 0f;
                if (wander > 0f)
                {
                    float noiseT = (float)i / waypointCount * 3f + racerIndex * 17.3f;
                    wanderNoise = (Mathf.PerlinNoise(noiseT, racerIndex * 7.1f) - 0.5f) * 2f * wander * (profile != null ? profile.laneWidth : 4f);
                }

                waypoints[i] = rawPoints[i] + right * (laneOffset + wanderNoise);
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
                var list = racerRoot.GetComponentsInChildren<MInput>(true);
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
            if (RaceManager.Instance == null || !RaceManager.Instance.RaceStarted) return;

            if (raceStartTime < 0f)
                raceStartTime = Time.time;

            if (waypoints == null || waypoints.Length == 0)
            {
                SampleWaypoints();
                if (waypoints == null || waypoints.Length == 0) return;
                currentWaypointIndex = FindStartingWaypointIndex();
            }

            Vector3 pos = animal.transform.position;
            Vector3 posFlat = new Vector3(pos.x, 0f, pos.z);

            AdvanceWaypoints(posFlat);

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
            Vector3 splineDir = new Vector3(targetPos.x - pos.x, 0f, targetPos.z - pos.z);
            if (splineDir.sqrMagnitude < 0.01f)
            {
                int nextIdx = currentWaypointIndex + 1;
                if (nextIdx >= waypoints.Length) nextIdx = splineClosed ? 0 : currentWaypointIndex;
                splineDir = new Vector3(
                    waypoints[nextIdx].x - waypoints[currentWaypointIndex].x,
                    0f,
                    waypoints[nextIdx].z - waypoints[currentWaypointIndex].z);
                if (splineDir.sqrMagnitude < 0.01f) splineDir = animal.transform.forward;
            }
            splineDir.Normalize();

            bool inRampUp = raceStartTime >= 0f && (Time.time - raceStartTime) < SprintRampUpDuration;
            if (inRampUp && isOvertaking)
            {
                isOvertaking = false;
                overtakeTargetIndex = -1;
            }

            Vector3 avoidanceOffset = inRampUp ? Vector3.zero : CalculateAvoidanceOffset(pos, splineDir);
            Vector3 overtakeOffset = inRampUp ? Vector3.zero : CalculateOvertakeOffset(pos, splineDir);
            float draftingMult = inRampUp ? 1f : CalculateDraftingBoost(pos, splineDir);

            currentAvoidanceOffset = inRampUp
                ? Vector3.zero
                : Vector3.Lerp(currentAvoidanceOffset, avoidanceOffset, Time.deltaTime * LateralBlendSpeed);
            currentOvertakeOffset = inRampUp
                ? Vector3.zero
                : Vector3.Lerp(currentOvertakeOffset, overtakeOffset, Time.deltaTime * LateralBlendSpeed);

            Vector3 dir = (splineDir + currentAvoidanceOffset + currentOvertakeOffset).normalized;

            float moveMultiplier = 1f;
            bool sprint = true;
            if (!inRampUp && useCurvatureSpeed)
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
                float rubberMult = CalculateRubberBandMultiplier();
                moveMultiplier *= rubberMult;
            }

            moveMultiplier *= draftingMult;

            if (profile != null)
                moveMultiplier *= profile.baseSpeedMultiplier;

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

        private void AdvanceWaypoints(Vector3 posFlat)
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

        private float CalculateRubberBandMultiplier()
        {
            var rm = RaceManager.Instance;
            if (rm == null) return 1f;

            int myPosition = rm.GetRacerPosition(racerIndex);
            int totalRacers = rm.GetAllRacers().Count;
            if (totalRacers <= 1) return 1f;

            float myProgress = rm.GetRacerProgress(racerIndex);

            float leaderProgress = float.MinValue;
            float secondProgress = float.MinValue;
            foreach (var r in rm.GetAllRacers())
            {
                if (r.progress > leaderProgress)
                {
                    secondProgress = leaderProgress;
                    leaderProgress = r.progress;
                }
                else if (r.progress > secondProgress)
                {
                    secondProgress = r.progress;
                }
            }

            if (myPosition == 1)
            {
                float gapToSecond = myProgress - secondProgress;
                if (gapToSecond > 0.05f)
                    return Mathf.Max(rubberBandSlow, 1f - gapToSecond * 0.5f);
            }
            else if (myPosition == totalRacers)
            {
                float gapFromLeader = leaderProgress - myProgress;
                if (gapFromLeader > 0.05f)
                    return Mathf.Min(rubberBandBoost, 1f + gapFromLeader * 2f);
            }
            else
            {
                float gapFromLeader = leaderProgress - myProgress;
                if (gapFromLeader > 0.05f)
                    return Mathf.Min(rubberBandBoost, 1f + gapFromLeader * 1f);
            }

            return 1f;
        }

        private Vector3 CalculateAvoidanceOffset(Vector3 position, Vector3 forward)
        {
            if (profile == null) return Vector3.zero;

            if (Physics.SphereCast(position, profile.avoidanceRadius, forward, out RaycastHit hit, AvoidanceCastDistance, animalLayerMask))
            {
                if (hit.collider.transform.root == racerRoot) return Vector3.zero;

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 toObstacle = (hit.point - position).normalized;
                float side = Vector3.Dot(toObstacle, right);
                float steerSign = side >= 0f ? -1f : 1f;
                float proximity = 1f - (hit.distance / AvoidanceCastDistance);
                return right * steerSign * profile.avoidanceSteering * proximity;
            }

            return Vector3.zero;
        }

        private Vector3 CalculateOvertakeOffset(Vector3 position, Vector3 forward)
        {
            if (profile == null || RaceManager.Instance == null) return Vector3.zero;

            if (isOvertaking)
            {
                if (Time.time - overtakeStartTime > OvertakeTimeout)
                {
                    isOvertaking = false;
                    overtakeTargetIndex = -1;
                    return Vector3.zero;
                }

                var targetData = FindRacerByIndex(overtakeTargetIndex);
                if (targetData == null || targetData.root == null)
                {
                    isOvertaking = false;
                    overtakeTargetIndex = -1;
                    return Vector3.zero;
                }

                float distToTarget = Vector3.Distance(position, targetData.root.transform.position);
                if (distToTarget > profile.overtakeDistance * 1.5f)
                {
                    isOvertaking = false;
                    overtakeTargetIndex = -1;
                    return Vector3.zero;
                }

                float myProg = RaceManager.Instance.GetRacerProgress(racerIndex);
                float theirProg = RaceManager.Instance.GetRacerProgress(overtakeTargetIndex);
                if (myProg > theirProg + 0.02f)
                {
                    isOvertaking = false;
                    overtakeTargetIndex = -1;
                    return Vector3.zero;
                }

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                return currentOvertakeOffset.normalized * profile.lateralOffsetStrength;
            }

            if (Time.time - lastOvertakeCheckTime < OvertakeCheckInterval) return Vector3.zero;
            lastOvertakeCheckTime = Time.time;

            if (Random.value > profile.aggressiveness) return Vector3.zero;

            List<RaceManager.RacerData> allRacers = RaceManager.Instance.GetAllRacers();
            RaceManager.RacerData closestAhead = null;
            float closestDist = float.MaxValue;

            foreach (var r in allRacers)
            {
                if (r.racerIndex == racerIndex || r.root == null) continue;
                float dist = Vector3.Distance(position, r.root.transform.position);
                if (dist > profile.overtakeDistance) continue;

                Vector3 toOther = (r.root.transform.position - position).normalized;
                float dot = Vector3.Dot(forward, toOther);
                if (dot <= 0f) continue;

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestAhead = r;
                }
            }

            if (closestAhead == null) return Vector3.zero;

            Vector3 rightDir = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 leftRayDir = Quaternion.AngleAxis(-OvertakeRayAngleDeg, Vector3.up) * forward;
            Vector3 rightRayDir = Quaternion.AngleAxis(OvertakeRayAngleDeg, Vector3.up) * forward;

            bool leftBlocked = Physics.Raycast(position, leftRayDir, OvertakeRayDistance, animalLayerMask);
            bool rightBlocked = Physics.Raycast(position, rightRayDir, OvertakeRayDistance, animalLayerMask);

            if (leftBlocked && rightBlocked) return Vector3.zero;

            float steerSign;
            if (!leftBlocked && !rightBlocked)
                steerSign = Random.value > 0.5f ? 1f : -1f;
            else
                steerSign = leftBlocked ? 1f : -1f;

            isOvertaking = true;
            overtakeStartTime = Time.time;
            overtakeTargetIndex = closestAhead.racerIndex;
            return rightDir * steerSign * profile.lateralOffsetStrength;
        }

        private float CalculateDraftingBoost(Vector3 position, Vector3 forward)
        {
            if (profile == null || RaceManager.Instance == null) return 1f;

            List<RaceManager.RacerData> allRacers = RaceManager.Instance.GetAllRacers();
            foreach (var r in allRacers)
            {
                if (r.racerIndex == racerIndex || r.root == null || r.animal == null) continue;

                Vector3 otherPos = r.root.transform.position;
                float dist = Vector3.Distance(position, otherPos);
                if (dist > profile.draftingDistance) continue;

                Vector3 toOther = (otherPos - position).normalized;
                float dotForward = Vector3.Dot(forward, toOther);
                if (dotForward <= 0f) continue;

                Vector3 otherForward = r.animal.transform.forward;
                float alignment = Vector3.Dot(forward, otherForward);
                if (alignment > DraftingDotThreshold)
                    return profile.draftingSpeedBonus;
            }

            return 1f;
        }

        private RaceManager.RacerData FindRacerByIndex(int index)
        {
            if (RaceManager.Instance == null) return null;
            foreach (var r in RaceManager.Instance.GetAllRacers())
            {
                if (r.racerIndex == index) return r;
            }
            return null;
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

        /// <summary>Sets the current progress along the waypoint path.</summary>
        public void SetProgress(float normalizedT)
        {
            if (waypoints == null || waypoints.Length == 0) return;
            currentWaypointIndex = Mathf.Clamp(
                Mathf.RoundToInt(normalizedT * waypoints.Length),
                0, waypoints.Length - 1);
        }

        /// <summary>Resets waypoint progress to the start.</summary>
        public void ResetToStart()
        {
            currentWaypointIndex = (animal != null && waypoints != null && waypoints.Length > 0)
                ? FindStartingWaypointIndex()
                : 0;
            isOvertaking = false;
            overtakeTargetIndex = -1;
            currentAvoidanceOffset = Vector3.zero;
            currentOvertakeOffset = Vector3.zero;
            raceStartTime = -1f;
        }
    }
}
