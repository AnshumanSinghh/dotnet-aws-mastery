using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Options
{
    /// <summary>
    /// WHY: Options Pattern binds configuration to a typed class.
    /// ValidateOnStart catches misconfiguration at startup, not at runtime.
    /// OBJECTIVE: Centralise all AWS config in one place.
    /// PITFALL: Never put credentials here. Region and profile are
    /// infrastructure metadata, not secrets. Secrets go to Secrets Manager.
    /// </summary>
    public sealed class AwsOptions
    {
        public const string SectionName = "Aws";

        [Required(AllowEmptyStrings = false)]
        public string Region { get; init; } = string.Empty;

        // Optional: only used in local dev to pick a named profile
        // from ~/.aws/credentials. In Lambda/ECS this is always null.
        public string? Profile { get; init; }

        // The account ID is useful for constructing ARNs at runtime.
        // Not a secret — it's visible in the AWS Console URL.
        [Required(AllowEmptyStrings = false)]
        public string AccountId { get; init; } = string.Empty;
    }
}
