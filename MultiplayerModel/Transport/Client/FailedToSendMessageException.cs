namespace MultiplayerModel.Transport.Client;

public class FailedToSendMessageException(string? message, Exception? inner) : Exception(message, inner)
{
    public FailedToSendMessageException() : this(null, null)
    {
        
    }
}