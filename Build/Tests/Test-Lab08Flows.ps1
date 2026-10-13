# Runtime test for F8 (lab 08): drives the naive approval flow through the three situations the lab starts
# from and checks the Dataverse result and the run history. Leaves the seed data reset and the mode at default.
param(
    [Parameter(Mandatory)] [string] $Url,
    [Parameter(Mandatory)] [string] $EnvironmentId
)
$ErrorActionPreference = 'Stop'
$env:AZURE_CONFIG_DIR = Join-Path $env:LOCALAPPDATA 'AB400\azcli'
$base = $Url.TrimEnd('/') + '/api/data/v9.2'
function DvHeaders { @{ Authorization = 'Bearer ' + (az account get-access-token --resource $Url.TrimEnd('/') --query accessToken -o tsv); 'Content-Type' = 'application/json' } }
function FlowHeaders { @{ Authorization = 'Bearer ' + (az account get-access-token --resource https://service.flow.microsoft.com/ --query accessToken -o tsv) } }
$failures = 0
function Check($name, $ok, $detail) { if (-not $ok) { $script:failures++ }; "[{0}] {1}{2}" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $(if ($detail) { " ($detail)" } else { '' }) }

$flow = (Invoke-RestMethod -Headers (DvHeaders) -Uri "$base/workflows?`$filter=category eq 5 and name eq 'Lab 08 approval flow'&`$select=resourceid").value[0].resourceid
$runsUri = "https://api.flow.microsoft.com/providers/Microsoft.ProcessSimple/environments/$EnvironmentId/flows/$flow/runs?api-version=2016-11-01"
function Runs { (Invoke-RestMethod -Headers (FlowHeaders) -Uri $runsUri).value }
function WaitForNewRun($knownIds, $seconds = 240) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep 10
        $new = Runs | Where-Object { $knownIds -notcontains $_.name -and $_.properties.status -ne 'Running' }
        if ($new) { return $new | Sort-Object { $_.properties.startTime } | Select-Object -First 1 }
    }
    return $null
}
function Reset { Invoke-RestMethod -Method Post -Headers (DvHeaders) -Uri "$base/eqp_ResetLabData" -Body '{}' | Out-Null }
function Request3 { (Invoke-RestMethod -Headers (DvHeaders) -Uri "$base/eqp_requests?`$filter=eqp_name eq 'REQ-SEED-003'&`$select=eqp_requestid,eqp_requeststatus,eqp_availability,eqp_processingnote").value[0] }
function SetMode($value) {
    $def = (Invoke-RestMethod -Headers (DvHeaders) -Uri "$base/environmentvariabledefinitions?`$filter=schemaname eq 'eqp_AvailabilityMode'&`$select=environmentvariabledefinitionid").value[0].environmentvariabledefinitionid
    $existing = (Invoke-RestMethod -Headers (DvHeaders) -Uri "$base/environmentvariablevalues?`$filter=_environmentvariabledefinitionid_value eq $def&`$select=environmentvariablevalueid").value
    foreach ($v in $existing) { Invoke-RestMethod -Method Delete -Headers (DvHeaders) -Uri "$base/environmentvariablevalues($($v.environmentvariablevalueid))" | Out-Null }
    if ($value) {
        $body = @{ value = $value; schemaname = "eqp_AvailabilityMode_test"; 'EnvironmentVariableDefinitionId@odata.bind' = "/environmentvariabledefinitions($def)" } | ConvertTo-Json
        Invoke-RestMethod -Method Post -Headers (DvHeaders) -Uri "$base/environmentvariablevalues" -Body $body | Out-Null
    }
}
function Patch3($body) { $r = Request3; Invoke-RestMethod -Method Patch -Headers (DvHeaders) -Uri "$base/eqp_requests($($r.eqp_requestid))" -Body $body | Out-Null }

Reset; SetMode $null
Start-Sleep 20   # let any runs caused by the reset finish
$known = @(Runs | ForEach-Object name)

# 0. A reset with nothing to restore writes nothing, so it doesn't trigger the flow.
Reset
Start-Sleep 60
$extra = @(Runs | Where-Object { $known -notcontains $_.name })
Check "A reset with nothing to restore triggers no runs" ($extra.Count -eq 0) "$($extra.Count) new run(s)"
$known = @(Runs | ForEach-Object name)

# 1. Defect: an unrelated edit still triggers the flow (it runs, then the condition skips the work).
Patch3 '{"eqp_justification":"Unrelated edit for the lab 08 test."}'
$run = WaitForNewRun $known; $known += $run.name
Check "An unrelated edit triggers a run" ($null -ne $run) "status $($run.properties.status)"
$r = Request3
Check "...and the condition skips the work" ($r.eqp_availability -eq 100000000 -and -not $r.eqp_processingnote) "availability $($r.eqp_availability)"

# 2. Approval with the stand-in available: availability recorded, fulfilment started inline.
Reset; Start-Sleep 20; $known = @(Runs | ForEach-Object name)
Patch3 '{"eqp_requeststatus":100000001}'
$deadline = (Get-Date).AddSeconds(240)
do { Start-Sleep 10; $r = Request3 } while ($r.eqp_requeststatus -ne 100000003 -and (Get-Date) -lt $deadline)
Check "Approval records availability (Available)" ($r.eqp_availability -eq 100000001) "availability $($r.eqp_availability)"
Check "...and starts fulfilment inline" ($r.eqp_requeststatus -eq 100000003 -and $r.eqp_processingnote -like 'Fulfilment started*') "status $($r.eqp_requeststatus), note '$($r.eqp_processingnote)'"

# 3. Defect: in Error mode the availability call fails, the run fails, and nothing is recorded.
Start-Sleep 30; Reset; SetMode 'Error'; Start-Sleep 20; $known = @(Runs | ForEach-Object name)
Patch3 '{"eqp_requeststatus":100000001}'
$deadline = (Get-Date).AddSeconds(300); $failedRun = $null
do {
    Start-Sleep 15
    $failedRun = Runs | Where-Object { $known -notcontains $_.name -and $_.properties.status -eq 'Failed' } | Select-Object -First 1
} while (-not $failedRun -and (Get-Date) -lt $deadline)
$r = Request3
Check "In Error mode the run fails straight away (no retries in the naive flow)" ($null -ne $failedRun) "$($failedRun.properties.status)"
Check "...and nothing is recorded on the request" ($r.eqp_availability -eq 100000000 -and -not $r.eqp_processingnote -and $r.eqp_requeststatus -eq 100000001) "status $($r.eqp_requeststatus), availability $($r.eqp_availability)"

SetMode $null; Reset
"{0}" -f $(if ($failures -eq 0) { 'All F8 runtime checks passed.' } else { "$failures F8 runtime check(s) failed." })
exit $failures
