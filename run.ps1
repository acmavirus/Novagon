# Khởi động Novagon thông minh (tự động đóng instance cũ để cập nhật)
taskkill /F /IM Novagon.exe 2>$null
taskkill /F /IM OpenLaragon.UI.exe 2>$null
Get-CimInstance Win32_Process -Filter "Name = 'Novagon.exe' or Name = 'OpenLaragon.UI.exe'" -ErrorAction SilentlyContinue | Invoke-CimMethod -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null
Start-Sleep -Milliseconds 300

Write-Host "Đang biên dịch và khởi động Novagon..." -ForegroundColor Cyan
dotnet build -v q
if ($LASTEXITCODE -eq 0) {
    $exe = Join-Path $PSScriptRoot "src\Novagon.UI\bin\Debug\net10.0-windows\Novagon.exe"
    Start-Process $exe -WorkingDirectory $PSScriptRoot
    Write-Host "Novagon đã khởi chạy thành công!" -ForegroundColor Green
} else {
    Write-Host "Lỗi biên dịch!" -ForegroundColor Red
}
