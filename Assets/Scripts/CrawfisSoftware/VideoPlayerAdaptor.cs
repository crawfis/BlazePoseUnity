using CrawfisSoftware.EventManagement;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Video;

namespace CrawfisSoftware
{
    internal class VideoPlayerAdaptor : MonoBehaviour
    {
        [SerializeField] private VideoPlayer _videoPlayer;
        private void Awake()
        {
            if (_videoPlayer == null)
            {
                Debug.LogError("VideoPlayer is not assigned in the inspector.");
                return;
            }
            // Enable frameReady events
            _videoPlayer.sendFrameReadyEvents = true;
        }

        private void OnEnable()
        {
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.errorReceived += OnVideoError;
            _videoPlayer.frameReady += OnFrameReady;
            _videoPlayer.Play();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnFrameReady(VideoPlayer source, long frameIdx)
        {
            EventsPublisherSimple.Instance.PublishEvent("ImageUpdated", this, source.texture);
        }

        private void OnVideoPrepared(UnityEngine.Video.VideoPlayer source)
        {
            Debug.Log("Video prepared successfully.");
            _videoPlayer.Play();
        }
        private void OnVideoError(UnityEngine.Video.VideoPlayer source, string message)
        {
            Debug.LogError($"Video error: {message}");
        }
        private void OnDestroy()
        {
            Close();
        }

        public void Close()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= OnVideoPrepared;
                _videoPlayer.errorReceived -= OnVideoError;
                _videoPlayer.frameReady -= OnFrameReady;
            }
            // Stop the video when the object is destroyed
            if (_videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
            }
        }
    }
}