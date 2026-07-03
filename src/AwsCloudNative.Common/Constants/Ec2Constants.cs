namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for EC2 (Elastic Compute Cloud) integration.
    /// WHY: IMDS (Instance Metadata Service) endpoint paths and EC2 environment
    /// variable keys must never be scattered as magic strings across the codebase.
    /// A single path typo in an IMDS call causes silent credential resolution failure.
    /// </summary>
    public static class Ec2Constants
    {
        /// <summary>
        /// IMDS (Instance Metadata Service) v2 base URL.
        /// Link-local IP — only reachable from inside the EC2 instance itself.
        /// Never routable over the internet or through NAT Gateway.
        /// </summary>
        public const string ImdsBaseUrl = "http://169.254.169.254";

        /// <summary>
        /// IMDSv2 token endpoint. PUT request to this path returns a
        /// session token required for all subsequent metadata calls.
        /// WHY v2 only: IMDSv1 is vulnerable to SSRF (Server-Side Request Forgery)
        /// attacks — a compromised app can be tricked into leaking instance credentials.
        /// </summary>
        public const string ImdsTokenPath = "/latest/api/token";

        /// <summary>
        /// IMDSv2 session token TTL (Time To Live) header name.
        /// Value is the number of seconds the token remains valid.
        /// Maximum: 21600 seconds (6 hours).
        /// </summary>
        public const string ImdsTokenTtlHeader = "X-aws-ec2-metadata-token-ttl-seconds";

        /// <summary>
        /// IMDSv2 session token header name.
        /// Included in every metadata request after token acquisition.
        /// </summary>
        public const string ImdsTokenHeader = "X-aws-ec2-metadata-token";

        /// <summary>
        /// Default IMDSv2 token TTL in seconds.
        /// 21600 = 6 hours. Fetch once per startup, reuse for the process lifetime.
        /// PITFALL: Do not fetch a new token on every metadata call —
        /// excessive PUT requests to the token endpoint are rate-limited by AWS.
        /// </summary>
        public const int ImdsTokenTtlSeconds = 21600;

        /// <summary>
        /// IMDS metadata paths relative to ImdsBaseUrl/latest/meta-data/.
        /// Each path returns a specific piece of instance identity information.
        /// </summary>
        public static class MetadataPaths
        {
            private const string Base = "/latest/meta-data";

            /// <summary>
            /// Unique EC2 instance identifier.
            /// Example: i-0a1b2c3d4e5f67890
            /// Changes every time the instance is stopped and started.
            /// </summary>
            public const string InstanceId = Base + "/instance-id";

            /// <summary>
            /// EC2 instance type — determines CPU, memory, and network capacity.
            /// Example: t3.micro, m5.large, c6i.xlarge
            /// </summary>
            public const string InstanceType = Base + "/instance-type";

            /// <summary>
            /// The AZ (Availability Zone) this instance is running in.
            /// Example: ap-south-1a
            /// Use this to confirm correct AZ placement for multi-AZ setups.
            /// </summary>
            public const string AvailabilityZone = Base + "/placement/availability-zone";

            /// <summary>
            /// Private IPv4 address assigned to this instance within the VPC.
            /// Use for service-to-service communication within the same VPC.
            /// </summary>
            public const string PrivateIpv4 = Base + "/local-ipv4";

            /// <summary>
            /// Public IPv4 address if assigned. Empty for instances in private subnets.
            /// PITFALL: Never use public IP for internal service communication —
            /// traffic exits and re-enters the VPC unnecessarily (costs money + latency).
            /// </summary>
            public const string PublicIpv4 = Base + "/public-ipv4";

            /// <summary>
            /// IAM role name attached via Instance Profile.
            /// Confirms which role the SDK credential chain will use.
            /// Equivalent to checking GetCallerIdentity from Phase 1 Track 1.
            /// </summary>
            public const string IamRole = Base + "/iam/security-credentials/";
        }

        /// <summary>
        /// EC2 instance naming conventions for this solution.
        /// Pattern: {app}-{environment}-{purpose}
        /// </summary>
        public static class Instances
        {
            /// <summary>Base name for Orders API EC2 instances.</summary>
            public const string OrdersApi = "orders-api";
        }
    }
}
