return function (ctx)
  return function (config)
    local old_mode_kill = nil
    local modes = {}

    local function set_mode(name, params)
      if old_mode_kill ~= nil then
        old_mode_kill()
      end

      if config.on_change then
        config.on_change { name = name, params = params }
      end
      old_mode_kill = ctx.spawn(function ()
        while true do
          local status, err = pcall(modes[name], params)
          if not status then
            ctx.print(err)
            set_mode(config.fallback.name, config.fallback.params or {})
            return
          end

          ctx.sleep(1)
        end
      end)
    end

    local initial = config.initial or config.fallback
    set_mode(initial.name, initial.params or {})

    return setmetatable({}, {
      __index = function (self, name)
        if modes[name] == nil then
          error('no such mode "' .. name .. '"')
        end

        return function (params)
          set_mode(name, params or {})
        end
      end,
      __newindex = function (self, name, cb)
        modes[name] = cb
      end
    })
  end
end
