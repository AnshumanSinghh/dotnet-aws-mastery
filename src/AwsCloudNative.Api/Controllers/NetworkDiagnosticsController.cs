using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Diagnostic endpoint to verify the network context this service
    /// is running in — VPC placement, subnet, and metadata availability.
    /// WHY: When an ECS task or Lambda fails to reach Secrets Manager or RDS,
    /// the root cause is almost always a Security Group or Route Table
    /// misconfiguration. This endpoint surfaces the network identity
    /// of the running process to help diagnose these issues fast.
    /// PITFALL: Remove or gate behind internal-only access before production.
    /// </summary>
    [ApiController]
    [Route("api/diagnostics/network")]
    public class NetworkDiagnosticsController : ControllerBase
    {
        private readonly NetworkOptions _networkOptions;
        private readonly ILogger<NetworkDiagnosticsController> _logger;

        /// <summary>
        /// Injects resolved NetworkOptions and logger.
        /// </summary>
        public NetworkDiagnosticsController(
            IOptions<NetworkOptions> networkOptions,
            ILogger<NetworkDiagnosticsController> logger)
        {
            _networkOptions = networkOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Returns the resolved VPC configuration and the private IP (Internet Protocol)
        /// address assigned to this container or Lambda instance.
        /// The private IP confirms which subnet the process is running in —
        /// cross-reference with your VPC subnet CIDR blocks to verify
        /// correct private subnet placement.
        /// </summary>
        [HttpGet("context")]
        public IActionResult GetNetworkContext()
        {
            // Resolve the private IP of this process's network interface.
            // Inside ECS Fargate: this is the IP assigned to the task's ENI
            // (Elastic Network Interface) in the private subnet.
            // Inside Lambda: this is the IP from the VPC subnet Lambda was
            // configured to use.
            // Locally: this is your machine's LAN IP.
            var privateIp = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                
                .Select(addr => addr.Address.ToString())
                .FirstOrDefault() ?? "unresolved";

            // Detect whether IMDS (Instance Metadata Service) is available.
            // Available inside EC2, ECS, Lambda — not on local dev machines.
            // WHY: Confirms we are actually running inside AWS infrastructure.
            var isRunningInAws = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable(VpcConstants.EcsMetadataUriEnvVar))
                || !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV"));

            _logger.LogInformation(
                "Network context requested. PrivateIp={PrivateIp} RunningInAws={RunningInAws}",
                privateIp, isRunningInAws);

            return Ok(new
            {
                // VPC resource IDs resolved from Parameter Store
                VpcId = _networkOptions.VpcId,
                PrivateSubnetIds = _networkOptions.GetPrivateSubnetIds(),
                SecurityGroupId = _networkOptions.ServiceSecurityGroupId,

                // Runtime network identity of this process
                PrivateIpAddress = privateIp,
                IsRunningInAws = isRunningInAws,

                // Guidance: compare PrivateIpAddress against your VPC subnet
                // CIDR blocks to confirm correct subnet placement.
                // Expected: 10.0.2.x or 10.0.3.x (private subnet range)
                // If you see 10.0.1.x — the task landed in the public subnet.
                ExpectedCidrRange = VpcConstants.Cidr.PrivateSubnetA
            });
        }
    }
}
