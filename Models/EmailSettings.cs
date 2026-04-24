namespace Denkiishi_v2.Models;

public class EmailSettings
{
    public string Server { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string SenderName { get; set; } = "Denkiishi";
    public string SenderEmail { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}

