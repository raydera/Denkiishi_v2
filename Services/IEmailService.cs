using System.Threading;
using System.Threading.Tasks;

namespace Denkiishi_v2.Services;

public interface IEmailService
{
    /// <param name="isHtml">Quando <see langword="true"/>, o corpo é enviado como MIME <c>text/html</c>; caso contrário, <c>text/plain</c>.</param>
    Task SendAsync(string toEmail, string subject, string body, string? userId = null, bool isHtml = false, CancellationToken cancellationToken = default);
}

