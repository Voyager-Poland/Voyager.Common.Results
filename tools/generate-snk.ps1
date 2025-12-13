param(
    [string]$Output = "../Voyager.Common.Results.snk",
    [int]$KeySize = 2048
)

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider($KeySize)
$bytes = $rsa.ExportCspBlob($true)
[System.IO.File]::WriteAllBytes($Output, $bytes)
Write-Host "SNK generated at $Output (size: $($bytes.Length) bytes)"
