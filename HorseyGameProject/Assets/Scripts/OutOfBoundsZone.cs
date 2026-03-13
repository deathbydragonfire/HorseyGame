using UnityEngine;

namespace HorseyGame
{
    [RequireComponent(typeof(Collider))]
    public class OutOfBoundsZone : MonoBehaviour
    {
        private void Awake()
        {
            Collider c = GetComponent<Collider>();
            if (c != null && !c.isTrigger)
                c.isTrigger = true;
        }
    }
}
