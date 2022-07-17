return function (config)
  local res = {
    kp = config.kp,
    ki = config.ki,
    kd = config.kd,
    low_limit = config.low_limit,
    high_limit = config.high_limit,
    setpoint = config.setpoint,
    output = 0,
  }

  local proportional = 0
  local integral = 0
  local derivative = 0

  local last_measured_value = nil

  setmetatable(res, {
    __call = function (_, measured_value, dt)
      local error = res.setpoint - measured_value

      local d_measured_value = 0
      if last_measured_value ~= nil then
        d_measured_value = measured_value - last_measured_value
      end

      proportional = res.kp * error

      integral = integral + res.ki * error * dt
      if res.low_limit ~= nil and integral < res.low_limit then
        integral = res.low_limit
      end
      if res.high_limit ~= nil and integral > res.high_limit then
        integral = res.high_limit
      end

      derivative = -res.kd * d_measured_value / dt

      res.output = proportional + integral + derivative
      if res.low_limit ~= nil and res.output < res.low_limit then
        res.output = res.low_limit
      end
      if res.high_limit ~= nil and res.output > res.high_limit then
        res.output = res.high_limit
      end

      last_measured_value = measured_value

      return res.output
    end
  })

  return res
end
