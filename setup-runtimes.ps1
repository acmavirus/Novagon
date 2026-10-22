# Setup Runtimes tự động cho Novagon (Nginx 1.26 + PHP 8.3 NTS)
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "    Novagon - Cài đặt Runtimes Portable   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Tải Nginx 1.26.2
$nginxUrl = "http://nginx.org/download/nginx-1.26.2.zip"
$nginxZip = "nginx_temp.zip"
$nginxDest = "bin/nginx"

if (!(Test-Path "bin/nginx/nginx-1.26.2/nginx.exe")) {
    Write-Host "[1/2] Đang tải Nginx 1.26.2 (~2 MB)..." -ForegroundColor Yellow
    curl.exe -L -o $nginxZip $nginxUrl
    
    Write-Host "Đang giải nén Nginx vào bin/nginx/..." -ForegroundColor Yellow
    Expand-Archive -Path $nginxZip -DestinationPath $nginxDest -Force
    Remove-Item $nginxZip -Force

    # Copy mime.types sang etc/nginx/
    New-Item -ItemType Directory -Path "etc/nginx" -Force | Out-Null
    if (Test-Path "bin/nginx/nginx-1.26.2/conf/mime.types") {
        Copy-Item "bin/nginx/nginx-1.26.2/conf/mime.types" "etc/nginx/mime.types" -Force
    }

    Write-Host "-> Đã cài đặt Nginx 1.26.2 thành công!" -ForegroundColor Green
} else {
    Write-Host "-> Nginx 1.26.2 đã có sẵn trong bin/nginx/." -ForegroundColor Green
}

# 2. Tải PHP 8.3.10 NTS x64
$phpUrl = "https://downloads.php.net/~windows/releases/archives/php-8.3.10-nts-Win32-vs16-x64.zip"
$phpZip = "php_temp.zip"
$phpDest = "bin/php/php-8.3.10"

if (!(Test-Path "bin/php/php-8.3.10/php-cgi.exe")) {
    Write-Host "[2/2] Đang tải PHP 8.3.10 NTS x64 (~32 MB)..." -ForegroundColor Yellow
    curl.exe -L -o $phpZip $phpUrl
    
    Write-Host "Đang giải nén PHP vào bin/php/php-8.3.10/..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $phpDest -Force | Out-Null
    Expand-Archive -Path $phpZip -DestinationPath $phpDest -Force
    Remove-Item $phpZip -Force

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

# 3. Tải MariaDB 10.11.8 Portable x64
$mariaUrl = "https://archive.mariadb.org/mariadb-10.11.8/winx64-packages/mariadb-10.11.8-winx64.zip"
$mariaZip = "mariadb_temp.zip"
$mariaDest = "bin/mysql/mariadb-10.11.8"

if (!(Test-Path "bin/mysql/mariadb-10.11.8-winx64/bin/mysqld.exe") -and !(Test-Path "bin/mysql/mariadb-10.11.8/bin/mysqld.exe")) {
    Write-Host "[3/3] Đang tải MariaDB 10.11.8 Portable (~85 MB)..." -ForegroundColor Yellow
    curl.exe -L -o $mariaZip $mariaUrl
    
    Write-Host "Đang giải nén MariaDB vào bin/mysql/..." -ForegroundColor Yellow
    Expand-Archive -Path $mariaZip -DestinationPath "bin/mysql" -Force
    Remove-Item $mariaZip -Force

    Write-Host "-> Đã cài đặt MariaDB 10.11.8 thành công!" -ForegroundColor Green
} else {
    Write-Host "-> MariaDB 10.11.8 đã có sẵn trong bin/mysql/." -ForegroundColor Green
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  HOÀN TẤT! Bạn có thể nhấn 'Reload VHosts' hoặc 'Start All' trên Novagon." -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
