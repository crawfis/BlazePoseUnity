using CrawfisSoftware.EventManagement;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

namespace CrawfisSoftware
{
    public class PersonDetectionAdaptor : MonoBehaviour
    {
        [SerializeField] private PosePreview _posePreview;

        private void Awake()
        {
            EventsPublisherSimple.Instance.RegisterEvent("PersonDetected");
            EventsPublisherSimple.Instance.SubscribeToEvent("PersonDetected", OnPersonDetected);
            EventsPublisherSimple.Instance.RegisterEvent("NoPersonDetected");
            EventsPublisherSimple.Instance.SubscribeToEvent("NoPersonDetected", OnNoPersonDetected);
        }

        private void OnPersonDetected(object sender, object personData)
        {
            PersonBoundingCircle personBoundingCircle = (PersonBoundingCircle)personData;
            _posePreview.SetBoundingCircle(true, personBoundingCircle.origin, personBoundingCircle.radius);
        }

        private void OnNoPersonDetected(object sender, object personData)
        {
            _posePreview.SetBoundingCircle(false, Vector3.zero, 0f);
        }
    }
}