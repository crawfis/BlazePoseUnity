using UnityEngine;
using System.Collections;

namespace CrawfisSoftware
{
    public class DetectWebCams : MonoBehaviour
    {
        private WebCamDevice[] devices;

        IEnumerator Start()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                devices = WebCamTexture.devices;
                Debug.Log($"{devices.Length} cameras found");
                for (int cameraIndex = 0; cameraIndex < devices.Length; ++cameraIndex)
                {
                    string frontFacing = (devices[cameraIndex].isFrontFacing) ? "front-facing" : "back-facing";
                    Debug.Log($"{devices[cameraIndex].name} is a {frontFacing} camera");
                }
            }
            else
            {
                Debug.Log("no cameras found");
            }
        }
    }
}