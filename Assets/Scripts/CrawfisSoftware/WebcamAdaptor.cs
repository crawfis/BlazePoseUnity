using CrawfisSoftware.EventManagement;

using UnityEngine;
using UnityEngine.UI;

public class WebcamAdaptor : MonoBehaviour
{
    public int cameraIndex = 0;
    private WebCamTexture webcamTexture;

    private void OnEnable()
    {
        // Get the default webcam device
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > cameraIndex)
        {
            webcamTexture = new WebCamTexture(devices[cameraIndex].name);

            // Start the webcam feed
            webcamTexture.Play();
        }
        else
        {
            Debug.LogError("No webcam detected!");
        }
        Debug.Log("WebcamAdaptor started.");
    }
    private void Update()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            EventsPublisherSimple.Instance.PublishEvent("ImageUpdated", this, webcamTexture);
        }
    }

    void OnDisable()
    {
        // Stop the webcam feed when the object is disabled
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}