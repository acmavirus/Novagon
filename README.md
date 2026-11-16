# Novagon

> Modern, Portable & High-Performance Local Web Development Environment for Windows.

Novagon là môi trường phát triển web cục bộ hiện đại dành cho Windows (tương tự Laragon), được xây dựng trên nền tảng **.NET 10** với kiến trúc **Portable Stack** hoàn toàn độc lập, không phụ thuộc vào registry hệ thống hay dịch vụ nền phức tạp.

---

## 🚀 Tính Năng Nổi Bật

- **Kiến trúc Portable 100%**: Tất cả ứng dụng, cấu hình và cơ sở dữ liệu đều nằm trong một thư mục duy nhất (`C:\Novagon`).
- **Giao diện hiện đại**: Ứng dụng quản trị WPF/.NET 10 nhẹ nhàng, quản lý tiến trình mượt mà qua Windows Job Objects (tự động dọn sạch tiến trình con khi tắt app).
- **Hỗ trợ Đa Runtimes**:
  - Web Server: Nginx (tự động sinh vhosts động theo tên thư mục).
  - Ngôn ngữ: PHP 8.x NTS x64, Node.js LTS v20.x, Composer CLI.
  - Database: MariaDB / MySQL 10.x Portable.
  - Công cụ: Mailpit (kiểm thử email cục bộ), Redis cache.
- **SSL / HTTPS Cục Bộ Tự Động**: Tự động cấp phát chứng chỉ số SSL cho các tên miền `.test` hoặc `.local`.
- **Hệ thống Đóng gói & Phát hành (Releases)**: Hỗ trợ đóng gói bản cài đặt `.exe` tự động qua Inno Setup và CI/CD GitHub Actions với 2 phiên bản **Full** và **Lite**.

---

## 📦 Bản Cài Đặt (Releases)

Bạn có thể tải các bản phát hành mới nhất tại mục [Releases](https://github.com/acmavirus/Novagon/releases):

- **Novagon Full (`Novagon-Setup-{version}.exe`)**: Bộ cài đặt trọn gói đầy đủ tất cả runtimes (Nginx, PHP, MariaDB, Node.js, Redis, Composer).
- **Novagon Lite (`Novagon-Lite-Setup-{version}.exe`)**: Bộ cài đặt siêu nhẹ (~35-45 MB), chỉ bao gồm App Core, Nginx, PHP 8.3 và Composer.

---

## 🛠️ Hướng Dẫn Build Bản Cài Đặt (.exe)

Xem tài liệu chi tiết tại: [docs/RELEASES.md](docs/RELEASES.md)

### Đóng gói nhanh cục bộ:
```powershell
# Build bản Full (mặc định)
.\build-installer.ps1

# Build bản Lite
.\build-installer.ps1 -PackageType Lite

# Build cả 2 bản Full & Lite
.\build-installer.ps1 -PackageType Both
```

### Tự động phát hành bản mới (Release & Push lên GitHub Actions):
```powershell
.\release.ps1 -Version 1.0.1 -Push
```

---

## 📄 Giấy Phép
Dự án được phát triển bởi Novagon Team. Mọi đóng góp xin mở Pull Request hoặc Issue trên GitHub.
