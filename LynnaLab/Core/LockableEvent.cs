using System;

namespace LynnaLab {
    // This is just like an Event, but you can "Lock" it to pause callbacks, and "Unlock" it to
    // resume them. Useful for doing atomic operations.
    // NOTE: This assumes that only the most recent event matters. If multiple events occur while
    // locked, all but the last one will be skipped.
    public class LockableEvent<T> {
        public delegate void Handler(object sender, T args);


        Handler handler;
        int locked = 0;
        bool missedInvoke = false;
        object invokeSender;
        T invokeParam;

        public LockableEvent() {
        }

        public void Invoke(object sender, T args) {
            if (locked != 0) {
                missedInvoke = true;
                invokeSender = sender;
                invokeParam = args;
            }
            else {
                if (handler != null)
                    handler(sender, args);
            }
        }

        public void Lock() {
            locked++;
        }

        public void Unlock() {
            if (locked == 0)
                throw new Exception("Called Unlock on an already unlocked LockableEvent.");
            locked--;
            if (locked == 0 && missedInvoke) {
                Invoke(invokeSender, invokeParam);
                missedInvoke = false;
            }
        }

        // If an event triggered while locked, this clears the event to prevent it from running when
        // Unlock() is called.
        public void Clear() {
            missedInvoke = false;
        }


        // Static methods

        public static LockableEvent<T> operator+(LockableEvent<T> ev, Handler handler) {
            ev.handler += handler;
            return ev;
        }

        public static LockableEvent<T> operator-(LockableEvent<T> ev, Handler handler) {
            ev.handler -= handler;
            return ev;
        }


        // Specific operator+ for EventHandler. This is hacky.
        public static LockableEvent<T> operator+(LockableEvent<T> ev, EventHandler handler) {
            ev.handler += (o, a) => handler(o, a as EventArgs);
            return ev;
        }

        public static LockableEvent<T> operator-(LockableEvent<T> ev, EventHandler handler) {
            ev.handler -= (o, a) => handler(o, a as EventArgs); // TODO: this probably doesn't work
            return ev;
        }
    }
}
