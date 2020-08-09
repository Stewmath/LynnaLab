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
    public class EventWrapper<TEventSource, TEventArgs> {

        List<Tuple<TEventSource, EventInfo>> signallers = new List<Tuple<TEventSource, EventInfo>>();
        Delegate signalledDelegate;


        public EventWrapper() {
            // Get delegate to Signalled method
            MethodInfo miHandler = typeof(EventWrapper<TEventSource, TEventArgs>).GetMethod("Signalled", BindingFlags.NonPublic | BindingFlags.Instance);
            var tDelegate = typeof(EventHandler<TEventArgs>);
            signalledDelegate = Delegate.CreateDelegate(tDelegate, this, miHandler);
        }


        /// Add handlers using "eventWrapper.Event += <handler>"
        public event EventHandler<TEventArgs> Event;


        /// Pass an object along with the name of the event to subscribe to.
        public void Bind(TEventSource obj, string eventName) {
            EventInfo ev = obj.GetType().GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
            ev.GetAddMethod().Invoke(obj, new object[] { signalledDelegate });

            signallers.Add(new Tuple<TEventSource, EventInfo>(obj, ev));
        }

        /// Unsubscribe from all events.
        public void UnbindAll() {
            foreach (var pair in signallers)
                pair.Item2.GetRemoveMethod().Invoke(pair.Item1, new object[] { signalledDelegate });
            signallers.Clear();
        }

        void Signalled(object sender, TEventArgs args) {
            if (Event != null)
                Event(sender, args);
        }
    }

    /// Like EventWrapper but events are "weak" (does not prevent GC from cleaning up the handler's
    /// instance).
    public class WeakEventWrapper<TEventSource, TEventArgs> where TEventSource : class {

        List<WeakEventBinder<TEventSource, TEventArgs>> signallers = new List<WeakEventBinder<TEventSource, TEventArgs>>();

        // Need a reference to the Signalled function that's always the same object? (for
        // WeakEventManager)
        readonly EventHandler<TEventArgs> signalled;

        public WeakEventWrapper() {
            signalled = Signalled;
        }


        /// Add handlers using "eventWrapper.Event += <handler>"
        public event EventHandler<TEventArgs> Event;


        /// Pass an object along with the name of the event to subscribe to.
        public void Bind(TEventSource obj, string eventName) {
            var e = new WeakEventBinder<TEventSource, TEventArgs>(obj, eventName, signalled);
            signallers.Add(e);
        }

        /// Unsubscribe from all events.
        public void UnbindAll() {
            foreach (var e in signallers)
                e.RemoveHandler();
            signallers.Clear();
        }

        void Signalled(object sender, TEventArgs args) {
            if (Event != null)
                Event(sender, args);
        }
    }


    /// This is sort of a substitute for .NET's WeakEventManager class. Seems to not be available in
    /// Mono.
    ///
    /// It was initially intended to be public, but it turned out to be really similar to
    /// WeakEventWrapper so it's private now...
    ///
    /// In a nutshell: allows use of events using "weak pointers", ie. installing an event callback
    /// does not prevent the object with the handler from being garbage collected.
    ///
    /// Simply creating a new instance of the object will tie the event to the handler.
    ///
    /// NOTE NOTE NOTE CAVEAT CAVEAT CAVEAT: Calls to the "AddHandler" function MUST be done with an
    /// object of type "EventHandler<TEventArgs>", NOT with "FunctionName". It seems like in the
    /// latter case, a temporary delegate is created, which is immediately deleted, and since this
    /// uses a weak reference, that ruins everything... or something like that.
    class WeakEventBinder<TEventSource, TEventArgs> where TEventSource : class {

        EventHandler<TEventArgs> intermediateHandler = null;

        WeakReference<TEventSource> source;
        WeakReference<EventHandler<TEventArgs>> handler;
        EventInfo eventInfo;

        public WeakEventBinder(TEventSource source, string eventName, EventHandler<TEventArgs> handler) {
            this.source = new WeakReference<TEventSource>(source);
            this.handler = new WeakReference<EventHandler<TEventArgs>>(handler);

            // Could add BindingFlag.NonInstance, but there's no benefit to using this class in that
            // case? (since the whole point is to allow the GC to free the object)
            eventInfo = typeof(TEventSource).GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);

            AddHandler(source);
        }


        void AddHandler(TEventSource source) {
            // Create intermediate callback
            intermediateHandler = (sender, args) => {
                EventHandler<TEventArgs> h;
                handler.TryGetTarget(out h);

                if (h != null) // handler object has not been freed
                    h(sender, args);
                else
                    RemoveHandler();
            };

            eventInfo.GetAddMethod().Invoke(source, new object[] { intermediateHandler });
        }


        public void RemoveHandler() {
            TEventSource s;
            if (source.TryGetTarget(out s))
                eventInfo.GetRemoveMethod().Invoke(s, new object[] { intermediateHandler });
        }
    }
}
