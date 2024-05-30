using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Util {
    // Unlike "LockableEvent" class this operates tn the event handlers. Allows one to define a set
    // of handlers which can be "locked" to prevent their execution, and "unlocked" later.
    public class LockableEventGroup {
        int locked = 0;

        event Action unlockEvent;
        List<Action> handlers = new List<Action>();
        List<int> unlockLevels = new List<int>();

        public EventHandler<T> Add<T>(EventHandler<T> handler) {
            EventHandler<T> newHandler = (sender, args) => {
                Invoke(() => {
                    handler(sender, args);
                });
            };

            return newHandler;
        }

        public EventHandler Add(EventHandler handler) {
            return new EventHandler(Add<EventArgs>(new EventHandler<EventArgs>(handler)));
        }

        public void Invoke(Action action) {
            if (locked == 0)
                action();
            else {
                AddHandler(action);
            }
        }

        public void Lock() {
            locked++;
            unlockLevels.Add(handlers.Count);
        }

        public void Unlock() {
            if (locked == 0)
                throw new Exception("Can't unlock a non-locked LockableEventGroup.");
            locked--;
            unlockLevels.RemoveAt(unlockLevels.Count-1);
            Debug.Assert(locked == unlockLevels.Count);
            if (locked == 0) {
                if (unlockEvent != null)
                    unlockEvent();
            }
        }

        // Unlocks and clears all events that would have triggered since the last "Lock()".
        public void UnlockAndClear() {
            int firstHandler = unlockLevels[locked-1];
            for (int i=firstHandler; i < handlers.Count; i++)
                unlockEvent -= handlers[i];
            handlers.RemoveRange(firstHandler, handlers.Count - firstHandler);
            Unlock();
        }


        void AddHandler(Action handler) {
            Action func = null;
            func = () => {
                unlockEvent -= func;
                handler();
            };
            unlockEvent += func;
            handlers.Add(func);
        }
    }
}
