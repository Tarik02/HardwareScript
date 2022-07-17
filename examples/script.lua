local balanced = power_plan 'Збалансований'
local power_save = power_plan 'Економія енергії'
local high_performance = power_plan 'Висока продуктивність'


local cached_rgb = nil
local get_rgb = function ()
  if cached_rgb ~= nil and cached_rgb.dev.connected then
    return cached_rgb
  end

  local dev = openrgb { ip = '127.0.0.1', port = 6742 }

  cached_rgb = {
    dev = dev,
    gpu = dev.device 'ASUS TUF RTX 3080 O10G V2 GAMING',
    mouse = dev.device 'Razer Basilisk V2',
    motherboard = dev.device 'B550 GAMING X V2',
  }
  return cached_rgb
end

local case = {
  fans = {
    back_fan = sensor 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Fans / Fan #3',
    top_back_fan = sensor 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Fans / Fan #2',
    top_front_fan = sensor 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Fans / Fan #5',
    front_fan = sensor 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Fans / Fan #4',
  },

  controls = {
    back_fan = control 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Controls / Fan Control #3',
    top_back_fan = control 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Controls / Fan Control #2',
    top_front_fan = control 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Controls / Fan Control #5',
    front_fan = control 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Controls / Fan Control #4',
  },
}

local cpu = {
  powers = {
    package = sensor 'AMD Ryzen 5 5600X / Powers / Package',
  },

  temp = {
    core = sensor 'AMD Ryzen 5 5600X / Temperatures / Core (Tctl/Tdie)',
    ccd1 = sensor 'AMD Ryzen 5 5600X / Temperatures / CCD1 (Tdie)',
  },

  fans = {
    fan = sensor 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Fans / Fan #1',
  },

  controls = {
    fan = control 'Gigabyte B550 GAMING X V2 / ITE IT8688E / Controls / Fan Control #1',
  },
}

local gpu = {
  temp = {
    core = sensor 'NVIDIA GeForce RTX 3080 / Temperatures / GPU Core',
    hot_spot = sensor 'NVIDIA GeForce RTX 3080 / Temperatures / GPU Hot Spot',
    mem_junction = sensor 'NVIDIA GeForce RTX 3080 / Temperatures / GPU Memory Junction',
  },

  controls = {
    fan1 = control 'NVIDIA GeForce RTX 3080 / Controls / GPU Fan 1',
    fan2 = control 'NVIDIA GeForce RTX 3080 / Controls / GPU Fan 2',
  },
}

function events.keepalive()
  return true
end


spawn(function ()
  while true do
    events.emit('stats', {
      temps = {
        cpu = cpu.temp.core.value,
        gpu = gpu.temp.core.value,
      },
    })

    sleep(1)
  end
end)

local modes = modeset {
  initial = state.mode,
  fallback = {
    name = 'idle',
  },
  on_change = function (mode)
    print('set mode to ' .. mode.name, mode.params)
    state.mode = mode
    events.emit('set_mode', {
      mode = mode,
    })
  end,
}

function modes.silent()
  power_save.activate()

  while true do
    cpu.controls.fan.value = 40

    case.controls.front_fan.value = 0
    case.controls.top_front_fan.value = 0
    case.controls.top_back_fan.value = 0

    case.controls.back_fan.value = max(25, 40 - case.fans.back_fan.value)

    sleep(1)
  end
end

function modes.idle()
  power_save.activate()

  while true do
    cpu.controls.fan.value = 40

    case.controls.front_fan.value = 40
    case.controls.top_front_fan.value = 25
    case.controls.top_back_fan.value = 25
    case.controls.back_fan.value = 25

    sleep(1)
  end
end

function modes.semi()
  balanced.activate()

  while true do
    cpu.controls.fan.value = 70

    case.controls.front_fan.value = 65
    case.controls.top_front_fan.value = 30
    case.controls.top_back_fan.value = 50
    case.controls.back_fan.value = 60

    sleep(1)
  end
end

function modes.semi2()
  balanced.activate()

  while true do
    cpu.controls.fan.value = 80

    case.controls.front_fan.value = 70
    case.controls.top_front_fan.value = 40
    case.controls.top_back_fan.value = 55
    case.controls.back_fan.value = 70

    sleep(1)
  end
end

function modes.perf()
  balanced.activate()

  while true do
    cpu.controls.fan.value = 100

    case.controls.front_fan.value = 75
    case.controls.top_front_fan.value = 60
    case.controls.top_back_fan.value = 65
    case.controls.back_fan.value = 80

    sleep(1)
  end
end

function events.get_mode(event)
  return state.mode
end

function events.set_mode(event)
  modes[event.name](event.params)
  return state.mode
end


local rgb_modes = modeset {
  initial = state.rgb_mode,
  fallback = {
    name = 'default',
  },
  on_change = function (mode)
    print('set rgb mode to ' .. mode.name, mode.params)
    state.rgb_mode = mode
    events.emit('set_rgb_mode', {
      mode = mode,
    })
  end,
}

function rgb_modes.default()
  local rgb = get_rgb()

  rgb.motherboard.set_mode('Direct')
  rgb.mouse.set_mode('Direct')
  rgb.gpu.set_mode('Direct')

  local smoothedPower = smooth(function () return cpu.powers.package.value end, 1.5)
  local smoothedCpuTemp = smooth(function () return cpu.temp.core.value end, 2)

  while true do
    local colors = {}

    local v2 = linmap(smoothedCpuTemp(), 55, 80, 0, 255)
    local v = max(0, linmap(smoothedPower(), 45, 60, 0, 255) - v2 * 1.1)

    for i in range(1, 18) do
      colors[i] = {
        r = v2,
        g = v,
        b = v,
      }
    end

    rgb.motherboard.colors = slice(colors, 1, 12)
    rgb.mouse.colors = slice(colors, 13, 14)
    rgb.gpu.colors = slice(colors, 15, 18)

    sleep(0.1)
  end
end

function rgb_modes.static(params)
  local rgb = get_rgb()

  rgb.motherboard.set_mode('Direct')
  rgb.mouse.set_mode('Direct')
  rgb.gpu.set_mode('Direct')

  rgb.motherboard.colors = times(params.color, 12)
  rgb.mouse.colors = times(params.color, 2)
  rgb.gpu.colors = times(params.color, 4)
end

local offset = 0
local delta = delta_sleep()
function rgb_modes.colorcycle(params)
  local rgb = get_rgb()

  rgb.motherboard.set_mode('Direct')
  rgb.mouse.set_mode('Direct')
  rgb.gpu.set_mode('Direct')

  offset = offset + delta() * params.speed

  while true do
    local colors = {}
    local frequency = 0.9

    for i in range(1, 18) do
      colors[i] = {
        r = sin(frequency * i - offset * frequency - 0) * 127 + 128,
        g = sin(frequency * i - offset * frequency - 2) * 127 + 128,
        b = sin(frequency * i - offset * frequency - 4) * 127 + 128,
      }
    end

    rgb.motherboard.colors = slice(colors, 1, 12)
    rgb.mouse.colors = slice(colors, 13, 14)
    rgb.gpu.colors = slice(colors, 15, 18)

    offset = offset + delta(0.1) * params.speed
  end
end

function events.get_rgb_mode(event)
  return state.rgb_mode
end

function events.set_rgb_mode(event)
  rgb_modes[event.name](event.params)
  return state.rgb_mode
end
