<h1 align="center" style="border-bottom: none">
    AWSPCA CA  Gateway AnyCA Gateway REST Plugin
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

The AWSPCA CA  Gateway AnyCA Gateway REST plugin is compatible with the Keyfactor AnyCA Gateway REST 25.4.0 and later.

## Support
The AWSPCA CA  Gateway AnyCA Gateway REST plugin is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements

This integration is tested and confirmed as working for Anygateway REST 25.4 and above. Notice: Keyfactor Anygateway REST 25.4 requires the use of .Net 8.

## Installation

1. Install the AnyCA Gateway REST per the [official Keyfactor documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/InstallIntroduction.htm).

2. On the server hosting the AnyCA Gateway REST, download and unzip the latest [AWSPCA CA  Gateway AnyCA Gateway REST plugin](https://github.com/Keyfactor/aws-pca-caplugin/releases/latest) from GitHub.

3. Copy the unzipped directory (usually called `net6.0` or `net8.0`) to the Extensions directory:


    ```shell
    Depending on your AnyCA Gateway REST version, copy the unzipped directory to one of the following locations:
    Program Files\Keyfactor\AnyCA Gateway\AnyGatewayREST\net6.0\Extensions
    Program Files\Keyfactor\AnyCA Gateway\AnyGatewayREST\net8.0\Extensions
    ```

    > The directory containing the AWSPCA CA  Gateway AnyCA Gateway REST plugin DLLs (`net6.0` or `net8.0`) can be named anything, as long as it is unique within the `Extensions` directory.

4. Restart the AnyCA Gateway REST service.

5. Navigate to the AnyCA Gateway REST portal and verify that the Gateway recognizes the AWSPCA CA  Gateway plugin by hovering over the ⓘ symbol to the right of the Gateway on the top left of the portal.

## Configuration

1. Follow the [official AnyCA Gateway REST documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCA-Gateway.htm) to define a new Certificate Authority, and use the notes below to configure the **Gateway Registration** and **CA Connection** tabs:

    * **Gateway Registration**

        TODO Gateway Registration is a required section

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

2. Define [Certificate Profiles](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCP-Gateway.htm) and [Certificate Templates](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCA-Gateway.htm) for the Certificate Authority as required. One Certificate Profile must be defined per Certificate Template. It's recommended that each Certificate Profile be named after the Product ID. The AWSPCA CA  Gateway plugin supports the following product IDs:

    * **EndEntity**
    * **EndEntityClientAuth**
    * **EndEntityServerAuth**

3. Follow the [official Keyfactor documentation](https://software.keyfactor.com/Guides/AnyCAGatewayREST/Content/AnyCAGatewayREST/AddCA-Keyfactor.htm) to add each defined Certificate Authority to Keyfactor Command and import the newly defined Certificate Templates.



## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Any CA Gateways (REST)](https://github.com/orgs/Keyfactor/repositories?q=anycagateway).