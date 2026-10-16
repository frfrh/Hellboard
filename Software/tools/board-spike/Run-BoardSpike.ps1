# SPDX-License-Identifier: GPL-3.0-or-later
#
# Runs spike.py on the Hellboard via the raw REPL and checks the results
# (implementation plan P7, hellboard-protocol.md section 8).
#
# What it does: stops main.py with Ctrl-C, executes spike.py from RAM, exchanges a few JSON
# lines with it, then soft-resets the board so main.py runs again. Nothing is written to the
# board's flash.
#
# Before running: close VS Code / MicroPico (or disconnect it), so the COM port is free.
# Usage: .\Run-BoardSpike.ps1 [-Port COM5]

param(
    [string]$Port
)

$ErrorActionPreference = 'Stop'
$ctrlA = [char]1; $ctrlB = [char]2; $ctrlC = [char]3; $ctrlD = [char]4

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

# Reads until a complete line containing $marker has arrived.
function Read-Line([System.IO.Ports.SerialPort]$serial, [string]$marker, [int]$timeoutMs) {
    $text = ''
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $timeoutMs) {
        if ($serial.BytesToRead -gt 0) {
            $text += $serial.ReadExisting()
            $index = $text.IndexOf($marker)
            if ($index -ge 0 -and $text.IndexOf("`n", $index) -ge 0) { return $text }
        } else {
            Start-Sleep -Milliseconds 10
        }
    }
    throw "Timeout waiting for a line with '$marker'. Received: $text"
}

function Get-JsonLine([string]$text, [string]$pattern) {
    foreach ($line in $text -split "`n") {
        # The raw REPL prefixes the first output line with "OK".
        $start = $line.IndexOf('{')
        if ($start -lt 0) { continue }
        $trimmed = $line.Substring($start).Trim()
        if ($trimmed -match $pattern) { return $trimmed | ConvertFrom-Json }
    }
    return $null
}

if (-not $Port) { $Port = Find-HellboardPort }
Write-Host "Hellboard on $Port" -ForegroundColor Cyan

$code = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'spike.py')
$serial = New-Object System.IO.Ports.SerialPort $Port, 115200
$serial.DtrEnable = $true
$serial.RtsEnable = $true
$serial.Encoding = [Text.Encoding]::UTF8
$serial.Open()

$checks = [ordered]@{}
try {
    # 1. Stop main.py (Ctrl-C fallback of F8) and enter the raw REPL.
    $serial.Write("`r$ctrlC$ctrlC")
    Start-Sleep -Milliseconds 300
    $serial.DiscardInBuffer()
    $serial.Write("`r$ctrlA")
    [void](Read-Until $serial "raw REPL; CTRL-B to exit`r`n>" 3000)
    $checks['Ctrl-C stops main.py, raw REPL'] = $true

    # 2. Upload spike.py into RAM in small chunks and execute it.
    $bytes = [Text.Encoding]::UTF8.GetBytes($code)
    for ($i = 0; $i -lt $bytes.Length; $i += 256) {
        $serial.Write($bytes, $i, [Math]::Min(256, $bytes.Length - $i))
        Start-Sleep -Milliseconds 10
    }
    $serial.Write($ctrlD)
    $output = Read-Line $serial '"spike"' 5000
    if (-not $output.StartsWith('OK')) { throw "raw REPL did not accept the code: $output" }
    $spike = (Get-JsonLine $output '"spike"').results

    # 3. Echo phase: three requests while the simulated main loop keeps running.
    $loops = @()
    foreach ($id in 1..3) {
        $serial.Write("{""v"":1,""id"":$id,""cmd"":""ping""}`n")
        $answer = Read-Line $serial '"loops"' 2000
        $reply = Get-JsonLine $answer '"ack"'
        if ($reply.ack -ne $id) { throw "Unexpected answer to request ${id}: $answer" }
        $loops += $reply.loops
        Start-Sleep -Milliseconds 300
    }
    $serial.Write("{""v"":1,""id"":4,""cmd"":""quit""}`n")
    $tail = Read-Line $serial 'spike_done' 3000
    if (-not $tail.Contains("$ctrlD>")) { [void](Read-Until $serial "$ctrlD>" 3000) }

    $checks['JSON over sys.stdin, answered'] = $loops.Count -eq 3
    $checks['Main loop keeps running while waiting (non-blocking)'] = ($loops[1] - $loops[0]) -gt 10 -and ($loops[2] - $loops[1]) -gt 10
    $checks['json module'] = ($spike.json -replace ' ', '') -eq '{"a":[1,"b",null,true]}'
    $checks['hashlib.sha256'] = $spike.sha256 -eq 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad'
    $checks['binascii base64'] = $spike.base64 -eq 'Hello'
    $checks['machine.bootloader available'] = $spike.bootloader_available -eq $true
    $checks['os.rename available'] = $spike.rename_available -eq $true
    $checks['select.poll on sys.stdin'] = $spike.poll_stdin -eq 'ok'
    $checks['HID keys released'] = $spike.release_keys -eq 'ok'
}
finally {
    # 4. Leave the raw REPL and soft-reset: main.py starts again.
    try {
        $serial.Write("`r$ctrlC$ctrlC")   # stops the spike if it is still running after an error
        Start-Sleep -Milliseconds 200
        $serial.Write("$ctrlB")
        Start-Sleep -Milliseconds 200
        $serial.Write("$ctrlD")
        Start-Sleep -Milliseconds 500
    } catch { }
    $serial.Close()
}

Write-Host ''
Write-Host 'Board' -ForegroundColor Cyan
Write-Host "  MicroPython $($spike.micropython) ($($spike.platform)), RAM free $([Math]::Round($spike.ram_free / 1024)) KB, flash free $($spike.flash.free_kb) of $($spike.flash.total_kb) KB"
Write-Host "  Files: $($spike.files -join ', ')"
Write-Host ''
Write-Host 'Checks' -ForegroundColor Cyan
foreach ($entry in $checks.GetEnumerator()) {
    $color = if ($entry.Value) { 'Green' } else { 'Red' }
    $mark = if ($entry.Value) { 'PASS' } else { 'FAIL' }
    Write-Host ("  [{0}] {1}" -f $mark, $entry.Key) -ForegroundColor $color
}
Write-Host ("  Main loop iterations between requests: {0}" -f (($loops | ForEach-Object { $_ }) -join ' -> '))
Write-Host ''
Write-Host 'Raw results (please paste into the chat):' -ForegroundColor Cyan
$spike | ConvertTo-Json -Compress
