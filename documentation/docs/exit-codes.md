---
id: exit-codes
title: Exit Codes
sidebar_position: 17
---

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

A script or CI step decides what to do from the exit status of `build`. This page lists every status the
build command returns, what causes it, and what to do about it. The
[update tool](./update-tool.md) and the [PowerShell module](./powershell-module.md) have statuses of
their own, which those pages list.

The last column says whether the status exists in the code today. Statuses marked **contract only** are
promised by [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
but nothing returns them yet, so do not write a script that waits for one.

## Build command

| Status | Meaning | What to do | In code |
|---|---|---|---|
| 0 | Every target that had to run succeeded. | Nothing. | Implemented |
| 2 | The project configuration file is invalid. No target ran. | Read the `Configuration error:` lines in the output. Each names the file, the key and the rule. Correct the file as [Project Configuration File](./project-configuration.md#invalid-configuration) describes, then run again. | Implemented |
| 255 | A target failed, or a variable in `.build/.build.env.map` has no value. The build returns `-1`, which a shell shows as 255. | Find the target marked `Failed` in the summary table at the end of the output and read the error above it. For a missing variable the output says `Build Env Incomplete`. Set the variable or remove it from the map. See [Troubleshooting](./troubleshooting.md). | Implemented |
| 1 | The `build` wrapper got no status from the build program, or a step of `build node-template` threw an exception, such as a failed template clone. | Read the `[ERROR]` line above the exit. | Implemented |
| 3 | A required discovery target was not found. | Not applicable yet. Today the same failure ends the build with 255. | Contract only |
| 4 | An external template could not be fetched. | Not applicable yet. Today a failed template clone in `build node-template` ends with status 1. | Contract only |
| 5 | The Docker daemon was unavailable or rejected the request. | Not applicable yet. Today a failed Docker step ends the build with 255. | Contract only |
| 6 | A registry operation failed. | Not applicable yet. Today a failed push ends the build with 255. | Contract only |

The contract lists `1` for any other failure. The build does not do that: a failed target returns `-1`
(255), and `1` comes only from the wrapper. A script that must treat every failure alike should test for
any non-zero status rather than for `1`.

Exit status 2 is decided before anything else runs. It comes before the environment file is generated,
so no target starts, and no artifact or image changes.

## Where the statuses come from

- The status of `build docker`, `build node`, `build node-in-docker` and `build forge` is the status of
  the build program. The wrapper passes it on unchanged.
- `build node-template` is a PowerShell flow that does not go through the build program. A command such as
  the package manager that fails inside it ends the flow with that command's own status. A step that throws
  ends it with 1.
- A launcher does not change the status: the PowerShell module raises a terminating error carrying the
  container's status, so `2` passes through. The module adds statuses 3 and 5 for faults it finds before
  the container starts. See [Errors](./powershell-module.md#errors).
