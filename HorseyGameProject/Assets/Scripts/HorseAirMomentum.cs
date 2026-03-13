using UnityEngine;
using MalbersAnimations;
using MalbersAnimations.Controller;

namespace HorseyGame
{
    /// <summary>
    /// Preserves horizontal forward momentum when the horse transitions into the Fall state.
    /// Attach to the same GameObject as, or as a sibling of, the MAnimal component.
    /// </summary>
    public class HorseAirMomentum : MonoBehaviour
    {
        [Tooltip("Fraction of the captured horizontal velocity to reinject each FixedUpdate while falling. 1 = full preservation, 0 = no effect.")]
        [Range(0f, 1f)]
        public float momentumRetention = 1f;

        [Tooltip("How quickly the injected inertia speed fades out once grounded (seconds to reach zero).")]
        public float groundedFadeTime = 0.15f;

        private MAnimal animal;
        private bool isFalling;
        private Vector3 capturedHorizontalVelocity;

        private void Awake()
        {
            animal = GetComponentInChildren<MAnimal>(true);
            if (animal == null)
                animal = GetComponentInParent<MAnimal>(true);
        }

        private void OnEnable()
        {
            if (animal != null)
                animal.OnState += OnStateChanged;
        }

        private void OnDisable()
        {
            if (animal != null)
                animal.OnState -= OnStateChanged;
        }

        private void OnStateChanged(int stateId)
        {
            if (stateId == StateEnum.Fall)
            {
                isFalling = true;
                Vector3 velocity = animal.HorizontalVelocity;
                capturedHorizontalVelocity = Vector3.ProjectOnPlane(velocity, animal.UpVector);
            }
            else
            {
                isFalling = false;
            }
        }

        private void FixedUpdate()
        {
            if (!isFalling || animal == null) return;
            if (capturedHorizontalVelocity.sqrMagnitude < 0.001f) return;

            animal.InertiaPositionSpeed = capturedHorizontalVelocity * momentumRetention * Time.fixedDeltaTime;
        }
    }
}
