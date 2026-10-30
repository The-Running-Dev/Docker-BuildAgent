#!/usr/bin/env bash
#
# Tests Docker-in-Docker in a build-agent image, and that the image behaves as before without it.
#
# Usage: scripts/test-docker-in-docker.sh <image>
#
# Needs a Docker host that allows --privileged containers. Prints PASS or FAIL per check and
# exits 1 when any check fails. Everything it creates is named build-agent-dind-<pid>-* and
# removed on exit.

# The scripts passed to `inner` are single-quoted on purpose: they expand inside the container.
# shellcheck disable=SC2016

set -uo pipefail

image="${1:?usage: $0 <image>}"
prefix="build-agent-dind-$$"
volume="$prefix-data"
outer="$prefix-outer"
docs="$prefix-docs"
# busybox:1.37.0, pinned so the inner pulls are reproducible.
busybox=busybox@sha256:a7dbc319b1438115a207e11930dc4bb3e643dcb3ec0bdd694bb36cd8fad289ec
work="$(mktemp -d)"
failures=0

cleanup() {
    docker rm -f "$outer" "$docs" > /dev/null 2>&1
    docker volume rm -f "$volume" > /dev/null 2>&1
    docker image rm -f "$prefix-derived" > /dev/null 2>&1
    rm -rf "$work"
}
trap cleanup EXIT

pass() { echo "PASS: $*"; }
fail() {
    echo "FAIL: $*"
    failures=$((failures + 1))
}

# check <description> <command...>: runs the command, showing its output only when it fails.
check() {
    local description="$1" output
    shift
    if output="$("$@" 2>&1)"; then
        pass "$description"
    else
        fail "$description"
        echo "$output" | sed 's/^/    /'
    fi
}

# Runs a bash script inside the outer container, with the pinned image in $busybox.
inner() { docker exec -e busybox="$busybox" "$outer" bash -euo pipefail -c "$1"; }

# Waits until the outer container runs its command, which happens only after start-dockerd
# succeeded.
wait_for_outer() {
    local deadline=$((SECONDS + 120))
    until docker exec "$outer" pgrep -x sleep > /dev/null 2>&1; do
        if [ "$(docker inspect -f '{{.State.Running}}' "$outer")" != true ] || ((SECONDS >= deadline)); then
            docker logs "$outer" 2>&1 | tail -n 60 | sed 's/^/    /'
            return 1
        fi
        sleep 1
    done
}

echo "Image: $image"
echo "Size:  $(docker image inspect -f '{{.Size}}' "$image" | awk '{ printf "%.2f GB (%d bytes)", $1 / 1e9, $1 }')"
echo

echo "== Without BUILD_AGENT_DOCKERD"

check "image entrypoint is build-agent-entrypoint and CMD is pwsh" \
    test "$(docker image inspect -f '{{json .Config.Entrypoint}} {{json .Config.Cmd}}' "$image")" \
    = '["build-agent-entrypoint"] ["pwsh"]'
check "docs server healthcheck is unchanged" \
    test "$(docker image inspect -f '{{json .Config.Healthcheck.Test}}' "$image")" \
    = '["CMD-SHELL","curl -f http://localhost:3000 || exit 1"]'
check "no dockerd process runs" \
    docker run --rm "$image" bash -c '! pgrep -x dockerd'
check "default CMD starts pwsh" \
    bash -c "echo '\"pwsh-\" + \"ok\"' | docker run --rm -i '$image' | grep -q pwsh-ok"
check "exit status of the command is the container's" \
    bash -c "docker run --rm '$image' pwsh -NoProfile -Command 'exit 7'; test \$? = 7"
check "options still go to node through docker-entrypoint.sh" \
    bash -c "docker run --rm '$image' -e 'console.log(\"node-\" + \"ok\")' | grep -q node-ok"

# The docs server is started as before the image had an entrypoint, and must answer HTTP on port
# 3000. Whether it answers 200 at / is the site's concern, not the entrypoint's.
docs_answers() {
    docker run -d --name "$docs" "$image" \
        bash -c 'cd /app/docs-template && pnpm serve --port 3000 --host 0.0.0.0' > /dev/null || return 1
    local deadline=$((SECONDS + 120)) code
    while ((SECONDS < deadline)); do
        code="$(docker exec "$docs" curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/docs/)"
        [ "$code" != 000 ] && [ -n "$code" ] && return 0
        [ "$(docker inspect -f '{{.State.Running}}' "$docs")" = true ] || break
        sleep 2
    done
    docker logs "$docs" 2>&1 | tail -n 30
    return 1
}
check "docs server answers on port 3000" docs_answers
docker rm -f "$docs" > /dev/null 2>&1

echo
echo "== BUILD_AGENT_DOCKERD=1 without --privileged"

not_privileged() {
    local output status
    output="$(docker run --rm -e BUILD_AGENT_DOCKERD=1 "$image" true 2>&1)"
    status=$?
    echo "$output"
    [ "$status" != 0 ] && grep -q -- '--privileged' <<< "$output"
}
check "fails with a clear error and a non-zero exit" not_privileged

echo
echo "== BUILD_AGENT_DOCKERD=1 with --privileged and no data volume"

no_volume() {
    local output status
    output="$(docker run --rm --privileged -e BUILD_AGENT_DOCKERD=1 "$image" \
        pwsh -NoProfile -Command 'docker info -f "{{.ServerVersion}}"; exit 7' 2>&1)"
    status=$?
    echo "$output"
    [ "$status" = 7 ] && grep -q 'is not a volume' <<< "$output" &&
        grep -q 'dockerd stopped' <<< "$output"
}
check "warns about the missing volume, and the command's exit status is the container's" no_volume

bad_iptables() {
    local output status
    output="$(docker run --rm --privileged -e BUILD_AGENT_DOCKERD=1 \
        -e BUILD_AGENT_DOCKERD_IPTABLES=bogus "$image" true 2>&1)"
    status=$?
    echo "$output"
    [ "$status" != 0 ] && grep -q 'BUILD_AGENT_DOCKERD_IPTABLES must be nft or legacy' <<< "$output"
}
check "rejects an unknown BUILD_AGENT_DOCKERD_IPTABLES" bad_iptables

echo
echo "== BUILD_AGENT_DOCKERD=1 with the host's Docker socket mounted"

# MSYS_NO_PATHCONV keeps Git Bash on Windows from rewriting the socket path.
host_socket() {
    local output status
    output="$(MSYS_NO_PATHCONV=1 docker run --rm --privileged -e BUILD_AGENT_DOCKERD=1 \
        -v /var/run/docker.sock:/var/run/docker.sock "$image" true 2>&1)"
    status=$?
    echo "$output"
    [ "$status" != 0 ] && grep -q 'already in use' <<< "$output" &&
        ! grep -q 'dockerd is ready' <<< "$output"
}
check "refuses to start instead of reporting the host's daemon as ready" host_socket

# GitHub Actions overrides the entrypoint and mounts the host's socket; the documented step
# unmounts it and starts the daemon.
actions_step() {
    MSYS_NO_PATHCONV=1 docker run --rm --privileged -e BUILD_AGENT_DOCKERD=1 --entrypoint bash \
        -v /var/run/docker.sock:/var/run/docker.sock "$image" -euo pipefail -c '
            umount /var/run/docker.sock && start-dockerd
            test "$(pgrep -cx dockerd)" = 1
            docker info -f "{{.Name}}" | grep -qx "$(hostname)"
            stop-dockerd'
}
check "the GitHub Actions step starts the container's own daemon" actions_step

echo
echo "== An image built on this one, with its own entrypoint script"

# The script is not executable, so the documented ENTRYPOINT must name its interpreter.
derived() {
    local context="$work/derived"
    mkdir -p "$context"
    printf '#!/usr/bin/env bash\necho "derived-$1"\n' > "$context/entrypoint.sh"
    printf '%s\n' "FROM $image" "COPY --chmod=644 entrypoint.sh /opt/entrypoint.sh" \
        'ENTRYPOINT ["build-agent-entrypoint", "/bin/bash", "/opt/entrypoint.sh"]' \
        > "$context/Dockerfile"
    docker build -q -t "$prefix-derived" "$context" > /dev/null &&
        docker run --rm "$prefix-derived" ok | grep -qx derived-ok
}
check "ENTRYPOINT [build-agent-entrypoint, /bin/bash, script] runs the script" derived
docker image rm -f "$prefix-derived" > /dev/null 2>&1

echo
echo "== BUILD_AGENT_DOCKERD=1 with --privileged and a data volume"

docker volume create "$volume" > /dev/null
docker run -d --name "$outer" --privileged -e BUILD_AGENT_DOCKERD=1 \
    -v "$volume:/var/lib/docker" "$image" sleep infinity > /dev/null

if ! wait_for_outer; then
    fail "dockerd starts"
else
    pass "dockerd starts"

    check "socket is /var/run/docker.sock and DOCKER_HOST is unset" \
        inner 'test -S /var/run/docker.sock && test -z "${DOCKER_HOST:-}" && docker version'
    check "storage driver is not vfs" \
        inner 'driver="$(docker info -f "{{.Driver}}")"; echo "$driver"; test "$driver" != vfs'
    check "docker run of an image pinned by digest" \
        inner 'docker run --rm "$busybox" echo inner-ok | grep -q inner-ok'
    check "docker buildx build of a two-line Dockerfile" \
        inner 'mkdir -p /tmp/dind-build
               printf "FROM %s\nRUN echo built > /built\n" "$busybox" > /tmp/dind-build/Dockerfile
               docker buildx build --load -t dind-test:latest /tmp/dind-build
               docker run --rm dind-test:latest cat /built | grep -q built'
    check "inner container writes to a bind mount" \
        inner 'mkdir -p /tmp/dind-bind
               docker run --rm -v /tmp/dind-bind:/out "$busybox" sh -c "echo bind-ok > /out/file"
               grep -q bind-ok /tmp/dind-bind/file'
    check "published port is reachable from the outer container" \
        inner 'docker run -d --name dind-web -p 18080:80 "$busybox" \
                   sh -c "mkdir /www && echo port-ok > /www/index.html && httpd -f -p 80 -h /www"
               curl -fsS --retry 15 --retry-connrefused --retry-delay 1 http://localhost:18080/ | grep -q port-ok
               docker rm -f dind-web'
    check "docker compose up and down" \
        inner 'mkdir -p /tmp/dind-compose && cd /tmp/dind-compose
               printf "%s\n" \
                   "services:" \
                   "  web:" \
                   "    image: $busybox" \
                   "    command: sh -c \"mkdir /www && echo compose-ok > /www/index.html && httpd -f -p 80 -h /www\"" \
                   "    ports: [\"18081:80\"]" > compose.yaml
               docker compose up -d --wait
               curl -fsS --retry 15 --retry-connrefused --retry-delay 1 http://localhost:18081/ | grep -q compose-ok
               docker compose down
               test -z "$(docker ps -q)"'
    check "start-dockerd is idempotent" \
        inner 'start-dockerd && test "$(pgrep -cx dockerd)" = 1'

    clean_stop() {
        docker stop -t 30 "$outer" > /dev/null || return 1
        docker cp "$outer:/var/log/dockerd.log" - | tar -xO > "$work/dockerd.log" || return 1
        local exit_code
        exit_code="$(docker inspect -f '{{.State.ExitCode}}' "$outer")"
        docker logs "$outer" 2>&1 | tail -n 5
        test "$exit_code" != 137 &&
            grep -q 'Daemon shutdown complete' "$work/dockerd.log" &&
            docker logs "$outer" 2>&1 | grep -q 'dockerd stopped'
    }
    check "docker stop shuts dockerd down cleanly" clean_stop

    restarted() {
        docker start "$outer" > /dev/null && wait_for_outer &&
            inner 'docker image inspect dind-test:latest > /dev/null'
    }
    check "dockerd restarts on the same volume and keeps its images" restarted
fi

docker rm -f "$outer" > /dev/null 2>&1

echo
if ((failures > 0)); then
    echo "$failures check(s) failed."
    exit 1
fi
echo "All checks passed."
