# Templates

Built-in Dockerfile templates, copied into the image at `/nuke/templates/`. The `docker` build type uses
one when the project has no `Dockerfile`: it detects the application type and looks for
`Dockerfile.<type>`.

| File | Application type | Result |
|---|---|---|
| `Dockerfile.angular` | Angular | The built `browser` output served by nginx |
| `Dockerfile.node` | Node.js | The built application run with `npm start` |

Both read the build output from `./artifacts` (`ENV artifactsDir`). Any other detected type needs its own
`Dockerfile.<type>` in a template directory, or a `Dockerfile` in the project.

A project's own templates are searched before the built-in ones. The search order, the detection rules
and how to mount a custom directory are in
[Docker Templates](https://build-agent.subzerodev.com/docs/docker-templates), which is the reference.

Adding a file here makes it a built-in template for every consumer of the image, so treat it as a change
to a public surface.
