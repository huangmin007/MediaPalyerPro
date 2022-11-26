using System;
using System.Collections.Generic;
using System.Threading;

namespace Sttplay.ThreadPool
{
    public class Worker
    {
        private MiniThreadPool threadPool;
        private Thread thread;
        public Worker(MiniThreadPool pool, Action<object> threadcb)
        {
            threadPool = pool;
            thread = new Thread(new ParameterizedThreadStart(threadcb));
            thread.Start(this);
        }
        public MiniThreadPool GetPool()
        {
            return threadPool;
        }

        public void WaitRunComplete()
        {
            thread.Join();
        }
       
    }
    public class Job
    {
        private object param;
        private Action<object> func;
        public Job(Action<object> func, object param)
        {
            this.func = func;
            this.param = param;
        }
        public void Run()
        {
            func(param);
        }
    }
    public class MiniThreadPool
    {
        private int threadCount;
        private List<Worker> workers = new List<Worker>();
        private List<Job> jobs = new List<Job>();
        private readonly Object poolLock = new Object();
        private bool terminate;
        private bool drop;
        public MiniThreadPool(int threadCount)
        {
            if (threadCount < 1) threadCount = 1;
            this.threadCount = threadCount;
            this.terminate = false;
            this.drop = false;

            for (int i = 0; i < threadCount; i++)
            {
                Worker worker = new Worker(this, ThreadCallback);
            }
        }

        public void Close(bool drop)
        {
            this.drop = drop;
            Monitor.Enter(poolLock);
            terminate = true;
            Monitor.PulseAll(poolLock);
            Monitor.Exit(poolLock);
        }

        public void Release()
        {
            Close(drop);
            for (int i = 0; i < workers.Count; i++)
                workers[i].WaitRunComplete();
        }

        public bool PushJob(Action<object> func, object param)
        {
            bool ret = false;
            Monitor.Enter(poolLock);
            if(!terminate)
            {
                Job job = new Job(func, param);
                jobs.Add(job);
                Monitor.Pulse(poolLock);
            }
            Monitor.Exit(poolLock);
            return ret;
        }

        public int TaskCount()
        {
            return jobs.Count;
        }
        public static void ThreadCallback(object obj)
        {
            Worker worker = (Worker)obj;
            MiniThreadPool pool = worker.GetPool();
            while (true)
            {
                Monitor.Enter(pool.poolLock);
                while (pool.jobs.Count <= 0)
                {
                    if (pool.terminate)
                        break;
                    Monitor.Wait(pool.poolLock);
                }
                if (pool.terminate)
                {
                    Monitor.Exit(pool.poolLock);
                    break;
                }
                Job job = pool.jobs[0];
                pool.jobs.RemoveAt(0);
                Monitor.Exit(pool.poolLock);
                job.Run();
            }
            if (!pool.drop)
            {
                while(true)
                {
                    Monitor.Enter(pool.poolLock);
                    if(pool.jobs.Count <= 0)
                    {
                        Monitor.Exit(pool.poolLock);
                        break;
                    }
                    Job job = pool.jobs[0];
                    pool.jobs.RemoveAt(0);
                    Monitor.Exit(pool.poolLock);
                    job.Run();
                }
            }
        }
    }
}
