#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# EC2 User Data Bootstrap Script — AwsCloudNative.NET Orders API
#
# WHAT: Runs automatically as root on first EC2 instance start.
# WHY: Automates .NET installation and app deployment so every instance
#      produced by an Auto Scaling Group is identical and reproducible.
# PITFALL: User Data runs ONCE on first boot only.
#          To re-run after changes, use AWS Systems Manager Run Command
#          or bake a new AMI (Amazon Machine Image).
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail  # exit on error, undefined vars, pipe failures

echo "=== Starting AwsCloudNative Orders API bootstrap ==="

# ── Install .NET 10 Runtime ───────────────────────────────────────────────────
# WHY runtime only (not SDK): Production servers never need to compile code.
# Installing only the runtime keeps the instance footprint minimal.
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
    --channel 10.0 \
    --runtime aspnetcore \
    --install-dir /usr/share/dotnet

ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet

echo ".NET version: $(dotnet --version)"

# ── Create application user ───────────────────────────────────────────────────
# WHY non-root: Running the app as root means a compromised process
# has full OS access. A dedicated app user limits the blast radius.
useradd -r -s /bin/false appuser

# ── Pull application from S3 ──────────────────────────────────────────────────
# WHY S3 not git clone: S3 is faster, does not require git credentials,
# and the published artifact is the exact binary that was tested in CI/CD.
# The Instance Profile role must have s3:GetObject on this bucket.
APP_BUCKET="acn-orders-artifacts-dev"
APP_KEY="releases/latest/AwsCloudNative.Api.zip"
APP_DIR="/opt/acn-orders-api"

mkdir -p "$APP_DIR"
aws s3 cp "s3://${APP_BUCKET}/${APP_KEY}" /tmp/app.zip --region ap-south-1
unzip -q /tmp/app.zip -d "$APP_DIR"
chown -R appuser:appuser "$APP_DIR"
rm /tmp/app.zip

# ── Create systemd service ────────────────────────────────────────────────────
# WHY systemd: Ensures the app starts automatically after a reboot
# and is restarted automatically if it crashes.
# Restart=on-failure with RestartSec gives a brief backoff before retry.
cat > /etc/systemd/system/acn-orders-api.service << EOF
[Unit]
Description=AwsCloudNative Orders API
After=network.target

[Service]
Type=simple
User=appuser
WorkingDirectory=${APP_DIR}
ExecStart=/usr/local/bin/dotnet ${APP_DIR}/AwsCloudNative.Api.dll
Restart=on-failure
RestartSec=5

# Environment — non-secret config only.
# Secrets are resolved from Secrets Manager at runtime by the app.
# NEVER put passwords or API keys here — they appear in process listing.
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://+:8080
Environment=Aws__Region=ap-south-1

# Graceful shutdown — matches ShutdownTimeout in HostOptions
TimeoutStopSec=25

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable acn-orders-api
systemctl start acn-orders-api

echo "=== Bootstrap complete. Service status: ==="
systemctl status acn-orders-api --no-pager