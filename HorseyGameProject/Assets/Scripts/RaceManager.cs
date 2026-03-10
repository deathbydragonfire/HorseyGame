using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MalbersAnimations.Controller;
using MalbersAnimations;
using MalbersAnimations.HAP;

namespace HorseyGame
{
    [DefaultExecutionOrder(-100)]
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Racers")]
        public GameObject player;

        [Header("Race")]
        public int lapsToWin = 1;
        public MalbersAnimations.PathCreation.PathLink_Spline pathForProgress;

        [Header("Events")]
        public UnityEvent<int> OnRacerLap;
        public UnityEvent<int> OnRaceFinished;

        private bool raceFinished;
        private MAnimal playerAnimal;
        private readonly List<RacerData> racers = new List<RacerData>();
        private readonly Dictionary<int, float> lastLapTime = new Dictionary<int, float>();

        private const float LapCooldown = 5f;

        public bool RaceFinished => raceFinished;
        public int LapsToWin => lapsToWin;

        public class RacerData
        {
            public GameObject root;
            public MAnimal animal;
            public HorseRacerAI ai;
            public int racerIndex;
            public int laps;
            public float progress;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (player == null) player = GameObject.Find("Player");
            if (player != null)
            {
                playerAnimal = player.GetComponentInChildren<MAnimal>(true);
                var playerData = new RacerData
                {
                    root = player,
                    animal = playerAnimal,
                    ai = null,
                    racerIndex = 0,
                    laps = 0,
                    progress = 0f
                };
                racers.Add(playerData);
            }
        }

        private void Start()
        {
            DiscoverSceneRacers();
            EnsureCameraFollowsPlayer();
            StartCoroutine(EnsureCameraFollowsPlayerNextFrame());
        }

        private void DiscoverSceneRacers()
        {
            var allRacerIds = FindObjectsOfType<RacerId>(true);
            foreach (var rid in allRacerIds)
            {
                if (rid.IsPlayer) continue;
                if (FindRacer(rid.Id) != null) continue;
                RegisterRacer(rid.gameObject);
            }
        }

        private System.Collections.IEnumerator EnsureCameraFollowsPlayerNextFrame()
        {
            yield return null;
            EnsureCameraFollowsPlayer();
        }

        /// <summary>Registers an AI racer spawned at runtime.</summary>
        public void RegisterRacer(GameObject racerRoot)
        {
            if (racerRoot == null) return;

            var racerId = racerRoot.GetComponent<RacerId>();
            var mount = racerRoot.GetComponentInChildren<Mount>(true);
            MAnimal animal = null;
            if (mount != null && mount.Animal != null)
                animal = mount.Animal;
            else
                animal = racerRoot.GetComponentInChildren<MAnimal>(true);

            var ai = racerRoot.GetComponentInChildren<HorseRacerAI>(true);

            var data = new RacerData
            {
                root = racerRoot,
                animal = animal,
                ai = ai,
                racerIndex = racerId != null ? racerId.Id : racers.Count,
                laps = 0,
                progress = 0f
            };
            racers.Add(data);
        }

        private void EnsureCameraFollowsPlayer()
        {
            if (player == null) return;

            Transform playerAnimalTransform = playerAnimal != null ? playerAnimal.transform : player.transform;

            foreach (var racer in racers)
            {
                if (racer.racerIndex == 0) continue;
                if (racer.root == null) continue;
                DisableTransformHooksOn(racer.root);
            }

            ResetPlayerTransformHooks();

            var freeLookCams = FindObjectsOfType<MFreeLookCamera>();
            foreach (var cam in freeLookCams)
            {
                Transform target = cam.Target;
                bool isOnAI = false;
                foreach (var racer in racers)
                {
                    if (racer.racerIndex == 0 || racer.root == null) continue;
                    if (target != null && (target == racer.root.transform || target.IsChildOf(racer.root.transform)))
                    {
                        isOnAI = true;
                        break;
                    }
                }
                if (isOnAI || cam.Target == null)
                    cam.Target_Set(playerAnimalTransform);
            }

            var cinemachineCams = FindObjectsOfType<ThirdPersonFollowTarget>();
            foreach (var cam in cinemachineCams)
            {
                foreach (var racer in racers)
                {
                    if (racer.racerIndex == 0 || racer.root == null) continue;
                    bool isOnRacer = cam.transform.IsChildOf(racer.root.transform) || cam.transform == racer.root.transform;
                    if (isOnRacer)
                    {
                        cam.enabled = false;
                        if (cam.gameObject.TryGetComponent<Unity.Cinemachine.CinemachineCamera>(out var vcam))
                            vcam.enabled = false;
                    }
                }
            }
        }

        private void DisableTransformHooksOn(GameObject target)
        {
            var hooks = target.GetComponentsInChildren<MalbersAnimations.Scriptables.TransformHook>(true);
            foreach (var hook in hooks)
                hook.enabled = false;
        }

        private void ResetPlayerTransformHooks()
        {
            if (player == null) return;
            var playerHooks = player.GetComponentsInChildren<MalbersAnimations.Scriptables.TransformHook>(true);
            foreach (var hook in playerHooks)
                hook.UpdateHook();
        }

        private void Update()
        {
            BlockAIInput();
            UpdateProgress();
        }

        private void BlockAIInput()
        {
            if (raceFinished) return;

            foreach (var racer in racers)
            {
                if (racer.racerIndex == 0 || racer.root == null) continue;

                var animal = racer.animal;
                if (animal != null)
                {
                    if (animal.InputSource != null)
                        animal.InputSource.Enable(false);
                    var mount = animal.GetComponentInParent<Mount>();
                    if (mount != null)
                        mount.EnableInput(false);
                }

                var inputs = racer.root.GetComponentsInChildren<MInput>(true);
                for (int i = 0; i < inputs.Length; i++)
                {
                    if (inputs[i] != null && inputs[i].enabled)
                        inputs[i].enabled = false;
                }
            }
        }

        private void UpdateProgress()
        {
            foreach (var racer in racers)
            {
                if (racer.root == null) continue;
                float t;
                if (racer.ai != null)
                    t = racer.ai.CurrentProgress;
                else
                    t = GetSplineT(racer.animal);
                racer.progress = racer.laps + t;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Called by FinishLineTrigger when a racer crosses the line.</summary>
        public void OnRacerCrossedFinish(int racerId)
        {
            if (raceFinished) return;

            RacerData data = FindRacer(racerId);
            if (data == null) return;

            if (lastLapTime.TryGetValue(racerId, out float lastTime) && Time.time - lastTime < LapCooldown)
                return;

            lastLapTime[racerId] = Time.time;
            data.laps++;
            OnRacerLap?.Invoke(racerId);

            if (data.laps >= lapsToWin)
                FinishRace(racerId);
        }

        private void FinishRace(int winnerRacerId)
        {
            raceFinished = true;

            foreach (var racer in racers)
            {
                if (racer.ai != null) racer.ai.enabled = false;
                if (racer.animal != null && racer.racerIndex != 0)
                    racer.animal.StopMoving();
            }

            OnRaceFinished?.Invoke(winnerRacerId);
        }

        /// <summary>Returns the race position (1st, 2nd, etc.) for a given racer index.</summary>
        public int GetRacerPosition(int racerIndex)
        {
            RacerData target = FindRacer(racerIndex);
            if (target == null) return racers.Count;

            int position = 1;
            foreach (var racer in racers)
            {
                if (racer.racerIndex == racerIndex) continue;
                if (racer.progress > target.progress)
                    position++;
            }
            return position;
        }

        /// <summary>Returns the progress (laps + spline t) for a given racer.</summary>
        public float GetRacerProgress(int racerIndex)
        {
            RacerData data = FindRacer(racerIndex);
            return data != null ? data.progress : 0f;
        }

        /// <summary>Provides the full racer list for AI queries.</summary>
        public List<RacerData> GetAllRacers()
        {
            return racers;
        }

        public int PlayerLaps
        {
            get
            {
                RacerData p = FindRacer(0);
                return p != null ? p.laps : 0;
            }
        }

        private float GetSplineT(MAnimal animal)
        {
            if (pathForProgress == null || animal == null) return 0f;
            return pathForProgress.GetClosestTimeOnPath(animal.transform.position);
        }

        /// <summary>Resets all racers for a new race.</summary>
        public void ResetRace()
        {
            raceFinished = false;
            lastLapTime.Clear();
            foreach (var racer in racers)
            {
                racer.laps = 0;
                racer.progress = 0f;
                if (racer.ai != null)
                {
                    racer.ai.ResetToStart();
                    racer.ai.enabled = true;
                }
            }
        }

        private RacerData FindRacer(int racerIndex)
        {
            foreach (var racer in racers)
            {
                if (racer.racerIndex == racerIndex)
                    return racer;
            }
            return null;
        }
    }
}
