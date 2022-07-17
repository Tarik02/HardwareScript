return function (current_time)
  local pending_tasks = {}

  local function spawn(callback)
    local co = coroutine.create(function ()
      local ok, err = pcall(callback)
      if not ok then
        error(err)
      end
    end)
    table.insert(pending_tasks, 1, { 0, co })

    return function ()
      for i, task in ipairs(pending_tasks) do
        if co == task[2] then
          table.remove(pending_tasks, i)
          break
        end
      end
    end
  end

  local function sleep(secs)
    if secs == nil then
      return current_time
    end
    local run_at = current_time + secs
    local co = coroutine.running()
    local pos = #pending_tasks + 1
    for i, task in ipairs(pending_tasks) do
      if run_at < task[1] then
        pos = i
        break
      end
    end
    table.insert(pending_tasks, pos, { run_at, co })
    coroutine.yield()
    return current_time
  end

  local function run(t)
    current_time = t
    while #pending_tasks > 0 and current_time >= pending_tasks[1][1] do
      local task = table.remove(pending_tasks, 1)
      local status, res = coroutine.resume(task[2])
      if not status then
        error(res)
      end
    end

    if #pending_tasks == 0 then
      return nil
    end

    return pending_tasks[1][1] - current_time
  end

  return run, spawn, sleep
end
