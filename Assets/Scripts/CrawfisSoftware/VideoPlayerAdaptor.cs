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
        [SerializeField] private int _numberOfLoops = 1;
        private int _currentLoop = 1;

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
            _videoPlayer.loopPointReached += OnLoopPointReached;
            _currentLoop = 1;
            _videoPlayer.isLooping = _numberOfLoops > 1 ? true : false;
            _videoPlayer.Play();
            Debug.Log("VideoPlayerAdaptor started.");
        }

        private void OnDisable()
        {
            Debug.Log("VideoPlayerAdaptor Closing.");
            Close();
            Debug.Log("VideoPlayerAdaptor Closed.");
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            _currentLoop++;
            if (_currentLoop == _numberOfLoops)
            {
                _videoPlayer.isLooping = false;
            }
            else if (_currentLoop > _numberOfLoops)
            {
                EventsPublisherSimple.Instance.PublishEvent("ImageSourceCompleted", this, null);
            }
        }

        private void OnFrameReady(VideoPlayer source, long frameIdx)
        {
            EventsPublisherSimple.Instance.PublishEvent("ImageUpdated", this, source.texture);
        }

        private void OnVideoPrepared(UnityEngine.Video.VideoPlayer source)
        {
            Debug.Log("Video prepared successfully.");
            _videoPlayer.frameReady += OnFrameReady;
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