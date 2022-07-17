using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace HardwareScript
{
    public class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        static void Main()
        {
            if (!IsAdministrator())
            {
                var exeName = Process.GetCurrentProcess().MainModule!.FileName!;
                ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
                startInfo.Verb = "runas";
                startInfo.Arguments = "restart";
                Process.Start(startInfo);
                Application.Exit();
            }

            CreateSchedulerTask();

            var hwnd = GetConsoleWindow();
            ShowWindow(hwnd, SW_HIDE);

            System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = ci;

            var notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            notifyIcon.Visible = true;
            notifyIcon.Text = Application.ProductName;

            var contextMenu = new ContextMenuStrip();

            var autoRunMenuItem = new ToolStripButton("Start with Windows");
            autoRunMenuItem.Checked = CheckSchedulerTask();
            contextMenu.Items.Add(autoRunMenuItem);
            autoRunMenuItem.Click += (s, e) => {
                if (autoRunMenuItem.Checked) {
                    RemoveSchedulerTask();
                } else {
                    CreateSchedulerTask();
                }
                autoRunMenuItem.Checked = CheckSchedulerTask();
            };

            contextMenu.Items.Add("Show/Hide", null, (s, e) => {
                var hwnd = GetConsoleWindow();
                if (IsWindowVisible(hwnd)) {
                    ShowWindow(hwnd, SW_HIDE);
                } else {
                    ShowWindow(hwnd, SW_SHOW);
                }
            });
            contextMenu.Items.Add("Exit", null, (s, e) => {
                Application.Exit();
            });
            notifyIcon.ContextMenuStrip = contextMenu;

            var app = new App();

            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsPsuEnabled = true,
                IsBatteryEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };

            computer.Open();

            app.HardwareManager = new HardwareManager(computer);
            app.EventServer = new EventServer();

            app.HardwareManager.Open();
            app.EventServer.Start();

            app.ScriptRunner = new ScriptRunner(app);
            app.ScriptRunner.Start();

            Thread? updateThread = null;
            Thread? appThread = null;

            try {
                computer.Accept(new UpdateVisitor());

                updateThread = new Thread(() => {
                    while (app.IsRunning) {
                        computer.Accept(new UpdateVisitor());
                        try {
                            Thread.Sleep(1000);
                        } catch (ThreadInterruptedException) {
                            return;
                        }
                    }
                });
                updateThread.Start();

                appThread = new Thread(() => {
                    app.Run();
                });
                appThread.Start();

                Application.Run();
            } finally {
                app.Stop();

                updateThread?.Interrupt();

                appThread?.Join();
                updateThread?.Join();
            }
        }

        public static bool CheckSchedulerTask()
        {
            using (TaskService ts = new TaskService())
            {
                return ts.RootFolder.GetTasks(
                    new Regex(@"^HardwareScript$")
                ).Count > 0;
            }
        }

        public static void CreateSchedulerTask()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            using (TaskService ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();

                td.Triggers.Add(new LogonTrigger() {
                    UserId = identity.Name,
                });

                td.Actions.Add(
                    new ExecAction(
                        Application.ExecutablePath,
                        ""
                    )
                );

                td.Settings.RestartInterval = TimeSpan.FromMinutes(1);
                td.Settings.RestartCount = 3;
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;
                td.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                td.Principal.RunLevel = TaskRunLevel.Highest;

                ts.RootFolder.RegisterTaskDefinition(@"HardwareScript", td, TaskCreation.CreateOrUpdate, identity.Name, null, TaskLogonType.InteractiveToken);
            }
        }

        public static void RemoveSchedulerTask()
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(@"HardwareScript");
            }
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
