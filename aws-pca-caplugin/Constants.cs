// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

namespace Keyfactor.Extensions.CAPlugin.AWS;

public static class Constants
{
    public static Dictionary<string, string> TemplateARNs = new()
    {
        {
            "EndEntity",
            "arn:aws:acm-pca:::template/EndEntityCertificate/V1"
        },
        {
            "EndEntityClientAuth",
            "arn:aws:acm-pca:::template/EndEntityClientAuthCertificate/V1"
        },
        {
            "EndEntityServerAuth",
            "arn:aws:acm-pca:::template/EndEntityServerAuthCertificate/V1"
        }
    };

    // ---------------------------------------------------------------------
    // ConnectionInfo keys
    // ---------------------------------------------------------------------
    // Note: This CA Plugin aligns its auth-related field names to the
    // aws-auth-library specification so it can be used interchangeably.
    // See: Keyfactor/aws-auth-library README "Specification" section.

    /// <summary>
    ///     Role ARN for authentication. Supports the special case:
    ///     [profile]arn:aws:iam::123456789012:role/RoleName
    ///     which is only used when <see cref="UseDefaultSdkAuth" /> is true.
    /// </summary>
    public static string ROLE_ARN = "RoleArn";

    /// <summary>AWS Region (single region only).</summary>
    public static string REGION = "Region";

    /// <summary>AWS PCA Certificate Authority ARN.</summary>
    public static string CA_ARN = "CAArn";

    /// <summary>S3 bucket name used for PCA audit reports.</summary>
    public static string S3_BUCKET = "S3Bucket";

    // ---- Auth input object specification (aws-auth-library) ----
    public static string USE_DEFAULT_SDK_AUTH = "UseDefaultSdkAuth";
    public static string DEFAULT_SDK_ASSUME_ROLE = "DefaultSdkAssumeRole";
    public static string USE_OAUTH = "UseOAuth";
    public static string OAUTH_SCOPE = "OAuthScope";
    public static string OAUTH_GRANT_TYPE = "OAuthGrantType";
    public static string OAUTH_URL = "OAuthUrl";
    public static string OAUTH_CLIENT_ID = "OAuthClientId";
    public static string OAUTH_CLIENT_SECRET = "OAuthClientSecret";
    public static string USE_IAM = "UseIAM";
    public static string IAM_USER_ACCESS_KEY = "IAMUserAccessKey";
    public static string IAM_USER_ACCESS_SECRET = "IAMUserAccessSecret";
    public static string EXTERNAL_ID = "ExternalId";
    public static string Enabled = "Enabled";


    public static List<string> GetTemplateTypes()
    {
        return TemplateARNs.Keys.ToList();
    }
}

public class EnrollmentConfigConstants
{
    public const string LifetimeDays = "LifetimeDays";
    public const string SigningAlgorithm = "SigningAlgorithm";
}