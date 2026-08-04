// Copyright 2022 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Security.Cryptography.X509Certificates;
using System.Text;
using Amazon;
using Amazon.ACMPCA;
using Amazon.ACMPCA.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Keyfactor.AnyGateway.Extensions;
using Keyfactor.Extensions.CAPlugin.AWS.Interfaces;
using Keyfactor.Extensions.CAPlugin.AWS.Models;
using Keyfactor.Logging;
using Keyfactor.PKI.Enums.EJBCA;
using Keyfactor.PKI.PEM;
using Keyfactor.PKI.X509;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IssueCertificateRequest = Keyfactor.Extensions.CAPlugin.AWS.Models.IssueCertificateRequest;
using IssueCertificateResponse = Keyfactor.Extensions.CAPlugin.AWS.Models.IssueCertificateResponse;
using SigningAlgorithm = Amazon.ACMPCA.SigningAlgorithm;

namespace Keyfactor.Extensions.CAPlugin.AWS.Client;

/// <summary>
///     CSC-style client wrapper for AWS ACM PCA.
///     - Pulls config from IAnyCAPluginConfigProvider
///     - Returns response DTOs with RegistrationError instead of throwing for normal failure paths
///     - Normalizes all certificate statuses to Keyfactor's EndEntityStatus numeric values
/// </summary>
public sealed class AwsPcaClient : IAwsPcaClient
{
    private const string ENHANCED_KEY_USAGE_OID = "2.5.29.37";
    private const string SERVER_AUTH_OID = "1.3.6.1.5.5.7.3.1";
    private const string CLIENT_AUTH_OID = "1.3.6.1.5.5.7.3.2";
    private const string CODE_SIGNING_OID = "1.3.6.1.5.5.7.3.3";

    private readonly SemaphoreSlim _caInfoLock = new(1, 1);
    private readonly AWSCredentials AwsCredentials;
    private readonly string CaArn;
    private readonly ILogger Logger;

    private readonly IAmazonACMPCA PcaClient;
    private readonly RegionEndpoint Region;
    private readonly string RoleArn;
    private readonly string S3Bucket;
    private string? _caKeyAlgorithmName;
    private IAmazonS3? S3Client;

    public AwsPcaClient(IAnyCAPluginConfigProvider configProvider)
    {
        Logger = LogHandler.GetClassLogger<AwsPcaClient>();

        if (configProvider?.CAConnectionData == null)
            throw new ArgumentNullException(nameof(configProvider),
                "Config provider and CAConnectionData are required.");

        var enabled = bool.Parse(GetRequiredString(configProvider, "Enabled"));
        if (!enabled)
        {
            Logger.LogWarning($"The CA is currently in the Disabled state. It must be Enabled to perform operations. Skipping config validation and AWS PCA Client creation...");
            Logger.MethodExit();
            return;
        }

        CaArn = GetRequiredString(configProvider, ConfigKeys.CaArn);
        S3Bucket = GetRequiredString(configProvider, ConfigKeys.S3Bucket);

        RoleArn = GetRequiredString(configProvider, ConfigKeys.RoleArn);
        var regionName = GetRequiredString(configProvider, ConfigKeys.Region);
        Region = RegionEndpoint.GetBySystemName(regionName);

        // Resolve AWS credentials using Keyfactor.Extensions.Aws.Auth if available,
        // otherwise fall back to basic creds (legacy) or SDK default resolution.
        AwsCredentials = AwsAuthLibraryShim.ResolveCredentials(
            RoleArn,
            regionName,
            configProvider.CAConnectionData,
            Logger);

        PcaClient = new AmazonACMPCAClient(AwsCredentials, Region);
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        Logger.MethodEntry();

        try
        {
            _ = await PcaClient.DescribeCertificateAuthorityAsync(
                new DescribeCertificateAuthorityRequest { CertificateAuthorityArn = CaArn },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogError($"There was an error contacting AWS PCA: {e.Message}.");
            throw new Exception($"Error attempting to ping AWS PCA: {e.Message}.", e);
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    public async Task<IssueCertificateResponse> SubmitIssueCertificateAsync(
        IssueCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.MethodEntry(LogLevel.Debug);

        if (request == null)
            return new IssueCertificateResponse
            {
                RegistrationError = new RegistrationError
                {
                    Description = "Request was null.",
                    ErrorCode = "InvalidRequest"
                }
            };

        if (string.IsNullOrWhiteSpace(request.CsrPem))
            return new IssueCertificateResponse
            {
                RegistrationError = new RegistrationError
                {
                    Description = "CSR PEM is required.",
                    ErrorCode = "InvalidRequest"
                }
            };

        try
        {
            var csrBytes = PemUtilities.DERToPEM(PemUtilities.PEMToDER(request.CsrPem),
                PemUtilities.PemObjectType.CertRequest);


            var signingAlgoRes = await ResolveSigningAlgorithmAsync(request.SigningAlgorithm, cancellationToken)
                .ConfigureAwait(false);
            if (signingAlgoRes.Error != null)
                return new IssueCertificateResponse { RegistrationError = signingAlgoRes.Error };

            // Map the requested ProductId to its AWS PCA template ARN. Without this,
            // PCA falls back to EndEntityCertificate/V1 and every cert gets the default
            // Server+Client Auth EKU combination regardless of the selected product.
            if (string.IsNullOrWhiteSpace(request.ProductId) ||
                !Constants.TemplateARNs.TryGetValue(request.ProductId, out var templateArn))
                return new IssueCertificateResponse
                {
                    RegistrationError = new RegistrationError
                    {
                        Description = string.IsNullOrWhiteSpace(request.ProductId)
                            ? "ProductId is required to resolve the AWS PCA template ARN."
                            : $"Unsupported ProductId '{request.ProductId}'. Supported: {string.Join(", ", Constants.TemplateARNs.Keys)}",
                        ErrorCode = "InvalidRequest"
                    }
                };

            var signingAlgorithm = signingAlgoRes.Value!;
            var issueReq = new Amazon.ACMPCA.Model.IssueCertificateRequest
            {
                CertificateAuthorityArn = CaArn,
                Csr = new MemoryStream(Encoding.ASCII.GetBytes(csrBytes)),
                SigningAlgorithm = signingAlgorithm,
                TemplateArn = templateArn,
                IdempotencyToken = request.IdempotencyToken ?? Guid.NewGuid().ToString("N"),
                Validity = new Validity
                {
                    Type = ValidityPeriodType.DAYS,
                    Value = request.ValidityDays.HasValue && request.ValidityDays.Value > 0
                        ? request.ValidityDays.Value
                        : 365
                }
            };

            var resp = await PcaClient.IssueCertificateAsync(issueReq, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(resp?.CertificateArn))
                return new IssueCertificateResponse
                {
                    RegistrationError = new RegistrationError
                    {
                        Description = "IssueCertificate succeeded but CertificateArn was empty.",
                        ErrorCode = "IssueFailed"
                    }
                };

            return new IssueCertificateResponse
            {
                Result = new IssueCertificateResult
                {
                    CertificateArn = resp.CertificateArn,
                    CertificateId = SafeRequestIdFromArn(resp.CertificateArn)
                }
            };
        }
        catch (Exception ex)
        {
            return new IssueCertificateResponse { RegistrationError = ToRegistrationError(ex) };
        }
        finally
        {
            Logger.MethodExit(LogLevel.Debug);
        }
    }

    public Task<CertificateResponse> SubmitGetCertificateAsync(
        string certificateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(certificateId))
            return Task.FromResult(new CertificateResponse
            {
                Status = (int)EndEntityStatus.FAILED,
                RegistrationError = new RegistrationError
                {
                    Description = "certificateId is required.",
                    ErrorCode = "InvalidRequest"
                }
            });

        var arn = $"{CaArn}/certificate/{certificateId}";
        return SubmitGetCertificateByArnAsync(arn, cancellationToken);
    }

    public async Task<CertificateResponse> SubmitGetCertificateByArnAsync(
        string certificateArn,
        CancellationToken cancellationToken = default)
    {
        Logger.MethodEntry(LogLevel.Debug);

        if (string.IsNullOrWhiteSpace(certificateArn))
            return new CertificateResponse
            {
                Status = (int)EndEntityStatus.FAILED,
                RegistrationError = new RegistrationError
                {
                    Description = "certificateArn is required.",
                    ErrorCode = "InvalidRequest"
                }
            };

        try
        {
            var req = new GetCertificateRequest
            {
                CertificateArn = certificateArn,
                CertificateAuthorityArn = CaArn
            };

            // bounded retry for PCA's eventual issuance
            const int maxAttempts = 30;
            var delay = TimeSpan.FromSeconds(1);

            GetCertificateResponse? resp = null;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
                try
                {
                    resp = await PcaClient.GetCertificateAsync(req, cancellationToken).ConfigureAwait(false);
                    lastError = null;
                    break;
                }
                catch (RequestInProgressException rip)
                {
                    lastError = rip;
                    if (attempt < maxAttempts)
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

            if (resp == null)
            {
                // still in progress after retries
                var status = lastError != null
                    ? AwsPcaStatusMapper.FromGetCertificateException(lastError)
                    : (int)EndEntityStatus.INPROCESS;

                return new CertificateResponse
                {
                    Uuid = SafeRequestIdFromArn(certificateArn),
                    Status = status,
                    RegistrationError = lastError != null ? ToRegistrationError(lastError) : null
                };
            }

            if (string.IsNullOrWhiteSpace(resp.Certificate))
                return new CertificateResponse
                {
                    Uuid = SafeRequestIdFromArn(certificateArn),
                    Status = (int)EndEntityStatus.FAILED,
                    RegistrationError = new RegistrationError
                    {
                        Description = $"Certificate with ARN {certificateArn} not found or empty.",
                        ErrorCode = "NotFound"
                    }
                };

            // Parse cert so we can infer "product" from EKU and use real notBefore as order date
            var x509 = CertificateConverterFactory.FromPEM(resp.Certificate).ToX509Certificate2();

            var product =
                InferAndValidateTemplateTypeKey(
                    x509); // e.g. EndEntityServerAuth / EndEntityClientAuth / EndEntity / Unknown

            // Return leaf as base64 DER (no PEM headers), plus chain PEM if you want it for upstream extraction.
            var leafBase64 = resp.Certificate;
            var pemChain = resp.CertificateChain;

            return new CertificateResponse
            {
                Uuid = SafeRequestIdFromArn(certificateArn),

                // IMPORTANT: Keyfactor-friendly base64 DER
                Certificate = leafBase64,

                // Keep chain PEM for any downstream consumers that want it
                CertificatePemChain = pemChain,

                Status = AwsPcaStatusMapper.FromGetCertificateSuccess(),

                // Use the certificate’s validity start as “submission/order” timestamp (like the old code)
                OrderDate = x509.NotBefore.ToUniversalTime(),

                // Mirror old "ProductID" concept through your model field
                CertificateType = product
            };
        }
        catch (Exception ex)
        {
            return new CertificateResponse
            {
                Uuid = SafeRequestIdFromArn(certificateArn),
                Status = AwsPcaStatusMapper.FromGetCertificateException(ex),
                RegistrationError = ToRegistrationError(ex)
            };
        }
        finally
        {
            Logger.MethodExit(LogLevel.Debug);
        }
    }

    public async Task<RevokeResponse> SubmitRevokeCertificateAsync(
        string certificateId,
        RevocationReason revocationReason,
        CancellationToken cancellationToken = default)
    {
        Logger.MethodEntry(LogLevel.Debug);

        if (string.IsNullOrWhiteSpace(certificateId))
            return new RevokeResponse
            {
                Status = (int)EndEntityStatus.FAILED,
                RegistrationError = new RegistrationError
                {
                    Description = "certificateId is required. For AWS PCA this should be the hex serial number.",
                    ErrorCode = "InvalidRequest"
                }
            };

        try
        {
            var revokeReq = new RevokeCertificateRequest
            {
                CertificateAuthorityArn = CaArn,
                CertificateSerial = certificateId,
                RevocationReason = revocationReason
            };

            _ = await PcaClient.RevokeCertificateAsync(revokeReq, cancellationToken)
                .ConfigureAwait(false);

            return new RevokeResponse
            {
                RevokeSuccess = true,
                Status = (int)EndEntityStatus.REVOKED
            };
        }
        catch (Exception ex)
        {
            return new RevokeResponse
            {
                RevokeSuccess = false,
                Status = (int)EndEntityStatus.FAILED,
                RegistrationError = ToRegistrationError(ex)
            };
        }
        finally
        {
            Logger.MethodExit(LogLevel.Debug);
        }
    }


    public async Task<AuditReportResponse> SubmitAuditReportAsync(CancellationToken cancellationToken = default)
    {
        Logger.MethodEntry(LogLevel.Debug);

        try
        {
            var createReq = new CreateCertificateAuthorityAuditReportRequest
            {
                CertificateAuthorityArn = CaArn,
                S3BucketName = S3Bucket,
                AuditReportResponseFormat = AuditReportResponseFormat.JSON
            };

            var createResp = await PcaClient
                .CreateCertificateAuthorityAuditReportAsync(createReq, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(createResp?.S3Key))
                return new AuditReportResponse
                {
                    RegistrationError = new RegistrationError
                    {
                        Description = "Audit report request succeeded but no S3Key was returned.",
                        ErrorCode = "AuditReportFailed"
                    }
                };

            var json = await DownloadS3ObjectAsStringAsync(S3Bucket, createResp.S3Key, cancellationToken)
                .ConfigureAwait(false);

            var parsed = JsonConvert.DeserializeObject<List<ACMPCACertificate>>(json) ??
                         new List<ACMPCACertificate>();
            return new AuditReportResponse { Result = parsed };
        }
        catch (Exception ex)
        {
            return new AuditReportResponse { RegistrationError = ToRegistrationError(ex) };
        }
        finally
        {
            Logger.MethodExit(LogLevel.Debug);
        }
    }

    // -------------------------
    // Internals
    // -------------------------

    private async Task<string> DownloadS3ObjectAsStringAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        var s3 = await GetOrCreateS3ClientAsync(cancellationToken).ConfigureAwait(false);

        // ACM PCA audit-report generation is asynchronous: CreateCertificateAuthorityAuditReport
        // returns the S3 key before AWS has finished writing the object. Downloading immediately
        // races the report generation and fails with NoSuchKey ("The specified key does not
        // exist"). Poll for the object with backoff until it appears (or we time out).
        var deadline = DateTime.UtcNow.AddMinutes(3);
        var delay = TimeSpan.FromSeconds(2);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await s3.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucket,
                    Key = key
                }, cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(response.ResponseStream);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (
                (ex.ErrorCode == "NoSuchKey" || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                && DateTime.UtcNow < deadline)
            {
                Logger.LogDebug(
                    $"Audit report not yet available in S3 (bucket={bucket}, key={key}); " +
                    $"retrying in {delay.TotalSeconds:0}s.");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 15));
            }
        }
    }

    private async Task<IAmazonS3> GetOrCreateS3ClientAsync(CancellationToken cancellationToken)
    {
        if (S3Client != null) return S3Client;

        var creds = AwsCredentials;

        // bucket-region discovery (best-effort)
        var region = Region;
        try
        {
            using var probe = new AmazonS3Client(creds, RegionEndpoint.USEast1);
            var loc = await probe.GetBucketLocationAsync(new GetBucketLocationRequest
            {
                BucketName = S3Bucket
            }, cancellationToken).ConfigureAwait(false);

            var sys = NormalizeS3LocationConstraint(loc.Location);
            region = RegionEndpoint.GetBySystemName(sys);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                $"Could not determine S3 bucket region for {S3Bucket}. Falling back to PCA region {Region.SystemName}. Details: {ex.Message}");
        }

        S3Client = new AmazonS3Client(creds, region);
        return S3Client;
    }

    private static string NormalizeS3LocationConstraint(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return "us-east-1";
        if (string.Equals(location, "EU", StringComparison.OrdinalIgnoreCase)) return "eu-west-1";
        return location;
    }

    private static string GetRequiredString(IAnyCAPluginConfigProvider provider, string key)
    {
        if (!provider.CAConnectionData.TryGetValue(key, out var obj) || obj == null)
            throw new InvalidOperationException($"Missing required configuration value '{key}'.");

        var str = obj.ToString();
        if (string.IsNullOrWhiteSpace(str))
            throw new InvalidOperationException($"Configuration value '{key}' is empty.");

        return str!;
    }

    private static string SafeRequestIdFromArn(string certArn)
    {
        var parts = certArn.Split(new[] { "/certificate/" }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : certArn;
    }

    private static RegistrationError ToRegistrationError(Exception ex)
    {
        var err = new RegistrationError
        {
            ExceptionType = ex.GetType().FullName,
            Description = ex.Message,
            Raw = ex.ToString()
        };

        if (ex is AmazonServiceException ase)
        {
            err.ErrorCode = ase.ErrorCode;
            err.HttpStatusCode = (int)ase.StatusCode;
            err.AwsRequestId = ase.RequestId;
        }

        return err;
    }

    private static byte[] PemBlockToDer(string pem, string label)
    {
        // Accept either CSR PEM already, or base64 CSR without headers.
        if (!pem.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase))
            pem = $"-----BEGIN {label}-----{Wrap64(pem)}-----END {label}-----";

        var begin = $"-----BEGIN {label}-----";
        var end = $"-----END {label}-----";

        var start = pem.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
        if (start < 0) throw new FormatException($"PEM does not contain '{begin}'.");

        start += begin.Length;
        var stop = pem.IndexOf(end, start, StringComparison.OrdinalIgnoreCase);
        if (stop < 0) throw new FormatException($"PEM does not contain '{end}'.");

        var b64 = pem.Substring(start, stop - start)
            .Replace("", "")
            .Replace("", "")
            .Trim();

        return Convert.FromBase64String(b64);
    }

    private static string Wrap64(string b64)
    {
        b64 = b64.Replace("", "").Replace("", "").Trim();
        var sb = new StringBuilder();
        for (var i = 0; i < b64.Length; i += 64)
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    ///     Infers one of the supported template type keys:
    ///     EndEntity, EndEntityClientAuth, EndEntityServerAuth, CodeSigning.
    ///     Returns "Unknown" if certificate parsing/inspection fails.
    /// </summary>
    public static string InferTemplateTypeKey(X509Certificate2 cert)
    {
        try
        {
            bool server = false, client = false, codeSigning = false;

            // Find EKU extension (do not rely on indexer throwing)
            var eku = cert.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .FirstOrDefault(x => string.Equals(x.Oid?.Value, ENHANCED_KEY_USAGE_OID, StringComparison.Ordinal));

            if (eku != null)
                foreach (var usage in eku.EnhancedKeyUsages)
                    if (string.Equals(usage.Value, SERVER_AUTH_OID, StringComparison.Ordinal))
                        server = true;
                    else if (string.Equals(usage.Value, CLIENT_AUTH_OID, StringComparison.Ordinal))
                        client = true;
                    else if (string.Equals(usage.Value, CODE_SIGNING_OID, StringComparison.Ordinal))
                        codeSigning = true;

            // Map to your known keys
            if (codeSigning && !server && !client) return "CodeSigning";
            if (server && !client) return "EndEntityServerAuth";
            if (client && !server) return "EndEntityClientAuth";

            // both or neither -> generic end entity template
            return "EndEntity";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    ///     Optional strict validator: ensure the inferred type is one of Constants.TemplateARNs.Keys
    /// </summary>
    public static string InferAndValidateTemplateTypeKey(X509Certificate2 cert)
    {
        var key = InferTemplateTypeKey(cert);
        return Constants.TemplateARNs.ContainsKey(key) ? key : "Unknown";
    }


    private async Task<(string? Value, RegistrationError? Error)> GetCaKeyAlgorithmAsync(
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_caKeyAlgorithmName))
            return (_caKeyAlgorithmName, null);

        await _caInfoLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_caKeyAlgorithmName))
                return (_caKeyAlgorithmName, null);

            try
            {
                var resp = await PcaClient.DescribeCertificateAuthorityAsync(
                    new DescribeCertificateAuthorityRequest { CertificateAuthorityArn = CaArn },
                    cancellationToken).ConfigureAwait(false);

                // KeyAlgorithm is represented as a string in the service model; some SDK surfaces expose it as a string.
                // Use reflection to remain tolerant across AWSSDK.ACMPCA versions.
                var cfg = resp?.CertificateAuthority?.CertificateAuthorityConfiguration;
                if (cfg == null)
                    return (null,
                        new RegistrationError
                        {
                            ErrorCode = "AwsDescribeCaFailed",
                            Description = "DescribeCertificateAuthority returned no configuration."
                        });

                var prop = cfg.GetType().GetProperty("KeyAlgorithm");
                var val = prop?.GetValue(cfg)?.ToString();
                if (string.IsNullOrWhiteSpace(val))
                    return (null,
                        new RegistrationError
                        {
                            ErrorCode = "AwsDescribeCaFailed",
                            Description = "Unable to read CA KeyAlgorithm from DescribeCertificateAuthority response."
                        });

                _caKeyAlgorithmName = val.Trim();
                return (_caKeyAlgorithmName, null);
            }
            catch (Exception ex)
            {
                return (null,
                    new RegistrationError
                        { ErrorCode = "AwsDescribeCaFailed", Description = "Failed to query CA KeyAlgorithm." });
            }
        }
        finally
        {
            _caInfoLock.Release();
        }
    }

    private static bool IsKnownSigningAlgorithm(SigningAlgorithm alg)
    {
        var v = alg.Value;
        return v == SigningAlgorithm.SHA256WITHRSA.Value
               || v == SigningAlgorithm.SHA384WITHRSA.Value
               || v == SigningAlgorithm.SHA512WITHRSA.Value
               || v == SigningAlgorithm.SHA256WITHECDSA.Value
               || v == SigningAlgorithm.SHA384WITHECDSA.Value
               || v == SigningAlgorithm.SHA512WITHECDSA.Value
               || v == SigningAlgorithm.SM3WITHSM2.Value
               || v == SigningAlgorithm.ML_DSA_44.Value
               || v == SigningAlgorithm.ML_DSA_65.Value
               || v == SigningAlgorithm.ML_DSA_87.Value;
    }

    private static bool IsSigningAlgorithmCompatible(string caKeyAlgorithmName, SigningAlgorithm signingAlgorithm)
    {
        var ka = caKeyAlgorithmName.Trim();
        var sig = signingAlgorithm.Value;

        if (ka.StartsWith("RSA_", StringComparison.OrdinalIgnoreCase))
            return sig.EndsWith("WITHRSA", StringComparison.OrdinalIgnoreCase);

        if (ka.StartsWith("EC_", StringComparison.OrdinalIgnoreCase))
            return sig.EndsWith("WITHECDSA", StringComparison.OrdinalIgnoreCase);

        if (ka.Equals("SM2", StringComparison.OrdinalIgnoreCase))
            return sig.Equals(SigningAlgorithm.SM3WITHSM2.Value, StringComparison.OrdinalIgnoreCase);

        if (ka.StartsWith("ML_DSA_", StringComparison.OrdinalIgnoreCase))
            return sig.Equals(ka, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static SigningAlgorithm DefaultSigningAlgorithmForCaKey(string caKeyAlgorithmName)
    {
        var ka = caKeyAlgorithmName.Trim();

        if (ka.StartsWith("RSA_", StringComparison.OrdinalIgnoreCase))
            return SigningAlgorithm.SHA256WITHRSA;

        if (ka.StartsWith("EC_", StringComparison.OrdinalIgnoreCase))
        {
            if (ka.Equals("EC_secp384r1", StringComparison.OrdinalIgnoreCase))
                return SigningAlgorithm.SHA384WITHECDSA;

            if (ka.Equals("EC_secp521r1", StringComparison.OrdinalIgnoreCase))
                return SigningAlgorithm.SHA512WITHECDSA;

            return SigningAlgorithm.SHA256WITHECDSA;
        }

        if (ka.Equals("SM2", StringComparison.OrdinalIgnoreCase))
            return SigningAlgorithm.SM3WITHSM2;

        if (ka.Equals("ML_DSA_44", StringComparison.OrdinalIgnoreCase))
            return SigningAlgorithm.ML_DSA_44;
        if (ka.Equals("ML_DSA_65", StringComparison.OrdinalIgnoreCase))
            return SigningAlgorithm.ML_DSA_65;
        if (ka.Equals("ML_DSA_87", StringComparison.OrdinalIgnoreCase))
            return SigningAlgorithm.ML_DSA_87;

        // Fallback
        return SigningAlgorithm.SHA256WITHRSA;
    }

    private async Task<(SigningAlgorithm? Value, RegistrationError? Error)> ResolveSigningAlgorithmAsync(
        string? requestedSigningAlgorithm,
        CancellationToken cancellationToken)
    {
        var caKeyRes = await GetCaKeyAlgorithmAsync(cancellationToken).ConfigureAwait(false);
        if (caKeyRes.Error != null)
            return (null, caKeyRes.Error);

        var caKey = caKeyRes.Value!;
        if (!string.IsNullOrWhiteSpace(requestedSigningAlgorithm))
        {
            var resolved = SigningAlgorithm.FindValue(requestedSigningAlgorithm.Trim());
            if (!IsKnownSigningAlgorithm(resolved))
                return (null, new RegistrationError
                {
                    ErrorCode = "InvalidConfiguration",
                    Description =
                        $"SigningAlgorithm '{requestedSigningAlgorithm}' is not a supported AWS ACM PCA SigningAlgorithm value."
                });

            if (!IsSigningAlgorithmCompatible(caKey, resolved))
                return (null, new RegistrationError
                {
                    ErrorCode = "InvalidConfiguration",
                    Description =
                        $"SigningAlgorithm '{requestedSigningAlgorithm}' is not compatible with CA KeyAlgorithm '{caKey}'."
                });

            return (resolved, null);
        }

        return (DefaultSigningAlgorithmForCaKey(caKey), null);
    }

    private static class ConfigKeys
    {
        public const string CaArn = "CAArn";
        public const string RoleArn = "RoleArn";
        public const string Region = "Region";
        public const string S3Bucket = "S3Bucket";
    }
}
