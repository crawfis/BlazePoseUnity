using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.CrawfisSoftware
{
    internal class ApplicationQuit : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Debug.Log("Application is quitting...");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}