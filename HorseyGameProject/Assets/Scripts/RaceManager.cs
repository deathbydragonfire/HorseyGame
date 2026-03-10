using UnityEngine;
using UnityEngine.Events;
using MalbersAnimations.Controller;
using MalbersAnimations;
using MalbersAnimations.HAP;

namespace HorseyGame
{
    /// <summary>
    /// Tracks laps, race state, and progress for Player and Opponent. Used by FinishLineTrigger and HorseRacerAI (rubber-banding).
    /// Also blocks all keyboard/input on the Opponent every frame so only HorseRacerAI drives it.
    /// Assign Player and Opponent (root GameObjects with RacerId).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Racers")]
        [Tooltip("Player root (has RacerId = Player).")]
        public GameObject player;
        [Tooltip("Opponent root (has RacerId = Opponent, HorseRacerAI).")]
        public GameObject opponent;

        [Header("Race")]
        [Tooltip("Number of laps to complete to win.")]
        public int lapsToWin = 1;
        [Tooltip("Path for progress (optional). If set, progress = lap + spline t.")]
        public MalbersAnimations.PathCreation.PathLink_Spline pathForProgress;

        [Header("Events")]
        public UnityEvent<int> OnRacerLap;
        public UnityEvent<int> OnRaceFinished;

        private int playerLaps;
        private int opponentLaps;
        private bool raceFinished;
        private RacerId playerRacerId;
        private RacerId opponentRacerId;
        private HorseRacerAI opponentAI;
        private MalbersAnimations.Controller.MAnimal playerAnimal;
        private MalbersAnimations.Controller.MAnimal opponentAnimal;

        public int PlayerLaps => playerLaps;
        public int OpponentLaps => opponentLaps;
        public bool RaceFinished => raceFinished;
        public int LapsToWin => lapsToWin;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (player == null) player = GameObject.Find("Player");
            if (opponent == null) opponent = GameObject.Find("Opponent");

            if (player != null) playerRacerId = player.GetComponent<RacerId>();
            if (opponent != null)
            {
                opponentRacerId = opponent.GetComponent<RacerId>();
                opponentAI = opponent.GetComponentInChildren<HorseRacerAI>(true);
                opponentAnimal = opponent.GetComponentInChildren<MAnimal>(true);
            }
            if (player != null) playerAnimal = player.GetComponentInChildren<MAnimal>(true);
        }

        private void Start()
        {
            EnsureCameraFollowsPlayer();
            StartCoroutine(EnsureCameraFollowsPlayerNextFrame());
        }

        private System.Collections.IEnumerator EnsureCameraFollowsPlayerNextFrame()
        {
            yield return null;
            EnsureCameraFollowsPlayer();
        }

        private void EnsureCameraFollowsPlayer()
        {
            if (player == null || opponent == null) return;

            DisableOpponentTransformHooks();

            ResetPlayerTransformHooks();

            Transform playerAnimalTransform = playerAnimal != null ? playerAnimal.transform : player.transform;

            var freeLookCams = FindObjectsOfType<MalbersAnimations.MFreeLookCamera>();
            foreach (var cam in freeLookCams)
            {
                Transform target = cam.Target;
                if (target != null && (target == opponent.transform || target.IsChildOf(opponent.transform)))
                    cam.Target_Set(playerAnimalTransform);
                else if (cam.Target == null)
                    cam.Target_Set(playerAnimalTransform);
            }

            var cinemachineCams = FindObjectsOfType<MalbersAnimations.ThirdPersonFollowTarget>();
            foreach (var cam in cinemachineCams)
            {
                bool isOnOpponent = cam.transform.IsChildOf(opponent.transform) || cam.transform == opponent.transform;
                if (isOnOpponent)
                {
                    cam.enabled = false;
                    if (cam.gameObject.TryGetComponent<Unity.Cinemachine.CinemachineCamera>(out var vcam))
                        vcam.enabled = false;
                }
            }
        }

        private void DisableOpponentTransformHooks()
        {
            var allHooks = opponent.GetComponentsInChildren<MalbersAnimations.Scriptables.TransformHook>(true);
            foreach (var hook in allHooks)
            {
                hook.enabled = false;
            }
        }

        private void ResetPlayerTransformHooks()
        {
            var playerHooks = player.GetComponentsInChildren<MalbersAnimations.Scriptables.TransformHook>(true);
            foreach (var hook in playerHooks)
            {
                hook.UpdateHook();
            }
        }

        private void Update()
        {
            BlockOpponentInput();
        }

        private void BlockOpponentInput()
        {
            if (raceFinished) return;
            if (opponent == null) opponent = GameObject.Find("Opponent");
            if (opponent == null) return;
            Transform root = opponent.transform;
            var animal = opponentAnimal != null ? opponentAnimal : root.GetComponentInChildren<MAnimal>(true);
            if (animal != null)
            {
                if (animal.InputSource != null)
                    animal.InputSource.Enable(false);
                var mount = animal.GetComponentInParent<Mount>();
                if (mount != null)
                    mount.EnableInput(false);
            }
            var inputs = root.GetComponentsInChildren<MInput>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] != null && inputs[i].enabled)
                    inputs[i].enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void OnRacerCrossedFinish(int racerId)
        {
            if (raceFinished) return;

            if (racerId == 0)
            {
                playerLaps++;
                OnRacerLap?.Invoke(0);
                if (playerLaps >= lapsToWin)
                    FinishRace(0);
            }
            else if (racerId == 1)
            {
                opponentLaps++;
                OnRacerLap?.Invoke(1);
                if (opponentLaps >= lapsToWin)
                    FinishRace(1);
            }
        }

        private void FinishRace(int winnerRacerId)
        {
            raceFinished = true;
            if (opponentAI != null) opponentAI.enabled = false;
            if (opponentAnimal != null) opponentAnimal.StopMoving();
            OnRaceFinished?.Invoke(winnerRacerId);
        }

        /// <summary>Progress for rubber-banding: lap + normalized spline t (0..1). Higher = ahead. </summary>
        public float GetPlayerProgress()
        {
            float t = GetSplineT(playerAnimal);
            return playerLaps + t;
        }

        /// <summary>Progress for rubber-banding: lap + normalized spline t (0..1). Higher = ahead.</summary>
        public float GetOpponentProgress()
        {
            if (opponentAI != null)
                return opponentLaps + opponentAI.CurrentProgress;
            float t = GetSplineT(opponentAnimal);
            return opponentLaps + t;
        }

        private float GetSplineT(MAnimal animal)
        {
            if (pathForProgress == null || animal == null) return 0f;
            return pathForProgress.GetClosestTimeOnPath(animal.transform.position);
        }

        public void ResetRace()
        {
            playerLaps = 0;
            opponentLaps = 0;
            raceFinished = false;
            if (opponentAI != null)
            {
                opponentAI.ResetToStart();
                opponentAI.enabled = true;
            }
        }
    }
}
