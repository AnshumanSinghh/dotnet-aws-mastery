# ── Stage 1: Build ────────────────────────────────────────────────────────────
# WHY multi-stage: The SDK image (~900MB) is only needed to compile.
# The final runtime image uses the slim ASP.NET runtime (~120MB).
# Smaller image = faster ECR pull = faster ECS task startup.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first — before source code.
# WHY: Docker layer caching. If only source code changes (not .csproj),
# the restore layer is reused from cache. Restore is the slowest step.
COPY ["AwsCloudNative.sln", "./"]
COPY ["AwsCloudNative.Api/AwsCloudNative.Api.csproj",       "AwsCloudNative.Api/"]
COPY ["AwsCloudNative.Common/AwsCloudNative.Common.csproj", "AwsCloudNative.Common/"]

# Restore dependencies using the copied project files only.
RUN dotnet restore "AwsCloudNative.Api/AwsCloudNative.Api.csproj"

# Now copy all source code and publish.
COPY . .
WORKDIR /src/AwsCloudNative.Api

RUN dotnet publish "AwsCloudNative.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
# WHY aspnet:10.0 not sdk:10.0: Runtime image has no compiler or SDK.
# Only what ASP.NET Core needs to execute — nothing more.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# WHY non-root user: Running as root inside a container is a security risk.
# If the container is compromised, the attacker has root on the container filesystem.
# ECS Fargate supports non-root users — always use one in production.
# User will be created inside the Docker container's Linux operating system file system
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

# Copy only the published output from the build stage i.e STAGE 1.
COPY --from=build /app/publish .

# Document the port — does not publish it, just metadata for ECS task definition.
# The actual port mapping is configured in the ECS task definition port mappings.
EXPOSE 8080

# WHY ASPNETCORE_URLS: Overrides the default Kestrel port (5000/5001).
# ECS expects the container to listen on 8080 as configured in the task definition.
# PITFALL: If Kestrel listens on 5000 but ECS routes to 8080 — health checks fail.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AwsCloudNative.Api.dll"]