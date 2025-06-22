using CrawfisSoftware.EventManagement;

using UnityEngine;

namespace CrawfisSoftware
{
    public class ImagePreviewAdaptor : MonoBehaviour
    {
        public ImagePreview _imagePreview;

        public void Awake()
        {
            EventsPublisherSimple.Instance.RegisterEvent("ImageUpdated");
            EventsPublisherSimple.Instance.SubscribeToEvent("ImageUpdated", OnImageUpdated);
        }

        public void OnImageUpdated(object sender, object eventData)
        {
            Texture texture = eventData as Texture;
            _imagePreview.SetTexture(texture);
        }
    }
}