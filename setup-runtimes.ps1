# Setup Runtimes tự động cho Novagon (Nginx, PHP, MariaDB, Composer, Node.js)
param(
    [switch]$Full,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "    Novagon - Cài đặt Runtimes Portable   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Đảm bảo các thư mục cơ sở tồn tại
$baseDirs = @(
    "bin",
    "bin/nginx",
    "bin/php",
    "bin/mysql",
    "bin/nodejs",
    "bin/composer",
    "etc",
    "etc/nginx"
)
foreach ($dir in $baseDirs) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# 1. Tải Nginx 1.26.2 (~2 MB)
$nginxUrl = "http://nginx.org/download/nginx-1.26.2.zip"
$nginxZip = "nginx_temp.zip"
$nginxDest = "bin/nginx"

if ($Force -or !(Test-Path "bin/nginx/nginx-1.26.2/nginx.exe")) {
    Write-Host "[1/5] Đang tải Nginx 1.26.2 (~2 MB)..." -ForegroundColor Yellow
    curl.exe -L --retry 3 --retry-delay 2 -o $nginxZip $nginxUrl
    
    Write-Host "Đang giải nén Nginx vào bin/nginx/..." -ForegroundColor Yellow
    Expand-Archive -Path $nginxZip -DestinationPath $nginxDest -Force
    Remove-Item $nginxZip -Force -ErrorAction SilentlyContinue

    # Copy mime.types sang etc/nginx/
    if (Test-Path "bin/nginx/nginx-1.26.2/conf/mime.types") {
        Copy-Item "bin/nginx/nginx-1.26.2/conf/mime.types" "etc/nginx/mime.types" -Force
    }

    Write-Host "-> Đã cài đặt Nginx 1.26.2 thành công!" -ForegroundColor Green
} else {
    Write-Host "-> Nginx 1.26.2 đã có sẵn trong bin/nginx/." -ForegroundColor Green
}

# 2. Tải PHP 8.3.10 NTS x64 (~32 MB)
$phpUrl = "https://downloads.php.net/~windows/releases/archives/php-8.3.10-nts-Win32-vs16-x64.zip"
$phpZip = "php_temp.zip"
$phpDest = "bin/php/php-8.3.10"

if ($Force -or !(Test-Path "bin/php/php-8.3.10/php-cgi.exe")) {
    Write-Host "[2/5] Đang tải PHP 8.3.10 NTS x64 (~32 MB)..." -ForegroundColor Yellow
    curl.exe -L --retry 3 --retry-delay 2 -o $phpZip $phpUrl
    
    Write-Host "Đang giải nén PHP vào bin/php/php-8.3.10/..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $phpDest -Force | Out-Null
    Expand-Archive -Path $phpZip -DestinationPath $phpDest -Force
    Remove-Item $phpZip -Force -ErrorAction SilentlyContinue

    # Cấu hình php.ini
    if (Test-Path "$phpDest/php.ini-development") {
        Copy-Item "$phpDest/php.ini-development" "$phpDest/php.ini" -Force
        $iniContent = Get-Content "$phpDest/php.ini" -Raw
        
        # Bật extension_dir và các extension phổ biến
        $iniContent = $iniContent -replace ';extension_dir = "ext"', 'extension_dir = "ext"'
        $iniContent = $iniContent -replace ';extension=curl', 'extension=curl'
        $iniContent = $iniContent -replace ';extension=fileinfo', 'extension=fileinfo'
        $iniContent = $iniContent -replace ';extension=mbstring', 'extension=mbstring'
        $iniContent = $iniContent -replace ';extension=openssl', 'extension=openssl'
        $iniContent = $iniContent -replace ';extension=pdo_mysql', 'extension=pdo_mysql'
        $iniContent = $iniContent -replace ';extension=mysqli', 'extension=mysqli'
        
        Set-Content "$phpDest/php.ini" -Value $iniContent -Encoding UTF8
    }

    Write-Host "-> Đã cài đặt PHP 8.3.10 thành công!" -ForegroundColor Green
} else {
    Write-Host "-> PHP 8.3.10 đã có sẵn trong bin/php/." -ForegroundColor Green
}

# 3. Tải Composer CLI (~3 MB)
$composerDest = "bin/composer"
if ($Force -or !(Test-Path "$composerDest/composer.phar")) {
    Write-Host "[3/5] Đang tải Composer CLI (~3 MB)..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $composerDest -Force | Out-Null
    curl.exe -L --retry 3 --retry-delay 2 -o "$composerDest/composer.phar" "https://getcomposer.org/download/latest-stable/composer.phar"
    
    $batContent = "@ECHO OFF`r`nphp `"%~dp0composer.phar`" %*"
    [System.IO.File]::WriteAllText((Join-Path $composerDest "composer.bat"), $batContent, [System.Text.Encoding]::ASCII)
    Write-Host "-> Đã cài đặt Composer CLI thành công!" -ForegroundColor Green
} else {
    Write-Host "-> Composer đã có sẵn trong bin/composer/." -ForegroundColor Green
}

# 4. Tải MariaDB 10.11.8 Portable x64 (~85 MB zip, ~500 MB giải nén)
$mariaDest = "bin/mysql/mariadb-10.11.8-winx64"
if ($Force -or (!(Test-Path "$mariaDest/bin/mysqld.exe") -and !(Test-Path "bin/mysql/mariadb-10.11.8/bin/mysqld.exe"))) {
    Write-Host "[4/5] Đang tải MariaDB 10.11.8 Portable (~85 MB)..." -ForegroundColor Yellow
    $mariaUrl = "https://archive.mariadb.org/mariadb-10.11.8/winx64-packages/mariadb-10.11.8-winx64.zip"
    $mariaZip = "mariadb_temp.zip"
    curl.exe -L --retry 3 --retry-delay 2 -o $mariaZip $mariaUrl
    
    Write-Host "Đang giải nén MariaDB vào bin/mysql/..." -ForegroundColor Yellow
    Expand-Archive -Path $mariaZip -DestinationPath "bin/mysql" -Force
    Remove-Item $mariaZip -Force -ErrorAction SilentlyContinue

    # Đảm bảo có my.ini chuẩn UTF-8 No BOM
    $mariaDir = "bin/mysql/mariadb-10.11.8-winx64"
    if (Test-Path $mariaDir) {
        $iniPath = Join-Path $mariaDir "my.ini"
        if (!(Test-Path $iniPath)) {
            $portableIni = "[mysqld]`ndatadir=./data`nport=3306`nbind-address=127.0.0.1`ncharacter-set-server=utf8mb4`ncollation-server=utf8mb4_unicode_ci`ndefault-storage-engine=INNODB`nmax_allowed_packet=64M`n"
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($iniPath, $portableIni, $utf8NoBom)
        }
    }

    Write-Host "-> Đã cài đặt MariaDB 10.11.8 thành công!" -ForegroundColor Green
} else {
    Write-Host "-> MariaDB 10.11.8 đã có sẵn trong bin/mysql/." -ForegroundColor Green
}

# 5. Tải Node.js v20 LTS (~30 MB zip) nếu cờ -Full được bật
if ($Full) {
    $nodeDest = "bin/nodejs/node-v20"
    if ($Force -or !(Test-Path "$nodeDest/node.exe")) {
        Write-Host "[5/5] Đang tải Node.js v20 LTS x64 (~30 MB)..." -ForegroundColor Yellow
        $nodeUrl = "https://nodejs.org/dist/v20.18.0/node-v20.18.0-win-x64.zip"
        $nodeZip = "node_temp.zip"
        curl.exe -L --retry 3 --retry-delay 2 -o $nodeZip $nodeUrl

        Write-Host "Đang giải nén Node.js vào $nodeDest/..." -ForegroundColor Yellow
        $tempExtract = "bin/nodejs_temp"
        if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue }
        New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null
        Expand-Archive -Path $nodeZip -DestinationPath $tempExtract -Force
        Remove-Item $nodeZip -Force -ErrorAction SilentlyContinue

        $extractedDir = Get-ChildItem -Path $tempExtract -Directory | Select-Object -First 1
        if ($extractedDir) {
            New-Item -ItemType Directory -Path "bin/nodejs" -Force | Out-Null
            if (Test-Path $nodeDest) { Remove-Item $nodeDest -Recurse -Force -ErrorAction SilentlyContinue }
            Move-Item -Path $extractedDir.FullName -Destination $nodeDest -Force
        }
        Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "-> Đã cài đặt Node.js v20 thành công!" -ForegroundColor Green
    } else {
        Write-Host "-> Node.js đã có sẵn trong $nodeDest." -ForegroundColor Green
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  HOÀN TẤT CÀI ĐẶT RUNTIMES CHO NOVAGON!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
