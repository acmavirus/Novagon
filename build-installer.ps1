# =============================================================================
# Script tu dong dong goi bo cai dat Exe (Installer & Releases) cho Novagon
# Ho tro: Full (Full Stack + DB), Lite (Nginx + PHP + Composer), hoac Both
# =============================================================================
param(
    [string]$Version = "",
    [ValidateSet("Full", "Lite", "Both")][string]$PackageType = "Full",
    [switch]$SkipCompileApp,
    [switch]$SkipRuntimesCheck
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       NOVAGON - TIEN TRINH BUILD BAN CAI DAT EXE         " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Xac dinh Phien ban (Version)
$csprojPath = Join-Path $PSScriptRoot "src\Novagon.UI\Novagon.UI.csproj"
if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path $csprojPath) {
        $csprojContent = [xml](Get-Content $csprojPath -Raw)
        $Version = $csprojContent.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "1.0.0"
    }
}

Write-Host "[*] Phien ban muc tieu   : $Version" -ForegroundColor Green
Write-Host "[*] Loai bo cai dat      : $PackageType" -ForegroundColor Green

# 2. Tim Inno Setup Compiler (ISCC.exe)
$isccPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$iscc = $null
foreach ($p in $isccPaths) {
    if (Test-Path $p) {
        $iscc = $p
        break
    }
}

if (-not $iscc) {
    $whereIscc = (where.exe iscc 2>$null)
    if ($whereIscc) {
        $iscc = $whereIscc[0]
    }
}

if (-not $iscc) {
    Write-Host "[!] Khong tim thay Inno Setup Compiler (ISCC.exe)!" -ForegroundColor Red
    Write-Host "    Cai dat nhanh bang winget: winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
    Write-Host "    Hoac cai bang choco     : choco install innosetup -y" -ForegroundColor Yellow
    exit 1
}

Write-Host "[+] Da tim thay Inno Setup Compiler: $iscc" -ForegroundColor Green

# 3. Kiem tra va Tu dong tai Runtimes neu thieu
if (-not $SkipRuntimesCheck) {
    $hasNginx = Test-Path (Join-Path $PSScriptRoot "bin\nginx")
    $hasPhp   = Test-Path (Join-Path $PSScriptRoot "bin\php")
    if (-not $hasNginx -or -not $hasPhp) {
        Write-Host "`n[*] Phat hien thieu runtimes co ban, tien hanh tai tu dong..." -ForegroundColor Yellow
        $setupRuntimesScript = Join-Path $PSScriptRoot "setup-runtimes.ps1"
        if (Test-Path $setupRuntimesScript) {
            & $setupRuntimesScript
        }
    }
}

# 4. Bien dich Novagon.UI (Single-file Self-Contained Win-x64)
$appOut = Join-Path $PSScriptRoot "dist\app"
$appExe = Join-Path $appOut "Novagon.exe"

if ($SkipCompileApp -and (Test-Path $appExe)) {
    Write-Host "`n[1/4] Bo qua bien dich .NET theo yeu cau (-SkipCompileApp)." -ForegroundColor Gray
} else {
    Write-Host "`n[1/4] Dang bien dich Novagon (.NET 10 Self-Contained Win-x64 Single-file)..." -ForegroundColor Yellow
    if (Test-Path $appOut) {
        Remove-Item -Path $appOut -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    $publishArgs = @(
        "publish",
        "$csprojPath",
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:Version=$Version",
        "-p:FileVersion=$Version.0",
        "-p:AssemblyVersion=$Version.0",
        "-o", "$appOut"
    )
    
    & dotnet @publishArgs
    
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $appExe)) {
        Write-Host "[!] Bien dich that bai!" -ForegroundColor Red
        exit 1
    }
    Write-Host "[v] Bien dich hoan tat thanh cong: $appExe" -ForegroundColor Green
}

# Ham dong goi mot bien the (Variant: Full / Lite)
function Build-PackageVariant {
    param(
        [string]$VariantName,       # "Full" hoac "Lite"
        [string]$OutputBaseFilename # Ten file exe (khong co phan mo rong)
    )

    Write-Host "`n----------------------------------------------------------" -ForegroundColor Magenta
    Write-Host " TIEN HANH DONG GOI BIEN THE: [$VariantName] -> $OutputBaseFilename.exe" -ForegroundColor Magenta
    Write-Host "----------------------------------------------------------" -ForegroundColor Magenta

    # 1. Chuan bi staging
    $staging = Join-Path $PSScriptRoot "dist\package"
    if (Test-Path $staging) {
        Remove-Item -Path $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    # Copy Novagon.exe
    Copy-Item $appExe (Join-Path $staging "Novagon.exe") -Force

    # Copy bin/
    Write-Host " -> Copying runtimes (bin)..." -ForegroundColor Gray
    $stagingBin = Join-Path $staging "bin"
    New-Item -ItemType Directory -Path $stagingBin -Force | Out-Null

    $binSource = Join-Path $PSScriptRoot "bin"
    if (Test-Path $binSource) {
        Get-ChildItem -Path $binSource -Directory | ForEach-Object {
            $subDirName = $_.Name.ToLower()

            # Neu la ban Lite, bo qua cac runtime nang: mysql, nodejs, redis
            if ($VariantName -eq "Lite" -and ($subDirName -in @("mysql", "nodejs", "redis"))) {
                Write-Host "    [Lite] Bo qua runtime nang: $subDirName" -ForegroundColor DarkGray
                return
            }

            if ($subDirName -eq "mysql") {
                # Copy MariaDB/MySQL nhung loai bo database ca nhan
                Copy-Item -Path $_.FullName -Destination $stagingBin -Recurse -Force
                
                Get-ChildItem -Path (Join-Path $stagingBin "mysql\*\data") -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                    $dbName = $_.Name
                    if ($dbName -notin @("mysql", "performance_schema", "sys")) {
                        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
                Get-ChildItem -Path (Join-Path $stagingBin "mysql\*\data") -Filter "*.err" -ErrorAction SilentlyContinue | Remove-Item -Force
                Get-ChildItem -Path (Join-Path $stagingBin "mysql\*\data") -Filter "*.pid" -ErrorAction SilentlyContinue | Remove-Item -Force
                
                # Dam bao my.ini trong staging dung template portable UTF-8 No BOM
                Get-ChildItem -Path (Join-Path $stagingBin "mysql\*\my.ini") -ErrorAction SilentlyContinue | ForEach-Object {
                    $portableIni = "[mysqld]`ndatadir=./data`nport=3306`nbind-address=127.0.0.1`ncharacter-set-server=utf8mb4`ncollation-server=utf8mb4_unicode_ci`ndefault-storage-engine=INNODB`nmax_allowed_packet=64M`n"
                    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                    [System.IO.File]::WriteAllText($_.FullName, $portableIni, $utf8NoBom)
                }
            } else {
                Copy-Item -Path $_.FullName -Destination $stagingBin -Recurse -Force
            }
        }
    }

    # Copy etc/
    Write-Host " -> Copying configs (etc)..." -ForegroundColor Gray
    Copy-Item -Path (Join-Path $PSScriptRoot "etc") -Destination $staging -Recurse -Force

    # Don dep tat ca cac file vhost cu trong staging/etc/nginx/sites-enabled
    $sitesEnabled = Join-Path $staging "etc\nginx\sites-enabled"
    if (Test-Path $sitesEnabled) {
        Get-ChildItem -Path $sitesEnabled -Filter "*.conf" -ErrorAction SilentlyContinue | Remove-Item -Force
    }

    # Dọn dẹp cert SSL cá nhân trong staging
    $sslDir = Join-Path $staging "etc\ssl"
    if (Test-Path $sslDir) {
        Get-ChildItem -Path $sslDir -Include "*.crt","*.key" -File -ErrorAction SilentlyContinue | Remove-Item -Force
    }

    # Copy www/ (Chi lay index.php chao mung)
    Write-Host " -> Copying www (welcome page)..." -ForegroundColor Gray
    $stagingWww = Join-Path $staging "www"
    New-Item -ItemType Directory -Path $stagingWww -Force | Out-Null
    $indexSrc = Join-Path $PSScriptRoot "www\index.php"
    if (Test-Path $indexSrc) {
        Copy-Item $indexSrc (Join-Path $stagingWww "index.php") -Force
    }

    # Tao cac thu muc rong can thiet
    New-Item -ItemType Directory -Path (Join-Path $staging "logs") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $staging "backups") -Force | Out-Null

    # 2. Goi Inno Setup Compiler
    Write-Host " -> Dang dong goi Inno Setup (lzma2/ultra64)..." -ForegroundColor Yellow
    $issFile = Join-Path $PSScriptRoot "installer\Novagon.iss"
    $distInstaller = Join-Path $PSScriptRoot "dist\installer"
    if (!(Test-Path $distInstaller)) {
        New-Item -ItemType Directory -Path $distInstaller -Force | Out-Null
    }

    & "$iscc" "/DMyAppVersion=$Version" "/DOutputBaseFilename=$OutputBaseFilename" "$issFile"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[!] Dong goi $OutputBaseFilename that bai!" -ForegroundColor Red
        exit 1
    }
}

# 5. Thuc hien build theo PackageType
$distInstaller = Join-Path $PSScriptRoot "dist\installer"
if (!(Test-Path $distInstaller)) {
    New-Item -ItemType Directory -Path $distInstaller -Force | Out-Null
}

$variantsToBuild = @()
if ($PackageType -eq "Full" -or $PackageType -eq "Both") {
    $variantsToBuild += @{
        VariantName        = "Full"
        OutputBaseFilename = "Novagon-Setup-$Version"
    }
}
if ($PackageType -eq "Lite" -or $PackageType -eq "Both") {
    $variantsToBuild += @{
        VariantName        = "Lite"
        OutputBaseFilename = "Novagon-Lite-Setup-$Version"
    }
}

foreach ($var in $variantsToBuild) {
    Build-PackageVariant -VariantName $var.VariantName -OutputBaseFilename $var.OutputBaseFilename
}

# 6. Tao File Ma Bam SHA-256 Checksums
Write-Host "`n[4/4] Dang tinh toan ma bam SHA256 cho tat ca ban cai dat..." -ForegroundColor Yellow
$checksumFile = Join-Path $distInstaller "SHA256SUMS.txt"
$checksumLines = @()

$builtExes = Get-ChildItem -Path $distInstaller -Filter "Novagon*Setup-$Version.exe"
foreach ($exe in $builtExes) {
    $hash = (Get-FileHash -Path $exe.FullName -Algorithm SHA256).Hash.ToLower()
    $checksumLines += "$hash  $($exe.Name)"
}

if ($checksumLines.Count -gt 0) {
    [System.IO.File]::WriteAllLines($checksumFile, $checksumLines, [System.Text.Encoding]::UTF8)
    Write-Host "[+] Da xuat checksum: $checksumFile" -ForegroundColor Green
}

# 7. Ket qua tong ket
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " [THANH CONG] DA BUILD XONG CAC BAN CAI DAT RELEASES!" -ForegroundColor Green
Write-Host " Thu muc output: $distInstaller" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------" -ForegroundColor DarkGray

foreach ($exe in $builtExes) {
    $sizeMB = [math]::Round($exe.Length / 1MB, 2)
    $hash = (Get-FileHash -Path $exe.FullName -Algorithm SHA256).Hash.ToLower()
    Write-Host " File       : $($exe.Name)" -ForegroundColor White
    Write-Host " Dung luong : $sizeMB MB" -ForegroundColor Yellow
    Write-Host " SHA256     : $hash" -ForegroundColor DarkGray
    Write-Host "----------------------------------------------------------" -ForegroundColor DarkGray
}
Write-Host "==========================================================" -ForegroundColor Green