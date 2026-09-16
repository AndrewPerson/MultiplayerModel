using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MultiplayerModel.Actor;
using MultiplayerModel.Transport.Client;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.Default;

public class DefaultClientTransport : IClientTransport
{
    public Uri ServerUri { get; }
    public uint ClientId { get; }

    private readonly Uri listActorsUri;
    private readonly Uri downloadActorBaseUri;
    private readonly Uri sendMessageBaseUri;
    private readonly Uri receiveMessageOrderingUri;

    private readonly HttpClient httpClient = new();

    private readonly BufferBlock<MessageOrdering> messageOrderings = new();

    private readonly JsonSerializerOptions? messageSerialisationOptions;
    private readonly JsonSerializerOptions? actorSerialisationOptions;

    private readonly ILogger logger;

    private CancellationTokenSource? runningCanceller;
    private bool running;

    public static async Task<DefaultClientTransport> Create(
        Uri serverUri,
        JsonSerializerOptions? actorSerialisationOptions = null,
        JsonSerializerOptions? messageSerialisationOptions = null,
        ILogger? logger = null
    )
    {
        var clientIdUriBuilder = new UriBuilder(serverUri.Scheme, serverUri.Host, serverUri.Port)
        {
            Path = "id/new"
        };

        var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(clientIdUriBuilder.Uri, new NoContent());
        var id = BitConverter.ToUInt32(await response.Content.ReadAsByteArrayAsync());
        
        logger?.LogDebug("Received Client ID {id}", id);

        return new(serverUri, id, actorSerialisationOptions, messageSerialisationOptions, logger);
    }

    public DefaultClientTransport(
        Uri serverUri,
        uint clientId,
        JsonSerializerOptions? actorSerialisationOptions,
        JsonSerializerOptions? messageSerialisationOptions,
        ILogger? logger
    )
    {
        ServerUri = serverUri;

        ClientId = clientId;
        httpClient.DefaultRequestHeaders.Add("X-Client-Id", ClientId.ToString());

        listActorsUri = new UriBuilder(serverUri) { Path = "actors" }.Uri;
        downloadActorBaseUri = new UriBuilder(serverUri) { Path = "actors" }.Uri;
        sendMessageBaseUri = new UriBuilder(serverUri) { Path = "actors" }.Uri;
        receiveMessageOrderingUri = new UriBuilder(serverUri) { Path = "ordering" }.Uri;

        this.messageSerialisationOptions = messageSerialisationOptions;
        this.actorSerialisationOptions = actorSerialisationOptions;

        this.logger = logger ?? NullLogger.Instance;
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref running, true))
        {
            throw new InvalidOperationException($"{nameof(DefaultClientTransport)} is already running.");
        }

        try
        {
            runningCanceller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var runningCancellationToken = runningCanceller.Token;
            
            // TODO: Automatically dispose of response and stream
            var request = new HttpRequestMessage(HttpMethod.Get, receiveMessageOrderingUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, runningCancellationToken);
            response.EnsureSuccessStatusCode();
            
            var sseParser = SseParser.Create
            (
                await response.Content.ReadAsStreamAsync(runningCancellationToken),
                (_, data) => JsonSerializer.Deserialize<MessageOrdering>(data, messageSerialisationOptions)
            );
            
            logger.LogDebug("SSE connection to server established.");

            await foreach (var ordering in sseParser.EnumerateAsync(runningCancellationToken))
            {
                logger.LogDebug("Received ordering {ordering}", ordering.Data);
                messageOrderings.Post(ordering.Data);
            }
            
            logger.LogDebug("SSE connection to server closed.");

            runningCancellationToken.ThrowIfCancellationRequested();
            runningCanceller.Cancel();
        }
        finally
        {
            Interlocked.Exchange(ref running, false);
        }
    }

    public async Task<IReadOnlySet<TypelessActorId>> ListActors(CancellationToken cancellationToken = default)
    {
        // TODO: Handle non-ok status codes with custom exception
        using var response = await httpClient.GetAsync(listActorsUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = (await JsonSerializer.DeserializeAsync<HashSet<TypelessActorId>>
        (
            stream,
            actorSerialisationOptions,
            cancellationToken
        ))!;

        return result;
    }

    public async Task<IActor> DownloadActor(TypelessActorId actorId, CancellationToken cancellationToken = default)
    {
        var downloadActorUriBuilder = new UriBuilder(downloadActorBaseUri);
        downloadActorUriBuilder.Path += '/' + actorId.ContainerId.ToString() + '/' + actorId.LocalId;

        // TODO: Handle 404s and other non-ok status codes with custom exception
        using var response = await httpClient.GetAsync(downloadActorUriBuilder.Uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return (await JsonSerializer.DeserializeAsync<IActor>(stream, actorSerialisationOptions, cancellationToken))!;
    }

    public async Task SendMessage(TypelessActorId actorId, IMessage message, CancellationToken cancellationToken = default)
    {
        var sendMessageUriBuilder = new UriBuilder(sendMessageBaseUri);
        sendMessageUriBuilder.Path += '/' + actorId.ContainerId.ToString() + '/' + actorId.LocalId + "/message";

        var content = JsonContent.Create(message, null, messageSerialisationOptions);

        try
        {
            using var response = await httpClient.PostAsync(sendMessageUriBuilder.Uri, content, cancellationToken);
            logger.LogDebug("Sent message {message} to actor {actorId}", message, actorId);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Message request failed with {failure}", await response.Content.ReadAsStringAsync(cancellationToken));
                throw new FailedToSendMessageException();
            }
        }
        catch (FailedToSendMessageException)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Message request failed with exception");
            throw new FailedToSendMessageException(null, e);
        }
    }

    public async Task<MessageOrdering> ReceiveMessageOrdering(CancellationToken cancellationToken = default)
    {
        return await messageOrderings.ReceiveAsync(cancellationToken);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}