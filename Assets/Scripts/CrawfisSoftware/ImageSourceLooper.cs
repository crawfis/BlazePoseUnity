using CrawfisSoftware.EventManagement;

using System.Collections;

using UnityEngine;

namespace CrawfisSoftware
{
    internal class ImageSourceLooper : MonoBehaviour
    {
        [SerializeField] private GameObject[] _imageSourceTests;
        [SerializeField] private bool _randomlyChange = false;

        private int _imageSourceIndex = 0;
        private void Awake()
        {
            if (_imageSourceTests == null || _imageSourceTests.Length == 0)
            {
                Debug.LogError("No image source tests assigned in the inspector.");
                return;
            }
            DisableAll();
        }

        private void DisableAll()
        {
            foreach (var test in _imageSourceTests)
            {
                test.SetActive(false);
            }
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.1f); // Video player can take a moment to initialize
            if (_randomlyChange)
            {
                _imageSourceIndex = Random.Range(0, _imageSourceTests.Length);
                StartCoroutine(RandomlyChangeImageSource());
            }
            else
            {
                EventsPublisherSimple.Instance.SubscribeToEvent("ImageSourceCompleted", EnableNextImageSource);
            }
            _imageSourceTests[_imageSourceIndex].SetActive(true);
        }

        private IEnumerator RandomlyChangeImageSource()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(3f, 6f));
                int testIndex = Random.Range(0, _imageSourceTests.Length);
                if (testIndex == _imageSourceIndex) testIndex = (testIndex + 1) % _imageSourceTests.Length; // Ensure we change to a different source
                _imageSourceTests[_imageSourceIndex].SetActive(false);
                _imageSourceIndex = testIndex;
                _imageSourceTests[_imageSourceIndex].SetActive(true);
                EventsPublisherSimple.Instance.PublishEvent("ImageSourceChanged", this, _imageSourceTests[_imageSourceIndex]);
            }
        }

        private void EnableNextImageSource(object sender, object data)
        {
            _imageSourceTests[_imageSourceIndex].SetActive(false);
            _imageSourceIndex++;
            _imageSourceIndex %= _imageSourceTests.Length;
            _imageSourceTests[_imageSourceIndex].SetActive(true);
            EventsPublisherSimple.Instance.PublishEvent("ImageSourceChanged", this, _imageSourceTests[_imageSourceIndex]);
        }
    }
}