<#
.SYNOPSIS
    Instalacija dodatka Vanjsko uređenje PRO za AutoCAD 2020-2027.

.DESCRIPTION
    1. Pronalazi instalirane verzije AutoCAD-a (i vertikala: Civil 3D, Map 3D...) u registru.
    2. Po izboru (-Izgradi) kompajlira dodatak iz izvornog koda za pronađene verzije (treba .NET SDK).
    3. Kopira SiteDesignPro.bundle u ApplicationPlugins, uklanja Windows blokadu preuzetih datoteka,
       briše stare kopije (da se dodatak ne učita dvaput) i dodaje folder u pouzdane putanje (TRUSTEDPATHS).

.PARAMETER Izgradi
    Kompajlira dodatak iz izvornog koda (repozitorij) umjesto gotovog paketa.

.PARAMETER SviKorisnici
    Instalira u %ProgramData%\Autodesk\ApplicationPlugins (traži administratorska prava).
    Bez ovoga se instalira samo za trenutnog korisnika u %AppData%\Autodesk\ApplicationPlugins.

.PARAMETER Deinstaliraj
    Uklanja dodatak iz oba foldera i iz pouzdanih putanja.

.PARAMETER BezPitanja
    Ne čeka potvrdu korisnika (npr. za automatsku instalaciju).
#>
[CmdletBinding()]
param(
    [switch]$Izgradi,
    [switch]$SviKorisnici,
    [switch]$Deinstaliraj,
    [switch]$BezPitanja,
    [string]$Paket = '',
    [switch]$Pauza
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$BundleName = 'SiteDesignPro.bundle'
$ScriptPath = $MyInvocation.MyCommand.Path
$ScriptDir = Split-Path -Parent $ScriptPath
$ScriptParams = $PSBoundParameters
$LogFile = Join-Path ([System.IO.Path]::GetTempPath()) 'VanjskoUredjenjePRO-instalacija.log'

# Interna verzija AutoCAD-a -> folder u paketu, ciljni .NET i godina izdanja.
$Series = @(
    @{ Release = '23.1'; Folder = 'R23'; Tfm = 'net47';           Year = '2020' },
    @{ Release = '24.0'; Folder = 'R24'; Tfm = 'net48';           Year = '2021' },
    @{ Release = '24.1'; Folder = 'R24'; Tfm = 'net48';           Year = '2022' },
    @{ Release = '24.2'; Folder = 'R24'; Tfm = 'net48';           Year = '2023' },
    @{ Release = '24.3'; Folder = 'R24'; Tfm = 'net48';           Year = '2024' },
    @{ Release = '25.0'; Folder = 'R25'; Tfm = 'net8.0-windows';  Year = '2025' },
    @{ Release = '25.1'; Folder = 'R25'; Tfm = 'net8.0-windows';  Year = '2026' },
    @{ Release = '26.0'; Folder = 'R26'; Tfm = 'net10.0-windows'; Year = '2027' }
)

function Write-Log([string]$Text, [string]$Color = '') {
    if ($Color) { Write-Host $Text -ForegroundColor $Color } else { Write-Host $Text }
    try { Add-Content -LiteralPath $LogFile -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  ' + $Text) -Encoding UTF8 } catch { }
}

function Stop-WithError([string]$Text) {
    Write-Log ''
    Write-Log ('GREŠKA: ' + $Text) 'Red'
    Write-Log ('Dnevnik instalacije: ' + $LogFile)
    if ($Pauza) { Read-Host 'Pritisnite Enter za zatvaranje' | Out-Null }
    exit 1
}

function Find-SeriesInfo([string]$Release) {
    foreach ($s in $Series) { if ($s.Release -eq $Release) { return $s } }
    return $null
}

# Instalirani AutoCAD proizvodi iz HKLM\SOFTWARE\Autodesk\AutoCAD\R<ver>\ACAD-<id>:<jezik> (64-bitni pogled registra).
function Get-InstalledAutoCAD {
    $result = @()
    if ($env:OS -ne 'Windows_NT') { return $result }
    $hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    $root = $hklm.OpenSubKey('SOFTWARE\Autodesk\AutoCAD')
    if ($null -eq $root) { return $result }
    foreach ($rel in $root.GetSubKeyNames()) {
        if ($rel -notmatch '^R(\d+\.\d+)$') { continue }
        $release = $Matches[1]
        $relKey = $root.OpenSubKey($rel)
        foreach ($prod in $relKey.GetSubKeyNames()) {
            if ($prod -notmatch '^ACAD-') { continue }
            $k = $relKey.OpenSubKey($prod)
            if ($null -eq $k) { continue }
            $name = [string]$k.GetValue('ProductName')
            $location = [string]$k.GetValue('AcadLocation')
            if (-not $name -or -not $location -or -not (Test-Path -LiteralPath (Join-Path $location 'acad.exe'))) { continue }
            $result += [pscustomobject]@{ Release = $release; Key = ('R' + $release + '\' + $prod); Name = $name; Location = $location; Info = (Find-SeriesInfo $release) }
        }
    }
    return $result
}

function Get-RepoRoot {
    $dir = $ScriptDir
    for ($i = 0; $i -lt 4 -and $dir; $i++) {
        if (Test-Path -LiteralPath (Join-Path $dir 'autocad-site-design-pro\src\SiteDesignPro.AutoCAD\SiteDesignPro.AutoCAD.csproj')) { return $dir }
        $dir = Split-Path -Parent $dir
    }
    return $null
}

function Get-DotnetSdkMajors {
    $majors = @()
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) { return $majors }
    foreach ($line in (& dotnet --list-sdks 2>$null)) {
        if ($line -match '^(\d+)\.') { $majors += [int]$Matches[1] }
    }
    return $majors
}

# Kompajlira dodatak za tražene ciljne .NET verzije i sklapa SiteDesignPro.bundle u <repo>\dist.
function Build-Bundle([string]$Repo, [string[]]$Tfms) {
    $majors = Get-DotnetSdkMajors
    if ($majors.Count -eq 0) {
        Stop-WithError ('.NET SDK nije instaliran. Instalirajte .NET SDK 8 (i SDK 10 za AutoCAD 2027) sa https://dotnet.microsoft.com/download ' +
            'ili pokrenite INSTALIRAJ.bat iz gotovog instalacionog paketa (bez kompajliranja).')
    }
    $max = ($majors | Measure-Object -Maximum).Maximum
    Write-Log ('.NET SDK: ' + (($majors | Sort-Object -Unique) -join ', '))
    $buildable = @()
    foreach ($t in $Tfms) {
        if ($t -eq 'net10.0-windows' -and $max -lt 10) { Write-Log 'Za AutoCAD 2027 treba .NET SDK 10 - ta verzija se preskače.' 'Yellow'; continue }
        if ($max -lt 8) { Stop-WithError 'Potreban je .NET SDK 8 ili noviji.' }
        $buildable += $t
    }
    if ($buildable.Count -eq 0) { Stop-WithError 'Nema verzije koja se može kompajlirati s instaliranim .NET SDK-om.' }

    $proj = Join-Path $Repo 'autocad-site-design-pro\src\SiteDesignPro.AutoCAD\SiteDesignPro.AutoCAD.csproj'
    foreach ($t in $buildable) {
        Write-Log ('Kompajliranje za ' + $t + ' ...') 'Cyan'
        & dotnet build -c Release -f $t --nologo -v minimal $proj 2>&1 | ForEach-Object { Write-Log ('  ' + $_) }
        if ($LASTEXITCODE -ne 0) { Stop-WithError ('Kompajliranje za ' + $t + ' nije uspjelo (pogledajte ispis iznad).') }
    }

    $bundle = Join-Path $Repo ('dist\' + $BundleName)
    if (Test-Path -LiteralPath $bundle) { Remove-Item -LiteralPath $bundle -Recurse -Force }
    $adapterBin = Join-Path $Repo 'autocad-site-design-pro\src\SiteDesignPro.AutoCAD\bin\Release'
    $coreNet47 = Join-Path $Repo 'autocad-site-design-pro\src\SiteDesignPro.Core\bin\Release\net47'
    $folders = @{ 'net47' = 'R23'; 'net48' = 'R24'; 'net8.0-windows' = 'R25'; 'net10.0-windows' = 'R26' }
    foreach ($t in $buildable) {
        $out = Join-Path $adapterBin $t
        $target = Join-Path $bundle ('Contents\Win64\' + $folders[$t])
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        foreach ($n in 'SiteDesignPro.AutoCAD.dll', 'SiteDesignPro.Core.dll', 'SiteDesignPro.AutoCAD.deps.json', 'SiteDesignPro.AutoCAD.pdb', 'SiteDesignPro.Core.pdb') {
            $f = Join-Path $out $n
            if (Test-Path -LiteralPath $f) { Copy-Item -LiteralPath $f -Destination $target }
        }
        if ($t -like 'net4*') {
            # .NET Framework: System.Text.Json i zavisnosti idu uz dodatak.
            Get-ChildItem -LiteralPath $coreNet47 -Filter *.dll | Where-Object { $_.Name -ne 'SiteDesignPro.Core.dll' } |
                ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target }
        }
    }
    Copy-Item -LiteralPath (Join-Path $Repo 'autocad-site-design-pro\build\PackageContents.xml') -Destination $bundle
    Copy-Item -LiteralPath (Join-Path $Repo 'shared\catalog') -Destination (Join-Path $bundle 'Resources\catalog') -Recurse
    Copy-Item -LiteralPath (Join-Path $Repo 'shared\schema') -Destination (Join-Path $bundle 'Resources\schema') -Recurse
    $docs = Join-Path $bundle 'Contents\Docs'
    New-Item -ItemType Directory -Force -Path $docs | Out-Null
    foreach ($md in 'UPUTSTVO.md', 'PRIRUCNIK.md', 'INSTALACIJA.md', 'KOMPATIBILNOST.md', 'OGRANICENJA.md', 'IZVJESTAJ_TESTIRANJA.md', 'REGISTAR_LICENCI.md') {
        $f = Join-Path $Repo ('docs\' + $md)
        if (Test-Path -LiteralPath $f) { Copy-Item -LiteralPath $f -Destination $docs }
    }
    $links = (Get-ChildItem -LiteralPath $docs -Filter *.md | ForEach-Object { '<li><a href="' + $_.Name + '">' + $_.BaseName + '</a></li>' }) -join ''
    $html = '<!DOCTYPE html><html lang="bs"><head><meta charset="utf-8"><title>Vanjsko uređenje PRO</title></head><body>' +
        '<h1>Vanjsko uređenje PRO</h1><p>Dokumentacija (Markdown):</p><ul>' + $links + '</ul></body></html>'
    [System.IO.File]::WriteAllText((Join-Path $docs 'index.html'), $html, (New-Object System.Text.UTF8Encoding($false)))
    Write-Log ('Paket sklopljen: ' + $bundle) 'Green'
    return $bundle
}

# Gotov paket: parametar -Paket, folder pored skripte, zip pored skripte ili <repo>\dist.
function Find-Bundle {
    $candidates = @()
    if ($Paket) { $candidates += $Paket }
    $candidates += (Join-Path $ScriptDir $BundleName)
    $candidates += (Join-Path $ScriptDir 'SiteDesignPro.bundle.zip')
    $repo = Get-RepoRoot
    if ($repo) { $candidates += (Join-Path $repo ('dist\' + $BundleName)); $candidates += (Join-Path $repo 'dist\SiteDesignPro.bundle.zip') }
    foreach ($c in $candidates) {
        if (-not (Test-Path -LiteralPath $c)) { continue }
        if ($c -like '*.zip') {
            $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('VU-PRO-' + [guid]::NewGuid().ToString('N'))
            Unblock-File -LiteralPath $c -ErrorAction SilentlyContinue
            Expand-Archive -LiteralPath $c -DestinationPath $tmp -Force
            $inner = Join-Path $tmp $BundleName
            if (Test-Path -LiteralPath (Join-Path $inner 'PackageContents.xml')) { return $inner }
            continue
        }
        if (Test-Path -LiteralPath (Join-Path $c 'PackageContents.xml')) { return (Resolve-Path -LiteralPath $c).Path }
    }
    return $null
}

function Get-PluginRoots {
    return @(
        (Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'),
        (Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins')
    )
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Wait-AutoCADClosed {
    while ($true) {
        $running = @(Get-Process -Name acad -ErrorAction SilentlyContinue)
        if ($running.Count -eq 0) { return }
        Write-Log 'AutoCAD je pokrenut. Zatvorite AutoCAD (sačuvajte crteže) jer se inače postavke ne mogu sigurno upisati.' 'Yellow'
        if ($BezPitanja) { Stop-WithError 'AutoCAD je pokrenut.' }
        $answer = Read-Host 'Pritisnite Enter kada zatvorite AutoCAD (ili upišite P za prekid)'
        if ($answer -match '^[pP]') { Stop-WithError 'Instalacija prekinuta.' }
    }
}

# Nova vrijednost TRUSTEDPATHS: uklanja stare unose ovog paketa (bilo gdje), čuva sve ostale, po potrebi dodaje novi.
function Update-TrustedPaths([string]$Current, [string]$Entry, [bool]$Remove) {
    $parts = @($Current.Split(';') | Where-Object { $_ -and ($_.Trim().TrimEnd('.').TrimEnd('\') -notlike ('*\' + $BundleName)) })
    if (-not $Remove) { $parts += $Entry }
    return ($parts -join ';')
}

# Pouzdane putanje (TRUSTEDPATHS) za svaki profil pronađenih AutoCAD-a; profil postoji tek nakon prvog pokretanja.
function Set-TrustedPath([object[]]$Products, [string]$Folder, [bool]$Remove) {
    $entry = $Folder.TrimEnd('\') + '\...'
    $hkcu = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
    foreach ($p in $Products) {
        $profiles = $hkcu.OpenSubKey('Software\Autodesk\AutoCAD\' + $p.Key + '\Profiles')
        if ($null -eq $profiles) {
            if (-not $Remove) { Write-Log ('  ' + $p.Name + ': profil još ne postoji (AutoCAD nije pokretan). Ako se pojavi upozorenje o sigurnosti, odaberite "Always load".') 'Yellow' }
            continue
        }
        foreach ($profName in $profiles.GetSubKeyNames()) {
            $vars = $hkcu.OpenSubKey('Software\Autodesk\AutoCAD\' + $p.Key + '\Profiles\' + $profName + '\Variables', $true)
            if ($null -eq $vars) { continue }
            $current = [string]$vars.GetValue('TRUSTEDPATHS', '')
            $vars.SetValue('TRUSTEDPATHS', (Update-TrustedPaths $current $entry $Remove), [Microsoft.Win32.RegistryValueKind]::String)
            $vars.Close()
            Write-Log ('  ' + $p.Name + ' / profil "' + $profName + '": pouzdane putanje ' + $(if ($Remove) { 'očišćene' } else { 'dopunjene' }))
        }
    }
}

function Remove-OldBundles {
    foreach ($r in Get-PluginRoots) {
        $old = Join-Path $r $BundleName
        if (Test-Path -LiteralPath $old) {
            Remove-Item -LiteralPath $old -Recurse -Force
            Write-Log ('Uklonjena prethodna verzija: ' + $old)
        }
    }
}

function Invoke-Main {
    Write-Log '=================================================='
    Write-Log ' Vanjsko uređenje PRO - instalacija za AutoCAD'
    Write-Log '=================================================='
    if ($env:OS -ne 'Windows_NT') { Stop-WithError 'Instalacija radi samo na Windowsu.' }
    if ($SviKorisnici -and -not (Test-Admin)) {
        # ponovo pokreni skriptu s administratorskim pravima (UAC), s istim parametrima
        Write-Log 'Potrebna su administratorska prava - otvara se UAC upit...' 'Yellow'
        $argsList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $ScriptPath + '"'), '-Pauza')
        foreach ($k in $ScriptParams.Keys) {
            $v = $ScriptParams[$k]
            if ($v -is [System.Management.Automation.SwitchParameter]) { if ($v.IsPresent) { $argsList += ('-' + $k) } }
            else { $argsList += ('-' + $k); $argsList += ('"' + $v + '"') }
        }
        $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList $argsList -Verb RunAs -Wait -PassThru
        exit $proc.ExitCode
    }

    $products = @(Get-InstalledAutoCAD)
    Write-Log ''
    Write-Log 'Pronađeni AutoCAD proizvodi:'
    if ($products.Count -eq 0) { Write-Log '  nijedan (AutoCAD 2020-2027 nije pronađen u registru)' 'Yellow' }
    foreach ($p in $products) {
        if ($p.Info) { Write-Log ('  [podržano]     ' + $p.Name + '  (R' + $p.Release + ' -> ' + $p.Info.Folder + ')') 'Green' }
        else { Write-Log ('  [nije podržano] ' + $p.Name + '  (R' + $p.Release + ')') 'Yellow' }
    }
    $supported = @($products | Where-Object { $_.Info })

    if ($Deinstaliraj) {
        if ($SviKorisnici -and -not (Test-Admin)) { Stop-WithError 'Za uklanjanje iz %ProgramData% pokrenite kao administrator.' }
        Wait-AutoCADClosed
        foreach ($r in Get-PluginRoots) { Set-TrustedPath $supported (Join-Path $r $BundleName) $true }
        Remove-OldBundles
        Write-Log 'Dodatak je uklonjen. Crteži zadržavaju geometriju i podatke.' 'Green'
        return
    }

    if ($supported.Count -eq 0 -and -not $BezPitanja) {
        $a = Read-Host 'Nije pronađena podržana verzija AutoCAD-a. Ipak instalirati? (D/N)'
        if ($a -notmatch '^[dD]') { Stop-WithError 'Instalacija prekinuta.' }
    }

    if ($Izgradi) {
        $repo = Get-RepoRoot
        if (-not $repo) { Stop-WithError 'Izvorni kod nije pronađen. -Izgradi radi samo iz repozitorija projekta.' }
        $tfms = @($supported | ForEach-Object { $_.Info.Tfm } | Sort-Object -Unique)
        if ($tfms.Count -eq 0) { $tfms = @('net47', 'net48', 'net8.0-windows', 'net10.0-windows') }
        Write-Log ''
        Write-Log ('Kompajliranje za: ' + ($tfms -join ', ')) 'Cyan'
        $source = Build-Bundle $repo $tfms
    } else {
        $source = Find-Bundle
        if (-not $source) { Stop-WithError ('Paket ' + $BundleName + ' nije pronađen pored skripte. Raspakujte cijeli instalacioni zip ili pokrenite IZGRADI_I_INSTALIRAJ.bat iz repozitorija.') }
    }

    foreach ($p in $supported) {
        if (-not (Test-Path -LiteralPath (Join-Path $source ('Contents\Win64\' + $p.Info.Folder + '\SiteDesignPro.AutoCAD.dll')))) {
            Write-Log ('UPOZORENJE: paket nema dodatak za ' + $p.Name + ' (folder ' + $p.Info.Folder + ').') 'Yellow'
        }
    }

    if ($SviKorisnici -and -not (Test-Admin)) { Stop-WithError 'Instalacija za sve korisnike traži administratorska prava (desni klik -> Run as administrator).' }
    Wait-AutoCADClosed

    $root = if ($SviKorisnici) { Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins' } else { Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins' }
    $target = Join-Path $root $BundleName
    Write-Log ''
    Remove-OldBundles
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Recurse -Force
    Get-ChildItem -LiteralPath $target -Recurse -File | Unblock-File -ErrorAction SilentlyContinue
    Write-Log ('Instalirano u: ' + $target) 'Green'

    Write-Log 'Pouzdane putanje (SECURELOAD):'
    Set-TrustedPath $supported $target $false

    Write-Log ''
    Write-Log 'GOTOVO. Pokrenite AutoCAD:' 'Green'
    Write-Log '  - treba se pojaviti kartica trake VANJSKO UREĐENJE PRO'
    Write-Log '  - u komandnu liniju upišite SDPABOUT, zatim u praznom crtežu SDPSAMPLE'
    Write-Log '  - ako se kartica ne pojavi: upišite NETLOAD i odaberite DLL iz foldera'
    foreach ($p in $supported) { Write-Log ('      ' + $p.Name + ': ' + (Join-Path $target ('Contents\Win64\' + $p.Info.Folder + '\SiteDesignPro.AutoCAD.dll'))) }
    Write-Log ('Dnevnik instalacije: ' + $LogFile)
}

if (-not $env:SDP_INSTALLER_NO_MAIN) {
    Invoke-Main
    if ($Pauza) { Read-Host 'Pritisnite Enter za zatvaranje' | Out-Null }
}
