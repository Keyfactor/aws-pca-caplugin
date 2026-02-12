namespace Keyfactor.Extensions.CAPlugin.AWS.Models;

/// <summary>
///     Named to match CSC-style error plumbing: response.RegistrationError?.Description
/// </summary>
public sealed class RegistrationError
{
    public string? Description { get; set; }
    public string? ErrorCode { get; set; }
    public string? ExceptionType { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? AwsRequestId { get; set; }
    public string? Raw { get; set; }
}

public sealed class IssueCertificateResult
{
    public string? CertificateArn { get; set; }

    /// <summary>PCA request id segment (after /certificate/)</summary>
    public string? CertificateId { get; set; }
}

public sealed class IssueCertificateResponse
{
    public IssueCertificateResult? Result { get; set; }
    public RegistrationError? RegistrationError { get; set; }
}

/// <summary>
///     Client-side request DTO for issuing a certificate via AWS ACM PCA.
///     The CAPlugin layer should never deal with AWS SDK request types directly.
/// </summary>
public sealed class IssueCertificateRequest
{
    /// <summary>CSR PEM text (-----BEGIN CERTIFICATE REQUEST----- ...)</summary>
    public string CsrPem { get; set; } = string.Empty;

    /// <summary>Optional product id (template-ish) that plugin may want to flow through.</summary>
    public string? ProductId { get; set; }

    /// <summary>Optional desired term in days. If null, defaults to 365.</summary>
    public int? ValidityDays { get; set; }

    /// <summary>
    ///     Optional override for ACM PCA IssueCertificate.SigningAlgorithm.
    ///     If null/empty, the client auto-selects a compatible default based on the CA KeyAlgorithm.
    /// </summary>
    public string? SigningAlgorithm { get; set; }

    /// <summary>Optional idempotency token.</summary>
    public string? IdempotencyToken { get; set; }
}

/// <summary>
///     Response wrapper that carries the issued certificate payload and status.: contains the certificate payload + a
///     Keyfactor-accepted numeric status.
/// </summary>
public sealed class CertificateResponse
{
    /// <summary>Base64 DER end-entity certificate (no PEM headers).</summary>
    public string? Certificate { get; set; }

    /// <summary>Optional: PEM chain text, if you want to do additional parsing upstream.</summary>
    public string? CertificatePemChain { get; set; }

    /// <summary>
    ///     Keyfactor-accepted numeric status (Keyfactor.PKI.Enums.EJBCA.EndEntityStatus values).
    /// </summary>
    public int Status { get; set; }

    public string? CertificateType { get; set; }
    public string? Uuid { get; set; }
    public DateTime? OrderDate { get; set; }

    public RegistrationError? RegistrationError { get; set; }
}

public sealed class RevokeResponse
{
    public bool? RevokeSuccess { get; set; }

    /// <summary>Keyfactor numeric status: REVOKED or FAILED.</summary>
    public int Status { get; set; }

    public RegistrationError? RegistrationError { get; set; }
}

public sealed class AuditReportResponse
{
    public List<ACMPCACertificate>? Result { get; set; }
    public RegistrationError? RegistrationError { get; set; }
}

/// <summary>
///     AWS PCA audit report record.
///     Keep property casing aligned with AWS JSON.
/// </summary>
public sealed class ACMPCACertificate
{
    public string? eventType { get; set; }
    public string? certificateSerial { get; set; }
    public string? certificateAuthorityArn { get; set; }
    public string? certificateArn { get; set; }
    public string? createdAt { get; set; }
    public string? revokedAt { get; set; }
    public string? revocationReason { get; set; }
}