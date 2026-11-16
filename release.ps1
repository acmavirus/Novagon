# =============================================================================
# NOVAGON - Release Automation Tool
# Huong dan su dung:
#   .\release.ps1                      (Che do tuong tac hoi version)
#   .\release.ps1 -Version 1.0.1       (Chi dinh version truc tiep)
#   .\release.ps1 -Version 1.0.1 -Push (Tu dong commit, tag va push len GitHub)
# =============================================================================
param(
    [string]$Version = "",
    [ValidateSet("Full", "Lite", "Both")][string]$PackageType = "Both",
    [switch]$BuildLocal,
    [switch]$Push
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "           NOVAGON - CONG CU PHAT HANH RELEASE            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Kiem tra thu muc git
$gitRoot = git rev-parse --show-toplevel 2>$null
if (-not $gitRoot) {
    Write-Host "[!] Khong tim thay Git repository!" -ForegroundColor Red
    exit 1
}

# 2. Xac dinh version
$csprojPath = Join-Path $PSScriptRoot "src\Novagon.UI\Novagon.UI.csproj"
$currentVer = "1.0.0"
if (Test-Path $csprojPath) {
    $csprojXml = [xml](Get-Content $csprojPath -Raw)
    $currentVer = $csprojXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $currentVer) { $currentVer = "1.0.0" }
}

Write-Host "[*] Phien ban hien tai trong csproj: $currentVer" -ForegroundColor DarkGray

if ([string]::IsNullOrWhiteSpace($Version)) {
    $suggestedVer = $currentVer
    $parts = $currentVer.Split('.')
    if ($parts.Count -eq 3 -and [int]::TryParse($parts[2], [ref]$null)) {
        $nextPatch = [int]$parts[2] + 1
        $suggestedVer = "$($parts[0]).$($parts[1]).$nextPatch"
    }
    
    $inputVer = Read-Host "Nhap version moi can phat hanh (Enter de chon $suggestedVer)"
    if ([string]::IsNullOrWhiteSpace($inputVer)) {
        $Version = $suggestedVer
    } else {
        $Version = $inputVer.Trim().TrimStart('v')
    }
} else {
    $Version = $Version.Trim().TrimStart('v')
}

Write-Host "[+] Phien ban phat hanh: v$Version" -ForegroundColor Green

# 3. Cap nhat csproj
Write-Host "`n[1/4] Cap nhat metadata phien ban vao Novagon.UI.csproj..." -ForegroundColor Yellow
$csprojRaw = Get-Content $csprojPath -Raw
$csprojRaw = [System.Text.RegularExpressions.Regex]::Replace($csprojRaw, "<Version>.*?</Version>", "<Version>$Version</Version>")
$csprojRaw = [System.Text.RegularExpressions.Regex]::Replace($csprojRaw, "<FileVersion>.*?</FileVersion>", "<FileVersion>$Version.0</FileVersion>")
$csprojRaw = [System.Text.RegularExpressions.Regex]::Replace($csprojRaw, "<AssemblyVersion>.*?</AssemblyVersion>", "<AssemblyVersion>$Version.0</AssemblyVersion>")
[System.IO.File]::WriteAllText($csprojPath, $csprojRaw, [System.Text.Encoding]::UTF8)
Write-Host "[v] Da cap nhat phien ban thanh $Version." -ForegroundColor Green

# 4. Tuy chon Build Local
if ($BuildLocal) {
    Write-Host "`n[2/4] Tien hanh build thu nghiem ban cai dat cuc bo..." -ForegroundColor Yellow
    $buildScript = Join-Path $PSScriptRoot "build-installer.ps1"
    & $buildScript -Version $Version -PackageType $PackageType
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[!] Build thu nghiem that bai. Dung tien trinh release." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "`n[2/4] Bo qua build cuc bo (se do GitHub Actions dam nhan khi tao tag)." -ForegroundColor Gray
}

# 5. Git Commit & Tag
Write-Host "`n[3/4] Tao Git commit va tag..." -ForegroundColor Yellow
git add src/Novagon.UI/Novagon.UI.csproj .github/ installer/ build-installer.ps1 setup-runtimes.ps1 release.ps1 docs/
git commit -m "chore(release): bump version to v$Version"

$tagName = "v$Version"
$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "[!] Tag $tagName da ton tai. Xoa tag cu cuc bo de tao lai..." -ForegroundColor Yellow
    git tag -d $tagName
}

git tag -a $tagName -m "Release Novagon $tagName"
Write-Host "[v] Da tao git tag: $tagName" -ForegroundColor Green

# 6. Push len Remote
if ($Push) {
    Write-Host "`n[4/4] Dang push commit va tag len GitHub..." -ForegroundColor Yellow
    git push origin HEAD
    git push origin $tagName --force
    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " [THANH CONG] DA PUSH TAG $tagName LEN GITHUB!" -ForegroundColor Green
    Write-Host " GitHub Actions se tu dong build va tao ban cai dat EXE tai:" -ForegroundColor Cyan
    Write-Host " https://github.com/acmavirus/Novagon/actions" -ForegroundColor Cyan
    Write-Host " https://github.com/acmavirus/Novagon/releases" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    Write-Host "`n[4/4] Tiep theo, ban hay chay lenh sau de kich hoat build tren GitHub:" -ForegroundColor Yellow
    Write-Host "   git push origin HEAD; git push origin $tagName" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Green
}
