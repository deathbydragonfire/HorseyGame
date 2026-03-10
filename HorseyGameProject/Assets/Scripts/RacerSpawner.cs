using UnityEngine;
using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.HAP;
using MalbersAnimations.PathCreation;

namespace HorseyGame
{
    [DefaultExecutionOrder(-200)]
    public class RacerSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        public GameObject racerPrefab;

        [Header("Profiles")]
        public RacerProfile[] profiles;

        [Header("Start Positions")]
        public Transform[] startPositions;
        public float staggerDistance = 5f;

        [Header("Path")]
        public PathLink_Spline racePath;

        private void Awake()
        {
            if (racerPrefab == null || profiles == null || profiles.Length == 0)
            {
                Debug.LogError("[RacerSpawner] Missing racerPrefab or profiles.");
                return;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                GameObject spawned = Instantiate(racerPrefab);
                spawned.name = $"AI Racer {i + 1}";

                if (startPositions != null && i < startPositions.Length && startPositions[i] != null)
                {
                    spawned.transform.position = startPositions[i].position;
                    spawned.transform.rotation = startPositions[i].rotation;
                }
                else if (racePath != null)
                {
                    float totalLength = EstimateSplineLength();
                    float offset = staggerDistance * (i + 1);
                    float t = 1f - (offset / Mathf.Max(totalLength, 1f));
                    t = Mathf.Clamp01(t);

                    Vector3 spawnPos = racePath.GetPointAtTime(t);
                    spawned.transform.position = spawnPos;

                    float tNext = Mathf.Clamp01(t + 0.01f);
                    Vector3 nextPos = racePath.GetPointAtTime(tNext);
                    Vector3 fwd = (nextPos - spawnPos);
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.001f)
                        spawned.transform.rotation = Quaternion.LookRotation(fwd.normalized);
                }

                var racerId = spawned.GetComponent<RacerId>();
                if (racerId == null)
                    racerId = spawned.AddComponent<RacerId>();
                racerId.racerIndex = i + 1;

                var ai = spawned.GetComponent<HorseRacerAI>();
                if (ai == null)
                    ai = spawned.AddComponent<HorseRacerAI>();

                var mount = spawned.GetComponentInChildren<MalbersAnimations.HAP.Mount>(true);
                if (mount != null && mount.Animal != null)
                    ai.racer = mount.Animal.gameObject;
                else
                {
                    var animal = spawned.GetComponentInChildren<MalbersAnimations.Controller.MAnimal>(true);
                    ai.racer = animal != null ? animal.gameObject : spawned;
                }
                ai.racerIndex = i + 1;
                ai.profile = profiles[i];

                if (racePath != null)
                {
                    ai.pathLink = racePath;
                    var splineContainer = racePath.GetComponent<UnityEngine.Splines.SplineContainer>();
                    if (splineContainer != null)
                        ai.splineContainer = splineContainer;
                }

                DisableInputOnRacer(spawned);
                DestroyInputLinkComponents(spawned);

                SetLayerRecursive(spawned, LayerMask.NameToLayer("Animal"));

                if (RaceManager.Instance != null)
                    RaceManager.Instance.RegisterRacer(spawned);
            }
        }

        private void DisableInputOnRacer(GameObject racerRoot)
        {
            var inputs = racerRoot.GetComponentsInChildren<MInput>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] != null)
                    inputs[i].enabled = false;
            }
        }

        private void DestroyInputLinkComponents(GameObject racerRoot)
        {
            var allBehaviours = racerRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = allBehaviours.Length - 1; i >= 0; i--)
            {
                if (allBehaviours[i] == null) continue;
                string typeName = allBehaviours[i].GetType().FullName;
                if (typeName == "MalbersAnimations.InputSystem.MInputLink" ||
                    typeName == "UnityEngine.InputSystem.PlayerInput")
                {
                    allBehaviours[i].enabled = false;
                }
            }
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            if (layer < 0) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private float EstimateSplineLength()
        {
            if (racePath == null) return 100f;

            const int sampleCount = 50;
            float length = 0f;
            Vector3 prev = racePath.GetPointAtTime(0f);
            for (int i = 1; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                Vector3 curr = racePath.GetPointAtTime(t);
                length += Vector3.Distance(prev, curr);
                prev = curr;
            }
            return length;
        }
    }
}
