using AwsCloudNative.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace AwsCloudNative.Lambda.Extensions
{
    /// <summary>
    /// Configures the DI (Dependency Injection) container for Lambda.
    /// WHY separate extension: Lambda Annotations wires up a Host builder
    /// identical to ASP.NET Core. Keeping setup in an extension method
    /// makes it testable and consistent with the Web API project's pattern.
    /// PITFALL: Do not reference AwsCloudNative.Api here — Lambda must
    /// remain a lightweight, independently deployable unit.
    /// </summary>
    public static class LambdaServiceExtensions
    {
        /// <summary>
        /// Registers all services needed by Lambda functions in this project.
        /// Called once during the cold start init phase.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="configuration">The Lambda configuration root.</param>
        public static IServiceCollection AddLambdaServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // AWS SDK setup — uses execution role credentials automatically.
            // In Lambda, AWS injects temporary credentials via environment variables.
            // The SDK picks these up via the credential provider chain — zero config needed.
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());

            services.AddOptions<AwsOptions>()
                .BindConfiguration(AwsOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }

        /// <summary>
        /// Configures Serilog structured logging for Lambda.
        /// WHY console sink: Lambda captures stdout automatically and forwards
        /// it to CloudWatch Logs. No CloudWatch SDK calls needed for logging —
        /// just write to stdout and Lambda does the rest.
        /// PITFALL: Never use file sinks in Lambda — the Lambda filesystem
        /// is read-only except for /tmp. Use console → CloudWatch instead.
        /// </summary>
        public static IServiceCollection AddLambdaLogging(
            this IServiceCollection services)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                // Lambda injects this — use it to correlate logs
                // across all invocations of the same function version.
                .Enrich.WithProperty("FunctionName",
                    Environment.GetEnvironmentVariable(
                        Common.Constants.LambdaConstants.EnvironmentVariables.FunctionName)
                    ?? "local")
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {FunctionName} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            services.AddLogging(logging => logging.AddSerilog());
            return services;
        }
    }
}
