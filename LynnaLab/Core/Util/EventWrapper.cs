using System;
using System.Collections.Generic;

namespace Util {
    /** An EventWrapper is an intermediary between an object which triggers an event and an event
     * handler. The main motivation is to make it easier to unbind event handlers from objects we
     * don't care about anymore (ie. it's difficult (impossible?) if the handler is a lambda).
     *
     * Objects which trigger events must implement the "WrappableEvent" interface below.
     */
    public class EventWrapper<T> {
        // Add handlers using "eventWrapper.Event += <handler>"
        public event EventHandler<T> Event;

        List<WrappableEvent<T>> signallers = new List<WrappableEvent<T>>();

        public void Bind(WrappableEvent<T> signaller) {
            signaller.AddEventHandler(Signalled);
            signallers.Add(signaller);
        }

        public void UnbindAll() {
            foreach (var s in signallers)
                s.RemoveEventHandler(Signalled);
            signallers.Clear();
        }

        void Signalled(object sender, T args) {
            if (Event != null)
                Event(sender, args);
        }
    }

    public interface WrappableEvent<T> {
        void AddEventHandler(EventHandler<T> handler);
        void RemoveEventHandler(EventHandler<T> handler);
    }
}
