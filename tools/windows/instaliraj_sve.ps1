<#
.SYNOPSIS
    Vanjsko uređenje PRO: instalacija AutoCAD i SketchUp dodatka jednim klikom.
.DESCRIPTION
    Pokreće se iz raspakovanog zipa (folderi AutoCAD i SketchUp pored ove skripte).
    1. Traži da AutoCAD i SketchUp budu zatvoreni (datoteke dodatka su inače zaključane).
    2. AutoCAD: AutoCAD\instaliraj.ps1 (sve pronađene verzije 2020-2027).
    3. SketchUp: raspakuje pb_site_design_pro.rbz u Plugins svake instalirane verzije (2017-2027), stara verzija
       dodatka se prije toga uklanja.
    -Deinstaliraj (DEINSTALIRAJ_SVE.bat): uklanja oba dodatka. Crteži i SketchUp modeli zadržavaju geometriju i podatke.
#>
[CmdletBinding()]
param([switch]$BezPitanja, [switch]$Deinstaliraj)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host ''
Write-Host ('Vanjsko uređenje PRO - ' + $(if ($Deinstaliraj) { 'deinstalacija' } else { 'instalacija' }) + ' (AutoCAD i SketchUp)') -ForegroundColor Cyan
Write-Host ''

function Wait-Closed([string]$process, [string]$name) {
    while (Get-Process -Name $process -ErrorAction SilentlyContinue) {
        if ($BezPitanja) { throw "$name je otvoren; zatvorite ga i pokrenite instalaciju ponovo." }
        Write-Host "$name je otvoren. Sačuvajte rad i zatvorite ga, pa pritisnite Enter (ili upišite P za preskakanje)." -ForegroundColor Yellow
        $a = Read-Host
        if ($a -match '^[pP]') { return $false }
    }
    return $true
}

if ($Deinstaliraj) {
    $acad = Join-Path $Root 'AutoCAD\instaliraj.ps1'
    if (-not (Test-Path $acad)) { $acad = Join-Path $Root 'instaliraj.ps1' }
    if ((Test-Path $acad) -and (Wait-Closed 'acad' 'AutoCAD')) {
        Write-Host '== AutoCAD ==' -ForegroundColor Cyan
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $acad -Deinstaliraj -BezPitanja
    }
    if (Wait-Closed 'SketchUp' 'SketchUp') {
        Write-Host '== SketchUp ==' -ForegroundColor Cyan
        $found = $false
        foreach ($v in @(Get-ChildItem -Path (Join-Path $env:APPDATA 'SketchUp') -Directory -Filter 'SketchUp 20*' -ErrorAction SilentlyContinue)) {
            $plugins = Join-Path $v.FullName 'SketchUp\Plugins'
            $here = $false
            foreach ($item in @('pb_site_design_pro', 'pb_site_design_pro.rb')) {
                $path = Join-Path $plugins $item
                if (Test-Path $path) { Remove-Item -Recurse -Force $path; $found = $true; $here = $true }
            }
            if ($here) { Write-Host ("  " + $v.Name + ': uklonjeno') -ForegroundColor Green }
        }
        if (-not $found) { Write-Host '  SketchUp dodatak nije bio instaliran.' }
    }
    Write-Host ''
    Write-Host 'Gotovo. Postavke programa (%LOCALAPPDATA%\SiteDesignPro) su zadržane; obrišite folder ručno ako ih ne trebate.' -ForegroundColor Green
    return
}

# AutoCAD
$acad = Join-Path $Root 'AutoCAD\instaliraj.ps1'
if (Test-Path $acad) {
    if (Wait-Closed 'acad' 'AutoCAD') {
        Write-Host '== AutoCAD ==' -ForegroundColor Cyan
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $acad -BezPitanja
    } else {
        Write-Host 'AutoCAD dodatak je preskočen.' -ForegroundColor Yellow
    }
} else {
    Write-Host 'Folder AutoCAD nije pronađen pored skripte; AutoCAD dodatak je preskočen.' -ForegroundColor Yellow
}

# SketchUp
$rbz = Join-Path $Root 'SketchUp\pb_site_design_pro.rbz'
if (Test-Path $rbz) {
    if (Wait-Closed 'SketchUp' 'SketchUp') {
        Write-Host '== SketchUp ==' -ForegroundColor Cyan
        $base = Join-Path $env:APPDATA 'SketchUp'
        $versions = @(Get-ChildItem -Path $base -Directory -Filter 'SketchUp 20*' -ErrorAction SilentlyContinue)
        if ($versions.Count -eq 0) {
            Write-Host 'Nije pronađena nijedna instalacija SketchUpa (folder %APPDATA%\SketchUp\SketchUp 20xx). Instalirajte rbz ručno: Extension Manager > Install Extension.' -ForegroundColor Yellow
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        foreach ($v in $versions) {
            $plugins = Join-Path $v.FullName 'SketchUp\Plugins'
            New-Item -ItemType Directory -Force -Path $plugins | Out-Null
            $old = Join-Path $plugins 'pb_site_design_pro'
            if (Test-Path $old) { Remove-Item -Recurse -Force $old }
            $zip = [System.IO.Compression.ZipFile]::OpenRead($rbz)
            try {
                foreach ($entry in $zip.Entries) {
                    if ($entry.FullName.EndsWith('/')) { continue }
                    $target = Join-Path $plugins ($entry.FullName -replace '/', '\')
                    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
                    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
                }
            } finally { $zip.Dispose() }
            Get-ChildItem -Path $old -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
            Write-Host ("  " + $v.Name + ': instalirano u ' + $plugins) -ForegroundColor Green
        }
    } else {
        Write-Host 'SketchUp dodatak je preskočen.' -ForegroundColor Yellow
    }
}

Write-Host ''
Write-Host 'Gotovo. Pokrenite AutoCAD (kartica VANJSKO UREĐENJE PRO) i SketchUp (Proširenja > Vanjsko uređenje PRO).' -ForegroundColor Green
Write-Host 'Prvi put: u AutoCAD-u Pomoć > Brzi početak.'
