---
id: docker-templates
title: 🐳 Docker Templates
sidebar_position: 6
---

The following templates are included inside the Build Agent image at `/nuke/templates`. When you run a build and no `Dockerfile` exists in your project directory, the agent will:

1. Try to determine your application type (e.g., Angular, Node.js).
2. If a matching template exists (such as `Dockerfile.angular` or `Dockerfile.node`), it will be copied and used as your Dockerfile for the build.
3. You can customize these templates as needed by mapping the `/nuke/templates` to your host, or mapping an individual `Dockerfile`.

## 🅰️ Angular

This template is designed for deploying a built Angular application using NGINX. It expects your Angular app to be built and available in the `ArtifactsDir/browser` directory. The template configures NGINX to serve your static files efficiently, exposes port 80 for HTTP traffic, and is ideal for production deployments of Angular single-page applications.

[View Dockerfile](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/feature/next_version/Templates/Dockerfile.angular)

---

## 🟩 Node.js

This template is intended for running a Node.js application in a containerized environment. It assumes your application is built and located in the `ArtifactsDir` directory. The template sets up the working directory, installs only production dependencies for a lean image, and starts your app using `npm start`. This is suitable for deploying Node.js web servers or APIs in production.

[View Dockerfile](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/feature/next_version/Templates/Dockerfile.node)