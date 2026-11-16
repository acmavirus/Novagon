<?php
$phpVersion = phpversion();
$serverSoftware = $_SERVER['SERVER_SOFTWARE'] ?? 'Nginx';
$documentRoot = $_SERVER['DOCUMENT_ROOT'] ?? __DIR__;
$loadedExtensions = get_loaded_extensions();
?>
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Novagon - Local Development Environment</title>
    <style>
        :root {
            --bg-color: #0f172a;
            --card-bg: #1e293b;
            --card-border: #334155;
            --text-main: #f8fafc;
            --text-muted: #94a3b8;
            --accent: #38bdf8;
            --accent-glow: rgba(56, 189, 248, 0.2);
            --success: #10b981;
        }
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        }
        body {
            background-color: var(--bg-color);
            color: var(--text-main);
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 2rem 1rem;
        }
        .container {
            max-width: 860px;
            width: 100%;
        }
        .header {
            text-align: center;
            margin-bottom: 2.5rem;
        }
        .logo {
            display: inline-flex;
            align-items: center;
            gap: 12px;
            font-size: 2.5rem;
            font-weight: 800;
            letter-spacing: -0.025em;
            color: #ffffff;
            margin-bottom: 0.5rem;
        }
        .logo span {
            color: var(--accent);
        }
        .subtitle {
            color: var(--text-muted);
            font-size: 1.1rem;
        }
        .badge-status {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            background: rgba(16, 185, 129, 0.1);
            color: var(--success);
            padding: 6px 14px;
            border-radius: 9999px;
            font-size: 0.875rem;
            font-weight: 600;
            margin-top: 1rem;
            border: 1px solid rgba(16, 185, 129, 0.2);
        }
        .badge-status::before {
            content: '';
            width: 8px;
            height: 8px;
            background: var(--success);
            border-radius: 50%;
            box-shadow: 0 0 10px var(--success);
        }
        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 1.25rem;
            margin-bottom: 2rem;
        }
        .card {
            background: var(--card-bg);
            border: 1px solid var(--card-border);
            border-radius: 12px;
            padding: 1.25rem 1.5rem;
            transition: transform 0.2s, border-color 0.2s;
        }
        .card:hover {
            transform: translateY(-2px);
            border-color: var(--accent);
        }
        .card-title {
            color: var(--text-muted);
            font-size: 0.85rem;
            text-transform: uppercase;
            font-weight: 600;
            letter-spacing: 0.05em;
            margin-bottom: 0.5rem;
        }
        .card-value {
            font-size: 1.35rem;
            font-weight: 700;
            color: #fff;
        }
        .actions {
            display: flex;
            flex-wrap: wrap;
            gap: 1rem;
            justify-content: center;
            margin-bottom: 2.5rem;
        }
        .btn {
            background: var(--card-bg);
            border: 1px solid var(--card-border);
            color: #fff;
            padding: 0.75rem 1.5rem;
            border-radius: 8px;
            font-weight: 600;
            text-decoration: none;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            transition: all 0.2s;
        }
        .btn:hover {
            background: var(--accent);
            color: #0f172a;
            border-color: var(--accent);
            box-shadow: 0 0 15px var(--accent-glow);
        }
        .btn-primary {
            background: #0284c7;
            border-color: #0284c7;
        }
        .btn-primary:hover {
            background: #38bdf8;
        }
        .guide-box {
            background: var(--card-bg);
            border: 1px solid var(--card-border);
            border-radius: 12px;
            padding: 1.5rem;
            line-height: 1.6;
        }
        .guide-box h3 {
            margin-bottom: 0.75rem;
            font-size: 1.1rem;
            color: var(--accent);
        }
        .guide-box ol {
            padding-left: 1.25rem;
            color: var(--text-muted);
        }
        .guide-box li {
            margin-bottom: 0.5rem;
        }
        .guide-box code {
            background: rgba(0,0,0,0.3);
            color: #f1f5f9;
            padding: 2px 6px;
            border-radius: 4px;
            font-family: Consolas, monospace;
        }
        .footer {
            margin-top: 2.5rem;
            text-align: center;
            font-size: 0.85rem;
            color: var(--text-muted);
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <div class="logo">
                <span>&#11042;</span> Novagon
            </div>
            <p class="subtitle">Môi trường phát triển Web cục bộ hiện đại, tốc độ cao trên Windows</p>
            <div class="badge-status">Hệ thống đang hoạt động ổn định</div>
        </div>

        <div class="grid">
            <div class="card">
                <div class="card-title">PHP Version</div>
                <div class="card-value"><?= htmlspecialchars($phpVersion) ?></div>
            </div>
            <div class="card">
                <div class="card-title">Web Server</div>
                <div class="card-value"><?= htmlspecialchars(explode(' ', $serverSoftware)[0]) ?></div>
            </div>
            <div class="card">
                <div class="card-title">Database</div>
                <div class="card-value">MariaDB / MySQL</div>
            </div>
            <div class="card">
                <div class="card-title">Document Root</div>
                <div class="card-value" style="font-size: 0.95rem; word-break: break-all;"><?= htmlspecialchars($documentRoot) ?></div>
            </div>
        </div>

        <div class="actions">
            <a href="/phpmyadmin/" class="btn btn-primary" target="_blank">Quản lý Database (phpMyAdmin)</a>
            <a href="?phpinfo=1" class="btn">Xem PHP Info</a>
        </div>

        <?php if (isset($_GET['phpinfo'])): ?>
            <div style="background: #fff; color: #000; border-radius: 12px; padding: 1.5rem; overflow: auto; margin-bottom: 2rem;">
                <?php phpinfo(); exit; ?>
            </div>
        <?php endif; ?>

        <div class="guide-box">
            <h3>Bắt đầu phát triển dự án mới</h3>
            <ol>
                <li>Mở thư mục <code>www/</code> trong Novagon.</li>
                <li>Tạo một thư mục dự án mới, ví dụ <code>my-blog</code>.</li>
                <li>Novagon sẽ tự động kích hoạt Virtual Host và tên miền tương ứng: <code>http://my-blog.test</code> (hỗ trợ cả SSL <code>https://my-blog.test</code>).</li>
                <li>Quản lý Database tại cổng <code>3306</code> với tài khoản mặc định: User <code>root</code>, Password để trống.</li>
            </ol>
        </div>

        <div class="footer">
            Novagon v1.0.0 &bull; Fast, Portable &amp; Modern Development Stack
        </div>
    </div>
</body>
</html>
