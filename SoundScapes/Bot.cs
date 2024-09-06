using Downloader;
using SoundScapes.Data;
using SpotifyExplode;
using SpotifyExplode.Playlists;
using SpotifyExplode.Tracks;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YoutubeExplode;

namespace SoundScapes;

internal class Bot(string apiKey)
{
    private static readonly Queue<UserData> usersQueue = [];
    private readonly string API_KEY = apiKey;

    private readonly SpotifyClient spotifyClient = new();
    private readonly string downloadPath = Path.Combine(AppContext.BaseDirectory, "Downloaded");
    private readonly object uniqueFileIDLock = new();
    private readonly object downloadSpotifyAlbumLock = new();
    private readonly object downloadSpotifyPlaylistLock = new();

    private uint uniqueFileID = 0;

    private TelegramBotClient? telegramBotClient;

    public async Task InitializeBot()
    {
        string error_art = " ____       ____        ______      ____        __  __     \r\n/\\  _`\\    /\\  _`\\     /\\  _  \\    /\\  _`\\     /\\ \\/\\ \\    \r\n\\ \\ \\/\\_\\  \\ \\ \\L\\ \\   \\ \\ \\L\\ \\   \\ \\,\\L\\_\\   \\ \\ \\_\\ \\   \r\n \\ \\ \\/_/_  \\ \\ ,  /    \\ \\  __ \\   \\/_\\__ \\    \\ \\  _  \\  \r\n  \\ \\ \\L\\ \\  \\ \\ \\\\ \\    \\ \\ \\/\\ \\    /\\ \\L\\ \\   \\ \\ \\ \\ \\ \r\n   \\ \\____/   \\ \\_\\ \\_\\   \\ \\_\\ \\_\\   \\ `\\____\\   \\ \\_\\ \\_\\\r\n    \\/___/     \\/_/\\/ /    \\/_/\\/_/    \\/_____/    \\/_/\\/_/\r\n                                                           \r\n                                                           ";
        if (API_KEY == string.Empty)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error_art);
            Console.WriteLine("[ERROR]: API KEY IS NOT SET AS THE PARAMETER OF THE APP");
            Console.WriteLine("[ERROR]: RUN THE APPLICATION ONCE AGAIN WITH API KEY");
            Console.WriteLine("[ERROR]: EXAMPLE: ./soundscapes api_key");
            Console.ResetColor();
            return;
        }
        try
        {
            telegramBotClient = new TelegramBotClient(API_KEY);
            User userBot = await telegramBotClient.GetMeAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[INFO]: {userBot.Username} IS CONNECTED AND WORKING!");
            Console.ResetColor();
            if (Directory.Exists(downloadPath))
            {
                foreach (string file in Directory.EnumerateFiles(downloadPath))
                {
                    try
                    {
                        System.IO.File.Delete(file);
                    }
                    catch { }
                }
            }
            await telegramBotClient.ReceiveAsync(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync), default);
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error_art);
            Console.WriteLine("[ERROR]: WRONG API KEY IS SET AS THE PARAMETER OF THE APP");
            Console.WriteLine("[ERROR]: RUN THE APPLICATION ONCE AGAIN WITH CORRECT API KEY");
            Console.WriteLine("[ERROR]: EXAMPLE: ./soundscapes api_key");
            Console.ResetColor();
            return;
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
    {
        if (update.Type != UpdateType.Message || update.Message == null || update.Message.From == null) return;
        UserData userData = new(update.Message.From, update.Message.Chat.Id);
        usersQueue.Enqueue(userData);
        try
        {
            await BotOnMessageReceived(update.Message);
        }
        catch (Exception exception)
        {
            await HandleErrorAsync(telegramBotClient!, exception, token);
        }
    }

    public static async Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        string errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING | EXCEPTION]: {errorMessage}");
        Console.ResetColor();
        usersQueue.TryPeek(out UserData? userData);
        usersQueue.Dequeue();
        if (userData == null) return;
        await client.SendTextMessageAsync(userData.ChatId, "Трапилась помилка, спробуйте ще раз...", cancellationToken: token);
    }

    private async Task BotOnMessageReceived(Message? message)
    {
        usersQueue.TryPeek(out UserData? userData);

        if (userData == null)
        {
            usersQueue.Dequeue();
            return;
        }
        if (message == null || message.Text == null)
        {
            usersQueue.Dequeue();
            return;
        }
        if (message.Text.StartsWith("/start"))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[INFO]: USER {userData.User.Username} REQUESTED `START`!");
            Console.ResetColor();
            await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Привіт! Щоб отримати музикальний файл ви повинні надіслати посилання на трек або на плейлист якщо хочете завантажити декілька пісень одразу." +
                "\n\nПриклад посилань:\n" +
                "- Spotify Track: https://open.spotify.com/track/...\n" +
                "- Spotify Playlist: https://open.spotify.com/playlist/...\n" +
                "- Spotify Album: https://open.spotify.com/album/...\n" +
                "- YouTube Video: https://youtu.be/... або https://www.youtube.com/watch?v=...\n" +
                "- TikTok Video: https://www.tiktok.com/...\n" +
                "- Instagram Video: https://www.instagram.com/...\n" +
                "- Twitter (X) Video: https://x.com/...\n" +
                "Також планується (напевно) підтримка інших форматів медія. Щоб отримати список того що конвертується напишіть: /help_formats");
            usersQueue.Dequeue();
            return;
        }
        if (message.Text.StartsWith("/help_formats"))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[INFO]: USER {userData.User.Username} REQUESTED `HELP_FORMATS`!");
            Console.ResetColor();
            await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Наразі підтримуємо посилання:\n" +
                "- Spotify Track: https://open.spotify.com/track/...\n" +
                "- Spotify Playlist: https://open.spotify.com/playlist/...\n" +
                "- Spotify Album: https://open.spotify.com/album/...\n" +
                "- YouTube Video: https://youtu.be/... або https://www.youtube.com/watch?v=...\n" +
                "- TikTok Video: https://www.tiktok.com/...\n" +
                "- Instagram Video: https://www.instagram.com/...\n" +
                "- Twitter (X) Video: https://x.com/...\n" +
                "Також планується (напевно) підтримка інших форматів медія. Пишіть якщо потрібно у @j0nathan550");
            usersQueue.Dequeue();
            return;
        }
        if (!message.Text.StartsWith("https://open.spotify.com/track/") && !message.Text.StartsWith("https://open.spotify.com/playlist/") && !message.Text.StartsWith("https://open.spotify.com/album/") && !message.Text.StartsWith("https://youtu.be/") && !message.Text.StartsWith("https://www.youtube.com/watch?v=") && !message.Text.StartsWith("https://www.tiktok.com/") && !message.Text.StartsWith("https://www.instagram.com/") && !message.Text.StartsWith("https://x.com/"))
        {
            usersQueue.Dequeue();
            return;
        }

        UserData currentUser = userData;

        if (message.Text.StartsWith("https://open.spotify.com/track/")) currentUser.linkType = BaseUserData.LinkType.SpotifyTrack;
        else if (message.Text.StartsWith("https://open.spotify.com/playlist/")) currentUser.linkType = BaseUserData.LinkType.SpotifyPlaylist;
        else if (message.Text.StartsWith("https://open.spotify.com/album/")) currentUser.linkType = BaseUserData.LinkType.SpotifyAlbum;
        else if (message.Text.StartsWith("https://youtu.be/") || message.Text.StartsWith("https://www.youtube.com/watch?v=")) currentUser.linkType = BaseUserData.LinkType.YouTubeVideo;
        else if (message.Text.StartsWith("https://www.tiktok.com/")) currentUser.linkType = BaseUserData.LinkType.TikTokVideo;
        else if (message.Text.StartsWith("https://www.instagram.com/")) currentUser.linkType = BaseUserData.LinkType.InstagramVideo;
        else if (message.Text.StartsWith("https://x.com/")) currentUser.linkType = BaseUserData.LinkType.TwitterVideo;
        else
        {
            usersQueue.Dequeue();
            return;
        }

        switch (currentUser.linkType)
        {
            case BaseUserData.LinkType.SpotifyTrack:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING TRACK FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                Thread workerThreadSpotifyTrack = new(async () =>
                {
                    await DownloadSpotifyTrack(currentUser, message.Text);
                });
                workerThreadSpotifyTrack.Start();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Посилання на трек отримано, чекаєте завантаження!");
                break;
            case BaseUserData.LinkType.SpotifyPlaylist:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING PLAYLIST FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                Thread workerThreadSpotifyPlaylist = new(async () =>
                {
                    await DownloadSpotifyPlaylist(currentUser, message.Text);
                });
                workerThreadSpotifyPlaylist.Start();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Посилання на плейлист отримано, чекаєте завантаження!");
                break;
            case BaseUserData.LinkType.SpotifyAlbum:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING ALBUM FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                Thread workerThreadSpotifyAlbum = new(async () =>
                {
                    await DownloadSpotifyAlbum(currentUser, message.Text);
                });
                workerThreadSpotifyAlbum.Start();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Посилання на альбом отримано, чекаєте завантаження!");
                break;
            case BaseUserData.LinkType.YouTubeVideo:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING YOUTUBE VIDEO FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Поки що не працює!");
                break;
            case BaseUserData.LinkType.TikTokVideo:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING TIKTOK VIDEO FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Поки що не працює!");
                break;
            case BaseUserData.LinkType.InstagramVideo:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING INSTAGRAM VIDEO FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Поки що не працює!");
                break;
            case BaseUserData.LinkType.TwitterVideo:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: STARTED INSTALLING TWITTER VIDEO FOR USER {currentUser.User.Username}!");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(userData.ChatId, "Поки що не працює!");
                break;
        }
        usersQueue.Dequeue();
    }

    private async Task DownloadSpotifyTrack(UserData currentUser, string link)
    {
        PrepareDownloadFolder();
        try
        {
            Track track = await spotifyClient.Tracks.GetAsync(link, default);
            string? resultUrl = await spotifyClient.Tracks.GetDownloadUrlAsync(link);
            if (string.IsNullOrEmpty(resultUrl))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING | EXCEPTION]: COULDN'T FIND A DOWNLOAD URL FOR: {link}");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Нажаль, за цим посиланням не можливо завантажити трек, або трапилась помилка у базі, спробуйте ще раз!");
                return;
            }
            lock (uniqueFileIDLock)
            {
                uniqueFileID++;
                currentUser.UniqueFileID = uniqueFileID;
            }
            string path = Path.Combine(downloadPath, $"{track.Artists[0].Name} - {track.Title} - {currentUser.UniqueFileID}.mp3");
            currentUser.DownloadService.DownloadFileCompleted += async (sender, e) => await Downloader_DownloadSpotifyTrackFileCompleted(currentUser, track);
            await currentUser.DownloadService.DownloadFileTaskAsync(new DownloadPackage() { Urls = [resultUrl], FileName = path }, default);
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING | EXCEPTION]: COULDN'T FIND A DOWNLOAD URL FOR: {link}");
            Console.ResetColor();
            await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Нажаль, за цим посиланням не можливо завантажити трек, або трапилась помилка у базі, спробуйте ще раз!");
        }
    }

    private async Task Downloader_DownloadSpotifyTrackFileCompleted(UserData currentUser, Track track)
    {
        // Construct the path to the downloaded MP3 file
        string path = Path.Combine(downloadPath, $"{track.Artists[0].Name} - {track.Title} - {currentUser.UniqueFileID}.mp3");

        try
        {
            // Ensure the file exists
            if (System.IO.File.Exists(path))
            {
                // Create InputFile object from the local file
                using (FileStream stream = new(path, FileMode.Open, FileAccess.Read))
                {
                    InputFile audioFile = InputFile.FromStream(stream);

                    // Send the audio file to the user
                    await telegramBotClient!.SendAudioAsync(
                        chatId: currentUser.ChatId,
                        audio: audioFile,
                        title: track.Title,
                        performer: track.Artists[0].Name
                    );
                }

                // Send a confirmation message after the file is sent
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: TRACK ({track.Artists[0].Name} - {track.Title} - {currentUser.UniqueFileID}.mp3) IS SUCCESSFULLY UPLOADED TO USER {currentUser.User.Username}");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, $"Завантаження треку за посиланням: {track.Url} завершено!");
            }
            else
            {
                // Handle the case where the file does not exist
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING | EXCEPTION]: TRACK ({track.Artists[0].Name} - {track.Title} - {currentUser.UniqueFileID}.mp3) DOESN'T EXIST FOR USER {currentUser.User.Username}");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Помилка: Файл не знайдено, спробуйте ще раз...");
            }
        }
        catch (Exception ex)
        {
            // Handle any errors during file sending
            await HandleErrorAsync(telegramBotClient!, ex, default);
            await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, $"Сталася помилка: {ex.Message}, напишіть розробнику, та спробуйте ще раз.");
        }
        finally
        {
            currentUser.DownloadService.Dispose();

            // Optional: Delete the file after it's been sent (or keep it for future use)
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch { } // (perhaps file may play on actual bot which is not possible on prod)
        }
    }

    private async Task DownloadSpotifyPlaylist(UserData currentUser, string link)
    {
        PrepareDownloadFolder();
        try
        {
            List<Track> collectionOfTracks = await spotifyClient.Playlists.GetAllTracksAsync(link, default);
            List<DownloadedTrack> tracks = [];
            int total = collectionOfTracks.Count;
            int num = 1;
            foreach (Track track in collectionOfTracks)
            {
                if (track == null) continue;
                DownloadedTrack downloadedTrack = new(currentUser, track, downloadPath, $"{num++} / {total}");
                tracks.Add(downloadedTrack);
            }
            currentUser.DownloadService.DownloadFileCompleted += async (sender, e) => await Downloader_DownloadSpotifyTracksFromPlaylistFileCompleted(currentUser, tracks);
            foreach (DownloadedTrack track in tracks)
            {
                string? resultUrl = await spotifyClient.Tracks.GetDownloadUrlAsync(track.track.Id);
                if (string.IsNullOrEmpty(resultUrl))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[WARNING | EXCEPTION]: COULDN'T FIND A DOWNLOAD URL IN PLAYLIST FOR: {link}");
                    Console.ResetColor();
                    await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Нажаль, за цим посиланням не можливо завантажити треки з плейлиста, або трапилась помилка у базі, спробуйте ще раз!");
                    return;
                }
                await currentUser.DownloadService.DownloadFileTaskAsync(new DownloadPackage() { Urls = [resultUrl], FileName = track.path }, default);
            }
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING | EXCEPTION]: COULDN'T FIND A DOWNLOAD URL IN PLAYLIST FOR: {link}");
            Console.ResetColor();
            await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Нажаль, за цим посиланням не можливо завантажити треки з плейлиста, або трапилась помилка у базі, спробуйте ще раз!");
        }
    }

    private async Task Downloader_DownloadSpotifyTracksFromPlaylistFileCompleted(UserData currentUser, List<DownloadedTrack> tracks)
    {
        try
        {
            string path = string.Empty;
            string title = string.Empty;
            string author = string.Empty;
            string message = string.Empty;
            lock (downloadSpotifyPlaylistLock)
            {
                foreach (DownloadedTrack track in tracks)
                {
                    if (track.done)
                    {
                        continue;
                    }
                    if (System.IO.File.Exists(track.path))
                    {
                        string progress = track.progress;
                        author = track.track.Artists[0].Name;
                        title = track.track.Title;
                        path = track.path;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[INFO]: TRACK FROM PLAYLIST ({track.path}) IS SUCCESSFULLY UPLOADED TO USER {currentUser.User.Username}");
                        Console.ResetColor();
                        message = $"Завантажен трек з плейлисту: {track.track.Url}\nЗалишилось: {progress}";
                        track.done = true;
                        break;
                    }
                    else
                    {
                        // Handle the case where the file does not exist
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING | EXCEPTION]: PLAYLIST TRACK ({track.path}) DOESN'T EXIST FOR USER {currentUser.User.Username}");
                        Console.ResetColor();
                        message = $"Помилка при завантаженні файлу з плейлисту: {track.track.Url}";
                    }
                }
            }
            if (!string.IsNullOrEmpty(path))
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
                InputFile audioFile = InputFile.FromStream(stream);

                // Send the audio file to the user
                await telegramBotClient!.SendAudioAsync(
                    chatId: currentUser.ChatId,
                    audio: audioFile,
                    title: title,
                    performer: author
                );
            }
            if (!string.IsNullOrEmpty(message))
            {
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, message);
            }
        }
        catch (Exception ex)
        {
            // Handle any errors during file sending
            await HandleErrorAsync(telegramBotClient!, ex, default);
            await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, $"Сталася помилка: {ex.Message}, напишіть розробнику, та спробуйте ще раз.");
        }
        finally
        {
            // Cleanup - remove event handler and delete the local file
            bool completed = true;
            foreach (DownloadedTrack downloadTrack in tracks)
            {
                if (!downloadTrack.done)
                {
                    completed = false;
                    break;
                }
            }
            if (completed)
            {
                currentUser.DownloadService.Dispose();
                foreach (DownloadedTrack file in tracks)
                {
                    try
                    {
                        if (System.IO.File.Exists(file.path))
                        {
                            System.IO.File.Delete(file.path);
                        }
                    }
                    catch { } // (perhaps file may play on actual bot which is not possible on prod)   
                }
                tracks.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: SUCCESSFULLY PROCESSED ALL PLAYLIST TRACKS TO {currentUser.User.Username}");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, $"Завантаження треків з плейлисту завершено!");
            }
        }
    }

    private async Task DownloadSpotifyAlbum(UserData currentUser, string link)
    {
        PrepareDownloadFolder();
        try
        {
            List<Track> collectionOfTracks = await spotifyClient.Albums.GetAllTracksAsync(link, default);
            List<DownloadedTrack> tracks = [];
            int total = collectionOfTracks.Count;
            int num = 1;
            foreach (Track track in collectionOfTracks)
            {
                if (track == null) continue;
                DownloadedTrack downloadedTrack = new(currentUser, track, downloadPath, $"{num++} / {total}");
                tracks.Add(downloadedTrack);
            }
            currentUser.DownloadService.DownloadFileCompleted += async (sender, e) => await Downloader_DownloadSpotifyTracksFromAlbumFileCompleted(currentUser, tracks);
            foreach (DownloadedTrack track in tracks)
            {
                string? resultUrl = await spotifyClient.Tracks.GetDownloadUrlAsync(track.track.Id);
                if (string.IsNullOrEmpty(resultUrl))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[WARNING | EXCEPTION]: COULDN'T FIND A DOWNLOAD URL IN ALBUM FOR: {link}");
                    Console.ResetColor();
                    await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Нажаль, за цим посиланням не можливо завантажити треки з альбому, або трапилась помилка у базі, спробуйте ще раз!");
                    return;
                }
                await currentUser.DownloadService.DownloadFileTaskAsync(new DownloadPackage() { Urls = [resultUrl], FileName = track.path }, default);
            }
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING | EXCEPTION]: COULDN'T FIND A DOWNLOAD URL IN ALBUM FOR: {link}");
            Console.ResetColor();
            await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, "Нажаль, за цим посиланням не можливо завантажити треки з альбому, або трапилась помилка у базі, спробуйте ще раз!");
        }
    }

    private async Task Downloader_DownloadSpotifyTracksFromAlbumFileCompleted(UserData currentUser, List<DownloadedTrack> tracks)
    {
        try
        {
            string path = string.Empty;
            string title = string.Empty;
            string author = string.Empty;
            string message = string.Empty;
            lock (downloadSpotifyAlbumLock)
            {
                foreach (DownloadedTrack track in tracks)
                {
                    if (track.done)
                    {
                        continue;
                    }
                    if (System.IO.File.Exists(track.path))
                    {
                        string progress = track.progress;
                        author = track.track.Artists[0].Name;
                        title = track.track.Title;
                        path = track.path;  

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[INFO]: TRACK FROM ALBUM ({track.path}) IS SUCCESSFULLY UPLOADED TO USER {currentUser.User.Username}");
                        Console.ResetColor();
                        message = $"Завантажен трек з альбому: {track.track.Url}\nЗалишилось: {progress}";
                        break;
                    }
                    else
                    {
                        // Handle the case where the file does not exist
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING | EXCEPTION]: ALBUM TRACK ({track.path}) DOESN'T EXIST FOR USER {currentUser.User.Username}");
                        Console.ResetColor();
                        message = $"Помилка при завантаженні файлу з альбому: {track.track.Url}";
                        track.done = true;
                    }
                }
            }
            if (!string.IsNullOrEmpty(path))
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
                InputFile audioFile = InputFile.FromStream(stream);

                // Send the audio file to the user
                await telegramBotClient!.SendAudioAsync(
                    chatId: currentUser.ChatId,
                    audio: audioFile,
                    title: title,
                    performer: author
                );
            }
            if (!string.IsNullOrEmpty(message))
            {
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, message);
            }
        }
        catch (Exception ex)
        {
            // Handle any errors during file sending
            await HandleErrorAsync(telegramBotClient!, ex, default);
            await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, $"Сталася помилка: {ex.Message}, напишіть розробнику, та спробуйте ще раз.");
        }
        finally
        {
            // Cleanup - remove event handler and delete the local file
            bool completed = true;
            foreach (DownloadedTrack downloadTrack in tracks)
            {
                if (!downloadTrack.done)
                {
                    completed = false;
                    break;
                }
            }
            if (completed)
            {
                currentUser.DownloadService.Dispose();
                foreach (DownloadedTrack file in tracks)
                {
                    try
                    {
                        if (System.IO.File.Exists(file.path))
                        {
                            System.IO.File.Delete(file.path);
                        }
                    }
                    catch { } // (perhaps file may play on actual bot which is not possible on prod)   
                }
                tracks.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO]: SUCCESSFULLY PROCESSED ALL ALBUMS TRACKS TO {currentUser.User.Username}");
                Console.ResetColor();
                await telegramBotClient!.SendTextMessageAsync(currentUser.ChatId, $"Завантаження треків з альбому завершено!");
            }
        }
    }

    private void PrepareDownloadFolder() => Directory.CreateDirectory(downloadPath);

}