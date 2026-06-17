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
The AWSPCA CAPlugin AnyCA Gateway REST plugin is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements

This integration is tested and confirmed as working for Anygateway REST 24.4 and above. Notice: Keyfactor Anygateway REST 24.4 requires the use of .Net 8.

## Installation

1. Install the AnyCA Gateway REST per the [official Keyfactor documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/InstallIntroduction.htm).

2. On the server hosting the AnyCA Gateway REST, download and unzip the latest [AWSPCA CAPlugin AnyCA Gateway REST plugin](https://github.com/Keyfactor/aws-pca-caplugin/releases/latest) from GitHub.

3. Copy the unzipped directory (usually called `net6.0` or `net8.0`) to the Extensions directory:


    ```shell
    Depending on your AnyCA Gateway REST version, copy the unzipped directory to one of the following locations:
    Program Files\Keyfactor\AnyCA Gateway\AnyGatewayREST\net6.0\Extensions
    Program Files\Keyfactor\AnyCA Gateway\AnyGatewayREST\net8.0\Extensions
    ```

    > The directory containing the AWSPCA CAPlugin AnyCA Gateway REST plugin DLLs (`net6.0` or `net8.0`) can be named anything, as long as it is unique within the `Extensions` directory.

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


## Authentication (Access Key + Secret)

The CAPlugin currently supports **one** authentication method: **AWS Access Key ID + Secret Access Key**.  
**OAuth** and **Default SDK authentication** will be enabled in later updates. There is functionality present via the **Keyfactor AWS Authentication** library, but these alternate methods are currently ***untested***.

### What you need ready

Before configuring the CAPlugin, have the following prepared:

#### 1) IAMUserAccessKey and IAMUserAccessSecret
- **Access Key ID** (example format: `AKIAIOSFODNN7EXAMPLE`)
- **Secret Access Key** (example format: `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`)

#### 2) A target IAM Role the Gateway will run as (recommended)
Example:
- `arn:aws:iam::123456789012:role/Keyfactor-AnyGateway-AcmPcaRole`

**Role expectations:**
- The role must have permissions for:
  - **ACM PCA operations** (Issue/Get/Revoke/Describe + CA certificate chain retrieval)
  - **Audit report creation & status polling** (Create/Describe audit reports)
  - **S3 bucket access** to read/write audit report objects

#### 3) Permissions on the assumed role
The assumed role must have permissions for the AWS services the Gateway needs. This typically includes:
- `acm-pca:*` actions required for enrollment + revocation workflows
- Audit report actions (`acm-pca:CreateCertificateAuthorityAuditReport`, `acm-pca:DescribeCertificateAuthorityAuditReport`)
- S3 bucket and object access for the audit report destination bucket

**See the example IAM policies below in this README section**

#### 4) Region
Know the **AWS region** the connector should use (for service endpoints), e.g.:
- `us-east-1`

> Region must match the region of your **ACM Private CA**.

#### 5) CA ARN
Have the **Certificate Authority ARN** for the PCA you want to integrate with.

Example format:
- `arn:aws:acm-pca:<region>:<account-id>:certificate-authority/<ca-uuid>`

Example:
- `arn:aws:acm-pca:us-east-2:123456789012:certificate-authority/11111111-2222-3333-4444-555555555555`

#### 6) S3 Bucket
Choose an S3 bucket to store / retrieve ACM PCA audit reports.

You should have:
- **Bucket name** (example: `keyfactor-acmpca-audit-reports`, not the full bucket ARN!) 


> The role needs `s3:ListBucket` / `s3:GetBucketLocation` at the bucket ARN, and `s3:GetObject` / `s3:PutObject` on the object ARN pattern.

#### 7) PCA Root Cert
Download the **PCA root certificate** from AWS and have it ready to import into the Gateway **in `.pem` format**.

### Enabling all this in the Gateway Configuration Portal

#### 1) Register the Gateway CA and upload the Root CA certificate
1. Navigate to **Gateway Registration**.
2. Upload the **Root CA Certificate** you downloaded earlier (PEM).

#### 2) Configure the CA connection settings
1. Navigate to **CAConnection**.
2. Populate:
   - `RoleArn` (example: `arn:aws:iam::123456789012:role/Keyfactor-AnyGateway-AcmPcaRole`)
   - `Region` (example: `us-east-2`)
   - `CAArn` (example: `arn:aws:acm-pca:us-east-2:123456789012:certificate-authority/11111111-2222-3333-4444-555555555555`)
   - `S3Bucket` (example: `keyfactor-acmpca-audit-reports`)
   - `IAMUserAccessKey` (example: `AKIA...`)
   - `IAMUserAccessSecret` (example: `wJalrXU...`)

3. Set these auth toggles:
   - `UseDefaultSdkAuth` = `false`
   - `UseOAuth` = `false`
   - `UseIAM` = `true`

---

### Example IAM policies for the assumed role

The following examples are intended as **copy/adapt templates**. 

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