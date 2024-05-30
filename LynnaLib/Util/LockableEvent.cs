using System;
using System.Collections.Generic;

namespace Util
{
    // This is just like an Event, but you can "Lock" it to pause callbacks, and "Unlock" it to
    // resume them. Useful for doing atomic operations.
    public class LockableEvent<T>
    {
        EventHandler<T> handler;

        int locked = 0;

        List<Tuple<object, T>> savedInvokes = new List<Tuple<object, T>>();

        public LockableEvent()
        {
        }

        public void Invoke(object sender, T args)
        {
            if (locked != 0)
            {
                savedInvokes.Add(new Tuple<object, T>(sender, args));
            }
            else
            {
                if (handler != null)
                    handler(sender, args);
            }
        }

        public void Lock()
        {
            locked++;
        }

        public void Unlock()
        {
            if (locked == 0)
                throw new Exception("Called Unlock on an already unlocked LockableEvent.");
            locked--;
            if (locked == 0)
            {
                foreach (var t in savedInvokes)
                {
                    if (handler != null)
                        handler(t.Item1, t.Item2);
                }
                savedInvokes.Clear();
            }
        }

        // If an event triggered while locked, this clears the event to prevent it from running when
        // Unlock() is called.
        public void Clear()
        {
            savedInvokes.Clear();
        }


        // Static methods

        public static LockableEvent<T> operator +(LockableEvent<T> ev, EventHandler<T> handler)
        {
            ev.handler += handler;
            return ev;
        }

        public static LockableEvent<T> operator -(LockableEvent<T> ev, EventHandler<T> handler)
        {
            ev.handler -= handler;
            return ev;
        }
    }
}
