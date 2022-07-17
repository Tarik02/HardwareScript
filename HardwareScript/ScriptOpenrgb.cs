using OpenRGB.NET;
using OpenRGB.NET.Enums;
using OpenRGB.NET.Models;
using MoonSharp.Interpreter;
using System.Net.Sockets;

namespace HardwareScript
{
    class ScriptOpenrgb
    {
        protected ScriptRunner runner;

        protected OpenRGBClient client;

        public ScriptOpenrgb(ScriptRunner runner, OpenRGBClient client)
        {
            this.runner = runner;
            this.client = client;
        }

        protected void WithConnection(Action<OpenRGBClient> callback)
        {
            runner.App.RunInMainThread(() => {
                if (!client.Connected)
                {
                    return;
                }

                try
                {
                    callback(client);
                }
                catch (Exception e)
                {
                    if (!(e is IOException || e is SocketException))
                    {
                        System.Console.Write(e);
                    }
                }
            });
        }

        public DynValue ToLua(Script script)
        {
            var lua_client = new Table(script);

            lua_client["set_device_custom_mode"] = (Action<int>)(deviceId => {
                WithConnection(client => client.SetCustomMode(deviceId));
            });

            lua_client["set_device_colors"] = (Action<int, Table>)((deviceId, lua_colors) => {
                var colors = lua_colors.Values
                    .Select(
                        lua_color => {
                            return new OpenRGB.NET.Models.Color(
                                (byte) lua_color.Table.Get("r").Number,
                                (byte) lua_color.Table.Get("g").Number,
                                (byte) lua_color.Table.Get("b").Number
                            );
                        }
                    )
                    .ToArray();

                WithConnection(client => client.UpdateLeds(deviceId, colors));
            });

            lua_client["set_device_mode"] = (Action<int, int, uint?, string?, Table?>)((deviceId, modeId, speed, direction, lua_colors) => {
                var colors = lua_colors?.Values
                    .Select(
                        lua_color => {
                            return new OpenRGB.NET.Models.Color(
                                (byte) lua_color.Table.Get("r").Number,
                                (byte) lua_color.Table.Get("g").Number,
                                (byte) lua_color.Table.Get("b").Number
                            );
                        }
                    )
                    .ToArray();

                WithConnection(client => client.SetMode(
                    deviceId,
                    modeId,
                    speed,
                    direction != null ? stringToDirection(direction) : null,
                    colors
                ));
            });

            lua_client["connected"] = (Func<bool>)(() => client.Connected);

            lua_client["disconnect"] = (Action)(() => {
                client.Dispose();
            });

            return DynValue.NewTable(lua_client);
        }
        
        public DynValue DataToLua(Script script, Device[] devices)
        {
            var lua_devices = new Table(script);

            var id = 0;
            foreach (var device in devices)
            {
                var lua_device = new Table(script);
                lua_device["id"] = id++;
                lua_device["type"] = device.Type.ToString().ToLower();
                lua_device["vendor"] = device.Vendor;
                lua_device["description"] = device.Description;
                lua_device["version"] = device.Version;
                lua_device["serial"] = device.Serial;
                lua_device["location"] = device.Location;
                lua_devices[device.Name] = lua_device;

                var lua_device_modes = new Table(script);
                var modeId = 0;
                foreach (var mode in device.Modes)
                {
                    var lua_device_mode = new Table(script);
                    lua_device_mode["id"] = modeId++;

                    var lua_device_mode_flags = new Table(script);
                    lua_device_mode_flags["has_speed"] = mode.HasFlag(ModeFlags.HasSpeed);
                    lua_device_mode_flags["has_direction_lr"] = mode.HasFlag(ModeFlags.HasDirectionLR);
                    lua_device_mode_flags["has_direction_ud"] = mode.HasFlag(ModeFlags.HasDirectionUD);
                    lua_device_mode_flags["has_direction_hv"] = mode.HasFlag(ModeFlags.HasDirectionHV);
                    lua_device_mode_flags["has_brightness"] = mode.HasFlag(ModeFlags.HasBrightness);
                    lua_device_mode_flags["has_per_led_color"] = mode.HasFlag(ModeFlags.HasPerLedColor);
                    lua_device_mode_flags["has_mode_specific_color"] = mode.HasFlag(ModeFlags.HasModeSpecificColor);
                    lua_device_mode_flags["has_random_color"] = mode.HasFlag(ModeFlags.HasRandomColor);
                    lua_device_mode_flags["has_direction"] = mode.HasFlag(ModeFlags.HasDirection);
                    lua_device_mode["flags"] = lua_device_mode_flags;

                    lua_device_mode["speed_min"] = mode.SpeedMin;
                    lua_device_mode["speed_max"] = mode.SpeedMax;
                    lua_device_modes[mode.Name] = lua_device_mode;
                }
                lua_device["modes"] = lua_device_modes;
            }

            var result = new Table(script);
            result["devices"] = lua_devices;

            return DynValue.NewTable(result);
        }

        protected static Direction stringToDirection(string direction)
        {
            switch (direction) {
                case "none":
                    return Direction.None;
                case "left":
                    return Direction.Left;
                case "right":
                    return Direction.Right;
                case "up":
                    return Direction.Up;
                case "down":
                    return Direction.Down;
                case "horizontal":
                    return Direction.Horizontal;
                case "vertical":
                    return Direction.Vertical;
                default:
                    throw new ArgumentException($"String \"{direction}\" cannot be converted to direction");
            }
        }
    }
}
