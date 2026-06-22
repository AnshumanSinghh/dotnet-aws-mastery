using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// <para><b>[SELF-NOTE] AWS Cloud Architecture Foundations: SDK vs CDK</b></para>
    /// <para>
    /// <b>AWS SDK (Software Development Kit):</b> Used <i>inside</i> application runtime code. 
    /// It acts as the operational client to consume existing cloud resources.
    /// <br/>• Scope: Executed by the application at runtime (e.g., inside AWS Lambda or ECS).
    /// <br/>• Task: Data-plane actions (e.g., <c>S3Client.PutObjectAsync</c>, publishing to SQS, querying DynamoDB).
    /// </para>
    /// <para>
    /// <b>AWS CDK (Cloud Development Kit):</b> Used <i>outside</i> application runtime code. 
    /// It acts as the infrastructure blueprint compiler to provision cloud resources.
    /// <br/>• Scope: Executed on developer machines or CI/CD deployment pipelines via Node/CLI engines.
    /// <br/>• Task: Control-plane actions (e.g., creating the S3 Bucket resource, defining IAM roles, mapping SQS queues).
    /// </para>
    /// <br></br>
    /// WHY: No magic strings scattered across the codebase.
    /// Every ARN prefix, region placeholder, and service identifier
    /// lives here. When you rotate a resource, you change one line.
    /// PITFALL: Do NOT put actual account IDs or ARNs here —
    /// those belong in appsettings or Secrets Manager. This file
    /// is for structural constants only.
    /// </summary>    
    public static class AwsConstants
    {
        /// <summary>
        /// Utilises the <b>AWS CDK for .NET</b> to define cloud infrastructure stacks.<br/>
        /// <remarks>
        /// Self-Note: This code does not run inside the application runtime. It executes 
        /// during deployment to generate CloudFormation templates that provision the underlying AWS resources.
        /// </remarks>
        /// </summary>
        public static class Iam
        {
            // The service principals used in trust policies.
            // Useful when generating CDK stacks or documenting roles.
            public const string LambdaServicePrincipal = "lambda.amazonaws.com";
            public const string EcsTasksServicePrincipal = "ecs-tasks.amazonaws.com";
            public const string Ec2ServicePrincipal = "ec2.amazonaws.com";
        }

        /// <summary>
        /// Utilises the <b>AWS SDK v4 for .NET</b> to execute application-layer operations.<br/>
        /// <remarks>
        /// Self-Note: This code runs inside the live application process. It interacts with 
        /// infrastructure that must already be provisioned by the CDK stack.
        /// </remarks>
        /// </summary>
        public static class CredentialSources
        {
            // These match the names the SDK uses internally.
            // Used in diagnostics output — never in auth logic.
            public const string InstanceProfile = "InstanceProfileAWSCredentials";
            public const string EcsContainer = "ECSTaskCredentials";
            public const string Environment = "EnvironmentVariablesAWSCredentials";
            public const string SharedFile = "SharedFileAWSCredentials";
            public const string AssumedRole = "AssumeRoleAWSCredentials";
        }
    }
}
