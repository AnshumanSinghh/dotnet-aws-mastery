using Amazon.Lambda.Annotations;
using AwsCloudNative.Lambda.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AwsCloudNative.Lambda.Functions;

/// <summary>
/// Lambda Annotations DI bootstrap.
/// WHY this class exists: Lambda Annotations requires a class marked with
/// [LambdaStartup] to configure the DI container. This is the Lambda
/// equivalent of Program.cs in ASP.NET Core.
///
/// EXECUTION TIMING: This runs once during the cold start INIT phase —
/// before the first handler invocation. Every subsequent warm invocation
/// reuses the container built here. Keep this fast.
///
/// PITFALL: Do not perform I/O or make network calls here.
/// DI registration is synchronous. Async resource initialisation
/// belongs in the function handler or a lazy singleton.
/// </summary>
[LambdaStartup]
public class Startup
{
    /// <summary>
    /// Configures services for Lambda Annotations DI container.
    /// Equivalent to builder.Services.* in ASP.NET Core Program.cs.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        //// Example of creating the IConfiguration object and
        //// adding it to the dependency injection container.
        //var builder = new ConfigurationBuilder()
        //                    .AddJsonFile("appsettings.json", true);

        //// Add AWS Systems Manager as a potential provider for the configuration. This is 
        //// available with the Amazon.Extensions.Configuration.SystemsManager NuGet package.
        //builder.AddSystemsManager("/app/settings");

        //var configuration = builder.Build();
        //services.AddSingleton<IConfiguration>(configuration);

        //// Example of using the AWSSDK.Extensions.NETCore.Setup NuGet package to add
        //// the Amazon S3 service client to the dependency injection container.
        //services.AddAWSService<Amazon.S3.IAmazonS3>();


        // Build configuration from environment variables and appsettings.
        // In Lambda, environment variables are the primary config source —
        // set them in the Lambda console or via CDK/SAM (Serverless Application Model).
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        // Register structured logging → CloudWatch via stdout.
        services.AddLambdaLogging();

        // Register AWS SDK + Options.
        services.AddLambdaServices(configuration);
    }
}
