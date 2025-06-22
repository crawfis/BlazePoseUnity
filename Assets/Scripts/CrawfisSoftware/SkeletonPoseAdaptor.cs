using CrawfisSoftware.EventManagement;

using System;

using TMPro;

using UnityEngine;

namespace CrawfisSoftware
{
    internal class SkeletonPoseAdaptor : MonoBehaviour
    {
        [SerializeField] private PosePreview _posePreview;
        [SerializeField] private TextMeshPro _skeletonCountTMP;

        private int _skeletonCount;
        public void Awake()
        {
            EventsPublisherSimple.Instance.RegisterEvent("Skeleton");
            EventsPublisherSimple.Instance.SubscribeToEvent("Skeleton", HandleSkeleton);
            EventsPublisherSimple.Instance.SubscribeToEvent("NoPersonDetected", OnNoSkeleton);
        }

        private void OnNoSkeleton(object arg1, object arg2)
        {
            _posePreview.SetActive(false);
        }

        public void HandleSkeleton(object caller, object eventData)
        {
            _posePreview.SetActive(true);
            SkeletalData skeletalData = eventData as SkeletalData;
            _skeletonCount++;
            _skeletonCountTMP.text = $"Skeleton: {_skeletonCount}";
            for (int i = 0; i < skeletalData.skeletalPositions.Length; i++)
            {
                Vector3 position = skeletalData.skeletalPositions[i];
                _posePreview.SetKeyPoint(i, skeletalData.isTracked[i], position);
            }
        }
    }
}