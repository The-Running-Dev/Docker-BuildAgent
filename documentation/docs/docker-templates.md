---
id: docker-templates
title: 🐳 Docker Templates
sidebar_position: 6
---

Canonical contract (Docker-template discovery): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

The Build Agent supports flexible Docker template discovery to make it easy to use custom templates in your projects. When you run a build and no `Dockerfile` exists in your project directory, the agent will:

1. Try to determine your application type (e.g., Angular, Node.js).
2. Search for matching templates in multiple locations (see [Template Discovery Order](#template-discovery-order)).
3. If a matching template exists (such as `Dockerfile.angular` or `Dockerfile.node`), it will be copied and used as your Dockerfile for the build.

## 🔍 Template Discovery Order

The Build Agent searches for templates in the following order of priority:

1. **`TemplatesDir` as a directory** - The `TemplatesDir` value, when it names an existing directory
2. **`TemplatesDir` under your project root** - The same value resolved against the project root (for example `TemplatesDir: templates` finds `templates/` in your repository)

`TemplatesDir` defaults to `/nuke/templates`, the built-in templates inside the Build Agent image, so the image templates are used when you set nothing. A Dockerfile is taken from `<templates>/Dockerfile.<appType>` only when no Dockerfile exists at the configured path.

This approach allows you to:
- ✅ Store templates in your own repository (recommended)
- ✅ Override built-in templates with project-specific ones
- ✅ Fall back to container templates for quick starts

## 📁 Storing Templates in Your Repository

**Recommended approach:** Store your custom templates in your project repository:

```
your-project/
├── templates/
│   ├── Dockerfile.angular
│   └── Dockerfile.node
└── src/
    └── ...
```

This approach gives you:
- 🎯 **Version control** - Templates are versioned with your code
- 🔧 **Customization** - Tailor templates to your specific needs
- 👥 **Team sharing** - Templates are shared across your team
- 🔄 **No external dependencies** - No need to mount volumes or manage external template repositories

## 🛠️ Using Custom Template Directories

You can also specify a custom template directory using the `TemplatesDir` parameter:

```bash
# Mount your templates directory to the container
docker run --rm -it \
    -v "${PWD}:/workspace" \
    -v "${PWD}/my-templates:/custom-templates" \
    -v /var/run/docker.sock:/var/run/docker.sock \
    ghcr.io/the-running-dev/build-agent:latest \
    docker-build --templates-dir /custom-templates
```

## 🅰️ Angular

This template is designed for deploying a built Angular application using NGINX. It expects your Angular app to be built and available in the `ArtifactsDir/browser` directory. The template configures NGINX to serve your static files efficiently, exposes port 80 for HTTP traffic, and is ideal for production deployments of Angular single-page applications.

[View Dockerfile](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/feature/next_version/Templates/Dockerfile.angular)

---

## 🟩 Node.js

This template is intended for running a Node.js application in a containerized environment. It assumes your application is built and located in the `ArtifactsDir` directory. The template sets up the working directory, installs only production dependencies for a lean image, and starts your app using `npm start`. This is suitable for deploying Node.js web servers or APIs in production.

[View Dockerfile](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/feature/next_version/Templates/Dockerfile.node)