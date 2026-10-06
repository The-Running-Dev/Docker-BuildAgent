# BuildAgent.Tool

The `build-agent` command. It replaces a running container with one built from a newer image,
waits for the replacement to report healthy, and puts the original back if it does not.

```bash
dotnet tool install --global BuildAgent.Tool
build-agent update <container> [--image <reference>] [--health-timeout <duration>] [--no-restore] [--notify <url>]
build-agent update --clear-lock <container>
```

Documentation: https://build-agent.subzerodev.com/docs/update-tool
