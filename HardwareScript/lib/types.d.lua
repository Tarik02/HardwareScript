---@class Pid
---@field public output number

---@class Sensor
---@field public value number

---@class Control
---@field public value number


---@class OpenRGBDevice
---@field public colors table
OpenRGBDevice = {}

---@param name string
---@return nil
function OpenRGBDevice.set_mode(name) end


---@class OpenRGB
OpenRGB = {}

---@param name string
---@return OpenRGBDevice
function OpenRGB.device(name) end


---@class events

events = {}

---@param name string
---@param payload table|nil
---@return nil
function events.emit(name, payload)
end


state = {}


---@param conf any
---@return Pid
function pid(conf) end

---@param callback function
---@return function
function spawn(callback) end

---@param time number|nil
---@return number
function sleep(time) end

---@param time number|nil
---@return number
function delta_sleep(time) end

---@param name string
---@return Sensor
function sensor(name) end

---@param name string
---@return Control
function control(name) end

---@param name string
---@return any
function power_plan(name) end

---@param conf table
---@return OpenRGB
function openrgb(conf) end

---@param conf table
---@return table
function modeset(conf) end


---@param x number
---@return number
function sin(x) end

---@param x number
---@return number
function cos(x) end

---@param ... number
---@return number
function min(...) end

---@param ... number
---@return number
function max(...) end

---@param value number
---@param low number|nil
---@param high number|nil
---@return number
function clamp(value, low, high) end

---@param value number
---@param source_a number
---@param source_b number
---@param target_a number
---@param target_b number
---@return number
function linmap(value, source_a, source_b, target_a, target_b) end

---@param getter function<number>
---@param max_diff number
---@return function<number>
function smooth(getter, max_diff) end

---@param from number
---@param to number
---@param step number|nil
---@return function<number>, nil, number
function range(from, to, step) end

---@generic T
---@param input table<T>
---@param from number
---@param to number
---@return table<T>
function slice(input, from, to) end

---@generic T
---@param input T
---@param count number
---@return table<T>
function times(input, count) end
