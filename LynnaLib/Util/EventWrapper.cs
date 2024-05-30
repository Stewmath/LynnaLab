using System;
using System.Collections.Generic;
using System.Reflection;

namespace Util
{
    /// Allows one to create weak event handlers for a specific class. Weak events don't prevent the
    /// object receiving the event callback from being garbage collected.
    ///
    /// The instance of the "event source" class may be replaced at any time, and the events will be
    /// updated accordingly to fire on the new instance.
    ///
    /// NOTE: When this object is freed, the events may cease to fire (garbage collection kicks in
    /// due to weak references). Must maintain a reference to it as long as the events are relevant.
    public class WeakEventWrapper<TEventSource> where TEventSource : class
    {
        List<EventStruct> eventList = new List<EventStruct>();
        TEventSource eventSource;

        public WeakEventWrapper(TEventSource eventSource = null)
        {
            this.eventSource = eventSource;
        }


        /// Pass an event name that's in "TEventSource" and the handler for the event.
        public void Bind<TEventArgs>(string eventName, EventHandler<TEventArgs> handler)
        {
            EventStruct ev = new EventStruct();

            Action addHandlerMethod = () =>
            {
                var signaller = new WeakEventBinder<TEventSource, TEventArgs>(eventSource, ev.eventName, handler);
                ev.removeHandlerMethod = signaller.RemoveHandler;
            };

            ev.eventName = eventName;
            ev.addHandlerMethod = addHandlerMethod;

            if (eventSource != null)
                addHandlerMethod();

            eventList.Add(ev);
        }

        public void ReplaceEventSource(TEventSource source)
        {
            foreach (var ev in eventList)
            {
                ev.removeHandlerMethod?.Invoke();
                ev.removeHandlerMethod = null;
            }

            this.eventSource = source;

            if (source != null)
            {
                foreach (var ev in eventList)
                {
                    ev.addHandlerMethod();
                }
            }
        }

        /// Unsubscribe from all events.
        public void UnbindAll()
        {
            foreach (var ep in eventList)
                ep.removeHandlerMethod?.Invoke();
            eventList.Clear();
        }



        class EventStruct
        {
            public string eventName;
            public Action addHandlerMethod;
            public Action removeHandlerMethod;
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
    /// Simply creating a new instance of the object will tie the event to the handler. It shouldn't
    /// be necessary to explicitly maintain a reference to this object, since the event in the
    /// TEventSource object maintains a strong event link to this class.
    ///
    /// NOTE NOTE NOTE CAVEAT CAVEAT CAVEAT: Calls to the "AddHandler" function MUST be done with an
    /// object of type "EventHandler<TEventArgs>", NOT with "FunctionName". It seems like in the
    /// latter case, a temporary delegate is created, which is immediately deleted, and since this
    /// uses a weak reference, that ruins everything... or something like that.
    class WeakEventBinder<TEventSource, TEventArgs> where TEventSource : class
    {

        EventHandler<TEventArgs> intermediateHandler = null;

        WeakReference<TEventSource> source;
        WeakReference<EventHandler<TEventArgs>> handler;
        EventInfo eventInfo;

        public WeakEventBinder(TEventSource source, string eventName, EventHandler<TEventArgs> handler)
        {
            this.source = new WeakReference<TEventSource>(source);
            this.handler = new WeakReference<EventHandler<TEventArgs>>(handler);

            // Could add BindingFlag.NonInstance, but there's no benefit to using this class in that
            // case? (since the whole point is to allow the GC to free the object)
            eventInfo = typeof(TEventSource).GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);

            if (eventInfo == null)
                throw new ArgumentException("WeakEventBinder: Couldn't locate event \"" + eventName + "\".");

            AddHandler(source);
        }


        void AddHandler(TEventSource tangibleSource)
        {
            // Create intermediate callback
            intermediateHandler = (sender, args) =>
            {
                EventHandler<TEventArgs> h;
                handler.TryGetTarget(out h);

                if (h != null) // handler object has not been freed
                    h(sender, args);
                else
                    RemoveHandler();
            };

            eventInfo.GetAddMethod().Invoke(tangibleSource, new object[] { intermediateHandler });
        }


        public void RemoveHandler()
        {
            TEventSource s;
            if (source.TryGetTarget(out s))
                eventInfo.GetRemoveMethod().Invoke(s, new object[] { intermediateHandler });
        }
    }
}
