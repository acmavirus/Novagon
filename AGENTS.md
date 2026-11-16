# AGENTS.md - Quy Tắc Làm Việc Dành Cho Agent (Novagon Project)

## 🚨 QUY TẮC BẮT BUỘC VỀ GIT & PHÁT HÀNH (STRICT GIT POLICY)

- **TUYỆT ĐỐI KHÔNG TỰ Ý COMMIT HOẶC PUSH**:
  - Agent **KHÔNG ĐƯỢC PHÉP** tự ý chạy các lệnh:
    - `git commit`
    - `git push`
    - `git tag` (kèm push)
  - Chỉ được phép thực hiện commit hoặc push khi **NGƯỜI DÙNG YÊU CẦU TRỰC TIẾP VÀ RÕ RÀNG** (Ví dụ: *"commit đi", "hãy commit các file này", "push lên repo giúp tôi"*).
- **MỌI THAY ĐỔI CHỈ LƯU TẠI WORKSPACE CỤC BỘ**:
  - Khi hoàn thành tính năng, sửa bug hoặc cập nhật file: chỉ chỉnh sửa file trong thư mục làm việc, chạy build/test để xác nhận chạy đúng và báo cáo kết quả cho người dùng nghiệm thu.
  - Sau khi người dùng nghiệm thu và chỉ đạo commit, Agent mới thực hiện theo hướng dẫn của người dùng.

---

## 🛠️ CÁC NGUYÊN TẮC PHÁT TRIỂN DỰ ÁN NOVAGON

1. **Giao tiếp hoàn toàn bằng Tiếng Việt**: Phản hồi rõ ràng, mạch lạc, dễ hiểu.
2. **Không commit tệp nhị phân lớn vào Git**:
   - Thư mục `dist/` và các file `.exe` cài đặt tuyệt đối không đưa vào Git history (chỉ phát hành qua GitHub Releases hoặc lưu trữ cục bộ).
3. **PowerShell 5.1+ trên Windows**:
   - Sử dụng dấu `;` để ngăn cách các lệnh (KHÔNG dùng `&&`).
4. **Kiểm thử trước khi bàn giao**:
   - Kiểm tra biên dịch (`dotnet build src/Novagon.UI/Novagon.UI.csproj`) và kiểm tra giao diện trước khi thông báo hoàn tất cho người dùng.
