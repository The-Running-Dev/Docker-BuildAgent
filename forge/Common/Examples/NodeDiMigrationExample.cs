#nullable enable

using System;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Services;
using Notifications;
using Parameters; 
using DependencyInjection;

namespace Examples;

/// <summary>
/// Shows how existing Node build projects work with the new DI system.
/// This demonstrates backward compatibility and the migration path.
/// </summary>
public static class NodeDiMigrationExample
{
    /// <summary>
    /// Example of how the existing Node class already works with DI without changes.
    /// The Base class automatically initializes DI and provides services.
    /// </summary>
    public static void ShowExistingNodeCompatibility()
    {
        Console.WriteLine("=== Node Build Project DI Compatibility ===");
        Console.WriteLine();
        
        Console.WriteLine("1. EXISTING CODE STILL WORKS:");
        Console.WriteLine("   public class Node : Base<NodeParams, DiscordNotifications>");
        Console.WriteLine("   {");
        Console.WriteLine("       // No changes needed - DI works automatically!");
        Console.WriteLine("       protected override void Configure() { ... }");
        Console.WriteLine("       public Target Build => _ => _");
        Console.WriteLine("           .Executes(() => {");
        Console.WriteLine("               // GitService, GitHubService available via properties");
        Console.WriteLine("               Log.Information($\"Build Complete\");");
        Console.WriteLine("           });");
        Console.WriteLine("   }");
        Console.WriteLine();
        
        Console.WriteLine("2. SERVICES AVAILABLE:");
        Console.WriteLine("   - GitService (via base.GitService property)");
        Console.WriteLine("   - GitHubService (via base.GitHubService property)");
        Console.WriteLine("   - NotificationService (via base.NotificationService property)");
        Console.WriteLine("   - ServiceProvider (via base.ServiceProvider property)");
        Console.WriteLine();
        
        Console.WriteLine("3. MIGRATION OPTIONS:");
        ShowBasicUsage();
        ShowAdvancedUsage();
    }
    
    /// <summary>
    /// Shows basic usage where existing code requires no changes.
    /// </summary>
    private static void ShowBasicUsage()
    {
        Console.WriteLine("   OPTION A - No Changes Required:");
        Console.WriteLine("   public class Node : Base<NodeParams, DiscordNotifications>");
        Console.WriteLine("   {");
        Console.WriteLine("       public Target SomeTarget => _ => _");
        Console.WriteLine("           .Executes(() => {");
        Console.WriteLine("               // These work exactly as before:");
        Console.WriteLine("               var commits = GitService.GetCommits();");
        Console.WriteLine("               var releases = GitHubService.GetReleases();");
        Console.WriteLine("               NotificationService.SendMessage(\"Done!\");");
        Console.WriteLine("           });");
        Console.WriteLine("   }");
        Console.WriteLine();
    }
    
    /// <summary>
    /// Shows advanced usage leveraging the full DI container.
    /// </summary>
    private static void ShowAdvancedUsage()
    {
        Console.WriteLine("   OPTION B - Leverage Full DI:");
        Console.WriteLine("   public class Node : Base<NodeParams, DiscordNotifications>");
        Console.WriteLine("   {");
        Console.WriteLine("       public Target AdvancedTarget => _ => _");
        Console.WriteLine("           .Executes(() => {");
        Console.WriteLine("               // Access full DI container:");
        Console.WriteLine("               var logger = ServiceProvider.GetRequiredService<ILogger<Node>>();");
        Console.WriteLine("               var customService = ServiceProvider.GetService<ICustomService>();");
        Console.WriteLine("               ");
        Console.WriteLine("               logger.LogInformation(\"Using DI logger\");");
        Console.WriteLine("           });");
        Console.WriteLine("   }");
        Console.WriteLine();
    }
    
    /// <summary>
    /// Shows how to add custom services to an existing Node project.
    /// </summary>
    public static void ShowCustomServiceRegistration()
    {
        Console.WriteLine("=== Adding Custom Services to Node Projects ===");
        Console.WriteLine();
        
        Console.WriteLine("STEP 1 - Override ConfigureServices in your Node class:");
        Console.WriteLine("public class Node : Base<NodeParams, DiscordNotifications>");
        Console.WriteLine("{");
        Console.WriteLine("    protected override void ConfigureServices(IServiceCollection services)");
        Console.WriteLine("    {");
        Console.WriteLine("        // Call base to register core services");
        Console.WriteLine("        base.ConfigureServices(services);");
        Console.WriteLine("        ");
        Console.WriteLine("        // Add your custom services");
        Console.WriteLine("        services.AddSingleton<ICustomBuildService, CustomBuildService>();");
        Console.WriteLine("        services.AddScoped<IDeploymentService, DeploymentService>();");
        Console.WriteLine("    }");
        Console.WriteLine("}");
        Console.WriteLine();
        
        Console.WriteLine("STEP 2 - Use your custom services in targets:");
        Console.WriteLine("public Target Deploy => _ => _");
        Console.WriteLine("    .Executes(() => {");
        Console.WriteLine("        var deployService = ServiceProvider.GetRequiredService<IDeploymentService>();");
        Console.WriteLine("        deployService.Deploy(Parameters.ArtifactsDir);");
        Console.WriteLine("    });");
        Console.WriteLine();
    }
    
    /// <summary>
    /// Shows the complete build lifecycle with DI.
    /// </summary>
    public static void ShowBuildLifecycle()
    {
        Console.WriteLine("=== Node Build Lifecycle with DI ===");
        Console.WriteLine();
        
        Console.WriteLine("1. APPLICATION START");
        Console.WriteLine("   ├─ Main() calls Build<Node>(x => x.Build)");
        Console.WriteLine("   ├─ Nuke framework creates Node instance");
        Console.WriteLine("   └─ Base constructor runs InitializeDependencyInjection()");
        Console.WriteLine();
        
        Console.WriteLine("2. DI INITIALIZATION");
        Console.WriteLine("   ├─ ServiceCollection created");
        Console.WriteLine("   ├─ ConfigureServices() called (override in Node for custom services)");
        Console.WriteLine("   ├─ Core services registered (GitService, GitHubService, Notifications)");
        Console.WriteLine("   ├─ ServiceProvider built");
        Console.WriteLine("   └─ Services available via properties");
        Console.WriteLine();
        
        Console.WriteLine("3. BUILD EXECUTION");
        Console.WriteLine("   ├─ Configure() called");
        Console.WriteLine("   ├─ Target dependencies resolved");
        Console.WriteLine("   ├─ Targets execute with DI services available");
        Console.WriteLine("   └─ Build completes");
        Console.WriteLine();
        
        Console.WriteLine("4. CLEANUP");
        Console.WriteLine("   └─ ServiceProvider disposed automatically");
        Console.WriteLine();
    }
}
