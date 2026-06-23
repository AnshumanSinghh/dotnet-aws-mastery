using AwsCloudNative.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Wire up AWS SDK with execution role credential chain.
// This single call handles local dev (profile) and
// Lambda/ECS (execution role / task role) transparently.
builder.Services.AddProductionAws(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();