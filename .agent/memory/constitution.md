# Constitution - Novagon Agent Instructions

## 1. QUY TẮC CỐT LÕI VỀ GIT
- **KHÔNG TỰ Ý COMMIT / PUSH**: Agent tuyệt đối không tự ý chạy `git commit` hoặc `git push` sau khi sửa code hoặc tạo file.
- **CHỈ COMMIT KHI ĐƯỢC YÊU CẦU**: Chỉ thực hiện commit/push khi người dùng ra lệnh trực tiếp trong hội thoại.
- **KIỂM THỬ TẠI WORKSPACE CỤC BỘ**: Tất cả thay đổi code phải được build và kiểm thử cục bộ, sau đó báo cáo kết quả để người dùng tự xem xét và quyết định.

## 2. QUY CHUẨN KỸ THUẬT
- Ngôn ngữ giao tiếp: Tiếng Việt.
- Shell: PowerShell (ngăn cách lệnh bằng `;`, không dùng `&&`).
- Dự án: C# .NET 10 WPF (Novagon.UI), Portable Runtimes (Nginx, PHP, MariaDB, Node.js, Composer).
- Không commit file nhị phân lớn (> 100MB) vào Git; bản cài đặt exe quản lý qua GitHub Releases hoặc local dist.
