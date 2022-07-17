local lazy = require 'lib.lazy'

return function (ctx)
  return function (config)
    local co = coroutine.running()

    hw.connect_openrgb(config.ip or '127.0.0.1', config.port or 6742, function (...)
      coroutine.resume(co, ...)
    end)

    local err, client, data = coroutine.yield()
    if err ~= nil then
      error(err)
    end

    return setmetatable({
      device = function (name)
        local device_data = data.devices[name] or error('no such OpenRGB device "' .. name .. '"')

        return setmetatable({
          set_mode = function (mode_name, params)
            ctx.spawn(function ()
              if device_data.modes[mode_name] == nil then
                error('no such mode "' .. mode_name .. '" in device "' .. name .. '"')
              end

              client.set_device_mode(
                device_data.id,
                device_data.modes[mode_name].id,
                (params or {}).speed,
                (params or {}).direction,
                (params or {}).colors
              )
            end)
          end
        }, {
          __newindex = function (self, index, value)
            if index == 'colors' then
              ctx.spawn(function ()
                client.set_device_custom_mode(device_data.id)
                client.set_device_colors(device_data.id, value)
              end)
            end
          end,
          __gc = function (self)
            client.disconnect()
          end,
        })
      end
    }, {
      __index = function (self, index)
        if index == 'connected' then
          return client.connected()
        end
      end,
    })
  end
end
