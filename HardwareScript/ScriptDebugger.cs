using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;

namespace HardwareScript
{
    public class ScriptDebugger : IDebugger
    {
        private Script script;
        private bool shouldStop = false;

        public ScriptDebugger(Script script)
        {
            this.script = script;
        }

        public void Kill()
        {
            shouldStop = true;
        }

        public void Reset()
        {
            shouldStop = false;
        }

        public DebuggerAction GetAction(int ip, SourceRef sourceref)
        {
            if (shouldStop)
            {
                throw new ThreadInterruptedException();
            }

            return new DebuggerAction() {
                Action = DebuggerAction.ActionType.Run,
            };
        }

        public DebuggerCaps GetDebuggerCaps()
        {
            return 0;
        }

        public List<DynamicExpression> GetWatchItems()
        {
            return new List<DynamicExpression>();
        }

        public bool IsPauseRequested()
        {
            return shouldStop;
        }

        public void RefreshBreakpoints(IEnumerable<SourceRef> refs)
        {
            //
        }

        public void SetByteCode(string[] byteCode)
        {
            //
        }

        public void SetDebugService(DebugService debugService)
        {
            //
        }

        public void SetSourceCode(SourceCode sourceCode)
        {
            //
        }

        public void SignalExecutionEnded()
        {
            //
        }

        public bool SignalRuntimeException(ScriptRuntimeException ex)
        {
            return false;
        }

        public void Update(WatchType watchType, IEnumerable<WatchItem> items)
        {
            //
        }
    }
}
