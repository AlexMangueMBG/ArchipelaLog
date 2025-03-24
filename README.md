# ArchipelaLog
Discord Bot that logs obtained items on Archipelago sessions.

## Setup
1. Download the [latest version](https://github.com/AlexMangueMBG/ArchipelaLog/releases/latest) for your OS.
2. Put the file on an empty folder
3. [Follow this instructions to get your bot token.](https://docs.discordbotstudio.org/setting-up-dbs/finding-your-bot-token) Now you have two options to setup your bot token.  
  3.1 Create an environment variable named `BotToken` with your bot token as value.  
  3.2 Create a text file next to the executable named `BotToken.txt` and paste your bot token inside the file.
4. Start your executable.

## Usage
- You need to invite your bot into the server you want to have the logs. Keep in mind that this bot only works on server channels.
- On the channel you want to use as log, have someone with administrator permissions on the server type `setup <archipelagoIP> <archipelagoPort>`.
- Now, have all players type `slot <slotName> <notify>`.
  - slotName refers to the player's name inside Archipelago.
  - notify is a boolean variable. Set to true if you want to be pinged by the bot when you get an important item.
- If the archipelago server is restarted. Have an admin type `/reconnect` on the channel.

## License
This project is licensed under the [Creative Commons Attribution-NonCommercial 4.0 International License](https://creativecommons.org/licenses/by-nc/4.0/).

## Support
If you wish to support this or any other of my projects, consider [buying me a coffee](https://ko-fi.com/alexmangue).
