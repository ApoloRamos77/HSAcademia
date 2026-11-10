using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace HSAcademia.Infrastructure.Services.Push;

public sealed record PushMessage(string Type, string Title, string Body, IReadOnlyDictionary<string, string> Data);

public interface IPushSender
{
    /// <summary>Envía el mensaje y devuelve los celulares que ya no existen, para retirarlos.</summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken);
}

/// <summary>Sin clave de Firebase (desarrollo) no se envía nada: solo queda constancia en el registro.</summary>
public sealed class LoggingPushSender : IPushSender
{
    private readonly ILogger<LoggingPushSender> _logger;

    public LoggingPushSender(ILogger<LoggingPushSender> logger) => _logger = logger;

    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Notificación sin enviar (Firebase sin configurar): {Type} a {Count} celular(es).", message.Type, tokens.Count);
        _logger.LogDebug("Contenido de la notificación: {Title} | {Body}", message.Title, message.Body);
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}

public sealed class FirebasePushSender : IPushSender
{
    private const int MaxTokensPerRequest = 500;
    private const string ChannelId = "avisos";
    private const string Icon = "ic_stat_clubio";
    private const string Color = "#3EB1E5";

    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(string serviceAccountJson, ILogger<FirebasePushSender> logger)
    {
        _logger = logger;
#pragma warning disable CS0618 // La clave llega de la configuración del propio servidor, no de un tercero.
        var credential = GoogleCredential.FromJson(serviceAccountJson);
#pragma warning restore CS0618
        var app = FirebaseApp.Create(new AppOptions { Credential = credential }, "clubio");
        _messaging = FirebaseMessaging.GetMessaging(app);
        // El proyecto debe ser el mismo de google-services.json; si no, Firebase rechaza cada celular.
        _logger.LogInformation("Notificaciones activas con Firebase, proyecto {ProjectId}.",
            (credential.UnderlyingCredential as ServiceAccountCredential)?.ProjectId ?? "desconocido");
    }

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken)
    {
        var invalid = new List<string>();
        var failures = new List<FirebaseMessagingException>();
        var accepted = 0;
        foreach (var batch in tokens.Chunk(MaxTokensPerRequest))
        {
            var response = await _messaging.SendEachForMulticastAsync(new MulticastMessage
            {
                Tokens = batch,
                Notification = new Notification { Title = message.Title, Body = message.Body },
                Data = message.Data,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification { ChannelId = ChannelId, Icon = Icon, Color = Color, Sound = "default" }
                }
            }, cancellationToken);

            accepted += response.SuccessCount;
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var error = response.Responses[i].Exception;
                if (error == null) continue;
                failures.Add(error);
                if (error.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch)
                    invalid.Add(batch[i]);
            }
        }

        _logger.LogInformation("Notificación {Type}: Firebase aceptó {Accepted} de {Total} celular(es).", message.Type, accepted, tokens.Count);
        foreach (var group in failures.GroupBy(f => f.MessagingErrorCode))
        {
            _logger.LogWarning("Firebase rechazó la notificación {Type} en {Count} celular(es). Código {Code}: {Detail}",
                message.Type, group.Count(), group.Key?.ToString() ?? "sin código", group.First().Message);
        }
        return invalid;
    }
}
