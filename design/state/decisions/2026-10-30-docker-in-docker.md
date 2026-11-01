# decision/2026-10-30-docker-in-docker
Date: 2026-10-30
Anchor: 2026-10-30 — The image runs its own Docker daemon on opt-in, through an entrypoint that chains to the base image's
Status: accepted
StatedIn: unit/document/design-20-contract § Image invocation

## Claim
The image carries the Docker Engine (pinned by `DOCKER_VERSION` and `CONTAINERD_VERSION`) and has `ENTRYPOINT ["build-agent-entrypoint"]`, which runs `start-dockerd` and then the command through the base image's `docker-entrypoint.sh`; `CMD` stays `pwsh`. `start-dockerd` does nothing unless `BUILD_AGENT_DOCKERD=1`; with it, it fails with a non-zero exit when the container is not privileged, otherwise starts dockerd on `unix:///var/run/docker.sock` with its data in `/var/lib/docker` (a volume the caller mounts) and waits for it to answer. On stop, the entrypoint forwards the signal to the command, waits for it, and runs `stop-dockerd`; `stop-dockerd` is also callable on its own. Without the opt-in the image behaves as before.
