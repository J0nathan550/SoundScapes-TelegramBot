using Downloader;
using Telegram.Bot.Types;

namespace SoundScapes.Data;

internal class UserData(User userId, ChatId chatId) : BaseUserData
{
    public LinkType linkType = LinkType.None;
    public User User { get; private set; } = userId;
    public ChatId ChatId { get; private set; } = chatId;
    public DownloadService DownloadService { get; set; } = new();
    public uint UniqueFileID { get; set; } = 0;
    public object UniqueFileIDObject = new();

    public uint GetUniqueFileID()
    {
        lock (UniqueFileIDObject)
        {
            return ++UniqueFileID;
        }
    }
}