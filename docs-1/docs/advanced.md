---
id: advanced
title: "⚡ Advanced"
sidebar_position: 7
---

You can run various tools inside the container for reproducible environments:

```pwsh
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -w "/workspace" \
    build-agent:latest pwsh -Command "./build.ps1 --type docker"
```

## 🛠️ Tools Available in the Build Agent

The Build Agent Docker image comes pre-installed with a variety of tools for advanced automation and scripting:

- 🟢 Node.js & NPM
- 🅰️ Angular CLI (`ng`)
- 🟣 .NET 8 SDK (`dotnet`)
- 🐳 Docker CLI (`docker`)
- 💻 PowerShell (`pwsh`)
- 🗃️ Git
- 🔢 GitVersion
- 🏗️ Nuke Build
- 📝 TypeScript (`tsc`)
- 🌐 angular-cli-ghpages

Example:
```pwsh
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -w "/workspace" \
    build-agent:latest <Provide a Call to Your Tool>
```

Below are some fictional examples of how you might call the agent to use these tools:

### Example: Run Angular CLI

```pwsh
pwsh -Command "ng build --configuration production"
```

### Example: Use GitVersion to get semantic version

```pwsh
pwsh -Command "gitversion /output json"
```

### Example: Build and push a Docker image

```pwsh
docker build -t my-app:latest .
docker push my-app:latest
```

### Example: Run a custom Nuke build target

```pwsh
pwsh -Command "nuke --target Publish"
```

### Example: Compile TypeScript

```pwsh
tsc src/index.ts --outDir dist
```