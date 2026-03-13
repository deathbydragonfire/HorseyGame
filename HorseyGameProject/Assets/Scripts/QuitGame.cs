using UnityEngine;
using UnityEngine.InputSystem;

namespace HorseyGame
{
    /// <summary>Listens for the Escape key and quits the application.</summary>
    public class QuitGame : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Quit();
        }

        /// <summary>Exits the game in a build or stops play mode in the editor.</summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
