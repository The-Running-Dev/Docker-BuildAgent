---
id: dependency-injection
title: Dependency Injection
sidebar_position: 2
---

How the Forge build types get their services. This page describes the wiring and points at the
code; the code is the authority on signatures and lifetimes, so read it before relying on this
summary.

## Where it lives

| Concern | File |
|---|---|
| The base class every build type inherits | [`forge/Common/Base.cs`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/forge/Common/Base.cs) |
| Service registration | [`forge/Common/DependencyInjection/ServiceCollectionExtensions.cs`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/forge/Common/DependencyInjection/ServiceCollectionExtensions.cs) |
| The static container used by a few static code paths | [`forge/Common/DependencyInjection/ServiceLocator.cs`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/forge/Common/DependencyInjection/ServiceLocator.cs) |
| The services themselves | [`forge/Common/Services/`](https://github.com/The-Running-Dev/Docker-BuildAgent/tree/main/forge/Common/Services) |
| The notification implementations | [`forge/Common/Notifications/`](https://github.com/The-Running-Dev/Docker-BuildAgent/tree/main/forge/Common/Notifications) |

## How a build gets its services

Every build type derives from `Base<TParams, TNotifications>`, which derives from NUKE's
`NukeBuild`. `TParams` is the build's parameter class and `TNotifications` is the notification
implementation to register (each build type uses `DiscordNotifications`).

When NUKE initializes the build, `Base` calls `InitializeDependencyInjection()`. That method:

1. Registers the shared services with `AddForgeServices()`.
2. Registers the notification implementation with `AddNotificationServices<TNotifications>()`.
3. Calls the `ConfigureServices(IServiceCollection)` hook, which does nothing by default. A build
   type overrides it to add its own registrations.
4. Builds the `ServiceProvider`, which is exposed on the build as `ServiceProvider`.

Both `InitializeDependencyInjection()` and `ConfigureServices()` are `protected virtual`. No build type
currently overrides `ConfigureServices()`, so every build gets exactly the shared services and the
notification implementation. Each build gets its own provider; nothing is shared between builds.

`Base` exposes the common services as properties (`GitService`, `GitHubService`,
`NotificationService`, `Logger`) that resolve from `ServiceProvider`. The build components in
[`forge/Common/Components/`](https://github.com/The-Running-Dev/Docker-BuildAgent/tree/main/forge/Common/Components)
resolve the service they need from the same `ServiceProvider` rather than constructing it.

## What is registered

`AddForgeServices()` and `AddNotificationServices<TNotifications>()` register the services below.
`GitService`, `GitHubService`, `NodeService` and `DockerService` are registered under both their
concrete type and their interface, and the interface resolves to the concrete registration.
`ChangeLogConfigService` is registered only under `IChangeLogConfigService`. The notification
implementation is registered twice, as `INotifications` and as its own type, each a singleton.

| Service | Lifetime |
|---|---|
| `GitService`, `GitHubService` | Singleton |
| `ChangeLogConfigService` | Scoped |
| `NodeService` (through `AddNodeServices()`) | Scoped |
| `DockerService` (through `AddDockerServices()`) | Scoped |
| The notification implementation | Singleton |

It also replaces the default logging providers with a console logger that uses the `forge`
formatter, which prints `HH:mm:ss [INF] message`.

A build never creates a scope, so a scoped service resolved from the root provider behaves as one
instance for the whole build.

## The static container

`ServiceLocator` is a separate, static holder for one provider. It exists for code that has no
access to the build instance. The only production use is in `Base.Build`, which initializes it
with the default services and uses it to mark the project directory as a safe git directory before
the targets run. Everything else should take its services from the build's `ServiceProvider`.

`DockerServiceDecorator` and `DockerSimulationService` are not registered by
`AddForgeServices()`. Only tests use them.

## Testing

The tests are in `forge/Common.Tests` and use xUnit and Moq:

- [`DependencyInjection/ServiceLocatorTests.cs`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/forge/Common.Tests/DependencyInjection/ServiceLocatorTests.cs)
  covers the static container.
- [`Build/BaseTests.cs`](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/forge/Common.Tests/Build/BaseTests.cs)
  covers `Base`, including how services are registered.
- [`Services/`](https://github.com/The-Running-Dev/Docker-BuildAgent/tree/main/forge/Common.Tests/Services)
  has one test class per service.

To test code that depends on a service, depend on its interface (`IGitService`, `IGitHubService`,
`IDockerService`, `INodeService`, `IChangeLogConfigService`, `INotifications`) and pass a Moq mock,
or build a small `ServiceCollection` in the test.

See the [development guide](./development-guide.md) for how to build and run the tests.
