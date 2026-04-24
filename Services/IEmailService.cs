using System.Threading;
using System.Threading.Tasks;

namespace Denkiishi_v2.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string body, string? userId = null, CancellationToken cancellationToken = default);
}

