using Amazon.ACMPCA;
using Keyfactor.Extensions.CAPlugin.AWS.Models;
using IssueCertificateResponse = Keyfactor.Extensions.CAPlugin.AWS.Models.IssueCertificateResponse;

namespace Keyfactor.Extensions.CAPlugin.AWS.Interfaces;

public interface IAwsPcaClient
{
    Task<IssueCertificateResponse> SubmitIssueCertificateAsync(
        IssueCertificateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a cert by "certificateId" which is the PCA requestId segment (same conceptual role as CSC UUID).
    /// </summary>
    Task<CertificateResponse> SubmitGetCertificateAsync(
        string certificateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a cert by full ARN.
    /// </summary>
    Task<CertificateResponse> SubmitGetCertificateByArnAsync(
        string certificateArn,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revoke in PCA is by certificate serial (hex). We keep CSC-like signature: one string ID.
    ///     In AWS plugin, pass hexSerialNumber as certificateId.
    /// </summary>
    Task<RevokeResponse> SubmitRevokeCertificateAsync(
        string certificateId,
        RevocationReason revocationReason,
        CancellationToken cancellationToken = default);


    /// <summary>
    ///     Used for sync scenarios where you need an issuance/revocation log.
    ///     PCA doesn't have "list certs", so we proxy to the PCA audit report.
    /// </summary>
    Task<AuditReportResponse> SubmitAuditReportAsync(
        CancellationToken cancellationToken = default);

    Task PingAsync(CancellationToken cancellationToken = default);
}