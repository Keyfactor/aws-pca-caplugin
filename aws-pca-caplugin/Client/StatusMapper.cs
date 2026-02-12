using Amazon.ACMPCA.Model;
using Amazon.Runtime;
using Keyfactor.PKI.Enums.EJBCA;

namespace Keyfactor.Extensions.CAPlugin.AWS.Client;

internal static class AwsPcaStatusMapper
{
    public static int FromGetCertificateSuccess()
    {
        return (int)EndEntityStatus.GENERATED;
    }

    public static int FromGetCertificateException(Exception ex)
    {
        // Pending issuance: PCA explicitly tells you “still working”
        if (ex is RequestInProgressException)
            return (int)EndEntityStatus.INPROCESS;

        // Not found / wrong ARN / not yet present (outside RequestInProgress)
        if (ex is ResourceNotFoundException)
            return (int)EndEntityStatus.FAILED;

        if (ex is AmazonServiceException)
            return (int)EndEntityStatus.FAILED;

        return (int)EndEntityStatus.FAILED;
    }

    /// <summary>
    ///     Best-effort mapping from PCA audit report eventType to Keyfactor status.
    /// </summary>
    public static int FromAuditEventType(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return (int)EndEntityStatus.FAILED;

        switch (eventType.Trim().ToUpperInvariant())
        {
            case "CERTIFICATE_ISSUED":
            case "ISSUED":
            case "ISSUE_CERTIFICATE":
                return (int)EndEntityStatus.GENERATED;

            case "CERTIFICATE_REVOKED":
            case "REVOKED":
            case "REVOKE_CERTIFICATE":
                return (int)EndEntityStatus.REVOKED;

            // Some AWS report variants/log pipelines include request/creation-like events
            case "CERTIFICATE_REQUESTED":
            case "REQUEST_RECEIVED":
            case "CREATED":
                return (int)EndEntityStatus.INPROCESS;

            default:
                return (int)EndEntityStatus.FAILED;
        }
    }
}