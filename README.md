<h1 align="center" style="border-bottom: none">
    AWSPCA CAPlugin AnyCA Gateway REST Plugin
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-pilot-3D1973?style=flat-square" alt="Integration Status: pilot" />
<a href="https://github.com/Keyfactor/aws-pca-caplugin/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/aws-pca-caplugin?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/aws-pca-caplugin?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/aws-pca-caplugin/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#requirements">
    <b>Requirements</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=anycagateway">
    <b>Related Integrations</b>
  </a>
</p>

This integration allows for the Synchronization, Enrollment, and Revocation of certificates from the AWS ACM PCA. This is the AnyGateway REST version.

## Compatibility

The AWSPCA CAPlugin AnyCA Gateway REST plugin is compatible with the Keyfactor AnyCA Gateway REST 25.4.0 and later.

## Support
The AWSPCA CAPlugin AnyCA Gateway REST plugin is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com.

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements

This integration is tested and confirmed as working for Anygateway REST 24.4 and above. Notice: Keyfactor Anygateway REST 24.4 requires the use of .Net 8.

## Installation

1. Install the AnyCA Gateway REST per the [official Keyfactor documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/InstallIntroduction.htm).

2. On the server hosting the AnyCA Gateway REST, download and unzip the latest [AWSPCA CAPlugin AnyCA Gateway REST plugin](https://github.com/Keyfactor/aws-pca-caplugin/releases/latest) from GitHub.

3. Copy the unzipped directory (usually called `net8.0` or `net10.0`) to the Extensions directory:


    ```shell
    Depending on your AnyCA Gateway REST version, copy the unzipped directory to one of the following locations:
    Program Files\Keyfactor\AnyCA Gateway\AnyGatewayREST\net8.0\Extensions
    Program Files\Keyfactor\AnyCA Gateway\AnyGatewayREST\net10.0\Extensions
    ```

    > The directory containing the AWSPCA CAPlugin AnyCA Gateway REST plugin DLLs (`net8.0` or `net10.0`) can be named anything, as long as it is unique within the `Extensions` directory.

4. Restart the AnyCA Gateway REST service.

5. Navigate to the AnyCA Gateway REST portal and verify that the Gateway recognizes the AWSPCA CAPlugin plugin by hovering over the ⓘ symbol to the right of the Gateway on the top left of the portal.

## Configuration

1. Follow the [official AnyCA Gateway REST documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCA-Gateway.htm) to define a new Certificate Authority, and use the notes below to configure the **Gateway Registration** and **CA Connection** tabs:

    * **Gateway Registration**

        Download the **PCA root certificate** from AWS and have it ready to import into the Gateway **in `.pem` format**.

    * **CA Connection**

        Populate using the configuration fields collected in the [requirements](#requirements) section.

        * **RoleArn** - Destination Role ARN to use for AWS auth. Supports the [profile] prefix when using Default SDK auth, e.g. [myprofile]arn:aws:iam::123456789012:role/MyRole.
        * **Region** - AWS Region (single region only, e.g. us-east-1).
        * **CAArn** - AWS ACM PCA Certificate Authority ARN. Example: arn:aws:acm-pca:us-east-1:123456789012:certificate-authority/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        * **S3Bucket** - S3 bucket name used for PCA audit reports (inventory). The AWS identity in the Role context must have read/write permissions to this bucket.
        * **UseDefaultSdkAuth** - Use AWS SDK default credential inference (supports EC2 instance role, environment variables, shared credentials, etc.). If RoleArn is prefixed with [profile], that profile is prioritized.
        * **DefaultSdkAssumeRole** - If UseDefaultSdkAuth is true, setting this to true will perform AssumeRole into RoleArn using the inferred SDK credentials.
        * **UseOAuth** - Use OAuth OIDC authentication to obtain a token, then AssumeRole into RoleArn.
        * **OAuthScope** - OAuth scope to request.
        * **OAuthGrantType** - OAuth grant type to request (commonly client_credentials).
        * **OAuthUrl** - OAuth token endpoint URL.
        * **OAuthClientId** - OAuth client id (secret).
        * **OAuthClientSecret** - OAuth client secret (secret).
        * **UseIAM** - Use IAM user access key/secret to AssumeRole into RoleArn.
        * **IAMUserAccessKey** - IAM user access key (secret).
        * **IAMUserAccessSecret** - IAM user access secret (secret).
        * **ExternalId** - Optional sts:ExternalId to supply on AssumeRole calls.
        * **Enabled** - Flag to Enable or Disable gateway functionality. Disabling is primarily used to allow creation of the CA prior to configuration information being available.

2. Define [Certificate Profiles](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCP-Gateway.htm) and [Certificate Templates](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCA-Gateway.htm) for the Certificate Authority as required. One Certificate Profile must be defined per Certificate Template. It's recommended that each Certificate Profile be named after the Product ID. The AWSPCA CAPlugin plugin supports the following product IDs:

    * **EndEntity**
    * **EndEntityClientAuth**
    * **EndEntityServerAuth**
    * **CodeSigning**

3. Follow the [official Keyfactor documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCA-Keyfactor.htm) to add each defined Certificate Authority to Keyfactor Command and import the newly defined Certificate Templates.

4. In Keyfactor Command (v12.3+), for each imported Certificate Template, follow the [official documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Configuring%20Template%20Options.htm) to define enrollment fields for each of the following parameters:

    * **LifetimeDays** - OPTIONAL: The number of days of validity to use when requesting certs. If not provided, default is 365
    * **SigningAlgorithm** - Required: AWS ACM PCA certificate signature algorithm to use when issuing certificates. Value is an AWS PCA SigningAlgorithm enum name (case-insensitive), e.g. SHA256WITHRSA, SHA384WITHRSA, SHA256WITHECDSA. If omitted, the plugin selects a default compatible with the CA key algorithm.

## Authentication

The CAPlugin authenticates to AWS through the **Keyfactor AWS Authentication** library, which supports several methods. All of the methods below are supported and validated against ACM PCA. Choose **one** method per CA connection by setting the corresponding toggles/fields in **CAConnection**.

### Known Issues

At present, a fresh install of Keyfactor Command 24.4 used in conjuction with Keyfactor Gateway REST 25.4.0.0 is confirmed as working.  A fresh install of Command 25.3 used with REST 25.4.0.0 is also confirmed as working.
Latest version of Command 25.4 may run into issues, investigation into compatibility issues is ongoing.

### What you need ready

> **Key concept — the *destination* identity.** Whichever identity the plugin ends up operating as — the assumed `RoleArn`, or the *originating* identity when no Assume Role is performed — is the **destination**, and it is what must hold the ACM PCA + S3 audit-report permissions (see [Example IAM policies](#example-iam-policies-for-the-destination-identity)). For every Assume-Role method the **originating** identity additionally needs `sts:AssumeRole` on the target role, and the target role's **trust policy** must allow that originating principal (see [Example trust policies](#example-trust-policies)).

### Common requirements (all methods)

- **Region** — the AWS region for service endpoints; must match your **ACM Private CA** region (e.g. `us-east-2`).
- **CAArn** — the Certificate Authority ARN, e.g. `arn:aws:acm-pca:<region>:<account-id>:certificate-authority/<ca-uuid>`.
- **S3Bucket** — bucket name (not ARN) for ACM PCA audit reports, e.g. `keyfactor-acmpca-audit-reports`. The destination identity needs `s3:ListBucket`/`s3:GetBucketLocation` on the bucket and `s3:GetObject`/`s3:PutObject` on `<bucket>/*`, and ACM PCA itself must be allowed to write to it (see [Example policy for bucket](#example-policy-for-bucket)).
- **PCA Root Cert** — download the PCA root certificate from AWS in `.pem` format to upload under **Gateway Registration**.
- **Scan interval** — ACM PCA limits audit-report generation to roughly **once per 30 minutes** per CA. Set the CA's `ServiceSettings` full-scan interval to **≥ 30 minutes** to avoid throttling.

### Method configuration

For every method, also set the common `Region`, `CAArn`, and `S3Bucket`, and upload the Root CA under **Gateway Registration**. Set all auth toggles you are **not** using to `false`.

#### IAM User (Access Key + Secret)
- `UseIAM` = `true`
- `IAMUserAccessKey` (e.g. `AKIAIOSFODNN7EXAMPLE`), `IAMUserAccessSecret` (e.g. `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`)
- `RoleArn` — the destination role to assume (e.g. `arn:aws:iam::123456789012:role/Keyfactor-AnyGateway-AcmPcaRole`)

**AWS setup:** the IAM user needs `sts:AssumeRole` on `RoleArn`; `RoleArn`'s trust policy must allow the IAM user (see [Assume-Role trust](#example-trust-policies)); `RoleArn` holds the [PCA + S3 permissions](#example-iam-policies-for-the-destination-identity).

#### IAM User + ExternalId
As **IAM User**, plus set `ExternalId`. The target role's trust policy must include a matching `sts:ExternalId` condition (see [Assume-Role with ExternalId](#example-trust-policies)). Use this when the role owner requires a shared secret to prevent the confused-deputy problem.

#### Default SDK (no assume)
- `UseDefaultSdkAuth` = `true`, `DefaultSdkAssumeRole` = `false` (`RoleArn` is ignored)

The Gateway host's **ambient AWS identity** is the destination and must hold the PCA + S3 permissions directly. The AWS SDK resolves this from, in order: `AWS_*` environment variables, the shared credentials file `[default]` profile, or an EC2 instance role. On a Windows AnyCA Gateway host the service runs as `NETWORK SERVICE`, so provide credentials via the machine-level env var `AWS_SHARED_CREDENTIALS_FILE` (pointing at a credentials file readable by the service) or the service account's `%USERPROFILE%\.aws\credentials`.

#### Default SDK + Assume Role
- `UseDefaultSdkAuth` = `true`, `DefaultSdkAssumeRole` = `true`, `RoleArn` = destination role

The ambient identity (resolved as above) is the **originating** account and performs `sts:AssumeRole` into `RoleArn`. The ambient identity needs `sts:AssumeRole`; `RoleArn`'s trust allows it and holds the PCA + S3 permissions.

#### Credential Profile (± Assume Role)
Prefix the `RoleArn` with a `[profile-name]`, e.g. `[myprofile]arn:aws:iam::123456789012:role/Keyfactor-AnyGateway-AcmPcaRole`, and set `UseDefaultSdkAuth` = `true`. The named profile must exist in the shared AWS credentials file **on the Gateway host** (same location rules as Default SDK above).

- **No assume** (`DefaultSdkAssumeRole` = `false`): the profile identity is the destination and needs the PCA + S3 permissions directly.
- **With assume** (`DefaultSdkAssumeRole` = `true`): the profile is the originating identity and assumes `RoleArn` (which holds the permissions and trusts the profile identity).

#### OAuth (OIDC federation)
- `UseOAuth` = `true`
- `OAuthClientId`, `OAuthClientSecret`, `OAuthUrl` (the token endpoint), `OAuthScope`, `OAuthGrantType` (`client_credentials`)
- `RoleArn` = destination role (federated web-identity role)

The plugin requests an OAuth token (`client_credentials` grant, client id/secret sent as an HTTP **Basic** `Authorization` header) and calls `sts:AssumeRoleWithWebIdentity` on `RoleArn`.

**AWS setup:** create an IAM **OIDC identity provider** for the token issuer, and set `RoleArn`'s trust policy to allow `sts:AssumeRoleWithWebIdentity` for that provider with the appropriate `aud` (and optionally `sub`) conditions (see [OAuth/web-identity trust](#example-trust-policies)).

> OAuth notes: the OIDC provider must accept the client id/secret as a **Basic auth header** (not POST body); **DPoP must be disabled** on the app; and the token lifetime must be **≤ 12 hours** — STS `AssumeRoleWithWebIdentity` caps `DurationSeconds` at 43200, and the plugin derives the session duration from the token's `expires_in`.

### Setting up AWS Authentication (Examples)

> [!NOTE]
> Several different options are offered for authenticating with AWS.
> Documentation for how these options work is maintained in the [aws-auth-library](https://github.com/Keyfactor/aws-auth-library) repository.

The following examples show potential configurations for Roles in AWS with different selected authentication methods. Your configuration steps may differ depending on the specific requirements of your use case. In every case the **destination identity** must hold the ACM Private CA + S3 audit-report permissions (see [Example IAM policies](#example-iam-policies-for-the-destination-identity)), and every Assume-Role method additionally requires the *originating* identity to have `sts:AssumeRole` and the target role's **trust policy** to allow that principal (see [Example trust policies](#example-trust-policies)).

<details>
<summary>Host instance credentials using Default SDK and Assume Role</summary>

Select the `Use Default SDK Auth` option (`UseDefaultSdkAuth=true`) to have the plugin load the Gateway host's ambient AWS credentials. If the Gateway runs on an EC2 instance, this is the IAM Role assigned to the instance; on a non-EC2 Windows host it is whatever the service account resolves from `AWS_SHARED_CREDENTIALS_FILE` or `%USERPROFILE%\.aws\credentials` (the AnyCA Gateway service runs as `NETWORK SERVICE`).

If that ambient identity is itself the Destination account identity to use with ACM Private CA, no additional Role needs to be configured. If it is only to be used initially and a separate `RoleArn` is designated as the Destination account, also select `Assume new Role using Default SDK Auth` (`DefaultSdkAssumeRole=true`).

**AWS Setup**
_Note: to use instance credentials, the CAPlugin's Gateway host must be running inside an EC2 instance._
1. Assign or note the existing IAM Role assigned to the EC2 instance (or the credentials configured for the service account).
2. If Assume Role is used, ensure a [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is set up on the Destination role that allows the ambient identity to assume it.
3. Verify the permissions match the requirements for accessing ACM Private CA (see [Example IAM policies](#example-iam-policies-for-the-destination-identity)).

</details>

<details>
<summary>OAuth OIDC Identity Provider (Okta example)</summary>

Select the `Use OAuth` option (`UseOAuth=true`) for a CA connection to use an OAuth Identity Provider, and supply `OAuthClientId`, `OAuthClientSecret`, `OAuthUrl` (token endpoint), `OAuthScope`, and `OAuthGrantType` (`client_credentials`).

**AWS Setup**
1. A 3rd party [Identity Provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) — an IAM **OIDC identity provider** for the token issuer — needs to be set up in AWS.
2. An [AWS Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) (the `RoleArn`) needs to be created for use with your Identity Provider.
3. Ensure the [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is set up for that role with the Identity Provider, allowing `sts:AssumeRoleWithWebIdentity` with the appropriate `aud` (and optionally `sub`) conditions (see [OAuth / web-identity trust](#example-trust-policies)).
4. Verify the permissions match the requirements for accessing ACM Private CA.

**OKTA Setup**
1. Ensure your Authorization Server is set up in Okta.
2. Ensure the appropriate scopes are set up in Okta.
3. Set up an Okta App (client-credentials / API Services type). The app must send the client id/secret as a **Basic auth header** (not POST body), have **DPoP disabled**, and issue tokens with a lifetime **≤ 12 hours** (STS caps `DurationSeconds` at 43200).

</details>

<details>
<summary>IAM User credentials to Assume Role</summary>

Select the `Use IAM` option (`UseIAM=true`) for a CA connection to use an IAM User credential, supplying `IAMUserAccessKey` and `IAMUserAccessSecret`.

**AWS Setup**
1. An [AWS Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) to Assume with your IAM User needs to be created (set as `RoleArn`).
2. Ensure a [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is set up for that role that allows the IAM User (see [Assume-Role trust](#example-trust-policies)). If using the ExternalId modifier, add a matching `sts:ExternalId` condition.
3. AWS does not support programmatic access for AWS SSO accounts. The account used here must be a standard AWS IAM User with an Access Key credential type.
4. Verify the permissions match the requirements for accessing ACM Private CA (see [Example IAM policies](#example-iam-policies-for-the-destination-identity)).

</details>

---

### Example IAM policies for the destination identity

These permissions belong on the **destination identity** (the assumed `RoleArn`, or the ambient/profile identity when no Assume Role is performed). The following examples are intended as **copy/adapt templates**. 

#### Example 1: Minimal PCA issuance/retrieval/revocation

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PrivateCABasicOps",
      "Effect": "Allow",
      "Action": [
        "acm-pca:IssueCertificate",
        "acm-pca:GetCertificate",
        "acm-pca:RevokeCertificate",
        "acm-pca:DescribeCertificateAuthority",
        "acm-pca:GetCertificateAuthorityCertificate"
      ],
      "Resource": "arn:aws:acm-pca:<region>:<account-id>:certificate-authority/<ca-uuid>"
    }
  ]
}
```

#### Example 2: PCA issuance + audit reports + S3 audit bucket access

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PrivateCAIssueAndFetch",
      "Effect": "Allow",
      "Action": [
        "acm-pca:IssueCertificate",
        "acm-pca:GetCertificate",
        "acm-pca:DescribeCertificateAuthority",
        "acm-pca:GetCertificateAuthorityCertificate"
      ],
      "Resource": [
        "arn:aws:acm-pca:<region>:<account-id>:certificate-authority/<ca-uuid>"
      ]
    },
    {
      "Sid": "PrivateCAAuditReportOps",
      "Effect": "Allow",
      "Action": [
        "acm-pca:CreateCertificateAuthorityAuditReport",
        "acm-pca:DescribeCertificateAuthorityAuditReport"
      ],
      "Resource": "*"
    },
    {
      "Sid": "AuditReportBucketAccessForCaller",
      "Effect": "Allow",
      "Action": [
        "s3:GetBucketLocation",
        "s3:ListBucket"
      ],
      "Resource": "arn:aws:s3:::<audit-bucket-name>"
    },
    {
      "Sid": "AuditReportObjectAccessForCaller",
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject"
      ],
      "Resource": "arn:aws:s3:::<audit-bucket-name>/*"
    }
  ]
}
```
---

### Example trust policies

Every Assume-Role method requires a **trust policy** on the destination role that allows the originating principal to assume it. (Default SDK *no-assume* and Credential Profile *no-assume* need no trust policy — the identity is used directly.)

#### Assume Role — IAM User, Default SDK + Assume, or Credential Profile + Assume

`<<ORIGINATING PRINCIPAL>>` is the IAM user ARN (IAM User method) or the ambient/profile identity ARN (Default SDK / Credential Profile + Assume).

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "AWS": "<<ORIGINATING PRINCIPAL ARN>>" },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

#### Assume Role with ExternalId

Add a matching `sts:ExternalId` condition; the CAConnection `ExternalId` value must equal it.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "AWS": "<<ORIGINATING PRINCIPAL ARN>>" },
      "Action": "sts:AssumeRole",
      "Condition": { "StringEquals": { "sts:ExternalId": "<<your-external-id>>" } }
    }
  ]
}
```

#### OAuth — web-identity trust

`RoleArn` for the OAuth method must trust the OIDC provider you registered for your token issuer. Condition on the token `aud` (audience) and optionally `sub` (for `client_credentials`, `sub` is typically the client id).

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "arn:aws:iam::<account-id>:oidc-provider/<issuer-host>/<issuer-path>" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "<issuer-host>/<issuer-path>:aud": "<<audience>>",
          "<issuer-host>/<issuer-path>:sub": "<<client-id>>"
        }
      }
    }
  ]
}
```

---

### Example policy for bucket
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowACMPCAWriteAuditReports",
      "Effect": "Allow",
      "Principal": {
        "Service": "acm-pca.amazonaws.com"
      },
      "Action": "s3:PutObject",
      "Resource": "arn:aws:s3:::<audit-bucket-name>/*",
      "Condition": {
        "StringEquals": {
          "aws:SourceAccount": "<account-id>"
        },
        "ArnLike": {
          "aws:SourceArn": "arn:aws:acm-pca:<region>:<account-id>:certificate-authority/<ca-uuid>"
        }
      }
    }
  ]
}
```

---

## Signing algorithm selection (ACM PCA)

The connector supports an optional **template / product parameter** named `SigningAlgorithm` that controls the **certificate signature algorithm**
passed to AWS ACM PCA `IssueCertificate`.

- If **not set**, the plugin will **auto-select** a compatible default based on the CA `KeyAlgorithm` returned by
  `DescribeCertificateAuthority`.
- If **set**, the plugin validates the value and **rejects incompatible combinations** before calling AWS.

### Where to configure

Set `SigningAlgorithm` on the **AnyGateway template** (product parameters), alongside `LifetimeDays`.

### Valid `SigningAlgorithm` values (AWS PCA)

- RSA family: `SHA256WITHRSA`, `SHA384WITHRSA`, `SHA512WITHRSA`
- ECDSA family: `SHA256WITHECDSA`, `SHA384WITHECDSA`, `SHA512WITHECDSA`
- SM2: `SM3WITHSM2`
- ML-DSA (post-quantum): `ML_DSA_44`, `ML_DSA_65`, `ML_DSA_87`

### Allowed CA key algorithm <-> signing algorithm combinations

The CA key algorithm is the PCA CA **KeyAlgorithm** (not the subject key in the CSR). The signing algorithm must match the CA key family.

| CA KeyAlgorithm | Allowed SigningAlgorithm values |
|---|---|
| `RSA_2048`, `RSA_3072`, `RSA_4096` | `SHA256WITHRSA`, `SHA384WITHRSA`, `SHA512WITHRSA` |
| `EC_prime256v1`, `EC_secp384r1`, `EC_secp521r1` | `SHA256WITHECDSA`, `SHA384WITHECDSA`, `SHA512WITHECDSA` |
| `SM2` | `SM3WITHSM2` |
| `ML_DSA_44` | `ML_DSA_44` |
| `ML_DSA_65` | `ML_DSA_65` |
| `ML_DSA_87` | `ML_DSA_87` |

### Auto-selection defaults

When `SigningAlgorithm` is omitted, the plugin selects:

- RSA CAs -> `SHA256WITHRSA`
- EC P-256 -> `SHA256WITHECDSA`
- EC P-384 -> `SHA384WITHECDSA`
- EC P-521 -> `SHA512WITHECDSA`
- SM2 -> `SM3WITHSM2`
- ML-DSA -> exact-match (`ML_DSA_44/65/87`)

## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Any CA Gateways (REST)](https://github.com/orgs/Keyfactor/repositories?q=anycagateway).
