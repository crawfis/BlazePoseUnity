using CrawfisSoftware.EventManagement;

using System.Collections;

using UnityEngine;

namespace CrawfisSoftware
{
    public enum ImageSourceType
    {
        WebCam,
        VideoPlayer,
        ImageSequence
    }
    internal class ImageSourceController : MonoBehaviour
    {
        [SerializeField] private ImageSourceType _imageSourceType = ImageSourceType.WebCam;
        [SerializeField] private WebcamAdaptor _webCamAdaptor;
        [SerializeField] private VideoPlayerAdaptor _videoPlayerAdaptor;
        [SerializeField] private ImageSequenceAdaptor _imageSequenceAdaptor;

        private ImageSourceType _oldImageType;
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);
            EnableImageSource();
            StartCoroutine(RandomlyChangeImageSource());
        }

        private IEnumerator RandomlyChangeImageSource()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(3f, 4f));
                _imageSourceType = (ImageSourceType)Random.Range(0, System.Enum.GetValues(typeof(ImageSourceType)).Length);
            }
        }
        private void Update()
        {
            if (_oldImageType != _imageSourceType)
            {
                _videoPlayerAdaptor.gameObject.SetActive(false);
                _imageSequenceAdaptor.gameObject.SetActive(false);
                _webCamAdaptor.gameObject.SetActive(false);
                EnableImageSource();
            }
        }

        private void EnableImageSource()
        {
            _oldImageType = _imageSourceType;
            switch (_imageSourceType)
            {
                case ImageSourceType.WebCam:
                    if (_webCamAdaptor != null)
                    {
                        _webCamAdaptor.gameObject.SetActive(true);
                    }
                    break;
                case ImageSourceType.VideoPlayer:
                    if (_videoPlayerAdaptor != null)
                    {
                        _videoPlayerAdaptor.gameObject.SetActive(true);
                    }
                    break;
                case ImageSourceType.ImageSequence:
                    if (_imageSequenceAdaptor != null)
                    {
                        _imageSequenceAdaptor.gameObject.SetActive(true);
                    }
                    break;
            }
            EventsPublisherSimple.Instance.PublishEvent("ImageSourceChanged", this, _imageSourceType);
        }
    }
}