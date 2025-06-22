using CrawfisSoftware.EventManagement;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace CrawfisSoftware
{
    internal class ImageSequenceAdaptor : MonoBehaviour
    {
        [SerializeField] private List<Texture> _images;
        [SerializeField] private float _timeDelay = 1f;

        private Coroutine _playCoroutine;

        private void Start()
        {
            if (_images == null || _images.Count == 0)
            {
                Debug.LogError("No images assigned to ImageSequenceAdaptor.");
                return;
            }
        }
        private void OnEnable()
        {
            _playCoroutine = StartCoroutine(PlayImageSequence());
        }

        private void OnDisable()
        {
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
        }

        private IEnumerator PlayImageSequence()
        {
            int imageIndex = 0;
            while (true)
            {
                EventsPublisherSimple.Instance.PublishEvent("ImageUpdated", this, _images[imageIndex]);
                yield return new WaitForSeconds(_timeDelay);
                imageIndex++;
                imageIndex %= _images.Count;
            }
        }
    }
}