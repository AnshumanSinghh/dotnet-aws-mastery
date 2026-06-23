# Lab Verification Steps
## Step 1 — Local dev (shared credentials file)
 - Run the API and hit the diagnostics endpoint:

```bash
dotnet run --project AwsCloudNative.Api
curl https://localhost:5001/api/diagnostics/iam/identity
```

### Expected response — your IAM user ARN:

```JSON
 {
  "account": "123456789012",
  "userId": "AIDAEXAMPLEUSERID",
  "arn": "arn:aws:iam::123456789012:user/anshuman",
  "source": "IAM User (local dev — ensure MFA is enabled)"
 }
```

## Step 2 — Simulate an execution role locally using AssumeRole
- Create a test role in the AWS Console with this trust policy (replace 123456789012 and anshuman):
- The Trust Policy lives inside the IAM Role in AWS, not on your machine. It answers one question:
> "Who is allowed to call AssumeRole on me?"

```JSON
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": { "AWS": "arn:aws:iam::123456789012:user/anshuman" },
    "Action": "sts:AssumeRole"
  }]
}
```

### Add a named profile to `~/.aws/config`:

```ini
[profile lab-execution-role]
role_arn       = arn:aws:iam::123456789012:role/LabOrderProcessorRole  ← "what to assume"
source_profile = default                                                ← "who is doing the assuming"
```

> Update `appsettings.json` → `"Profile": "lab-execution-role"`, re-run, and hit the endpoint again. 
  The response ARN will now show `assumed-role/LabOrderProcessorRole/...` — proving the SDK is correctly
  assuming the role your Lambda or ECS task would use in production.

- Now Question is how and why ?
  - `[profile lab-execution-role]`:
  > This is the profile name. When appsettings.json says "Profile": "lab-execution-role", 
    the SDK looks for exactly this label in ~/.aws/config. It is just a named lookup key — nothing 
    special about the name itself.

  - `role_arn = arn:aws:iam::123456789012:role/LabOrderProcessorRole`:
  > This tells the SDK: "When this profile is selected, do not use my own IAM user credentials directly. 
    Instead, call STS:AssumeRole on this ARN and use the temporary credentials you get back."  
    - Why ? Because in this arn "arn:aws:iam::123456789012`:role`/LabOrderProcessorRole" we have given
      `:role` which tells don't use `IAM user creds` because for IAM it should be `:user`.

  - `source_profile = default`:
  > This tells the SDK: "Use the credentials from the `[default]` profile to make that `AssumeRole` call." 
    In other words — use your IAM user credentials to request the temporary role credentials. You need 
    permission to assume the role, and that permission comes from your base IAM user. 
  - ### when the SDK reads `role_arn` in `~/.aws/config` and sees `:role/` in the ARN, it knows:
  > "This is not a user. I cannot sign requests directly with this. I must call STS AssumeRole to get 
    temporary credentials that represent this role." 

  - **NOTE:** Here, `:user` or `:role` is a `resource type` (Ex: resource type = "user"). 
  - The ARN (Amazon Resource Name) structure is AWS's way of identifying what type of thing is being referred to. 
  - The segment after the account ID is the resource type, and it changes the entire behaviour. 


- So the sequence the SDK executes silently when your app starts:
 
```
1. Read appsettings.json → "Profile": "lab-execution-role"
2. Open ~/.aws/config → find [profile lab-execution-role]
3. See role_arn → decide to call STS AssumeRole
4. Use source_profile = default → pick up IAM user credentials
5. Call AWS STS: "I am user 'anshuman', please give me temporary 
   credentials for role 'LabOrderProcessorRole'"
6. AWS checks the Trust Policy on that role (Piece 2)
7. If allowed → STS returns temporary AccessKeyId + SecretKey + SessionToken
8. SDK caches these temporary credentials
9. Every subsequent API call is signed with these temporary credentials
```


## Step 3 — Least-privilege policy to attach to the lab role
- In the AWS Console, attach this inline policy to `LabOrderProcessorRole`. It is the minimum required 
  for your API to run so far (CloudWatch Logs + STS GetCallerIdentity):

```JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowLogging",
      "Effect": "Allow",
      "Action": ["logs:CreateLogGroup","logs:CreateLogStream","logs:PutLogEvents"],
      "Resource": "arn:aws:logs:ap-south-1:123456789012:log-group:/aws/lambda/AwsCloudNative*:*"
    },
    {
      "Sid": "AllowCallerIdentity",
      "Effect": "Allow",
      "Action": "sts:GetCallerIdentity",
      "Resource": "*"
    }
  ]
}
```

- Note that `sts:GetCallerIdentity` technically requires `Resource: *` because STS does not have 
  resource-level scoping for this action — this is the documented AWS exception to the least-privilege
  resource rule.

## The Complete ARN Resource Type Cheatsheet

```
arn:aws:iam::123456789012:user/anshuman          → IAM user      (permanent, long-term keys)
arn:aws:iam::123456789012:role/SomeName          → IAM role      (must be assumed, temporary)
arn:aws:iam::123456789012:policy/SomeName        → IAM policy    (attached to users/roles)
arn:aws:iam::123456789012:group/SomeName         → IAM group     (collection of users)

arn:aws:sts::123456789012:assumed-role/X/session → Assumed role  (active temporary session)

arn:aws:s3:::my-bucket-name                      → S3 bucket     (no region, no account)
arn:aws:dynamodb:ap-south-1:123456789012:table/X → DynamoDB table
arn:aws:sqs:ap-south-1:123456789012:queue-name   → SQS queue
arn:aws:lambda:ap-south-1:123456789012:function:X→ Lambda function

```

- The resource type segment (`:user/`, `:role/`, `:table/`, `:function:`) is AWS's type system embedded directly in 
  the identifier. Once you can read ARNs fluently, IAM policies, CloudWatch logs, and error messages all become
  significantly easier to debug.

## The Generic ARN Structure

```
arn  :  partition  :  service  :  region  :  account-id  :  resource-type/resource-id
 1         2            3          4            5                    6

```

- Every ARN has exactly these 6 components separated by :. Let me break each one down.

### Each Component Explained

```
arn:aws:iam::123456789012:user/anshuman
 │    │   │   │     │         │
 │    │   │   │     │         └── 6. resource-type / resource-id
 │    │   │   │     └──────────── 5. account-id
 │    │   │   └────────────────── 4. region (empty for IAM — IAM is global)
 │    │   └────────────────────── 3. service
 │    └────────────────────────── 2. partition
 └─────────────────────────────── 1. literal prefix — always "arn"

```

### COMPONENTS:
- Component 1 — `arn`:
> Always the literal string arn. Never changes. It is just a prefix that tells AWS "this is an Amazon Resource Name".

- Component 2 — `partition`
> The AWS infrastructure partition this resource lives in.

```
aws          → standard global AWS (what you will always use)
aws-cn       → AWS China regions (separate infrastructure)
aws-us-gov   → AWS GovCloud (US government isolated regions)

```

- Component 3 — `service`
> The AWS service that owns this resource.

```
iam          → Identity and Access Management
s3           → Simple Storage Service
dynamodb     → DynamoDB
sqs          → Simple Queue Service
sns          → Simple Notification Service
lambda       → Lambda
ecs          → Elastic Container Service
ec2          → Elastic Compute Cloud
cognito-idp  → Cognito User Pools
secretsmanager → Secrets Manager
logs         → CloudWatch Logs

```

- Component 4 — region
> The AWS region where the resource lives.

```
ap-south-1      → Asia Pacific (Mumbai)
us-east-1       → US East (N. Virginia)
eu-west-1       → Europe (Ireland)
ap-southeast-1  → Asia Pacific (Singapore)

```

- Empty for global services. IAM and S3 are global — they have no region in their ARN:

```
arn:aws:iam::123456789012:user/anshuman
             ↑
             empty — IAM is global, not region-specific

arn:aws:s3:::my-bucket-name
          ↑↑
          both region and account-id are empty — S3 bucket names are globally unique

```


- Component 5 — `account-id`
> The 12-digit AWS account number that owns this resource.

```
123456789012    → your AWS account ID

```

- Empty for S3 buckets because bucket names are globally unique across all AWS accounts — no 
  account ID is needed to identify them.


- Component 6 — resource-type/resource-id
> This is the most variable part. The format depends on the service:

```
resource-type/resource-id    → most services
resource-type:resource-id    → some services use colon instead of slash
resource-id only             → when type is implied by the service

```

### Side by Side — All Formats You Will Use:

```
SERVICE          ARN
─────────────────────────────────────────────────────────────────────────────────
IAM user         arn:aws:iam::123456789012:user/anshuman
IAM role         arn:aws:iam::123456789012:role/LabOrderProcessorRole
IAM policy       arn:aws:iam::123456789012:policy/OrdersReadPolicy

S3 bucket        arn:aws:s3:::my-bucket-name
S3 object        arn:aws:s3:::my-bucket-name/uploads/file.pdf

DynamoDB table   arn:aws:dynamodb:ap-south-1:123456789012:table/Orders
DynamoDB index   arn:aws:dynamodb:ap-south-1:123456789012:table/Orders/index/GSI-Status

SQS queue        arn:aws:sqs:ap-south-1:123456789012:acn-orders-events-prod
SNS topic        arn:aws:sns:ap-south-1:123456789012:acn-orders-notifications

Lambda function  arn:aws:lambda:ap-south-1:123456789012:function:OrderProcessor
ECS cluster      arn:aws:ecs:ap-south-1:123456789012:cluster/acn-orders-cluster
ECS task         arn:aws:ecs:ap-south-1:123456789012:task/acn-orders-cluster/a1b2c3

Cognito pool     arn:aws:cognito-idp:ap-south-1:123456789012:userpool/ap-south-1_AbCdEf
Secrets Manager  arn:aws:secretsmanager:ap-south-1:123456789012:secret:acn/orders/db-Xk92a
CloudWatch logs  arn:aws:logs:ap-south-1:123456789012:log-group:/aws/lambda/OrderProcessor

```

### The Pattern That Makes It Readable
> Once you internalise the 6 components, you can read or build any ARN instantly:

```
Question                    Component       Where to look
──────────────────────────────────────────────────────────
What type of identifier?    1 → "arn"       always present
Which cloud infrastructure? 2 → partition   almost always "aws"
Which AWS product?          3 → service     maps to SDK client name
Which geography?            4 → region      empty if global service
Whose account?              5 → account-id  your 12-digit account number
Which specific resource?    6 → type/id     the actual resource name

```

- One Rule to Remember
> If a field is empty, the colons stay.
- This is why S3 bucket ARNs look like they have "extra" colons:

```
arn:aws:s3:::my-bucket-name
          ││
          │└── account-id is empty (S3 names are globally unique)
          └─── region is empty (S3 is a global service)

The two colons are not a typo — they are placeholders for the empty fields.

```