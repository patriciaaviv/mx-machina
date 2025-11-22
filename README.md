# MX-Machina Pomodoro Plugin

A productivity-focused plugin for the Logitech MX 4 mouse that brings a full-featured Pomodoro timer with Google Calendar integration, statistics tracking, and focus mode.

## Features

- **Pomodoro Timer** - Classic 25/5/15 minute work/break cycles with adjustable durations
- **Google Calendar Integration** - Automatically logs completed focus sessions to your calendar
- **Statistics Dashboard** - Track your productivity with a beautiful web-based dashboard
- **Focus Mode** - Closes distracting apps (Discord, Slack, Steam, etc.) with one button press
- **Smart Notifications** - macOS notifications for timer events
- **Persistent Data** - OAuth tokens and statistics persist across restarts

## Installation

1. Build the plugin using Visual Studio or `dotnet build`
2. The plugin will be available in Logitech Options+

## Configuration

### Google Calendar Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable the **Google Calendar API**
4. Create OAuth 2.0 credentials:
   - Application type: **Web application**
   - Authorized redirect URI: `http://localhost:8080/callback`
5. Copy your Client ID and Client Secret

6. Create `secrets.json` in `~/Library/Application Support/MXMachinaPlugin/`:

```json
{
  "GoogleCalendar": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret"
  }
}
```

7. Add your email as a test user in OAuth consent screen settings

## Available Actions

| Action | Description |
|--------|-------------|
| **Pomodoro Timer** | Start/pause the timer. Shows remaining time on display. |
| **Timer Settings** | Adjust work/break durations by rotating. Press to reset to defaults. |
| **Calendar Auth** | Authenticate with Google Calendar. Shows connection status. |
| **Statistics** | Opens web dashboard with your productivity stats. |
| **Focus Mode** | Toggles focus mode - closes distracting apps. |

## Statistics Dashboard

Press the Statistics button to open a web dashboard showing:

- Today's completed pomodoros
- This week's total
- All-time total
- Total focus time
- Current streak (consecutive days)
- Best streak

## Focus Mode

When enabled, Focus Mode closes these apps:
- Discord
- Steam
- Slack
- Telegram
- WhatsApp
- Messages

## Data Storage

All data is stored in `~/Library/Application Support/MXMachinaPlugin/`:

| File | Purpose |
|------|---------|
| `secrets.json` | Google OAuth credentials |
| `tokens.json` | OAuth access/refresh tokens |
| `statistics.json` | Session history and stats |

## Timer Defaults

- Work session: 25 minutes
- Short break: 5 minutes
- Long break: 15 minutes (after 4 pomodoros)

## Development

### Prerequisites

- .NET 8.0 SDK
- Logitech Options+ with Actions SDK
- possession of a Logitech MX 4 mouse 😄

### Building

```bash
cd src
dotnet build
```

### Project Structure

```
src/
├── Actions/           # Logitech action commands
│   ├── PomodoroCommand.cs
│   ├── PomodoroAdjustment.cs
│   ├── GoogleCalendarAuthCommand.cs
│   ├── StatisticsCommand.cs
│   └── FocusModeCommand.cs
├── Services/          # Core services
│   ├── GoogleCalendarService.cs
│   ├── StatisticsService.cs
│   ├── FocusModeService.cs
│   ├── NotificationService.cs
│   └── PomodoroService.cs
└── Helpers/
    └── PomodoroTimer.cs
```

## License

MIT License

## Author

Built with the Logitech Actions SDK
