## Overview

This integration allows for the Synchronization, Enrollment, and Revocation of certificates from the AWS ACM PCA. This is the AnyGateway REST version.

## Requirements

This integration is tested and confirmed as working for Anygateway REST 24.4 and above. Notice: Keyfactor Anygateway REST 24.4 requires the use of .Net 8.

## Authentication (Access Key + Secret)

The CAPlugin currently supports **one** authentication method: **AWS Access Key ID + Secret Access Key**.  
**OAuth** and **Default SDK authentication** will be enabled in later updates. There is functionality present via the **Keyfactor AWS Authentication** library, but these alternate methods are currently ***untested***.

### What you need ready

Before configuring the CAPlugin, have the following prepared:

#### 1) IAMUserAccessKey and IAMUserAccessSecret
- **Access Key ID** (example format: `AKIAIOSFODNN7EXAMPLE`)
- **Secret Access Key** (example format: `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`)

#### 2) A target IAM Role the connector will run as (recommended)
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

## Gateway Registration

TODO Gateway Registration is a required section

