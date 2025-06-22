using CrawfisSoftware.EventManagement;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

namespace CrawfisSoftware
{
    internal class FaceBoundingBoxAdaptor : MonoBehaviour
    {
        [SerializeField] private PosePreview _posePreview;

        private void Awake()
        {
            EventsPublisherSimple.Instance.RegisterEvent("FaceDetected");
            EventsPublisherSimple.Instance.SubscribeToEvent("FaceDetected", OnFaceDetected);
            EventsPublisherSimple.Instance.RegisterEvent("NoFaceDetected");
            EventsPublisherSimple.Instance.SubscribeToEvent("NoFaceDetected", OnNoFaceDetected);
        }

        private void OnFaceDetected(object sender, object faceBoundingBoxData)
        {
            FaceBoundingBox faceBoundingBox = (FaceBoundingBox)faceBoundingBoxData;
            _posePreview.SetBoundingBox(true, faceBoundingBox.faceWorldPosition, faceBoundingBox.boundingBoxHeight);
            //_posePreview.SetBoundingBox(true, faceWorldPosition, boundingBoxHeight);
            //_posePreview.SetBoundingCircle(true, keyPointOnePosition, personDetectionRadius);
        }

        private void OnNoFaceDetected(object sender, object faceBoundingBoxData)
        {
            _posePreview.SetBoundingBox(false, Vector3.zero, Vector2.one);
        }
    }
}