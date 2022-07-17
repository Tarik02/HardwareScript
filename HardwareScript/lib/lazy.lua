local lazy
lazy = function (resolver)
  local status = 'pending'
  local value = nil

  local on_resolved = {}
  local on_rejected = {}

  resolver(
    function (result)
      status = 'resolved'
      value = result
      for _, callback in ipairs(on_resolved) do
        callback(value)
      end
      on_resolved = nil
      on_rejected = nil
    end,
    function (err)
      status = 'rejected'
      value = err
      for _, callback in ipairs(on_rejected) do
        callback(value)
      end
      on_resolved = nil
      on_rejected = nil
    end
  )

  return {
    map = function (mapper)
      return lazy(function (resolve, reject)
        on_resolved[#on_resolved+1] = function (result)
          local status, res = pcall(mapper, result)
          if status then
            resolve(res)
          else
            reject(res)
          end
        end
        on_rejected[#on_rejected+1] = reject
      end)
    end,
    get = function ()
      if status == 'pending' then
        local co = coroutine.running()
        on_resolved[#on_resolved+1] = function ()
          coroutine.resume(co)
        end
        on_rejected[#on_rejected+1] = function ()
          coroutine.resume(co)
        end
        coroutine.yield()
      end

      if status == 'resolved' then
        return value
      elseif status == 'rejected' then
        error(value)
      end
    end
  }
end

return lazy
