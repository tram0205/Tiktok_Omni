# tiktok_Omni — bảo trì định kỳ

## Git / vị trí thư mục project

- **Không nên** để toàn bộ repo trong thư mục OneDrive đang backup (build sinh `bin`/`obj`, profile trình duyệt, Playwright → rất nặng, dễ lỗi đồng bộ).
- **Sao lưu mã nguồn**: dùng Git (GitHub/GitLab/…). File `.gitignore` đã loại `bin/`, `obj/`, `.vs/`, output tạm, log, thư mục video tạm.
- **Clone / làm việc**: tạo thư mục ngoài OneDrive, ví dụ `C:\Dev\tiktok_Omni`, rồi:
  - `git clone <url-repo> .`
  - `dotnet restore`
  - `dotnet build`
  - Với Playwright: trong thư mục project test/app, chạy `pwsh bin/Debug/net472/playwright.ps1 install` (hoặc theo hướng dẫn package Microsoft.Playwright) nếu build báo thiếu browser.
- **Khởi tạo Git lần đầu** (máy đã cài [Git for Windows](https://git-scm.com/download/win)): có thể chạy `tiktok_Omni\tools\setup-git-repo.cmd` (tự `git init` + `git add -A` + `git status` tại thư mục solution), sau đó `git commit` và `git remote add` theo repo GitHub/GitLab của bạn.
- **Nếu gõ `git` mà PowerShell báo không nhận**: đóng mở lại Cursor/VS/terminal, hoặc dùng **Git Bash**, hoặc gọi đầy đủ `"%ProgramFiles%\Git\cmd\git.exe"`.
- **Đẩy mã lên GitHub** (sau khi đã có commit cục bộ):
  1. Đọc **`tiktok_Omni/tools/HUONG_DAN_REPO_GITHUB.txt`** (từng bước tiếng Việt).
  2. Double-click **`github-mo-trang-tao-repo.cmd`** → tạo repo trên web (không tick README).
  3. Double-click **`github-gan-remote-va-push.cmd`** → dán URL HTTPS → đợi push xong.
  4. Hoặc tay: `git remote add origin https://github.com/TÊN_BẠN/TÊN_REPO.git` → `git push -u origin main`
- **Cấu hình & key**: `appsettings.json` nằm **cạnh file .exe** sau khi build (không commit được nhờ `.gitignore`); trên máy mới, chạy app một lần hoặc nhập lại tab **Cài đặt** (DPAPI gắn với user Windows — xem mục dưới).

## TikTok Web / Playwright

- TikTok thường **đổi DOM và selector**. Khi warm-up, đăng bài hoặc đăng nhập **đột ngột lỗi**, kiểm tra trước:
  - File `Services/BrowserAutomation.cs`: URL đăng nhập, selector nút **Đăng nhập**, phát hiện đã đăng nhập.
  - Luồng warm-up trong `TikTokAutomation.cs` và các lệnh Playwright (search, mở video, like/comment).
- Khuyến nghị: sau mỗi lần TikTok **đổi giao diện lớn**, chạy **Dry Run** trước, rồi **Live** trên một profile phụ.

## Cấu hình / secrets

- `appsettings.json`: các API key và `ProxyPass` có thể được lưu dạng **`DPAPI1:`** (mã hóa theo user Windows). Đổi máy hoặc user Windows có thể cần nhập lại key trong tab **Cài đặt**.

## Phụ thuộc trình duyệt

- **Playwright**: giữ phiên bản package khớp với runtime (`dotnet build` / `playwright install` khi nâng version).

## Kiểm thử tự động

- Project `tiktok_Omni.Tests`: `dotnet test` từ thư mục solution.
