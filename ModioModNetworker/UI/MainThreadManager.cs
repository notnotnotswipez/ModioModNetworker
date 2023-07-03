using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModioModNetworker.UI
{
    public class MainThreadManager
    {
        private static ConcurrentQueue<GenericThreadingJob> genericThreadJobs = new ConcurrentQueue<GenericThreadingJob>();

        public static void HandleQueue()
        {
            if (genericThreadJobs.Count > 0)
            {
                GenericThreadingJob request;
                if (genericThreadJobs.TryDequeue(out request))
                {
                    request.action.Invoke();
                }
            }
        }

        public static void QueueAction(Action action) {
            genericThreadJobs.Enqueue(new GenericThreadingJob()
            {
                action = action
            });
        }
    }

    public class GenericThreadingJob
    {
        public Action action;
    }
}
