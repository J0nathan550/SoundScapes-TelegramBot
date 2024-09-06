using SpotifyExplode.Tracks;

namespace SoundScapes.Data;

internal class DownloadedTrack(UserData currentUser, Track track, string downloadPath, string progress)
{
    public Track track = track;
    public string path = Path.Combine(downloadPath, $"{track.Artists[0].Name} - {track.Title} - {currentUser.GetUniqueFileID()}.mp3");
    public string progress = progress;
    public bool done = false;
}