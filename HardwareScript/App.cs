using System.Collections.Concurrent;

namespace HardwareScript
{
    public class App
    {
        private Thread mainThread;
        private EventWaitHandle mainThreadSleeper = new EventWaitHandle(false, EventResetMode.AutoReset);
        private ConcurrentQueue<Action> runInMainThreadQueue = new ConcurrentQueue<Action>();

        public HardwareManager HardwareManager;
        public EventServer EventServer;
        public ScriptRunner ScriptRunner;

        public bool IsRunning { get; private set; } = true;

        public App()
        {
            IsRunning = true;
        }

        public void Run()
        {
            mainThread = Thread.CurrentThread;

            while (IsRunning) {
                Action action;
                while (runInMainThreadQueue.TryDequeue(out action)) {
                    action();
                }

                ScriptRunner.Update();

                try {
                    mainThreadSleeper.WaitOne(1000);
                } catch (ThreadInterruptedException) {
                    continue;
                }
            }
        }

        public void RunInMainThread(Action callback)
        {
            runInMainThreadQueue.Enqueue(callback);
            mainThreadSleeper.Set();
        }

        public void Stop()
        {
            IsRunning = false;
            mainThreadSleeper.Set();
            ScriptRunner.Stop();
        }
    }
}
