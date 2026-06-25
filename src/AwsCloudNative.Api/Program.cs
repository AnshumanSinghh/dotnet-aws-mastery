using AwsCloudNative.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Wire up AWS SDK with execution role credential chain.
// This single call handles local dev (profile) and
// Lambda/ECS (execution role / task role) transparently.
builder.Services.AddProductionAws(builder.Configuration);

// Phase 1 · Track 2 — Cognito JWT authentication + authorisation policies
builder.Services.AddProductionCognitoAuth(builder.Configuration);

var app = builder.Build();

// ORDER MATTERS:
// UseAuthentication must come before UseAuthorization.
// The middleware pipeline is sequential — authentication resolves
// the identity first, then authorisation evaluates it.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();