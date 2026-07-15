namespace MultiplayerModel.Extension;

public static class UriExtension
{
    extension(Uri uri)
    {
        public string[] CleanSegments => uri.Segments.Skip(1).Select(s => s.EndsWith('/') ? s[..^1] : s).ToArray();
    }
}