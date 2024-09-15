# SoundScapes

SoundScapes is a simple Telegram bot that converts Spotify tracks into actual `.mp3` files and sends them to the user.
Video demo:


https://github.com/user-attachments/assets/f774829a-bcbe-4496-a232-7caaac8588d8


## How It Works
- The bot uses the [SpotifyExplode API](https://github.com/J0nathan550/SoundScapes-TelegramBot/tree/master/SpotifyExplode-API) made by [jerry08](https://github.com/jerry08/SpotifyExplode) to grab a Spotify track.
- The bot then converts the track into an `.mp3` file and sends it to the user through Telegram.

## Repository Structure
- **SpotifyExplode API:** The main API used to fetch Spotify tracks. You can find the implementation [here](https://github.com/J0nathan550/SoundScapes-TelegramBot/tree/master/SpotifyExplode-API).
- **Bot Implementation:** The core logic of the Telegram bot itself is located [here](https://github.com/J0nathan550/SoundScapes-TelegramBot/tree/master/SoundScapes).

## Getting Started

### Running in Visual Studio
1. Open the project in Visual Studio.
2. Go to the project's Debug properties.
3. Specify the parameters in the following format: `-k API_KEY`.
4. Run the project in Debug mode.

### Running on a Machine
To release and run the bot on your machine (Windows, Linux, or MacOS):

1. Open a terminal or command prompt and navigate to the folder where you have output the bot using the `cd` command.
2. Run the program with the following command (replace `API_KEY` with your actual API key):

   **Linux Example:**
   ```bash
   ./SoundScapes -k API_KEY
   ```
   
If you encounter file permission issues on Linux, use the following command to make the file executable:
```bash
chmod +x ./SoundScapes
```
