using UnityEngine;

namespace HorseyGame
{
    public class RacerId : MonoBehaviour
    {
        public int racerIndex = 0;

        public int Id => racerIndex;
        public bool IsPlayer => racerIndex == 0;
    }
}
