// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Amazon.ACMPCA;
using Keyfactor.AnyGateway.Extensions;
using Keyfactor.Extensions.CAPlugin.AWS.Client;
using Keyfactor.Extensions.CAPlugin.AWS.Interfaces;
using Keyfactor.Extensions.CAPlugin.AWS.Models;
using Keyfactor.Logging;
using Keyfactor.PKI.Enums.EJBCA;
using Keyfactor.PKI.PEM;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.CAPlugin.AWS;

public class AWSPCACAPlugin : IAnyCAPlugin
{
    private readonly ILogger Logger;
    private ICertificateDataReader _certificateDataReader;

    public AWSPCACAPlugin()
    {
        Logger = LogHandler.GetClassLogger<AWSPCACAPlugin>();
    }

    private IAwsPcaClient AwsClient { get; set; }


    //done
    public void Initialize(IAnyCAPluginConfigProvider configProvider, ICertificateDataReader certificateDataReader)
    {
        Logger.MethodEntry(LogLevel.Debug);
        _certificateDataReader = certificateDataReader;
        AwsClient = new AwsPcaClient(configProvider);
        Logger.MethodExit(LogLevel.Debug);
    }

    //done
    public async Task<AnyCAPluginCertificate> GetSingleRecord(string caRequestID)
    {
        Logger.MethodEntry();

        try
        {
            var returnedCert = await AwsClient.SubmitGetCertificateAsync(caRequestID).ConfigureAwait(false);

            if (returnedCert?.RegistrationError != null)
                throw new Exception($"GetSingleRecord failed: {returnedCert.RegistrationError.Description}");

            var pemChain = returnedCert?.CertificatePemChain ?? string.Empty;
            pemChain = pemChain.Replace("\r\n", "\n").Replace("\r", "\n");

            var endEntityPem = GetEndEntityCertificate(pemChain);

            var cert = new AnyCAPluginCertificate
            {
                CARequestID = caRequestID,
                Certificate = endEntityPem,
                Status = returnedCert?.Status ?? (int)EndEntityStatus.FAILED
            };

            Logger.MethodExit();
            return cert;
        }
        catch (Exception e)
        {
            Logger.LogError($"Error Occurred getting single cert {e.Message}");
            Logger.MethodExit();
            throw;
        }
    }

    //done
    public async Task Synchronize(BlockingCollection<AnyCAPluginCertificate> blockingBuffer, DateTime? lastSync,
        bool fullSync, CancellationToken cancelToken)
    {
        Logger.MethodEntry();
        Logger.LogTrace(
            $"Synchronize started. fullSync={fullSync}, lastSync={lastSync?.ToUniversalTime().ToString("O") ?? "null"}");

        try
        {
            var report = await AwsClient.SubmitAuditReportAsync(cancelToken).ConfigureAwait(false);
            if (report?.RegistrationError != null)
                throw new Exception($"AWS audit report failed: {report.RegistrationError.Description}");

            var items = report?.Result ?? new List<ACMPCACertificate>();
            Logger.LogDebug($"Sync found {items.Count} audit records.");

            foreach (var audit in items)
            {
                cancelToken.ThrowIfCancellationRequested();

                // Determine the CARequestID.
                // Prefer requestId segment from certificateArn; otherwise use serial -> requestId lookup.
                var caRequestId = SafeRequestIdFromArn(audit.certificateArn);

                if (string.IsNullOrWhiteSpace(caRequestId) && !string.IsNullOrWhiteSpace(audit.certificateSerial))
                    try
                    {
                        caRequestId = await _certificateDataReader
                            .GetRequestIDBySerialNumber(audit.certificateSerial)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogTrace(
                            $"Could not map serial to requestId. serial={audit.certificateSerial}. {ex.Message}");
                    }

                if (string.IsNullOrWhiteSpace(caRequestId))
                {
                    Logger.LogTrace(
                        "Skipping audit record: cannot determine CARequestID (no ARN requestId, no serial mapping).");
                    continue;
                }

                // Incremental filter: use audit timestamps (revokedAt preferred, else createdAt).
                var eventTime = ParseAwsAuditTimeUtc(audit.revokedAt) ?? ParseAwsAuditTimeUtc(audit.createdAt);
                if (!fullSync && lastSync.HasValue && eventTime.HasValue &&
                    eventTime.Value <= lastSync.Value.ToUniversalTime())
                {
                    Logger.LogTrace(
                        $"Skipping {caRequestId}: eventTime {eventTime:O} <= lastSync {lastSync.Value.ToUniversalTime():O}");
                    continue;
                }

                // Map AWS audit event -> Keyfactor numeric EndEntityStatus.
                var newStatus = AwsPcaStatusMapper.FromAuditEventType(audit.eventType);

                // Fallbacks if AWS eventType is missing/unknown:
                if (newStatus == (int)EndEntityStatus.FAILED)
                {
                    if (!string.IsNullOrWhiteSpace(audit.revokedAt))
                        newStatus = (int)EndEntityStatus.REVOKED;
                    else
                        newStatus = (int)EndEntityStatus.GENERATED;
                }

                // Optional: if expired, treat as historical (only if not revoked).
                try
                {
                    var exp = _certificateDataReader.GetExpirationDateByRequestId(caRequestId);
                    if (exp.HasValue && exp.Value.ToUniversalTime() <= DateTime.UtcNow &&
                        newStatus == (int)EndEntityStatus.GENERATED) newStatus = (int)EndEntityStatus.HISTORICAL;
                }
                catch (Exception ex)
                {
                    Logger.LogTrace($"Expiration lookup failed for {caRequestId}: {ex.Message}");
                }

                // Skip unchanged statuses unless full sync.
                if (!fullSync)
                {
                    bool exists;
                    try
                    {
                        exists = await _certificateDataReader.DoesCertExistForRequestID(caRequestId)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(
                            $"DoesCertExistForRequestID failed for {caRequestId}: {ex.Message}. Proceeding as if it does not exist.");
                        exists = false;
                    }

                    if (exists)
                        try
                        {
                            var oldStatus = await _certificateDataReader.GetStatusByRequestID(caRequestId)
                                .ConfigureAwait(false);
                            if (oldStatus == newStatus)
                            {
                                Logger.LogTrace(
                                    $"Skipping {caRequestId}: unchanged status ({newStatus}) and not full sync.");
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(
                                $"GetStatusByRequestID failed for {caRequestId}: {ex.Message}. Proceeding to emit.");
                        }
                }

                // If we only need to update status (revoked/historical) and you don't want to fetch the cert again:
                // - For revoked/historical, Keyfactor usually still accepts just status update, but depending on your pipeline,
                //   you may want to always include Certificate for GENERATED.
                // Here: fetch cert for GENERATED/HISTORICAL; skip fetch for REVOKED if ARN missing.
                string? certB64 = null;
                string? productId = null;

                if (newStatus == (int)EndEntityStatus.GENERATED || newStatus == (int)EndEntityStatus.HISTORICAL)
                {
                    if (string.IsNullOrWhiteSpace(audit.certificateArn))
                    {
                        Logger.LogTrace($"Skipping {caRequestId}: no certificateArn to retrieve certificate.");
                        continue;
                    }

                    var certResp = await AwsClient.SubmitGetCertificateByArnAsync(audit.certificateArn, cancelToken)
                        .ConfigureAwait(false);

                    if (certResp?.Status == (int)EndEntityStatus.INPROCESS)
                    {
                        // Emit in-process update without cert payload
                        blockingBuffer.Add(new AnyCAPluginCertificate
                        {
                            CARequestID = caRequestId,
                            Status = (int)EndEntityStatus.INPROCESS,
                            ProductID = certResp.CertificateType
                        }, cancelToken);

                        continue;
                    }

                    if (certResp?.RegistrationError != null)
                    {
                        Logger.LogTrace(
                            $"Skipping {caRequestId}: GetCertificate error: {certResp.RegistrationError.Description}");
                        continue;
                    }

                    certB64 = certResp.Certificate;
                    productId = certResp.CertificateType;


                    if (string.IsNullOrWhiteSpace(certB64))
                    {
                        Logger.LogTrace($"Skipping {caRequestId}: unable to obtain end-entity certificate payload.");
                        continue;
                    }
                }

                var finalsubmit = new AnyCAPluginCertificate
                {
                    CARequestID = caRequestId,
                    Certificate =
                        GetEndEntityCertificate(certB64), // null is OK for REVOKED updates if your pipeline accepts it
                    Status = newStatus,
                    ProductID = productId
                };
                // Emit to buffer as AnyGateway expects.
                blockingBuffer.Add(finalsubmit, cancelToken);
            }

            blockingBuffer.CompleteAdding();
            Logger.MethodExit();
        }
        catch (Exception e)
        {
            Logger.LogError($"AWS PCA Synchronize failed: {LogHandler.FlattenException(e)}");
            blockingBuffer.CompleteAdding();
            throw;
        }
    }

    //do

    public async Task<int> Revoke(string caRequestID, string hexSerialNumber, uint revocationReason)
    {
        Logger.MethodEntry();

        try
        {
            // AWS PCA revocation is by CERTIFICATE SERIAL (hex).
            // Prefer the explicit serial parameter, but keep a safe fallback.
            var serial = NormalizeHexSerial(hexSerialNumber);
            if (string.IsNullOrWhiteSpace(serial))
                serial = NormalizeHexSerial(caRequestID); // legacy fallback only

            if (string.IsNullOrWhiteSpace(serial))
            {
                Logger.LogError("Revoke failed: no valid certificate serial number provided.");
                return (int)EndEntityStatus.FAILED;
            }

            var awsReason = MapToAwsRevocationReason(revocationReason);

            // CAPlugin calls client; client returns Keyfactor numeric status already normalized.
            // This assumes your IAwsPcaClient has: SubmitRevokeCertificateAsync(string serialHex, RevocationReason reason)
            var resp = await AwsClient
                .SubmitRevokeCertificateAsync(serial, awsReason)
                .ConfigureAwait(false);

            if (resp?.RegistrationError != null)
            {
                Logger.LogError($"AWS revoke failed: {resp.RegistrationError.Description}");
                return (int)EndEntityStatus.FAILED;
            }

            // Prefer the normalized numeric status from the client response if present.
            if (resp != null && resp.Status > 0)
                return resp.Status;

            // Fallback if Status wasn't implemented on response
            return resp?.RevokeSuccess == true
                ? (int)EndEntityStatus.REVOKED
                : (int)EndEntityStatus.FAILED;
        }
        catch (Exception ex)
        {
            Logger.LogError($"AWS revoke threw: {LogHandler.FlattenException(ex)}");
            return (int)EndEntityStatus.FAILED;
        }
        finally
        {
            Logger.MethodExit();
        }
    }


    public async Task<EnrollmentResult> Enroll(
        string csr,
        string subject,
        Dictionary<string, string[]> san,
        EnrollmentProductInfo productInfo,
        RequestFormat requestFormat,
        EnrollmentType enrollmentType)
    {
        Logger.MethodEntry(LogLevel.Debug);

        try
        {
            if (productInfo == null || string.IsNullOrWhiteSpace(productInfo.ProductID))
                return new EnrollmentResult
                {
                    Status = (int)EndEntityStatus.FAILED,
                    StatusMessage = "ProductID is required."
                };

            // Validate template type (productId) is known
            if (!Constants.TemplateARNs.ContainsKey(productInfo.ProductID))
            {
                var supported = string.Join(", ", Constants.GetTemplateTypes());
                return new EnrollmentResult
                {
                    Status = (int)EndEntityStatus.FAILED,
                    StatusMessage = $"Unsupported ProductID '{productInfo.ProductID}'. Supported: {supported}"
                };
            }

            // Validity days
            var days = 365;
            if (productInfo.ProductParameters != null &&
                productInfo.ProductParameters.TryGetValue(EnrollmentConfigConstants.LifetimeDays, out var daysStr) &&
                int.TryParse(daysStr, out var parsed) &&
                parsed > 0)
                days = parsed;


            // Optional signing algorithm override (template parameter)
            string? signingAlgorithm = null;
            if (productInfo.ProductParameters != null &&
                productInfo.ProductParameters.TryGetValue(EnrollmentConfigConstants.SigningAlgorithm,
                    out var algoStr) &&
                !string.IsNullOrWhiteSpace(algoStr))
                signingAlgorithm = algoStr.Trim();

            // Normalize CSR to PEM (keeps your existing behavior)
            csr = PemUtilities.DERToPEM(PemUtilities.PEMToDER(csr), PemUtilities.PemObjectType.CertRequest);

            switch (enrollmentType)
            {
                case EnrollmentType.New:
                {
                    return await IssueAndFetchAsync(
                            csr,
                            productInfo.ProductID,
                            days,
                            signingAlgorithm,
                            "Certificate Issued")
                        .ConfigureAwait(false);
                }

                case EnrollmentType.RenewOrReissue:
                {
                    if (productInfo.ProductParameters == null ||
                        !TryGetProductParam(productInfo.ProductParameters, "PriorCertSN", out var priorSn) ||
                        string.IsNullOrWhiteSpace(priorSn))
                        return new EnrollmentResult
                        {
                            Status = (int)EndEntityStatus.FAILED,
                            StatusMessage =
                                "Renew/Reissue requires ProductParameters['PriorCertSN'] (hex serial number)."
                        };

                    string priorRequestId;
                    try
                    {
                        priorRequestId = await _certificateDataReader
                            .GetRequestIDBySerialNumber(priorSn)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return new EnrollmentResult
                        {
                            Status = (int)EndEntityStatus.FAILED,
                            StatusMessage = $"Could not resolve PriorCertSN to request id: {ex.Message}"
                        };
                    }

                    var expiration = _certificateDataReader.GetExpirationDateByRequestId(priorRequestId);
                    var isRenewal = expiration.HasValue && expiration.Value.ToUniversalTime() <= DateTime.UtcNow;

                    var msg = isRenewal ? "Certificate Renewed" : "Certificate Reissued";
                    var token = BuildIdempotencyToken(isRenewal ? "renew" : "reissue", priorRequestId, csr);

                    // Still "IssueCertificate" under the hood; PCA doesn't have first-class renew/reissue.
                    return await IssueAndFetchAsync(
                            csr,
                            productInfo.ProductID,
                            days,
                            msg,
                            // Optional: stable-ish idempotency (helps avoid duplicates if caller retries quickly)
                            token)
                        .ConfigureAwait(false);
                }

                default:
                    return new EnrollmentResult
                    {
                        Status = (int)EndEntityStatus.FAILED,
                        StatusMessage = $"EnrollmentType '{enrollmentType}' is not supported for AWS PCA."
                    };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"AWS PCA Enroll failed: {LogHandler.FlattenException(ex)}");
            return new EnrollmentResult
            {
                Status = (int)EndEntityStatus.FAILED,
                StatusMessage = ex.Message
            };
        }
        finally
        {
            Logger.MethodExit(LogLevel.Debug);
        }
    }

    //do
    public async Task Ping()
    {
        Logger.MethodEntry();
        try
        {
            Logger.LogInformation("Ping request received");
            await AwsClient.PingAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogError($"There was an error contacting AWS: {e.Message}.");
            throw new Exception($"Error attempting to ping AWS: {e.Message}.", e);
        }

        Logger.MethodExit();
    }

    //do
    public async Task ValidateCAConnectionInfo(Dictionary<string, object> connectionInfo)
    {
    }

    //do
    public async Task ValidateProductInfo(EnrollmentProductInfo productInfo,
        Dictionary<string, object> connectionInfo)
    {
        var certType = Constants.GetTemplateTypes().Find(x =>
            x.Equals(productInfo.ProductID, StringComparison.InvariantCultureIgnoreCase));

        if (certType == null) throw new ArgumentException($"Cannot find {productInfo.ProductID}", "ProductId");

        Logger.LogInformation($"Validated {certType} ({certType})configured for AnyGateway");
    }

    //done

    public Dictionary<string, PropertyConfigInfo> GetCAConnectorAnnotations()
    {
        return new Dictionary<string, PropertyConfigInfo>
        {
            // -----------------------------------------------------------------
            // Required text inputs
            // -----------------------------------------------------------------
            [Constants.ROLE_ARN] = new()
            {
                Comments =
                    "Destination Role ARN to use for AWS auth. Supports the [profile] prefix when using Default SDK auth, e.g. [myprofile]arn:aws:iam::123456789012:role/MyRole.",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            },
            [Constants.REGION] = new()
            {
                Comments = "AWS Region (single region only, e.g. us-east-1).",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            },

            // -----------------------------------------------------------------
            // PCA-specific connection settings
            // -----------------------------------------------------------------
            [Constants.CA_ARN] = new()
            {
                Comments =
                    "AWS ACM PCA Certificate Authority ARN. Example: arn:aws:acm-pca:us-east-1:123456789012:certificate-authority/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            },
            [Constants.S3_BUCKET] = new()
            {
                Comments =
                    "S3 bucket name used for PCA audit reports (inventory). The AWS identity in the Role context must have read/write permissions to this bucket.",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            },

            // -----------------------------------------------------------------
            // Auth configuration (aligned with aws-auth-library spec)
            // -----------------------------------------------------------------
            [Constants.USE_DEFAULT_SDK_AUTH] = new()
            {
                Comments =
                    "Use AWS SDK default credential inference (supports EC2 instance role, environment variables, shared credentials, etc.). If RoleArn is prefixed with [profile], that profile is prioritized.",
                Hidden = false,
                DefaultValue = "false",
                Type = "Bool"
            },
            [Constants.DEFAULT_SDK_ASSUME_ROLE] = new()
            {
                Comments =
                    "If UseDefaultSdkAuth is true, setting this to true will perform AssumeRole into RoleArn using the inferred SDK credentials.",
                Hidden = false,
                DefaultValue = "false",
                Type = "Bool"
            },
            [Constants.USE_OAUTH] = new()
            {
                Comments =
                    "Use OAuth OIDC authentication to obtain a token, then AssumeRole into RoleArn.",
                Hidden = false,
                DefaultValue = "false",
                Type = "Bool"
            },
            [Constants.OAUTH_SCOPE] = new()
            {
                Comments = "OAuth scope to request.",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            },
            [Constants.OAUTH_GRANT_TYPE] = new()
            {
                Comments = "OAuth grant type to request (commonly client_credentials).",
                Hidden = false,
                DefaultValue = "client_credentials",
                Type = "String"
            },
            [Constants.OAUTH_URL] = new()
            {
                Comments = "OAuth token endpoint URL.",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            },
            [Constants.OAUTH_CLIENT_ID] = new()
            {
                Comments = "OAuth client id (secret).",
                Hidden = true,
                DefaultValue = "",
                Type = "Secret"
            },
            [Constants.OAUTH_CLIENT_SECRET] = new()
            {
                Comments = "OAuth client secret (secret).",
                Hidden = true,
                DefaultValue = "",
                Type = "Secret"
            },
            [Constants.USE_IAM] = new()
            {
                Comments = "Use IAM user access key/secret to AssumeRole into RoleArn.",
                Hidden = false,
                DefaultValue = "false",
                Type = "Bool"
            },
            [Constants.IAM_USER_ACCESS_KEY] = new()
            {
                Comments = "IAM user access key (secret).",
                Hidden = true,
                DefaultValue = "",
                Type = "Secret"
            },
            [Constants.IAM_USER_ACCESS_SECRET] = new()
            {
                Comments = "IAM user access secret (secret).",
                Hidden = true,
                DefaultValue = "",
                Type = "Secret"
            },
            [Constants.EXTERNAL_ID] = new()
            {
                Comments = "Optional sts:ExternalId to supply on AssumeRole calls.",
                Hidden = false,
                DefaultValue = "",
                Type = "String"
            }
        };
    }

    //done
    public Dictionary<string, PropertyConfigInfo> GetTemplateParameterAnnotations()
    {
        return new Dictionary<string, PropertyConfigInfo>
        {
            // this is used to passdown csr details/prefill. will be overridden by commmand if not present. 
            [EnrollmentConfigConstants.LifetimeDays] = new()
            {
                Comments =
                    "OPTIONAL: The number of days of validity to use when requesting certs. If not provided, default is 365",
                Hidden = false,
                DefaultValue = 365,
                Type = "Number"
            },
            // this is used to passdown csr details/prefill. will be overridden by commmand if not present. 
            [EnrollmentConfigConstants.SigningAlgorithm] = new()
            {
                Comments =
                    "Required: AWS ACM PCA certificate signature algorithm to use when issuing certificates. Value is an AWS PCA SigningAlgorithm enum name (case-insensitive), e.g. SHA256WITHRSA, SHA384WITHRSA, SHA256WITHECDSA. If omitted, the plugin selects a default compatible with the CA key algorithm.",
                Hidden = false,
                DefaultValue = "SHA256WITHRSA",
                Type = "String"
            }
        };
    }

    //done
    public List<string> GetProductIds()
    {
        return Constants.GetTemplateTypes();
    }

    private async Task<EnrollmentResult> IssueAndFetchAsync(
        string csrPem,
        string productId,
        int validityDays,
        string? signingAlgorithm,
        string statusMessageOnSuccess,
        string? idempotencyToken = null)
    {
        // Build plugin DTO (NOT AWS SDK request)
        var issueReq = new IssueCertificateRequest
        {
            CsrPem = csrPem,
            ProductId = productId,
            ValidityDays = validityDays,
            SigningAlgorithm = signingAlgorithm,
            IdempotencyToken = idempotencyToken ?? Guid.NewGuid().ToString("N")
        };

        var issueResp = await AwsClient.SubmitIssueCertificateAsync(issueReq).ConfigureAwait(false);

        if (issueResp?.RegistrationError != null ||
            issueResp?.Result == null ||
            string.IsNullOrWhiteSpace(issueResp.Result.CertificateArn))
            return new EnrollmentResult
            {
                Status = (int)EndEntityStatus.FAILED,
                StatusMessage = issueResp?.RegistrationError?.Description ?? "AWS PCA IssueCertificate failed."
            };

        var certArn = issueResp.Result.CertificateArn;
        var caRequestId = issueResp.Result.CertificateId ?? SafeRequestIdFromArn(certArn) ?? certArn;

        var certResp = await AwsClient.SubmitGetCertificateByArnAsync(certArn).ConfigureAwait(false);

        if (certResp?.RegistrationError != null)
        {
            if (certResp.Status == (int)EndEntityStatus.INPROCESS)
                return new EnrollmentResult
                {
                    CARequestID = caRequestId,
                    Status = (int)EndEntityStatus.INPROCESS,
                    StatusMessage = "Certificate request is in process."
                };

            return new EnrollmentResult
            {
                CARequestID = caRequestId,
                Status = (int)EndEntityStatus.FAILED,
                StatusMessage = certResp.RegistrationError.Description
            };
        }

        if (string.IsNullOrWhiteSpace(certResp?.Certificate))
            return new EnrollmentResult
            {
                CARequestID = caRequestId,
                Status = (int)EndEntityStatus.FAILED,
                StatusMessage = "AWS PCA returned an empty certificate."
            };

        return new EnrollmentResult
        {
            CARequestID = caRequestId,
            Certificate = certResp.Certificate, // should be base64 DER leaf (client responsibility)
            Status = certResp.Status, // numeric EndEntityStatus
            StatusMessage = statusMessageOnSuccess
        };
    }

    private static bool TryGetProductParam(
        Dictionary<string, string> parameters,
        string key,
        out string value)
    {
        // Be forgiving about casing – CSC code had mixed casing ("priorcertsn" vs "PriorCertSN")
        foreach (var kvp in parameters)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }

        value = string.Empty;
        return false;
    }

    #region PRIVATE

    private static string NormalizeHexSerial(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Allow "0x..." or plain hex; remove separators.
        var s = input.Trim();

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(2);

        s = s.Replace(":", "").Replace("-", "").Replace(" ", "");

        // Basic validation: must be hex
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var isHex = (c >= '0' && c <= '9')
                        || (c >= 'a' && c <= 'f')
                        || (c >= 'A' && c <= 'F');
            if (!isHex) return string.Empty;
        }

        return s;
    }

    private static RevocationReason MapToAwsRevocationReason(uint revocationReason)
    {
        return revocationReason switch
        {
            1 => RevocationReason.KEY_COMPROMISE,
            2 => RevocationReason.CERTIFICATE_AUTHORITY_COMPROMISE, // AWS naming: CA_COMPROMISE
            3 => RevocationReason.AFFILIATION_CHANGED,
            4 => RevocationReason.SUPERSEDED,
            5 => RevocationReason.CESSATION_OF_OPERATION,
            9 => RevocationReason.PRIVILEGE_WITHDRAWN,
            10 => RevocationReason.A_A_COMPROMISE, // AWS naming: AA_COMPROMISE
            _ => RevocationReason.UNSPECIFIED
        };
    }

    private static string? SafeRequestIdFromArn(string? certificateArn)
    {
        if (string.IsNullOrWhiteSpace(certificateArn)) return null;

        // Expected: <caArn>/certificate/<requestId>
        var marker = "/certificate/";
        var idx = certificateArn.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        return certificateArn[(idx + marker.Length)..];
    }

    private static DateTime? ParseAwsAuditTimeUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // AWS audit report timestamps are typically ISO-8601 with timezone.
        // Use DateTimeOffset to correctly interpret 'Z' and offsets.
        if (DateTimeOffset.TryParse(raw, out var dto))
            return dto.UtcDateTime;

        // Fallback: attempt DateTime parse.
        if (DateTime.TryParse(raw, out var dt))
            return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();

        return null;
    }

    //Trying to fix leaf extraction
    private static readonly Regex PemBlock = new(
        "-----BEGIN CERTIFICATE-----\\s*(?<b64>[A-Za-z0-9+/=\\r\\n]+?)\\s*-----END CERTIFICATE-----",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex Ws = new("\\s+", RegexOptions.Compiled);

    /// <summary>
    ///     Returns the end-entity certificate as Base64 DER (no PEM headers), or "" if none could be found.
    /// </summary>
    public string GetEndEntityCertificate(string pemChain)
    {
        if (string.IsNullOrWhiteSpace(pemChain))
        {
            Logger.LogWarning("Empty PEM input.");
            return string.Empty;
        }

        // 1) Extract certs block-by-block, ignoring any garbage outside of valid fences.
        var certs = ExtractCertificates(pemChain);
        if (certs.Count == 0)
        {
            Logger.LogWarning("No valid certificate blocks found in input.");
            return string.Empty;
        }

        // 2) Pick the leaf (end-entity).
        var leaf = FindLeaf(certs);
        if (leaf is null)
        {
            Logger.LogWarning("Could not determine end-entity certificate from the provided chain.");
            return string.Empty;
        }

        try
        {
            // 3) Export to DER and Base64 (no headers).
            var der = leaf.Export(X509ContentType.Cert);
            var b64 = Convert.ToBase64String(der);
            Logger.LogTrace("End-entity certificate exported successfully.");
            return b64;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export end-entity certificate.");
            return string.Empty;
        }
        finally
        {
            // Dispose everything we created.
            foreach (var c in certs) c.Dispose();
        }
    }

    private static string EnsureCsrPem(string csr, RequestFormat requestFormat)
    {
        if (string.IsNullOrWhiteSpace(csr))
            throw new ArgumentException("CSR is required.", nameof(csr));

        // If it's already PEM, return as-is.
        if (csr.Contains("BEGIN CERTIFICATE REQUEST", StringComparison.OrdinalIgnoreCase) ||
            csr.Contains("BEGIN NEW CERTIFICATE REQUEST", StringComparison.OrdinalIgnoreCase))
            return csr;

        // Otherwise assume DER/base64-ish input and normalize to PEM.
        // RequestFormat may say Base64, Binary, etc. We’ll treat it as base64 DER if it’s not PEM.
        // This mirrors your old connector behavior of DER->PEM.
        var der = PemUtilities
            .PEMToDER(csr); // if csr is base64, this still works in many toolkits; if not, adjust to Convert.FromBase64String
        return PemUtilities.DERToPEM(der, PemUtilities.PemObjectType.CertRequest);
    }

    private List<X509Certificate2> ExtractCertificates(string pem)
    {
        var results = new List<X509Certificate2>();

        foreach (Match m in PemBlock.Matches(pem))
        {
            var b64 = m.Groups["b64"].Value;
            if (string.IsNullOrWhiteSpace(b64))
            {
                Logger.LogTrace("Skipping empty PEM block.");
                continue;
            }

            // Normalize: remove all whitespace and non-base64 spacers that sometimes creep in
            b64 = Ws.Replace(b64, string.Empty);

            // Strict Base64 decode with validation.
            try
            {
                // Convert.TryFromBase64String is fast and avoids temporary arrays when possible
                if (!Convert.TryFromBase64String(b64, new Span<byte>(new byte[GetDecodedLength(b64)]),
                        out var bytesWritten))
                {
                    // Fallback to FromBase64String to trigger a clear exception path
                    var discard = Convert.FromBase64String(b64);
                    bytesWritten = discard.Length; // unreachable if invalid
                }

                var der = Convert.FromBase64String(b64);
                var cert = new X509Certificate2(der);
                results.Add(cert);
                Logger.LogTrace($"Imported certificate: Subject='{cert.Subject}', Issuer='{cert.Issuer}'");
            }
            catch (FormatException fex)
            {
                Logger.LogWarning(fex, "Invalid Base64 inside a PEM block; skipping this block.");
            }
            catch (CryptographicException cex)
            {
                Logger.LogWarning(cex, "DER payload failed to parse as X509; skipping this block.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Unexpected error while parsing a PEM block; skipping this block.");
            }
        }

        return results;
    }

    private static string BuildIdempotencyToken(string purpose, string priorRequestId, string csrPem)
    {
        // Keep purpose short: "n" | "r" | "i"
        var p = purpose switch
        {
            "renew" => "r",
            "reissue" => "i",
            _ => "n"
        };

        // Hash a small stable fingerprint. Output 32 hex chars.
        var material = $"{p}|{priorRequestId}|{StableCsrFingerprint(csrPem)}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(material));
        var hex = Convert.ToHexString(hash).ToLowerInvariant(); // 32 chars

        // Prefix + 32 must be <=36; use 1 + 1 + 32 = 34 chars
        return $"{p}_{hex}";
    }

    private static string StableCsrFingerprint(string csrPem)
    {
        // strip whitespace to avoid trivial formatting differences
        var compact = new string(csrPem.Where(c => !char.IsWhiteSpace(c)).ToArray());
        // take a small slice to keep hashing cheap; not security-sensitive
        return compact.Length > 128 ? compact.Substring(0, 128) : compact;
    }

    // Heuristic leaf selection:
    //  - Prefer a certificate with CA=false (BasicConstraints) and whose Subject is not an Issuer of any other cert.
    //  - If multiple, prefer the one whose Subject does not appear as any Issuer at all.
    //  - As a last resort, pick the one with the longest chain distance (i.e., not issuing others).
    private X509Certificate2? FindLeaf(IReadOnlyList<X509Certificate2> certs)
    {
        // Build sets for quick lookups
        var issuers = new HashSet<string>(certs.Select(c => c.Issuer), StringComparer.OrdinalIgnoreCase);
        var subjects = new HashSet<string>(certs.Select(c => c.Subject), StringComparer.OrdinalIgnoreCase);

        bool IsCa(X509Certificate2 c)
        {
            try
            {
                var bc = c.Extensions["2.5.29.19"]; // Basic Constraints
                if (bc is X509BasicConstraintsExtension bce)
                    return bce.CertificateAuthority;
            }
            catch
            {
                /* ignore and treat as unknown */
            }

            return false; // if unknown, bias towards non-CA for end-entity picking
        }

        // Candidates that do not issue others (their Subject is not an Issuer of any other).
        var nonIssuers = certs.Where(c =>
            !certs.Any(o =>
                !ReferenceEquals(o, c) && string.Equals(o.Issuer, c.Subject, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // Prefer non-CA among non-issuers
        var nonIssuerNonCa = nonIssuers.Where(c => !IsCa(c)).ToList();
        if (nonIssuerNonCa.Count == 1) return nonIssuerNonCa[0];
        if (nonIssuerNonCa.Count > 1)
            // If multiple, pick the one whose subject appears least as an issuer (tie-breaker unnecessary here since nonIssuers already exclude issuers).
            return nonIssuerNonCa[0];

        // If that failed, pick any non-CA that is not an issuer in the set of all issuers
        var anyNonCa = certs.Where(c => !IsCa(c)).ToList();
        if (anyNonCa.Count == 1) return anyNonCa[0];
        if (anyNonCa.Count > 1)
        {
            // Prefer one whose subject is not equal to any issuer (a stricter non-issuer check across entire set)
            var strict = anyNonCa.FirstOrDefault(c => !issuers.Contains(c.Subject));
            if (strict != null) return strict;

            return anyNonCa[0];
        }

        // Last resort: pick the cert that issues nobody else (even if CA=true)
        if (nonIssuers.Count > 0) return nonIssuers[0];

        // Give up
        return null;
    }

    private static int GetDecodedLength(string b64)
    {
        // Approximate decoded length: 3/4 of input, minus padding effect
        var len = b64.Length;
        var padding = 0;
        if (len >= 2)
        {
            if (b64[^1] == '=') padding++;
            if (b64[^2] == '=') padding++;
        }

        return Math.Max(0, len / 4 * 3 - padding);
    }

    private string ExportCollectionToPem(X509Certificate2Collection collection)
    {
        var pemBuilder = new StringBuilder();

        foreach (var cert in collection)
        {
            pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
            pemBuilder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            pemBuilder.AppendLine("-----END CERTIFICATE-----");
        }

        return pemBuilder.ToString();
    }

    private static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    private string PreparePemTextFromApi(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return string.Empty;

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            // Not even Base64; nothing we can do.
            return string.Empty;
        }

        // Try UTF-8 first (strict); if it fails, decode as Latin-1 to avoid loss.
        string text;
        try
        {
            text = Utf8Strict.GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            text = Latin1.GetString(raw);
        }

        // Drop UTF-8/UTF-16 BOMs if present
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];

        // Normalize line endings to '\n' (keep line structure!)
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Remove NUL and non-printable control chars, but keep \n and \t
        text = new string(text.Where(ch =>
            ch == '\n' || ch == '\t' || (ch >= ' ' && ch != '\u007F')
        ).ToArray());

        return text;
    }

    #endregion
}