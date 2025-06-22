using System;
using System.Collections.Generic;

namespace CrawfisSoftware.EventManagement
{
    internal class EventsPublisherSimple
    {
        // Define the events that occur in the game
        private readonly Dictionary<string, Action<object, object>> events = new Dictionary<string, Action<object, object>>();
        private readonly List<Action<string, object, object>> allSubscribers = new List<Action<string, object, object>>();
        public static EventsPublisherSimple Instance { get; private set; }
        static EventsPublisherSimple()
        {
            Instance = new EventsPublisherSimple();
        }
        private EventsPublisherSimple() { }

        public void RegisterEvent(string eventName)
        {
            if (!events.ContainsKey(eventName))
            {
                events.Add(eventName, NullCallback);
            }
        }
        public void SubscribeToEvent(string eventName, in Action<object, object> callback)
        {
            if (events.ContainsKey(eventName))
                events[eventName] += callback;
        }

        public void UnsubscribeToEvent(string eventName, in Action<object, object> callback)
        {
            if (events.ContainsKey(eventName))
                events[eventName] -= callback;
        }

        // Todo: Either need to pass an event string (type) around or change this signature to include it for these only.
        public void SubscribeToAllEvents(in Action<string, object, object> callback)
        {
            allSubscribers.Add(callback);
        }

        public void UnsubscribeToAllEvents(in Action<string, object, object> callback)
        {
            allSubscribers.Remove(callback);
        }

        public void PublishEvent(string eventName, object sender, object data)
        {
            if (events.TryGetValue(eventName, out Action<object, object> eventDelegate))
                eventDelegate(sender, data);
            foreach (var handler in allSubscribers)
                handler(eventName, sender, data);
        }

        private void NullCallback(object sender, object data)
        {
        }
    }
}