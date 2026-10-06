---
id: update-tool
title: "Update and Rollback Tool"
sidebar_position: 15
---

# Update and Rollback Tool

The update tool replaces a running container with one built from a newer image, waits for the
replacement to report healthy, and puts the original back if it does not.

:::caution Not yet published
The tool is built in this repository but is not published as a package, and it has no installed
command name. Both depend on an open decision, **U-1** in the contract (who owns the tool feed
and its publishing keys). This page shows the command as `<tool>`; there is nothing to install
yet.
:::

## Commands

```text
<tool> update <container> [--image <reference>] [--health-timeout <duration>]
                          [--no-restore] [--notify <url>]
<tool> update --clear-lock <container>
```

| Option | Meaning |
|---|---|
| `<container>` | The name of the container to update. Required. |
| `--image <reference>` | The image to move to. Default: the container's own image reference, looked up again so a moved tag is picked up. |
| `--health-timeout <duration>` | How long to wait for the replacement to report healthy. A bare number is seconds; `120s`, `2m` and `1h` also work. Default 120 seconds. Must be greater than zero. |
| `--no-restore` | Leave the replacement in place if it is unhealthy instead of restoring the original. It does not apply when the replacement cannot be created: the original is always restored then. |
| `--notify <url>` | After the outcome is logged, post a notification for it to this webhook. A notification that fails, times out (10 seconds) or has no URL prints a warning without the URL and never changes the exit status. A refused update has no outcome and sends none. |
| `--clear-lock` | Remove the update lock for `<container>` and do nothing else. It accepts no other option. |

If the image already matches the running container, the tool reports success and changes nothing.

## What an update does

1. Refuses if a lock for the container exists, or if the target cannot be reproduced safely.
2. Resolves the target image, always pulling it first so a tag that moved in the registry is
   picked up. If the pull fails but an image with that reference is already present, the tool
   prints a warning that the local image may be out of date and uses it; if there is none, it
   stops with exit 30. A reference that is an image ID is never pulled. If the resolved image is
   the one already running, stops here with success.
3. Takes a lock, then inspects the container again so a change made before the lock was taken is
   seen, checks for leftovers of an earlier update, keeps the current image under a
   `buildagent-prior` tag, and writes a start entry to the update log.
4. Stops the container, renames it to a `buildagent-prior-…` name, creates the replacement with the
   same settings on the new image, and starts it.
5. Waits for the replacement's own health check to report healthy.
6. If healthy, removes the prior container and records success. If not, restores the original
   (unless `--no-restore`) and records the outcome. If the replacement cannot be created in step 4,
   the original is restored whatever `--no-restore` says.

A refusal before step 4 leaves the target untouched, and any lock it took is released.

### What it will not update

A container is refused (exit 20) when it:

- was started with `--rm`
- carries an orchestrator ownership label, because the orchestrator owns its updates
- uses a legacy container link
- has an anonymous volume
- has a setting the replacement would not carry over. The refusal names each one. They include
  privileged mode, added or dropped capabilities, devices, security options, resource limits
  (memory, CPU, PIDs, ulimits), DNS and extra hosts, tmpfs mounts, a custom hostname or domain
  name, network aliases or fixed addresses, a non-default runtime, IPC or PID mode, a TTY or open
  stdin, a stop timeout, mount propagation, and a user, working directory, stop signal or health
  check that differs from the image's own.

The logging driver and its options are carried over to the replacement.

### Health checks

The replacement is judged by its own declared health check. A container that declares none
cannot be verified: the update treats the replacement as failed and restores the original (exit
10), or leaves it with `--no-restore` (exit 11). A replacement that stops running before it
reports healthy is treated the same way.

### Interruption

Pressing Ctrl+C, or sending SIGTERM, stops the update safely; a second signal ends the tool at
once. Before the container is stopped, the tool removes the `buildagent-prior` tag and the lock it
took and exits with status 1, leaving the target untouched. Once the container has been stopped,
the tool restores the original whatever `--no-restore` says, records the outcome as interrupted,
releases the lock, and exits with the restore's status (10, or 12 if the restore failed).

### The lock and the update log

The lock is a container named `buildagent-lock-<hash>`. If an update is interrupted, the next one
refuses with exit 21 and names the owning host and process. After checking that nothing is still
running, release it with `--clear-lock`. Clearing a lock touches nothing else: not the target, not
the prior container, not the log.

The update log is a file, `updates.jsonl`, under `%LOCALAPPDATA%\Docker-BuildAgent` on Windows and
under `$XDG_STATE_HOME/docker-buildagent` (default `~/.local/state/docker-buildagent`) elsewhere.
A start entry with no outcome, or a prior container left over from an earlier update, makes the
next update refuse (exit 22) until you remove it. A log line that cannot be parsed also refuses
every update on the host, with exit 22, until it is corrected.

## Exit statuses

| Status | Meaning |
|---|---|
| 0 | Updated, or already on the requested image. |
| 10 | The replacement was unhealthy and the original was restored. |
| 11 | The replacement was unhealthy and was left in place (`--no-restore`). |
| 12 | The replacement was unhealthy and the original could not be restored. |
| 20 | Refused: the container does not exist, or its shape is not supported. |
| 21 | Refused: a lock is held. |
| 22 | Refused: leftovers from an earlier update are present, or the update log has a line that cannot be parsed. |
| 23 | Refused: the current image could not be kept. |
| 24 | Refused: the update log could not be written. |
| 30 | The target image could not be obtained. |
| 1 | Any other failure, including a bad command line or no `docker` on `PATH`. |

Every refusal message names the container, the reason, and what clears it.

Canonical contract: [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
