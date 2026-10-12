using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Back.Services;

public class RecoveryMailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FrontendUrl { get; set; } = "http://localhost:4200";
    public bool Configured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public interface IRecoveryMail
{
    bool Configured { get; }
    bool Enqueue(string email, string token);
}

// Cola acotada: el endpoint siempre responde igual, sin esperar al servidor de correo.
public sealed class RecoveryMail(IOptions<RecoveryMailOptions> options, ILogger<RecoveryMail> logger)
    : BackgroundService, IRecoveryMail
{
    private readonly Channel<(string Email, string Token)> queue = Channel.CreateBounded<(string, string)>(100);
    public bool Configured => options.Value.Configured;
    public bool Enqueue(string email, string token) => queue.Writer.TryWrite((email, token));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var config = options.Value;
                // El fragmento no se envía al servidor web ni aparece en sus registros de acceso.
                var link = $"{config.FrontendUrl.TrimEnd('/')}/restablecer#email={Uri.EscapeDataString(item.Email)}&token={item.Token}";
                using var message = new MailMessage(config.From, item.Email)
                {
                    Subject = "Restablece tu contraseña de Nudo",
                    Body = $"Recibimos una solicitud para restablecer tu contraseña.\n\nAbre este enlace, válido durante 15 minutos y para un solo uso:\n{link}\n\nSi no lo solicitaste, ignora este mensaje. Tu contraseña no ha cambiado.",
                };
                using var client = new SmtpClient(config.Host, config.Port)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(config.Username, config.Password),
                };
                await client.SendMailAsync(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // No registrar direcciones, enlaces, contraseñas ni el mensaje del proveedor SMTP.
                logger.LogError("No se pudo entregar un correo de recuperación ({Tipo}).", ex.GetType().Name);
            }
        }
    }
}
