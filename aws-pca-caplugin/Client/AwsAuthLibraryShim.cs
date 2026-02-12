using Amazon.Runtime;
using Keyfactor.Extensions.Aws;
using Keyfactor.Extensions.Aws.Models;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.CAPlugin.AWS.Client;

/// <summary>
///     Strongly-typed adapter over Keyfactor.Extensions.Aws (aws-auth-library).
///     No reflection and no fallback behavior.
/// </summary>
internal static class AwsAuthLibraryShim
{
    public static AWSCredentials ResolveCredentials(
        string roleArn,
        string regionSystemName,
        IReadOnlyDictionary<string, object> connectionInfo,
        ILogger logger)
    {
        logger.MethodEntry(LogLevel.Debug);

        if (string.IsNullOrWhiteSpace(roleArn))
            throw new ArgumentException("RoleArn is required.", nameof(roleArn));
        if (string.IsNullOrWhiteSpace(regionSystemName))
            throw new ArgumentException("Region is required.", nameof(regionSystemName));
        if (connectionInfo is null)
            throw new ArgumentNullException(nameof(connectionInfo));

        // Map CA Plugin fields -> aws-auth-library models.
        // Note: aws-auth-library uses RoleARN (caps) and AuthCustomFieldParameters uses IamUserAccessKey naming.
        var custom = new AuthCustomFieldParameters
        {
            UseDefaultSdkAuth = GetBool(connectionInfo, Constants.USE_DEFAULT_SDK_AUTH),
            DefaultSdkAssumeRole = GetBool(connectionInfo, Constants.DEFAULT_SDK_ASSUME_ROLE),
            UseOAuth = GetBool(connectionInfo, Constants.USE_OAUTH),
            OAuthScope = GetString(connectionInfo, Constants.OAUTH_SCOPE),
            OAuthGrantType = GetString(connectionInfo, Constants.OAUTH_GRANT_TYPE),
            OAuthUrl = GetString(connectionInfo, Constants.OAUTH_URL),
            OAuthClientId = GetString(connectionInfo, Constants.OAUTH_CLIENT_ID),
            OAuthClientSecret = GetString(connectionInfo, Constants.OAUTH_CLIENT_SECRET),
            UseIAM = GetBool(connectionInfo, Constants.USE_IAM),
            // Soft back-compat: if legacy AccessKey/AccessSecret are set, map them into IAM fields.
            IamUserAccessKey = GetString(connectionInfo, Constants.IAM_USER_ACCESS_KEY),
            IamUserAccessSecret = GetString(connectionInfo, Constants.IAM_USER_ACCESS_SECRET),
            ExternalId = GetString(connectionInfo, Constants.EXTERNAL_ID)
        };

        var authParams = new AuthenticationParameters
        {
            RoleARN = roleArn,
            Region = regionSystemName,
            CustomFields = custom
        };

        // If you have IPAMSecretResolver available in your hosting environment, pass it here.
        // In AnyGateway CAPlugin context we don't have it directly, so we pass null.
        var util = new AwsAuthUtility(null);
        var extCred = util.GetCredentials(authParams);

        var creds = extCred.GetAwsCredentialObject();
        logger.LogInformation(
            $"AWS credentials resolved via aws-auth-library. Method={extCred.CredentialMethod} RoleArn={extCred.RoleArn} Region={extCred.Region?.SystemName}");
        logger.MethodExit(LogLevel.Debug);
        return creds;
    }

    private static string? GetString(IReadOnlyDictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return null;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static bool GetBool(IReadOnlyDictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return false;
        if (v is bool b) return b;

        // AnyGateway sometimes supplies "true"/"false" as strings.
        var s = v.ToString();
        if (bool.TryParse(s, out var parsed)) return parsed;

        // Also handle 0/1.
        if (int.TryParse(s, out var i)) return i != 0;

        return false;
    }
}