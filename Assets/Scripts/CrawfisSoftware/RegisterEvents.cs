using UnityEngine;

namespace CrawfisSoftware.EventManagement
{
    internal static class RegisterEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAllEvents()
        {
            EventsPublisherSimple.Instance.RegisterEvent("Skeleton");
            EventsPublisherSimple.Instance.RegisterEvent("PersonDetected");
            EventsPublisherSimple.Instance.RegisterEvent("NoPersonDetected");
            EventsPublisherSimple.Instance.RegisterEvent("FaceDetected");
            EventsPublisherSimple.Instance.RegisterEvent("NoFaceDetected");
            EventsPublisherSimple.Instance.RegisterEvent("ImageUpdated");
            EventsPublisherSimple.Instance.RegisterEvent("ImageSourceChanged");
        }
    }
}