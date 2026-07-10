# PharmaSynth NPC voice generation (user-approved ElevenLabs TTS, 2026-07-11).
#
# Reads Assets/PharmaSynth/Audio/Voice/voice-manifest.json (from the Unity menu
# Tools > PharmaSynth > Voice > Export Voice Manifest) and generates one MP3 per
# line into Assets/PharmaSynth/Audio/Voice/<Speaker>/<id>.mp3. Incremental: a
# file that already exists is skipped, so re-runs only fetch new/changed lines
# (a changed line gets a new id).
#
# Usage (PowerShell, from the repo root):
#   $env:ELEVENLABS_API_KEY = "sk_..."          # your key — NEVER commit it
#   .\Tools\voice\generate-voice.ps1 -SampleOnly    # 2 lines per speaker → listen first!
#   .\Tools\voice\generate-voice.ps1                # full pass after voice approval
#
# Voices: pick from https://elevenlabs.io/voice-library and paste the ids below
# (defaults are ElevenLabs premade voices: bright/energetic for the robot guide,
# deep/authoritative for the examiner — audition and swap freely).
param(
    [string]$PharmeeVoiceId = "pFZP5JQG7iQjIQuC4Bku",   # "Lily" — bright, crisp
    [string]$JimenezVoiceId = "onwK4e9ZLuTAKqWW03F9",   # "Daniel" — stern, older male
    [string]$ModelId = "eleven_flash_v2_5",              # 0.5 credits/char
    [switch]$SampleOnly
)

$ErrorActionPreference = "Stop"
if (-not $env:ELEVENLABS_API_KEY) { throw "Set ELEVENLABS_API_KEY first (your ElevenLabs API key)." }

$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$manifestPath = Join-Path $root "Assets/PharmaSynth/Audio/Voice/voice-manifest.json"
if (-not (Test-Path $manifestPath)) { throw "Manifest not found — run Tools > PharmaSynth > Voice > Export Voice Manifest in Unity first." }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$log = @()
$done = 0; $skipped = 0; $failed = 0
$sampleCount = @{ Pharmee = 0; Jimenez = 0 }

foreach ($line in $manifest.lines) {
    if ($SampleOnly) {
        if ($sampleCount[$line.speaker] -ge 2) { continue }
        $sampleCount[$line.speaker]++
    }

    $dir = Join-Path $root ("Assets/PharmaSynth/Audio/Voice/" + $line.speaker)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $file = Join-Path $dir ($line.id + ".mp3")
    if (Test-Path $file) { $skipped++; continue }

    $voiceId = if ($line.speaker -eq "Jimenez") { $JimenezVoiceId } else { $PharmeeVoiceId }
    $body = @{ text = $line.text; model_id = $ModelId } | ConvertTo-Json -Depth 3
    $uri = "https://api.elevenlabs.io/v1/text-to-speech/$voiceId`?output_format=mp3_44100_128"

    try {
        Invoke-RestMethod -Method Post -Uri $uri -Body $body -ContentType "application/json" `
            -Headers @{ "xi-api-key" = $env:ELEVENLABS_API_KEY } -OutFile $file
        $done++
        $log += "$($line.speaker),$($line.id),$($line.chars),ok"
        Write-Host ("[{0}] {1}  {2}" -f $line.speaker, $line.id, $line.text.Substring(0, [Math]::Min(60, $line.text.Length)))
        Start-Sleep -Milliseconds 350   # stay well under the API rate limit
    }
    catch {
        $failed++
        $log += "$($line.speaker),$($line.id),$($line.chars),FAILED: $($_.Exception.Message)"
        Write-Warning ("FAILED {0}: {1}" -f $line.id, $_.Exception.Message)
    }
}

$log | Out-File (Join-Path $PSScriptRoot "generation-log.csv") -Encoding utf8
Write-Host ""
Write-Host ("Generated {0}, skipped {1} existing, {2} failed." -f $done, $skipped, $failed)
Write-Host "Next: in Unity run Tools > PharmaSynth > Voice > Import & Wire Voice Clips."
