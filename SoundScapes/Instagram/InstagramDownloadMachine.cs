namespace SoundScapes.Instagram;

public class InstagramDownloadMachine
{
    private string fileExtension = string.Empty, fileKey = string.Empty;

    private const string ImgExtension = ".jpg";
    private const string VideoExtension = ".mp4";

    private const string VideoKey = "video_url\": \"";
    private const string ImgKey = "display_url\": \"";

    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// Returns the file's stream from the specified Instagram URL.
    /// </summary>
    /// <param name="url">Instagram URL to an image or video.</param>
    /// <returns>File's Stream.</returns>
    public async Task<Stream> DownloadAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string responseFromServer = await response.Content.ReadAsStringAsync();
        string fileUrl = GetFileUrl(responseFromServer);

        return await GetFileStreamAsync(fileUrl);
    }

    /// <summary>
    /// Downloads the video or image from Instagram to the specified path.
    /// </summary>
    /// <param name="url">Instagram URL to an image or video.</param>
    /// <param name="path">Path to download the video or image.</param>
    public async Task DownloadToPathAsync(string url, string path)
    {
        await using Stream stream = await DownloadAsync(url);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        await File.WriteAllBytesAsync(path, memoryStream.ToArray());
    }

    private async Task<Stream> GetFileStreamAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    private string GetFileUrl(string html)
    {
        if (html.Contains(VideoKey))
        {
            fileExtension = VideoExtension;
            fileKey = VideoKey;
        }
        else
        {
            fileExtension = ImgExtension;
            fileKey = ImgKey;
        }

        int auxIndex = html.IndexOf(fileKey, StringComparison.Ordinal);
        if (auxIndex == -1) throw new InvalidOperationException("Key not found in HTML content");

        string partial = html[auxIndex..];
        int endOfUrl = partial.IndexOf(fileExtension, StringComparison.Ordinal) + fileExtension.Length;

        string fileUrl = partial[..endOfUrl][fileKey.Length..];
        return fileUrl;
    }
}