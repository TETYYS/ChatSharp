using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChatSharp
{
    internal class RequestManager
    {
        public RequestManager()
        {
            PendingOperations = new ConcurrentDictionary<string, RequestOperation>();
        }

        internal ConcurrentDictionary<string, RequestOperation> PendingOperations { get; private set; }

        public async ValueTask ExecuteOperation(string key, object State)
        {
            RequestOperation req;
            if (PendingOperations.TryGetValue(key, out req))
            {
                Interlocked.Increment(ref req.RefCount);
                await req.Ev.WaitAsync();
                return;
            }

            if (!PendingOperations.TryAdd(key, req = new RequestOperation(State)))
            {
                throw new ArgumentException("An item with the same key has already been added");
            }

            await req.Ev.WaitAsync();
        }

        public object GetState(string key)
        {
            var realKey = PendingOperations.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            return PendingOperations[realKey].State;
        }

        public void CompleteOperation(string key)
        {
            var operation = PendingOperations[key];
            if (operation.RefCount > 0)
                Interlocked.Decrement(ref operation.RefCount);
            else
                PendingOperations.TryRemove(key, out var _);
            
            operation.Ev.Set();
        }

        internal class RequestOperation
        {
            public object State { get; set; }
            public AsyncManualResetEvent Ev { get; set; }
            public int RefCount = 1;

            public RequestOperation(object state)
            {
                State = state;
                Ev = new AsyncManualResetEvent(false);
            }
        }
    }
}
