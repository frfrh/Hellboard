# SPDX-License-Identifier: GPL-3.0-or-later
#
# Loads the hellpod animation test (Firmware/libs/hellpod.py + test_main.py) into the Hellboard's RAM
# via the raw REPL and starts it. Nothing is written to the board's flash. The test keeps running
# after this script ends; hold button 1_4 for 2 s (or replug the board) to return to the normal
# firmware.
#
# Before running: close the Companion and VS Code / MicroPico, so the COM port is free.
# Usage: .\Run-HellpodTest.ps1 [-Port COM5]

param(
    [string]$Port
)

$ErrorActionPreference = 'Stop'
$ctrlA = [char]1; $ctrlC = [char]3; $ctrlD = [char]4

function Find-HellboardPort {
    $devices = Get-CimInstance Win32_PnPEntity | Where-Object { $_.PNPDeviceID -match 'VID_2E8A' -and $_.Name -match '\((COM\d+)\)' }
    $ports = @($devices | ForEach-Object { if ($_.Name -match '\((COM\d+)\)') { $Matches[1] } })
    if ($ports.Count -eq 0) { throw 'No RP2040 serial port (USB VID 2E8A) found. Is the Hellboard connected?' }
    if ($ports.Count -gt 1) { throw "Several RP2040 ports found ($($ports -join ', ')); choose one with -Port." }
    return $ports[0]
}

function Read-Until([System.IO.Ports.SerialPort]$serial, [string]$marker, [int]$timeoutMs) {
    $text = ''
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $timeoutMs) {
        if ($serial.BytesToRead -gt 0) {
            $text += $serial.ReadExisting()
            if ($text.Contains($marker)) { return $text }
        } else {
            Start-Sleep -Milliseconds 10
        }
    }
    throw "Timeout waiting for '$($marker -replace "[\x00-\x1f]", '?')'. Received: $text"
}

# hellpod.py + test_main.py as one program; the import of hellpod is dropped.
$animation = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '..\..\..\Firmware\libs\hellpod.py')
$test = (Get-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'test_main.py') |
    Where-Object { $_ -notmatch '# board: removed when concatenated' }) -join "`n"
$code = ($animation + "`n" + $test) -replace "`r", ''

if (-not $Port) { $Port = Find-HellboardPort }
Write-Host "Hellboard on $Port" -ForegroundColor Cyan

$serial = New-Object System.IO.Ports.SerialPort $Port, 115200
$serial.DtrEnable = $true
$serial.RtsEnable = $true
$serial.Encoding = [Text.Encoding]::UTF8
$serial.WriteTimeout = 3000     # a board that stopped reading must not block the script forever
$serial.Open()
try {
    $serial.Write("`r$ctrlC$ctrlC")
    Start-Sleep -Milliseconds 300
    $serial.DiscardInBuffer()
    $serial.Write("`r$ctrlA")
    [void](Read-Until $serial "raw REPL; CTRL-B to exit`r`n>" 3000)

    $bytes = [Text.Encoding]::UTF8.GetBytes($code)
    for ($i = 0; $i -lt $bytes.Length; $i += 256) {
        $serial.Write($bytes, $i, [Math]::Min(256, $bytes.Length - $i))
        Start-Sleep -Milliseconds 10
    }
    $serial.Write($ctrlD)
    $answer = Read-Until $serial 'OK' 5000

    # A syntax or start-up error ends the program at once; give it a moment to show up.
    Start-Sleep -Milliseconds 1500
    $answer += $serial.ReadExisting()
    if ($answer.Contains('Traceback') -or $answer.Contains("$ctrlD")) {
        throw "The test did not start: $answer"
    }
}
finally {
    $serial.Close()
}
Write-Host "Test running ($($bytes.Length) bytes in RAM). Hold 1_4 for 2 s to return to the normal firmware." -ForegroundColor Green
