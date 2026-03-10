using UnityEngine;

namespace HorseyGame
{
    /// <summary>
    /// Identifies a racer for the finish line and race manager (Player = 0, Opponent = 1).
    /// Attach to the root of Player and Opponent (same level as MAnimal or parent).
    /// </summary>
    public class RacerId : MonoBehaviour
    {
        public enum Racer { Player = 0, Opponent = 1 }

        public Racer racer = Racer.Player;

        public int Id => (int)racer;
        public bool IsPlayer => racer == Racer.Player;
        public bool IsOpponent => racer == Racer.Opponent;
    }
}
