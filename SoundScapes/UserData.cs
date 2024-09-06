using Downloader;
using Telegram.Bot.Types;

namespace SoundScapes;

internal class UserData(User userId, ChatId chatId) : BaseUserData
{
    public LinkType linkType = LinkType.None;
    public User User { get; private set; } = userId;
    public ChatId ChatId { get; private set; } = chatId;
    public DownloadService DownloadService { get; set; } = new();
}