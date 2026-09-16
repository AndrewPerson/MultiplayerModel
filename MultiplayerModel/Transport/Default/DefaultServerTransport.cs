using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MultiplayerModel.Actor;
using MultiplayerModel.Extension;
using MultiplayerModel.Transport.Server;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.Default;

public class DefaultServerTransport : IServerTransport
{
    public IListActorsHandler? ListActorsHandler { get; set; }
    public IActorDownloadHandler? DownloadHandler { get; set; }

    private readonly JsonSerializerOptions? messageSerialisationOptions;
    private readonly JsonSerializerOptions? actorSerialisationOptions;

    private readonly HttpListener server;

    private uint nextClientId = 1;

    private readonly ConcurrentDictionary<uint, MessageOrderingSseConnection> messageOrderingConnections = new();

    private readonly BufferBlock<(TypelessActorId, IMessage)> incomingMessages = new();

    private readonly ILogger logger;

    private CancellationTokenSource? runningCanceller;
    private bool running;

    public DefaultServerTransport(
        string[] serverUrls,
        JsonSerializerOptions? actorSerialisationOptions = null,
        JsonSerializerOptions? messageSerialisationOptions = null,
        ILogger? logger = null
    )
    {
        this.messageSerialisationOptions = messageSerialisationOptions;
        this.actorSerialisationOptions = actorSerialisationOptions;
        
        server = new();
        foreach (var url in serverUrls)
        {
            server.Prefixes.Add(url);
        }

        this.logger = logger ?? NullLogger.Instance;
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref running, true))
        {
            throw new InvalidOperationException($"{nameof(DefaultServerTransport)} is already running.");
        }

        try
        {
            runningCanceller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var runningCancellationToken = runningCanceller.Token;

            server.Start();

            while (!runningCancellationToken.IsCancellationRequested)
            {
                var context = await server.GetContextAsync();
                using (logger.BeginScope(context))
                {
                    try
                    {
                        switch (context.Request)
                        {
                            case { Url.AbsolutePath: "/ordering", HttpMethod: "GET" }:
                                ConnectOrderingSse(context);
                                break;
                            case { Url.AbsolutePath: "/id/new", HttpMethod: "POST" }:
                                NewId(context);
                                break;
                            case { Url.AbsolutePath: "/actors", HttpMethod: "GET" }:
                                await Actors(context, runningCancellationToken);
                                break;
                            case
                            {
                                Url.CleanSegments: ["actors", var containerIdString, var localIdString],
                                HttpMethod: "GET"
                            }:
                                await Actor(context, containerIdString, localIdString, runningCancellationToken);
                                break;
                            case
                            {
                                Url.CleanSegments: ["actors", var containerIdString, var localIdString, "message"],
                                HttpMethod: "POST"
                            }:
                                await Message(context, containerIdString, localIdString, runningCancellationToken);
                                break;
                            default:
                                context.Response.StatusCode = 404;
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception occured when handling request {request}", context.Request);
                        context.Response.StatusCode = 500;
                    }

                    logger.LogDebug("{response}", context.Response);

                    if (context.Response.ContentType != "text/event-stream")
                    {
                        await context.Response.OutputStream.FlushAsync(runningCancellationToken);
                        context.Response.Close();
                    }
                    else
                    {
                        logger.LogDebug("Scope for request logging has ended, but the request lives on due to it being used for SSE");
                    }
                }
            }
        }
        finally
        {
            server.Stop();
            Interlocked.Exchange(ref running, false);
        }
    }

    private void ConnectOrderingSse(HttpListenerContext context)
    {
        if (!ValidId(context, out var id))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }
        
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache,no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["ContentEncoding"] = "identity";
        context.Response.SendChunked = true;
        context.Response.KeepAlive = true;

        context.Response.Headers["Access-Control-Allow-Origin"] = "*";

        var messageOrderings = new BufferBlock<MessageOrdering>();
        var messageOrderingConnection = new MessageOrderingSseConnection(
            messageOrderings,
            SseFormatter.WriteAsync
            (
                messageOrderings
                    .ReceiveAllAsync(runningCanceller?.Token ?? CancellationToken.None)
                    .Select(o => new SseItem<MessageOrdering>(o)),
                context.Response.OutputStream,
                WriteMessageOrderingSse,
                runningCanceller?.Token ?? CancellationToken.None
            )
        );
        
        if (messageOrderingConnections.TryAdd(id, messageOrderingConnection))
        {
            messageOrderingConnection.SseWritingTask.ContinueWith(t =>
            {
                messageOrderingConnections.TryRemove(new(id, messageOrderingConnection));

                if (t.IsFaulted)
                    logger.LogError(t.Exception, "Client {id} disconnected due to error", id);
                else
                    logger.LogInformation("Client {id} disconnected", id);
            });
        }
        else
        {
            logger.LogDebug("Failed to create message ordering SSE connection for Client {id} due to one already existing", id);
            context.Response.Abort();
        }
    }

    private void WriteMessageOrderingSse(SseItem<MessageOrdering> item, IBufferWriter<byte> writer)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(item.Data, messageSerialisationOptions);
        writer.Write(json);
    }

    private void NewId(HttpListenerContext context)
    {
        var id = Interlocked.Increment(ref nextClientId);

        Span<byte> idBytes = stackalloc byte[4];
        // We want the id as it was before it was incremented, so we use this (hacky?) trick
        BitConverter.TryWriteBytes(idBytes, id - 1);
        
        logger.LogDebug("Generated ID {id}", id - 1);

        context.Response.OutputStream.Write(idBytes);
    }

    private async Task Actors(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (!ValidId(context, out _))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        if (ListActorsHandler is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("List Actors Handler missing"));
            logger.LogDebug("List Actors Handler missing");
            return;
        }

        await JsonSerializer.SerializeAsync
        (
            context.Response.OutputStream,
            ListActorsHandler.ListActors(),
            actorSerialisationOptions,
            cancellationToken
        );
    }

    private async Task Actor(HttpListenerContext context, string containerIdString, string localIdString,
        CancellationToken cancellationToken)
    {
        if (!ValidId(context, out _))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        if (DownloadHandler is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Download Handler missing"));
            logger.LogDebug("Download Handler missing");
            return;
        }

        if (!uint.TryParse(containerIdString, out var containerId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Invalid Container ID"));
            logger.LogDebug("Invalid message request because {containerId} is not a uint", containerIdString);
            return;
        }

        if (!ulong.TryParse(localIdString, out var localId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Invalid Local ID"));
            logger.LogDebug("Invalid message request because {localId} is not a ulong", localIdString);
            return;
        }

        var actor = DownloadHandler.GetActorForDownload(new(containerId, localId));

        if (actor is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        await JsonSerializer.SerializeAsync
        (
            context.Response.OutputStream,
            actor,
            actorSerialisationOptions,
            cancellationToken
        );
    }

    private async Task Message(HttpListenerContext context, string containerIdString, string localIdString,
        CancellationToken cancellationToken)
    {
        if (!ValidId(context, out var id))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Invalid Client ID"));
            return;
        }

        if (!uint.TryParse(containerIdString, out var containerId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Invalid Container ID"));
            logger.LogDebug("Invalid message request because {containerId} is not a uint", containerIdString);
            return;
        }

        if (!ulong.TryParse(localIdString, out var localId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Invalid Local ID"));
            logger.LogDebug("Invalid message request because {localId} is not a ulong", localIdString);
            return;
        }

        var actorId = new TypelessActorId(containerId, localId);

        var message = await JsonSerializer.DeserializeAsync<IMessage>
        (
            context.Request.InputStream,
            messageSerialisationOptions,
            cancellationToken
        );

        if (message is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            logger.LogDebug("Invalid message request because message could not be deserialised.");

            if (context.Request.InputStream.CanSeek)
            {
                context.Request.InputStream.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(context.Request.InputStream);
                
                logger.LogDebug("{message}", await reader.ReadToEndAsync(cancellationToken));
            }
            else
            {
                logger.LogDebug("Cannot log message due to request input stream not being rewindable");
            }

            return;
        }

        if (message.Id.ContainerId != id)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.OutputStream.Write(Encoding.Default.GetBytes("Invalid Container ID"));
            logger.LogDebug("Invalid message request because Container ID {containerId} does not match Client ID {clientId}", containerIdString, id);
            return;
        }

        incomingMessages.Post((actorId, message));
    }

    private bool ValidId(HttpListenerContext context, out uint id)
    {
        // We don't use 0 because that's the server id and if we used that and if anything then used
        // the returned id without checking the result of this method, that would be bad.
        id = uint.MaxValue;

        var idStrings = context.Request.Headers.Get("X-Client-Id")?.Split(',');
        if (idStrings is null || idStrings.Length != 1)
        {
            if (idStrings is null)
            {
                logger.LogDebug("Invalid ID because no X-Client-Id header was found");
            }
            else
            {
                logger.LogDebug("Invalid ID because the X-Client-Id appeared {count} times instead of just once", idStrings.Length);
            }
            
            return false;
        }

        if (!uint.TryParse(idStrings[0], out id))
        {
            id = uint.MaxValue;
            logger.LogDebug("Invalid ID because {id} is not a uint", idStrings[0]);
            return false;
        }

        if (id == 0)
        {
            id = uint.MaxValue;
            logger.LogDebug("Invalid ID because 0 is the server's own ID");
            return false;
        }

        if (id < nextClientId)
        {
            return true;
        }

        id = uint.MaxValue;
        logger.LogDebug("Invalid ID because {id} has not been generated yet", id);
        return false;
    }

    public async Task<(TypelessActorId, IMessage)> ReceiveMessage(CancellationToken cancellationToken = default)
    {
        return await incomingMessages.ReceiveAsync(cancellationToken);
    }

    public Task SendMessageOrdering
    (
        IReadOnlyList<AddressedMessage> messages,
        IReadOnlyDictionary<TypelessActorId, int?> actorHashes,
        long version,
        CancellationToken cancellationToken = default
    )
    {
        var ordering = new MessageOrdering(messages, actorHashes, version);
        foreach (var connection in messageOrderingConnections.Values)
        {
            connection.MessageOrderings.Post(ordering);
        }
        
        logger.LogDebug("Sent message ordering {ordering}", ordering);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        runningCanceller?.Dispose();
        ((IDisposable)server).Dispose();

        // TODO: I don't think this is thread safe. Does it matter?
        foreach (var connection in messageOrderingConnections.Values)
        {
            connection.SseWritingTask.Dispose();
        }
    }

    private record MessageOrderingSseConnection(BufferBlock<MessageOrdering> MessageOrderings, Task SseWritingTask);
}