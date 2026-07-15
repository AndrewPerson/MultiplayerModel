using System.Net.WebSockets;

namespace MultiplayerModel.Transport.Default;

public static class WebSocketExtensions
{
    public static async Task<(byte[], bool)> ReceiveMessageAsync
    (
        this WebSocket socket,
        int bufferSize = 128,
        CancellationToken cancellationToken = default
    )
    {        
        var totalBuffer = new List<byte>(bufferSize);
        var buffer = new byte[bufferSize];

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            
            // ReSharper disable once RedundantExplicitParamsArrayCreation
            totalBuffer.AddRange(new ReadOnlySpan<byte>(buffer, 0, result.Count));

            if (result.EndOfMessage)
            {
                return ([.. totalBuffer], result.CloseStatus != null);
            }
        }
    }
}