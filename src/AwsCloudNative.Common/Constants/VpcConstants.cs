using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Structural constants for VPC (Virtual Private Cloud) networking configuration.
    /// WHY: Port numbers and CIDR (Classless Inter-Domain Routing) blocks scattered
    /// as magic numbers across infrastructure definitions are a maintenance hazard.
    /// One source of truth here prevents misconfigured Security Group rules.
    /// PITFALL: Actual VPC IDs, Subnet IDs, and Security Group IDs are
    /// environment-specific — they belong in Parameter Store, not here.
    /// </summary>
    public static class VpcConstants
    {
        /// <summary>
        /// Port numbers used by services in this solution.
        /// These must match the inbound rules on the corresponding Security Groups.
        /// </summary>
        public static class Ports
        {
            /// <summary>ASP.NET Core API HTTP (HyperText Transfer Protocol) port inside the container.</summary>
            public const int ApiHttp = 8080;

            /// <summary>ASP.NET Core API HTTPS (HTTP Secure) port inside the container.</summary>
            public const int ApiHttps = 8443;

            /// <summary>PostgreSQL database port. Must be open from API Security Group to RDS Security Group.</summary>
            public const int PostgreSql = 5432;

            /// <summary>Redis / Valkey port. Must be open from API Security Group to ElastiCache Security Group.</summary>
            public const int Redis = 6379;

            /// <summary>HTTPS outbound port. Must be open in outbound rules for Secrets Manager, SSM, Cognito JWKS calls.</summary>
            public const int Https = 443;
        }

        /// <summary>
        /// Standard VPC CIDR (Classless Inter-Domain Routing) block conventions.
        /// /16 gives 65,536 IPs for the VPC.
        /// /24 gives 256 IPs per subnet — standard for service subnets.
        /// PITFALL: Once a VPC CIDR is set it cannot be changed.
        /// Plan your IP ranges before creating the VPC.
        /// </summary>
        public static class Cidr
        {
            /// <summary>VPC address space. All subnets must fall within this range.</summary>
            public const string VpcBlock = "10.0.0.0/16";

            /// <summary>Public subnet — hosts Load Balancer and NAT (Network Address Translation) Gateway.</summary>
            public const string PublicSubnet = "10.0.1.0/24";

            /// <summary>Private subnet A — hosts ECS tasks, Lambda, RDS, ElastiCache.</summary>
            public const string PrivateSubnetA = "10.0.2.0/24";

            /// <summary>
            /// Private subnet B — second AZ (Availability Zone) for high availability.
            /// RDS and ECS require at least two AZs for production deployments.
            /// </summary>
            public const string PrivateSubnetB = "10.0.3.0/24";
        }

        /// <summary>
        /// IMDS (Instance Metadata Service) endpoint.
        /// Available inside every EC2 instance, ECS task, and Lambda execution environment.
        /// Used by the AWS SDK to resolve region, account ID, and temporary credentials.
        /// PITFALL: This IP is link-local — only reachable from inside the instance.
        /// Never route it through NAT or a proxy.
        /// </summary>
        public const string ImdsEndpoint = "http://169.254.169.254/latest/meta-data/";

        /// <summary>
        /// ECS (Elastic Container Service) task metadata endpoint environment variable name.
        /// The SDK reads this automatically inside Fargate tasks to resolve credentials.
        /// You never call this directly in application code.
        /// </summary>
        public const string EcsMetadataUriEnvVar = "ECS_CONTAINER_METADATA_URI_V4";
    }
}
