<#
.SYNOPSIS
    Approves an equipment request directly through the Dataverse Web API, bypassing
    any client-side (form) validation.

.DESCRIPTION
    This script is the "prepared Web API request" used in Lab 01 to show that a
    client-side check on a form only runs when a user saves the form -- it never
    runs for a caller that updates the row directly, such as this script.

    It gets an access token for the target Dataverse environment with the Azure
    CLI (the same credentials you're already signed in with), then sends a PATCH
    request that sets the named request's eqp_requeststatus column to Approved
    (100000001).

.PARAMETER EnvironmentUrl
    The Dataverse environment URL. Copy the Web API endpoint from the Power Apps
    maker portal (Settings > Developer resources) -- either the full endpoint
    (for example, https://yourorg.crm.dynamics.com/api/data/v9.2) or just the
    organization URL (https://yourorg.crm.dynamics.com) both work.

.PARAMETER RequestNumber
    The request number to approve, for example REQ-SEED-001 or REQ-SEED-002.

.EXAMPLE
    .\Approve-RequestViaWebApi.ps1 -EnvironmentUrl "https://yourorg.crm.dynamics.com" -RequestNumber "REQ-SEED-001"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvironmentUrl,

    [Parameter(Mandatory = $true)]
    [string]$RequestNumber
)

$ErrorActionPreference = 'Stop'

# Status Approved = 100000001 (foundation spec §1, eqp_requeststatus choice values).
$approvedStatus = 100000001

# Accept either the organization root URL or the full Web API endpoint and
# normalize to the organization root, so either value copied from the
# Developer resources page works.
$organizationUrl = ($EnvironmentUrl -replace '/api/data/v[\d\.]+/?$', '').TrimEnd('/')
$apiUrl = "$organizationUrl/api/data/v9.2"

Write-Host "Getting an access token for $organizationUrl ..."
$tokenJson = az account get-access-token --resource $organizationUrl --query accessToken -o tsv
if (-not $tokenJson) {
    throw "Couldn't get an access token. Run 'az login' first, and confirm the environment URL."
}

$authHeaderValue = "Bearer " + $tokenJson
$headers = @{
    Authorization      = $authHeaderValue
    "Content-Type"     = "application/json; charset=utf-8"
    "OData-MaxVersion" = "4.0"
    "OData-Version"    = "4.0"
    "If-Match"         = "*"
}

# eqp_name is the Request number column and carries the eqp_key_requestnumber
# alternate key, so the request row can be addressed by its request number
# without looking up its record ID first.
$uri = "$apiUrl/eqp_requests(eqp_name='$RequestNumber')"
$body = @{ eqp_requeststatus = $approvedStatus } | ConvertTo-Json

Write-Host "Approving $RequestNumber directly through the Web API (no form, no client-side check) ..."
try {
    Invoke-RestMethod -Uri $uri -Method Patch -Headers $headers -Body $body | Out-Null
    Write-Host "$RequestNumber was set to Approved."
}
catch {
    # Surface the Dataverse error message (for example, the plug-in's rejection message)
    # whether it was captured automatically (PowerShell 7+) or needs reading from the
    # response stream (Windows PowerShell 5.1).
    $details = $_.ErrorDetails.Message
    if (-not $details -and $_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $details = $reader.ReadToEnd()
    }
    Write-Error "The Web API rejected the request for $RequestNumber. $details"
}
