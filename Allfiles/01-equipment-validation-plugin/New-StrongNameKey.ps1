<#
.SYNOPSIS
    Creates the strong-name key that signs the Lab 01 plug-in assembly.

.DESCRIPTION
    Dataverse requires plug-in assemblies to be signed. Strong-name keys aren't shipped with the lab
    files, so you create your own: this script writes a new 1024-bit key to AB400.Lab01\AB400.Lab01.snk,
    the file the project is already set up to sign with. It doesn't replace an existing key unless you
    add -Force.

.EXAMPLE
    .\New-StrongNameKey.ps1
#>
[CmdletBinding()]
param(
    [string] $Path,
    [switch] $Force
)

# Windows PowerShell 5.1 doesn't set $PSScriptRoot in parameter defaults, so the default is set here.
if (-not $Path) {
    $Path = Join-Path $PSScriptRoot 'AB400.Lab01\AB400.Lab01.snk'
}
$Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

if ((Test-Path $Path) -and -not $Force) {
    Write-Host "A key already exists at $Path. Keep it, or run again with -Force to replace it."
    return
}

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider(1024)
try {
    [System.IO.File]::WriteAllBytes($Path, $rsa.ExportCspBlob($true))
}
finally {
    $rsa.Dispose()
}
Write-Host "Created the strong-name key $Path."
