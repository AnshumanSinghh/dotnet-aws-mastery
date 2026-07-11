using AwsCloudNative.Api.Extensions;
using AwsCloudNative.Api.HealthChecks;
using AwsCloudNative.Api.Services;
using AwsCloudNative.Common.Options;
using AwsCloudNative.Data;
using AwsCloudNative.Data.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Step 1: Resolve AWS region from config before secrets are loaded.
// WHY: Secrets Manager and Parameter Store need to know the region
// before the full IConfiguration pipeline is built.
var awsRegion = builder.Configuration
    .GetSection(AwsOptions.SectionName)
    .GetValue<string>("Region") ?? "ap-south-1";

// ── Step 2: Add Secrets Manager + Parameter Store into IConfiguration.
// MUST happen before builder.Build() so all IOptions<T> bindings
// see the resolved secret values at startup validation time.
// In Development: these sources are optional (missing = no crash).
// In Production:  these sources are required (missing = crash fast).
builder.Configuration.AddProductionSecrets(
    builder.Environment,
    awsRegion);


builder.Services.AddControllers();

// ── Step 3: AWS SDK credential chain (Track 1)
// Wire up AWS SDK with execution role credential chain.
// This single call handles local dev (profile) and
// Lambda/ECS (execution role / task role) transparently.
builder.Services.AddProductionAws(builder.Configuration);

// ── Step 4: Cognito JWT authentication (Track 2)
// Phase 1 · Track 2 — Cognito JWT authentication + authorisation policies
builder.Services.AddProductionCognitoAuth(builder.Configuration);

// ── Step 5: Bind resolved secrets and parameters to strongly-typed Options.
// ValidateOnStart fires here — if any required value is missing,
// the app refuses to start with a clear descriptive error.
builder.Services.AddProductionSecretsOptions(builder.Configuration);

// ── Step 6: VPC-aware networking + health checks (Track 4)
builder.Services.AddProductionNetworking();

// Phase 2 Track 1 — Lambda SDK client for direct invocation
builder.Services.AddProductionLambdaClient();

// Phase 2 Track 2 — IHttpClientFactory for API Gateway service-to-service calls
// WHY AddHttpClient: manages connection pooling and handler lifetimes.
// Never instantiate HttpClient directly in controllers or services.
builder.Services.AddHttpClient();

// Phase 2 Track 3 — ECS health checks + graceful shutdown
builder.Services.AddProductionEcs();

// Phase 2 Track 4 — EC2 IMDS named HttpClient
builder.Services.AddProductionEc2();

// Phase 2 Track 5 — Step Functions workflow execution
builder.Services.AddProductionWorkflows();

// Phase 3 Track 1 — Amazon S3
builder.Services.AddProductionS3();
builder.Services.AddScoped<S3FileService>();

// Phase 3 Track 2 — Amazon RDS + EF Core 10
builder.Services.AddProductionDatabase(builder.Configuration);

// WHY AddDbContextFactory in addition to AddDbContext:
// RdsHealthCheck runs outside HTTP request scope — it needs
// IDbContextFactory to create DbContext instances independently.
builder.Services.AddDbContextFactory<OrdersDbContext>(
    options => options.UseNpgsql(), ServiceLifetime.Scoped);

// Register RDS health check
builder.Services
    .AddHealthChecks()
    .AddCheck<RdsHealthCheck>(
        name: "rds-postgresql",
        tags: ["live", "database"]);

// Phase 3 Track 3 — Amazon DynamoDB
builder.Services.AddProductionDynamoDb();
builder.Services.AddSingleton<ProductService>();

var app = builder.Build();

// ORDER MATTERS:
// UseProductionNetworking - before Auth and Authz
// UseAuthentication must come before UseAuthorization.
// The middleware pipeline is sequential — authentication resolves
// the identity first, then authorisation evaluates it.

// /health — must be before auth so ALB can reach it.ALB doesnot use token that's why.
app.UseProductionNetworking(); 

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();