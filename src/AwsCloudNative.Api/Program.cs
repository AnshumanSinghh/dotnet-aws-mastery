using AwsCloudNative.Api.Extensions;
using AwsCloudNative.Common.Options;

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

var app = builder.Build();

// ORDER MATTERS:
// UseAuthentication must come before UseAuthorization.
// The middleware pipeline is sequential — authentication resolves
// the identity first, then authorisation evaluates it.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();