using System;

namespace LynnaLab {
    // Unlike "LockableEvent" class this operates tn the event handlers. Allows one to define a set
    // of handlers which can be "locked" to prevent their execution, and "unlocked" later.
    public class LockableEventGroup {
        int locked = 0;

        event Action unlockEvent;

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
                Action func = null;
                func = () => {
                    unlockEvent -= func;
                    action();
                };
                unlockEvent += func;
            }
        }

        public void Lock() {
            locked++;
        }

        public void Unlock() {
            if (locked == 0)
                throw new Exception("Can't unlock a non-locked LockableEventGroup.");
            locked--;
            if (locked == 0) {
                if (unlockEvent != null)
                    unlockEvent();
            }
        }
    }
}
