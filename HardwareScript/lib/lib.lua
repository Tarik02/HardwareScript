local create_scheduler = require 'lib.scheduler'
local dump = require 'lib.dump'
local json = require 'json'

local run, spawn, sleep = create_scheduler()

local serialized_state = hw.get_state() or '{}'
local state = json.parse(serialized_state)

local clamp = function (value, low, high)
  if low ~= nil and value < low then
    return low
  end
  if high ~= nil and value > high then
    return high
  end
  return value
end

local round = function (x)
  return x >= 0 and math.floor(x + 0.5) or math.ceil(x - 0.5)
end

local linmap = function (value, source_a, source_b, target_a, target_b)
  if value < source_a then
    return target_a
  end
  if value > source_b then
    return target_b
  end
  return target_a + (target_b - target_a) * (value - source_a) / (source_b - source_a)
end

local slice = function (source, from, to)
  local res = {}
  for i = from, to do
    res[#res+1] = source[i]
  end
  return res
end

local times = function (source, times)
  local res = {}
  if type(source) == 'function' then
    for i = 1, times do
      res[#res+1] = source(i)
    end
  else
    for i = 1, times do
      res[#res+1] = source
    end
  end
  return res
end

local range = function (from, to, step)
  step = step or 1
  return function(_, lastvalue)
    local nextvalue = lastvalue + step
    if step > 0 and nextvalue <= to or step < 0 and nextvalue >= to or
      step == 0
    then
      return nextvalue
    end
  end, nil, from - step
end

local function run_file(file)
  return spawn(function ()
    local t_run, t_spawn, t_sleep = create_scheduler(sleep())

    local events = {}

    events.emit = function (type, event)
      hw.bus_broadcast(json.serialize({ type = type, payload = event }))
    end

    hw.bus_on_event = function (sender, event)
      local res, event = pcall(json.parse, event)
      if not res then
        print(event)
        return
      end
      if event.type == 'emit' then
        return
      end

      if type(events[event.type]) == 'function' then
        t_spawn(function ()
          local status, res = pcall(events[event.type], event.payload)

          if event.id ~= nil then
            if status then
              hw.bus_send(sender, json.serialize({
                id = event.id,
                result = res,
              }))
            else
              hw.bus_send(sender, json.serialize({
                id = event.id,
                error = tostring(res),
              }))
            end
          elseif not status then
            print('Failed to handle event ' .. event.type .. ': ' .. res)
          end
        end)

        t_run(sleep())
      end
    end

    local env = {
      spawn = t_spawn,
      sleep = t_sleep,
      print = function (...)
        local items = { ... }
        for i, item in ipairs(items) do
          if type(item) ~= 'string' then
            items[i] = dump(item)
          end
        end
        print(table.unpack(items))
      end,
      pid = require 'lib.pid',
      sensor = hw.create_sensor,
      control = hw.create_control,
      power_plan = hw.power_plan,
      pcall = pcall,

      state = state,
      events = events,

      slice = slice,
      times = times,
      range = range,
      delta_sleep = function ()
        local prev = t_sleep()
        return function (ms)
          local now = t_sleep(ms)
          local dt = now - prev
          prev = now
          return dt
        end
      end,

      sin = math.sin,
      cos = math.cos,
      min = math.min,
      max = math.max,
      ceil = math.ceil,
      floor = math.floor,
      round = round,
      clamp = clamp,
      linmap = linmap,

      smooth = function (getter, max_diff)
        local current = getter()
        local last_updated = t_sleep()

        return function ()
          local dt = t_sleep() - last_updated
          last_updated = t_sleep()

          if dt > 10 then
            current = getter()
          else
            current = current + clamp((getter() - current) * dt, -max_diff * dt, max_diff * dt)
          end

          return current
        end
      end
    }

    env.openrgb = (require 'lib.openrgb')(env)
    env.modeset = (require 'lib.modeset')(env)

    local contents = hw.readfile(file)
    local status, result = pcall(load, contents, file, 't', env)
    if not status then
      print('error: ' .. result)
      return
    end

    status, result = pcall(result)
    if not status then
      print('error: ' .. result)
      return
    end

    local current_time = sleep(0)
    while true do
      local status, timeout = pcall(t_run, current_time)
      if not status then
        print('error: ' .. timeout)
        return
      end
      if timeout == nil then
        break
      end
      current_time = sleep(timeout)
    end
  end)
end

local prev_mtime = 0
local kill = nil

spawn(function ()
  while true do
    if hw.mtime(hw.file) ~= prev_mtime then
      if kill ~= nil then
        kill()
        kill = nil
      end
      prev_mtime = hw.mtime(hw.file)
      if prev_mtime ~= nil then
        kill = run_file(hw.file)
        print('restarted')
      else
        print('waiting for file "' .. hw.file .. '" to exist')
      end
    end

    local new_serialized_state = json.serialize(state)
    if new_serialized_state ~= serialized_state then
      serialized_state = new_serialized_state
      hw.set_state(serialized_state)
    end

    sleep(1)
  end
end)

return run
