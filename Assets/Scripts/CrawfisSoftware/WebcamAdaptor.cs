using CrawfisSoftware.EventManagement;

using System.Collections;

using UnityEngine;
using UnityEngine.UI;

public class WebcamAdaptor : MonoBehaviour
{
    [SerializeField] private int cameraIndex = 0;
    [SerializeField] private float _testTime = 3600f; // Duration to test the webcam feed, in seconds

    private WebCamTexture webcamTexture;
    private Coroutine _testCoroutine;

    private void OnEnable()
    {
        // Get the default webcam device
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > cameraIndex)
        {
            webcamTexture = new WebCamTexture(devices[cameraIndex].name);

            // Start the webcam feed
            webcamTexture.Play();
            _testCoroutine = StartCoroutine(StopAfterTime(_testTime));
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
        if (_testCoroutine != null)
        {
            StopCoroutine(_testCoroutine);
            _testCoroutine = null;
        }
    }

    private IEnumerator StopAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        EventsPublisherSimple.Instance.PublishEvent("ImageSourceCompleted", this, null);
    }
}