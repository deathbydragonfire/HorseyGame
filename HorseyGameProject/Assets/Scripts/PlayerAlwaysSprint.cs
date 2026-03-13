using UnityEngine;
using MalbersAnimations.Controller;

namespace HorseyGame
{
    /// <summary>
    /// Forces the player horse to always sprint without requiring the shift key.
    /// Attaches to any GameObject in the scene and auto-locates the player horse.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public class PlayerAlwaysSprint : MonoBehaviour
    {
        [Tooltip("Root GameObject tagged as the player. If null, falls back to finding 'Player' by name.")]
        public GameObject player;

        private MAnimal horseAnimal;

        private void Awake()
        {
            if (player == null)
                player = GameObject.Find("Player");

            if (player == null)
            {
                Debug.LogWarning("[PlayerAlwaysSprint] Could not find 'Player' GameObject.", this);
                return;
            }

            MAnimal[] animals = player.GetComponentsInChildren<MAnimal>(true);
            horseAnimal = System.Array.Find(
                animals,
                a => a.gameObject != player
                     && a.gameObject.CompareTag("Animal")
                     && a.gameObject.name.ToLower().Contains("horse")
            );

            if (horseAnimal == null && animals.Length > 0)
                horseAnimal = animals[animals.Length - 1];

            if (horseAnimal == null)
                Debug.LogWarning("[PlayerAlwaysSprint] Could not find horse MAnimal on player.", this);
        }

        private void Start()
        {
            ApplySprint();

            // Re-apply sprint each time a new state activates, in case MAnimal resets it.
            if (horseAnimal != null)
                horseAnimal.OnStateChange.AddListener(OnStateChange);
        }

        private void OnDestroy()
        {
            if (horseAnimal != null)
                horseAnimal.OnStateChange.RemoveListener(OnStateChange);
        }

        private void OnStateChange(int stateID)
        {
            ApplySprint();
        }

        private void ApplySprint()
        {
            if (horseAnimal != null)
                horseAnimal.SetSprint(true);
        }
    }
}
