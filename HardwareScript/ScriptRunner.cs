using System.Collections.Concurrent;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using LibreHardwareMonitor.Hardware;
using OpenRGB.NET;
using System.Reflection;

namespace HardwareScript
{
    public class ScriptRunner
    {
        public App App { get; private set; }

        protected Script script;
        protected ScriptDebugger? debugger;
        protected DynValue callback;
        protected long startedAt;

        protected Thread? scriptThread;
        protected EventWaitHandle scriptSleeper = new EventWaitHandle(false, EventResetMode.AutoReset);
        private ConcurrentQueue<Action> runInScriptThreadQueue = new ConcurrentQueue<Action>();
        protected long scriptThreadLastUpdated = 0;

        public bool IsStopping { get; private set; }
        public bool IsRunning { get; private set; }

        public ScriptRunner(App application)
        {
            this.App = application;
        }

        public void Start()
        {
            if (IsRunning) {
                return;
            }
            IsRunning = true;
            IsStopping = false;
            scriptThreadLastUpdated = DateTime.Now.Ticks;
            scriptThread = new Thread(Run);
            scriptThread.Start();
        }

        public void Stop()
        {
            if (IsStopping)
            {
                return;
            }

            IsStopping = true;
            scriptSleeper.Set();
            scriptThread?.Join();
        }

        public void Update()
        {
            if (IsRunning && (DateTime.Now.Ticks - scriptThreadLastUpdated) / TimeSpan.TicksPerSecond > 2) {
                System.Console.WriteLine("Script thread seems to be frozen. Killing");

                IsStopping = true;
                debugger?.Kill();
                scriptThread?.Interrupt();
                scriptThreadLastUpdated = DateTime.Now.Ticks;
                scriptThread?.Join();
                Start();
            }
        }

        public void RunInScriptThread(Action callback)
        {
            runInScriptThreadQueue.Enqueue(callback);
            scriptSleeper.Set();
        }

        protected void Run()
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HardwareScript"));

            try {
                script = new Script();
                debugger = new ScriptDebugger(script);
                script.AttachDebugger(debugger);

                script.Options.ScriptLoader = new EmbeddedResourcesScriptLoader(
                    Assembly.GetExecutingAssembly()
                ) {
                    ModulePaths = new string[] { "?.lua" },
                };

                this.startedAt = DateTime.Now.Ticks;

                var lib = new Table(script);
                lib["file"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HardwareScript", "script.lua");
                lib["create_sensor"] = (Func<string, DynValue>)(name => {
                    return DynValue.NewTable(SensorLua((ISensor) App.HardwareManager.nameToElement[name]));
                });
                lib["create_control"] = (Func<string, DynValue>)(name => {
                    return DynValue.NewTable(ControlLua((ISensor) App.HardwareManager.nameToElement[name]));
                });
                lib["readfile"] = (Func<string, DynValue>)(filename => {
                    return DynValue.NewString(
                        File.ReadAllText(filename)
                    );
                });
                lib["mtime"] = (Func<string, DynValue>)(filename => {
                    if (!File.Exists(filename))
                    {
                        return DynValue.Nil;
                    }
                    return DynValue.NewNumber(
                        new DateTimeOffset(File.GetLastWriteTime(filename)).ToUnixTimeSeconds()
                    );
                });
                lib["power_plan"] = (Func<string, DynValue>)(name => {
                    var powerPlan = PowerPlan.All.First(pp => pp.Name == name);

                    var res = new Table(script);
                    res["activate"] = (Action)(() => {
                        App.RunInMainThread(() => {
                            powerPlan.Activate();
                        });
                    });
                    return DynValue.NewTable(res);
                });
                script.Globals["hw"] = lib;

                App.EventServer!.OnMessage += (sender, message) => {
                    RunInScriptThread(() => {
                        var handler = lib.Get("bus_on_event");
                        if (handler.Type == DataType.Function) {
                            handler.Function.Call(sender, message);
                        }
                    });
                };

                lib["bus_send"] = (Func<string, string, DynValue>)((target, data) => {
                    App.EventServer?.Send(target, data);

                    return DynValue.Nil;
                });

                lib["bus_broadcast"] = (Func<string, DynValue>)(data => {
                    App.EventServer?.Broadcast(data);

                    return DynValue.Nil;
                });

                lib["connect_openrgb"] = (Action<string, int, DynValue>)((ip, port, callback) => {
                    App.RunInMainThread(() =>
                    {
                        try
                        {
                            var client = new OpenRGBClient(ip: ip, port: port, name: "HardwareScript", autoconnect: true);
                            var openrgb = new ScriptOpenrgb(
                                this,
                                client
                            );
                            var devices = client.GetAllControllerData();

                            RunInScriptThread(() => {
                                var data = openrgb.DataToLua(script, devices);

                                callback.Function.Call(
                                    DynValue.Nil,
                                    openrgb.ToLua(script),
                                    data
                                );
                            });
                        }
                        catch (Exception e)
                        {
                            callback.Function.Call(
                                e.Message,
                                DynValue.Nil,
                                DynValue.Nil
                            );
                        }
                    });
                });

                lib["get_state"] = (Func<string?>)(() => {
                    var stateFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HardwareScript", "state.json");

                    if (!File.Exists(stateFile)) {
                        return null;
                    }

                    return File.ReadAllText(stateFile);
                });

                lib["set_state"] = (Action<string>)(text => {
                    var stateFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HardwareScript", "state.json");

                    File.WriteAllText(stateFile, text);
                });

                this.callback = script.RequireModule("lib.lib").Function.Call();

                while (!IsStopping) {
                    try
                    {
                        Action action;
                        while (runInScriptThreadQueue.TryDequeue(out action)) {
                            action();
                        }

                        var res = Tick();
                        if (res == null) {
                            break;
                        }
                        scriptSleeper.WaitOne((int) res);
                    } catch (ThreadInterruptedException) {
                        continue;
                    }
                }
            } finally {
                IsStopping = false;
                IsRunning = false;
            }
        }

        protected int? Tick()
        {
            scriptThreadLastUpdated = DateTime.Now.Ticks;
            var result = this.callback.Function.Call((double)(DateTime.Now.Ticks - startedAt) / TimeSpan.TicksPerMillisecond / 1000);

            if (result.IsNil()) {
                return null;
            }

            return (int)(result.Number * 1000);
        }

        protected Table SensorLua(ISensor sensor)
        {
            var res = new Table(script);
            var meta = res.MetaTable = new Table(script);

            meta["__index"] = (Func<DynValue, string, DynValue>)((_, index) => {
                switch (index) {
                    case "value":
                        return DynValue.NewNumber((double) sensor.Value!);

                    default:
                        return DynValue.Nil;
                }
            });

            return res;
        }

        protected Table ControlLua(ISensor sensor)
        {
            var res = new Table(script);
            var meta = res.MetaTable = new Table(script);

            meta["__index"] = (Func<DynValue, string, DynValue>)((_, index) => {
                switch (index) {
                    case "value":
                        if (sensor.Control.ControlMode == ControlMode.Software) {
                            return DynValue.NewNumber((double) sensor.Control.SoftwareValue);
                        } else {
                            return DynValue.Nil;
                        }

                    case "min":
                        return DynValue.NewNumber((double) sensor.Control.MinSoftwareValue);

                    case "max":
                        return DynValue.NewNumber((double) sensor.Control.MaxSoftwareValue);

                    default:
                        return DynValue.Nil;
                }
            });

            meta["__newindex"] = (Action<DynValue, string, DynValue>)((_, index, value) => {
                switch (index) {
                    case "value":
                        if (value.IsNil()) {
                            App.RunInMainThread(() => {
                                sensor.Control.SetDefault();
                            });
                        } else {
                            float rawValue = (float) (value.CastToNumber()! + Random.Shared.NextDouble() * 2 - 1);

                            App.RunInMainThread(() => {
                                sensor.Control.SetSoftware(
                                    Math.Clamp(
                                        rawValue,
                                        sensor.Control.MinSoftwareValue,
                                        sensor.Control.MaxSoftwareValue
                                    )
                                );
                            });
                        }
                        break;
                }
            });

            return res;
        }
    }
}
