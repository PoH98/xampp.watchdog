using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace Xampp.Watchdog
{

    public class Task
    {
        private readonly object _lock = new object();
        private bool _complete;
        private int _counter;

        public Task AddTask(Action<object> action, object data)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => action(data);
            worker.RunWorkerCompleted += (sender, args) =>
            {
                try
                {
                    Monitor.Enter(_lock);
                    if (--_counter == 0)
                    {
                        Monitor.Pulse(_lock);
                    }
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            };

            try
            {
                Monitor.Enter(_lock);
                if (_complete)
                {
                    throw new Exception("task runner is complete");
                }

                _counter++;
                worker.RunWorkerAsync();
            }
            finally
            {
                Monitor.Exit(_lock);
            }
            return this;
        }

        public void Wait()
        {
            while (!_complete)
            {
                try
                {
                    Monitor.Enter(_lock);

                    if (_counter == 0)
                    {
                        _complete = true;
                        return;
                    }

                    Monitor.Wait(_lock);
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
        }
    }
}
