using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Options
{
    /// <summary>
    /// Strongly-typed network configuration resolved from SSM (Systems Manager)
    /// Parameter Store at startup.
    /// WHY: VPC resource IDs (subnet IDs, security group IDs) are environment-specific
    /// and change between dev, staging, and prod. They must never be hardcoded.
    /// These values are used by CDK stacks and diagnostic tooling — not by
    /// application business logic directly.
    /// </summary>
    public sealed class NetworkOptions
    {
        /// <summary>The IConfiguration section this class binds to.</summary>
        public const string SectionName = "Network";

        /// <summary>
        /// The VPC ID this service is deployed into.
        /// Format: vpc-xxxxxxxxxxxxxxxxx
        /// Resolved from Parameter Store — differs per environment.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string VpcId { get; init; } = string.Empty;

        /// <summary>
        /// Comma-separated list of private subnet IDs where ECS tasks and Lambda run.
        /// Format: subnet-xxxxxxxxx,subnet-yyyyyyyyy
        /// Always use private subnets for application workloads.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string PrivateSubnetIds { get; init; } = string.Empty;

        /// <summary>
        /// The Security Group ID attached to this service's ECS tasks or Lambda.
        /// Outbound rules on this SG must permit HTTPS (443) for AWS API calls
        /// and the database port for RDS connectivity.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string ServiceSecurityGroupId { get; init; } = string.Empty;

        /// <summary>
        /// Parses PrivateSubnetIds into an enumerable for use in CDK stack definitions.
        /// </summary>
        public IEnumerable<string> GetPrivateSubnetIds()
            => PrivateSubnetIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
}
