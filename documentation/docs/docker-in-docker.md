---
id: docker-in-docker
title: "Docker-in-Docker"
sidebar_position: 8.5
---

# Docker-in-Docker

The image carries the Docker Engine and can run its own Docker daemon, so builds inside it can
build images, run containers, and use Docker Compose or Testcontainers without the host's Docker
socket or a `docker:dind` sidecar. The daemon is opt-in: without `BUILD_AGENT_DOCKERD=1` the image
starts no daemon and behaves exactly as before.

Read [the security trade-off](#security-trade-off) before you use it.

## Running with Docker-in-Docker

```bash
docker run --rm --privileged \
    -e BUILD_AGENT_DOCKERD=1 \
    -v build-agent-docker:/var/lib/docker \
    -v ./:/workspace \
    --stop-timeout 30 \
    ghcr.io/the-running-dev/build-agent:latest \
    build docker --dry-run true
```

- `--privileged` is required. Without it the container exits with an error that says so.
- `BUILD_AGENT_DOCKERD=1` (or `true`) turns the daemon on. Any other value, or none, leaves it off.
- `-v <volume>:/var/lib/docker` holds the daemon's images, containers and build cache. Use a named
  volume, so the cache survives the container. Without one, the daemon falls back to the slow
  `vfs` storage driver on the container's own filesystem and prints a warning. Give each running
  container its own volume; two daemons must never share one.
- `--stop-timeout 30` gives the daemon time to stop its own containers when the container stops.
  Docker's default of 10 seconds can cut that short.

The daemon listens on `/var/run/docker.sock`, the CLI's default, so `DOCKER_HOST` stays unset and
every Docker client in the container finds it without configuration. Do not also mount the host's
socket at that path: `start-dockerd` then refuses to start, because the daemon could not listen
there and every Docker command would reach the host instead. A `DOCKER_HOST` set to another daemon
gets a warning, since Docker clients would use that daemon rather than this one.

## The contract

| What | Value |
|------|-------|
| Opt-in | `BUILD_AGENT_DOCKERD=1` |
| Start | `/usr/local/bin/start-dockerd` |
| Stop | `/usr/local/bin/stop-dockerd`; the image's entrypoint calls it when the command exits |
| Socket | `/var/run/docker.sock`, with `DOCKER_HOST` unset |
| Data | `/var/lib/docker`, a volume the caller mounts |
| Log | `/var/log/dockerd.log` |
| Run flags | `--privileged -v <volume>:/var/lib/docker` |

`start-dockerd` does nothing and exits 0 unless `BUILD_AGENT_DOCKERD` is `1` or `true`. Otherwise
it checks that the container is privileged and runs as root, prepares nested cgroups, picks the
`nft` or `legacy` iptables backend the kernel supports, starts `dockerd`, and exits 0 once the
daemon answers. It exits 1 without starting anything when `/var/run/docker.sock` is a mount or
another daemon already answers there. When the daemon does not answer in time, it stops it, prints the end of the log
and exits 1. Calling it again while the daemon runs waits for that daemon and starts no second
one.

`stop-dockerd` stops the daemon and waits for it to exit, so the data volume is left consistent.
It does nothing when no daemon runs, and exits 1 if the daemon is still running after the timeout.

| Variable | Default | Meaning |
|----------|---------|---------|
| `BUILD_AGENT_DOCKERD_TIMEOUT` | `60` | Seconds `start-dockerd` waits for the daemon to answer |
| `BUILD_AGENT_DOCKERD_STOP_TIMEOUT` | `30` | Seconds `stop-dockerd` waits for the daemon to exit |
| `BUILD_AGENT_DOCKERD_IPTABLES` | detected | `nft` or `legacy` forces that iptables backend |

## Shutdown

The image's entrypoint, `build-agent-entrypoint`, runs `start-dockerd` and then the command. When
the daemon is on, the entrypoint stays as the container's main process: it passes a stop signal to
the command, and once the command exits it runs `stop-dockerd` and exits with the command's
status. When the daemon is off, the entrypoint replaces itself with the command, as the image did
before it had an entrypoint.

A container that `build-agent update` replaced from an image older than Docker-in-Docker keeps
that image's entrypoint, `docker-entrypoint.sh`, because the update carries the container's
entrypoint over. `BUILD_AGENT_DOCKERD` does nothing there; create the container again with
`docker run` to get `build-agent-entrypoint`.

## Images built on this one

An image that sets its own `ENTRYPOINT` replaces `build-agent-entrypoint`, so it starts and stops
the daemon itself. The simplest way is to run its own entrypoint through the image's:

```dockerfile
FROM ghcr.io/the-running-dev/build-agent@sha256:<digest>
ENTRYPOINT ["build-agent-entrypoint", "/bin/bash", "/opt/entrypoint.sh"]
```

Name the interpreter as above. The command runs through the base image's `docker-entrypoint.sh`,
which runs a file that is not executable with `node`.

Otherwise, call `start-dockerd` before the main process and `stop-dockerd` after it exits. Both
are safe to call when `BUILD_AGENT_DOCKERD` is unset. A script that ends in `exec` hands the
container to that process and never runs `stop-dockerd`, so run the process as a child instead:

```bash
#!/usr/bin/env bash
start-dockerd || exit 1
/runner/run.sh "$@" &
child=$!
trap 'kill -TERM "$child" 2> /dev/null' TERM INT
# wait returns early when a signal arrives; keep waiting until the process has exited.
while kill -0 "$child" 2> /dev/null; do wait "$child"; done
wait "$child"
status=$?
stop-dockerd
exit "$status"
```

## In GitHub Actions

A job container is not privileged unless the workflow asks for it, and GitHub overrides the
entrypoint, so start the daemon in a step. GitHub also mounts the runner's Docker socket at
`/var/run/docker.sock` in every job container; unmount it first, so the daemon can listen there:

```yaml
container:
  image: ghcr.io/the-running-dev/build-agent:latest
  options: --privileged
  env:
    BUILD_AGENT_DOCKERD: '1'
steps:
  - run: umount /var/run/docker.sock && start-dockerd
    shell: bash
```

## Security trade-off

`--privileged` gives the container every capability, every device and an unconfined security
profile. Code that runs in it has root on the host. With Docker Desktop, the host is Docker
Desktop's virtual machine, which holds every other container and image on that machine. This is
the same trust as mounting the host's Docker socket, without sharing the host's images and
containers. Use Docker-in-Docker only for code you trust. See the
[Security Model](./security.md).

Canonical contract: [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
