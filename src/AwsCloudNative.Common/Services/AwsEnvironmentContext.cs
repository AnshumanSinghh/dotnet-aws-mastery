namespace AwsCloudNative.Common.Services
{
    /// <summary>
    /// Resolved AWS environment context — region, account ID, and ARN builders.
    /// Populated once at startup via STS GetCallerIdentity (Phase 1 Track 1).
    /// Extended here with Step Functions ARN builder.
    /// </summary>
    public sealed class AwsEnvironmentContext
    {
        /// <summary>AWS account ID resolved from STS at startup.</summary>
        public string AccountId { get; init; } = string.Empty;

        /// <summary>AWS region resolved from SDK environment chain at startup.</summary>
        public string Region { get; init; } = string.Empty;

        /// <summary>Builds a DynamoDB table ARN for this environment.</summary>
        public string DynamoDbTableArn(string tableName)
            => $"arn:aws:dynamodb:{Region}:{AccountId}:table/{tableName}";

        /// <summary>Builds an SQS queue ARN for this environment.</summary>
        public string SqsQueueArn(string queueName)
            => $"arn:aws:sqs:{Region}:{AccountId}:{queueName}";

        /// <summary>Builds an ECS cluster ARN for this environment.</summary>
        public string EcsClusterArn(string clusterName)
            => $"arn:aws:ecs:{Region}:{AccountId}:cluster/{clusterName}";

        /// <summary>Builds a Lambda function ARN for this environment.</summary>
        public string LambdaFunctionArn(string functionName)
            => $"arn:aws:lambda:{Region}:{AccountId}:function:{functionName}";

        /// <summary>
        /// Builds a Step Functions state machine ARN.
        /// Pattern: arn:aws:states:{region}:{accountId}:stateMachine:{name}
        /// WHY states (not stepfunctions): "states" is the service identifier
        /// in ARNs for Step Functions — not "stepfunctions". This is a common typo.
        /// </summary>
        public string StepFunctionsStateMachineArn(string stateMachineName)
            => $"arn:aws:states:{Region}:{AccountId}:stateMachine:{stateMachineName}";
    }
}
