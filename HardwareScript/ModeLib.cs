using MoonSharp.Interpreter;

namespace HardwareScript
{
    [MoonSharpUserData]
    public class ModeLib
    {
        public long time
        {
            get {
                return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }
        }
    }
}
