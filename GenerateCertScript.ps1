$PublisherName = "CN=bartoszsienkiewicz"
$CertificateName = "SupportTool_MSIX_Certificate"

# Get current directory
$currentDir = Get-Location

# Prompt user for password
Write-Host "Enter password for the certificate (input will be hidden):" -ForegroundColor Yellow
$securePassword = Read-Host -AsSecureString

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $PublisherName `
    -KeyUsage DigitalSignature `
    -FriendlyName $CertificateName `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(10)

Write-Host "Certificate created! Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

$certPath = Join-Path $currentDir "SupportTool_Certificate.pfx"
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $securePassword
Write-Host "PFX exported to: $certPath" -ForegroundColor Green

$cerPath = Join-Path $currentDir "SupportTool_Certificate.cer"
Export-Certificate -Cert $cert -FilePath $cerPath
Write-Host "CER exported to: $cerPath" -ForegroundColor Green