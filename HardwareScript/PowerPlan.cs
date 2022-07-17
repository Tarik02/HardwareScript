using System.Runtime.InteropServices;

namespace HardwareScript
{
    public class PowerPlan
    {
        [DllImport("PowrProf.dll")]
        private static extern UInt32 PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, UInt32 AcessFlags, UInt32 Index, ref Guid Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll")]
        private static extern UInt32 PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)]
        private static extern UInt32 PowerSetActiveScheme(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid);

        private enum AccessFlags : uint
        {
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18
        }

        public static IEnumerable<PowerPlan> All
        {
            get
            {
                var schemeGuid = Guid.Empty;

                uint sizeSchemeGuid = (uint)Marshal.SizeOf(typeof(Guid));
                uint schemeIndex = 0;

                while (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid) == 0)
                {
                    yield return new PowerPlan(schemeGuid);
                    ++schemeIndex;
                }
            }
        }

        public Guid Guid { get; }

        public string Name
        {
            get
            {
                uint sizeName = 1024;
                IntPtr pSizeName = Marshal.AllocHGlobal((int)sizeName);

                string friendlyName;

                try
                {
                    var schemeGuid = Guid;
                    PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pSizeName, ref sizeName);
                    friendlyName = Marshal.PtrToStringUni(pSizeName)!;
                }
                finally
                {
                    Marshal.FreeHGlobal(pSizeName);
                }

                return friendlyName;
            }
        }

        public PowerPlan(Guid guid)
        {
            this.Guid = guid;
        }

        public void Activate()
        {
            PowerSetActiveScheme(
                IntPtr.Zero,
                Guid
            );
        }
    }
}
