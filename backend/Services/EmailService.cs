using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace backend.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_config["Email:From"]) &&
            !string.IsNullOrEmpty(_config["Email:Password"]);

        public async Task<bool> SendAsync(string to, string subject, string htmlBody)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("Email not configured — skipping send to {To}", to);
                return false;
            }

            try
            {
                var fromAddress = _config["Email:From"]!;
                var fromName = _config["Email:FromName"] ?? "ADLV Store";
                var password = _config["Email:Password"]!;
                var host = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Email:SmtpPort"] ?? "587");

                var msg = new MimeMessage();
                msg.From.Add(new MailboxAddress(fromName, fromAddress));
                msg.To.Add(MailboxAddress.Parse(to));
                msg.Subject = subject;
                msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(fromAddress, password.Replace(" ", ""));
                await client.SendAsync(msg);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                return false;
            }
        }

        public static string BuildVerifyEmailHtml(string fullName, string verifyLink) => $@"
<!DOCTYPE html>
<html><head><meta charset='UTF-8'></head>
<body style='margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,Segoe UI,Roboto,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f5f5f5;padding:40px 20px;'><tr><td align='center'>
<table width='560' cellpadding='0' cellspacing='0' style='background:white;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.08);'>
  <tr><td style='background:linear-gradient(135deg,#DC2626,#991B1B);padding:32px;text-align:center;'>
    <div style='color:white;font-size:14px;letter-spacing:3px;font-style:italic;margin-bottom:4px;'>acmé de la vie</div>
    <div style='color:white;font-size:32px;font-weight:900;letter-spacing:2px;'>ADLV STORE</div>
  </td></tr>
  <tr><td style='padding:40px 32px;'>
    <h2 style='margin:0 0 16px;font-size:24px;color:#111;'>Xác thực email</h2>
    <p style='margin:0 0 12px;color:#444;font-size:15px;line-height:1.6;'>Chào <strong>{fullName}</strong>,</p>
    <p style='margin:0 0 20px;color:#444;font-size:15px;line-height:1.6;'>Cảm ơn bạn đã đăng ký tài khoản tại ADLV Store. Vui lòng nhấn nút bên dưới để xác thực địa chỉ email:</p>
    <div style='text-align:center;margin:32px 0;'>
      <a href='{verifyLink}' style='display:inline-block;background:#111;color:white;text-decoration:none;padding:14px 40px;font-weight:bold;letter-spacing:2px;text-transform:uppercase;font-size:14px;border-radius:6px;'>Xác thực email →</a>
    </div>
    <p style='margin:0 0 12px;color:#777;font-size:13px;line-height:1.6;'>Hoặc copy link sau vào trình duyệt:</p>
    <p style='margin:0 0 24px;color:#DC2626;font-size:12px;word-break:break-all;background:#FEF2F2;padding:12px;border-radius:6px;'>{verifyLink}</p>
    <p style='margin:0;color:#999;font-size:12px;line-height:1.6;'>Link này có hiệu lực trong <strong>24 giờ</strong>. Nếu bạn không đăng ký tài khoản tại ADLV Store, vui lòng bỏ qua email này.</p>
  </td></tr>
  <tr><td style='background:#111;padding:24px;text-align:center;'>
    <div style='color:white;font-size:12px;letter-spacing:2px;text-transform:uppercase;'>© 2026 ADLV Store</div>
    <div style='color:#888;font-size:11px;margin-top:8px;'>Email tự động — vui lòng không trả lời</div>
  </td></tr>
</table>
</td></tr></table></body></html>";

        public static string BuildResetPasswordHtml(string fullName, string resetLink) => $@"
<!DOCTYPE html>
<html><head><meta charset='UTF-8'></head>
<body style='margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,Segoe UI,Roboto,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f5f5f5;padding:40px 20px;'><tr><td align='center'>
<table width='560' cellpadding='0' cellspacing='0' style='background:white;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.08);'>
  <tr><td style='background:linear-gradient(135deg,#DC2626,#991B1B);padding:32px;text-align:center;'>
    <div style='color:white;font-size:14px;letter-spacing:3px;font-style:italic;margin-bottom:4px;'>acmé de la vie</div>
    <div style='color:white;font-size:32px;font-weight:900;letter-spacing:2px;'>ADLV STORE</div>
  </td></tr>
  <tr><td style='padding:40px 32px;'>
    <h2 style='margin:0 0 16px;font-size:24px;color:#111;'>Đặt lại mật khẩu</h2>
    <p style='margin:0 0 12px;color:#444;font-size:15px;line-height:1.6;'>Chào <strong>{fullName}</strong>,</p>
    <p style='margin:0 0 20px;color:#444;font-size:15px;line-height:1.6;'>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấn nút bên dưới để tạo mật khẩu mới:</p>
    <div style='text-align:center;margin:32px 0;'>
      <a href='{resetLink}' style='display:inline-block;background:#DC2626;color:white;text-decoration:none;padding:14px 40px;font-weight:bold;letter-spacing:2px;text-transform:uppercase;font-size:14px;border-radius:6px;'>Đặt lại mật khẩu →</a>
    </div>
    <p style='margin:0 0 12px;color:#777;font-size:13px;line-height:1.6;'>Hoặc copy link sau:</p>
    <p style='margin:0 0 24px;color:#DC2626;font-size:12px;word-break:break-all;background:#FEF2F2;padding:12px;border-radius:6px;'>{resetLink}</p>
    <p style='margin:0 0 8px;color:#999;font-size:12px;line-height:1.6;'>Link này có hiệu lực trong <strong>1 giờ</strong>.</p>
    <p style='margin:0;color:#999;font-size:12px;line-height:1.6;'>Nếu bạn không yêu cầu đặt lại mật khẩu, bỏ qua email này — tài khoản bạn vẫn an toàn.</p>
  </td></tr>
  <tr><td style='background:#111;padding:24px;text-align:center;'>
    <div style='color:white;font-size:12px;letter-spacing:2px;text-transform:uppercase;'>© 2026 ADLV Store</div>
    <div style='color:#888;font-size:11px;margin-top:8px;'>Email tự động — vui lòng không trả lời</div>
  </td></tr>
</table>
</td></tr></table></body></html>";
    }
}
