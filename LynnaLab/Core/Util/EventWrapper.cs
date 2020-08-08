using System;
using System.Collections.Generic;
using System.Reflection;

namespace Util {
    /** An EventWrapper is an intermediary between an object which triggers an event and an event
     * handler. The main motivation is to make it easier to unbind event handlers from objects we
     * don't care about anymore (ie. it's difficult (impossible?) if the handler is a lambda).
     *
     * This can subscribe to events of type "EventHandler<T>".
     */
    public class EventWrapper<T> {

        List<Tuple<object, EventInfo>> signallers = new List<Tuple<object, EventInfo>>();
        Delegate signalledDelegate;


        public EventWrapper() {
            // Get delegate to Signalled method
            MethodInfo miHandler = typeof(EventWrapper<T>).GetMethod("Signalled", BindingFlags.NonPublic | BindingFlags.Instance);
            var tDelegate = typeof(EventHandler<T>);
            signalledDelegate = Delegate.CreateDelegate(tDelegate, this, miHandler);
        }


        /// Add handlers using "eventWrapper.Event += <handler>"
        public event EventHandler<T> Event;


        /// Pass an object along with the name of the event to subscribe to.
        public void Bind(object obj, string eventName) {
            EventInfo ev = obj.GetType().GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);

            ev.GetAddMethod().Invoke(obj, new object[] { signalledDelegate });

            signallers.Add(new Tuple<object, EventInfo>(obj, ev));
        }

        /// Unsubscribe from all events.
        public void UnbindAll() {
            foreach (var pair in signallers)
                pair.Item2.GetRemoveMethod().Invoke(pair.Item1, new object[] { signalledDelegate });
            signallers.Clear();
        }

        void Signalled(object sender, T args) {
            if (Event != null)
                Event(sender, args);
        }
    }
}
