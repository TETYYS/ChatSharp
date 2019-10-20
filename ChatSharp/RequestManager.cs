using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSharp
{
    internal class RequestManager
    {
        public RequestManager()
        {
            PendingOperations = new Dictionary<string, RequestOperation>();
        }

        internal Dictionary<string, RequestOperation> PendingOperations { get; private set; }

        public async ValueTask ExecuteOperation(string key, object State)
        {
            RequestOperation req;
            if (PendingOperations.TryGetValue(key, out req))
            {
                await req.Ev.WaitAsync();
            }

            PendingOperations.Add(key, req = new RequestOperation(State));

            await req.Ev.WaitAsync();
        }

        public object GetState(string key)
        {
            var realKey = PendingOperations.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            return PendingOperations[realKey];
        }

        public void CompleteOperation(string key)
        {
            var operation = PendingOperations[key];
            operation.Ev.Set();
            PendingOperations.Remove(key);
        }

        internal class RequestOperation
        {
            public object State { get; set; }
            public AsyncManualResetEvent Ev { get; set; }

            public RequestOperation(object state)
            {
                State = state;
                Ev = new AsyncManualResetEvent(false);
            }
        }
    }
}
