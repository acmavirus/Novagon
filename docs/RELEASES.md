# Hướng Dẫn Đóng Gói & Phát Hành Bản Cài Đặt (Releases) Novagon

Tài liệu này hướng dẫn chi tiết quy trình xây dựng, đóng gói và phát hành các bản cài đặt thực thi (`.exe`) cho Novagon cả trên môi trường cục bộ (Local) và thông qua hệ thống tự động hóa GitHub Actions (CI/CD Releases).

---

## 1. Các Biến Thể Bản Cài Đặt (Release Variants)

Hệ thống đóng gói hỗ trợ 2 biến thể bản cài đặt chính để phục vụ đa dạng nhu cầu của người dùng:

| Biến Thể | Tên File Đầu Ra | Runtimes Đi Kèm | Dung Lượng Ước Tính | Mục Đích |
| :--- | :--- | :--- | :--- | :--- |
| **Full Stack** *(Mặc định)* | `Novagon-Setup-{Version}.exe` | Nginx, PHP 8.3, MariaDB 10.11, Node.js v20, Redis, Composer, Mailpit | ~170 MB | Trọn gói all-in-one, cài xong dùng được ngay toàn bộ stack. |
| **Lite** | `Novagon-Lite-Setup-{Version}.exe` | Nginx, PHP 8.3, Composer, Tools | ~35 - 45 MB | Siêu nhẹ, tải nhanh, phù hợp cho lập trình viên PHP thuần túy hoặc máy cấu hình vừa. |

---

## 2. Đóng Gói Bản Cài Đặt Cục Bộ (Local Build)

### 2.1. Yêu Cầu Môi Trường
- **Hệ điều hành**: Windows 10/11 x64.
- **.NET SDK**: .NET 10 SDK (hoặc cài đặt qua script `dotnet-install.ps1`).
- **Inno Setup**: Phiên bản 6.x trở lên (`ISCC.exe`).
  - Cài đặt nhanh bằng winget: `winget install JRSoftware.InnoSetup`
  - Hoặc bằng chocolatey: `choco install innosetup -y`

### 2.2. Các Lệnh Đóng Gói Thường Dùng

Mở PowerShell tại thư mục gốc của dự án:

```powershell
# 1. Đóng gói bản mặc định (Full) với phiên bản đọc từ csproj
.\build-installer.ps1

# 2. Đóng gói bản Lite siêu nhẹ
.\build-installer.ps1 -PackageType Lite

# 3. Đóng gói cùng lúc cả 2 bản (Full và Lite)
.\build-installer.ps1 -PackageType Both

# 4. Chỉ định phiên bản cụ thể
.\build-installer.ps1 -Version 1.0.1 -PackageType Both

# 5. Đóng gói nhanh (bỏ qua bước biên dịch lại C# nếu app đã build sẵn)
.\build-installer.ps1 -PackageType Lite -SkipCompileApp
```

### 2.3. Đầu Ra Sau Khi Build
Các file hoàn chỉnh sẽ nằm tại thư mục `dist\installer\`:
- `Novagon-Setup-{Version}.exe`
- `Novagon-Lite-Setup-{Version}.exe`
- `SHA256SUMS.txt`: Chứa mã băm SHA256 của từng file exe để người dùng kiểm tra tính toàn vẹn.

---

## 3. Quy Trình Tự Động Hóa Release Với GitHub Actions

Dự án đã được tích hợp quy trình CI/CD hoàn chỉnh tại `.github/workflows/release.yml`.

### Cách 1: Sử dụng công cụ tự động hóa `release.ps1` (Khuyên dùng)
Công cụ này sẽ tự động cập nhật số phiên bản vào `Novagon.UI.csproj`, tạo Git commit, gắn tag và push lên GitHub để kích hoạt build:

```powershell
# Chế độ tương tác từng bước
.\release.ps1

# Chỉ định phiên bản và tự động push lên GitHub
.\release.ps1 -Version 1.0.1 -Push

# Kiểm tra build local trước rồi mới push
.\release.ps1 -Version 1.0.1 -BuildLocal -Push
```

### Cách 2: Tạo Git Tag thủ công
Khi bạn push một tag có định dạng `v*.*.*`, GitHub Actions sẽ tự động kích hoạt tiến trình đóng gói:

```powershell
# 1. Tạo tag phiên bản mới
git tag -a v1.0.1 -m "Release Novagon v1.0.1"

# 2. Đẩy tag lên GitHub
git push origin v1.0.1
```

### Cách 3: Chạy thủ công trên giao diện GitHub Web
1. Truy cập vào kho mã nguồn trên GitHub: `https://github.com/acmavirus/Novagon/actions`.
2. Chọn workflow **Build & Publish Releases**.
3. Bấm **Run workflow**:
   - Nhập **Release Version** (ví dụ: `1.0.1`).
   - Chọn **Package Type** (`Both`, `Full`, hoặc `Lite`).
   - Tùy chọn đánh dấu **Pre-release** nếu là bản thử nghiệm.
4. Bấm **Run workflow**.

---

## 4. Kết Quả Trên GitHub Releases
Khi tiến trình GitHub Actions hoàn thành:
1. Một bản phát hành mới sẽ được tạo tự động tại tab **Releases** trên GitHub.
2. Bản phát hành tự động sinh **Release Notes** dựa trên các commit mới.
3. Các tệp đính kèm sẽ bao gồm:
   - `Novagon-Setup-{Version}.exe`
   - `Novagon-Lite-Setup-{Version}.exe`
   - `SHA256SUMS.txt`

---

## 5. Xác Thực Mã Băm (SHA-256 Checksums)

Người dùng tải về có thể xác thực tính toàn vẹn của tệp cài đặt bằng lệnh PowerShell:

```powershell
Get-FileHash -Path "Novagon-Setup-1.0.0.exe" -Algorithm SHA256
```
Sau đó đối chiếu với mã trong tệp `SHA256SUMS.txt`.
