# Noble Sampler for Stream Deck

Windows-first Stream Deck sampler inspired by the GoXLR sampler.

## Included features

- Four global banks
- Up to 32 slots in each bank
- Continuous Windows system-output capture using WASAPI loopback
- Configurable rolling buffer from 0 to 15 seconds
- Quick tap to play a saved clip
- Hold for 400 ms, by default, to begin recording
- Release to save the buffered audio plus everything captured while held
- Per-button name, slot, buffer length, hold delay, and playback volume
- Next bank, previous bank, or direct Bank 1-4 buttons
- Stop Playback action
- Clips stored as WAV files under `%LOCALAPPDATA%\NobleSampler`

## Requirements

- Windows 10 or newer
- Stream Deck 7.1 or newer
- .NET 8 SDK for the first build
- Node.js is only required when editing/rebuilding the plugin source

## Installation

1. Install the .NET 8 SDK from Microsoft.
2. Close Stream Deck completely from the system tray.
3. Right-click `install.ps1` and select **Run with PowerShell**.
4. Reopen Stream Deck.
5. Add **Sampler Slot**, **Sampler Bank**, and **Stop Playback** actions from the Noble Sampler category.

If PowerShell blocks the script, run this command once in PowerShell:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then run:

```powershell
.\install.ps1
```

## Recommended 15-key layout

- Keys 1-12: Sampler Slots 1-12
- Key 13: Previous Bank
- Key 14: Next Bank
- Key 15: Stop Playback

You can instead create four dedicated bank buttons and assign each one directly to Bank 1, 2, 3, or 4.

## Controls

- **Tap:** Play the clip from the current bank and configured slot.
- **Hold:** After the configured hold delay, recording starts.
- **Release:** Save the preceding rolling-buffer audio and the held recording to that slot.

Recording replaces the clip currently stored in that bank and slot.

## Audio routing note

The first version plays samples to the Windows default output device. To send samples into Discord or in-game voice, route that output through a virtual audio device such as VB-CABLE, VoiceMeeter, SteelSeries Sonar, or Wave Link.

## Development

Rebuild the plugin:

```powershell
cd plugin
npm install
npm run build
```

Build the audio service:

```powershell
.\audio-service\publish.ps1
```

The local audio API listens only on `127.0.0.1:17891`.

## Current MVP limitations

- Captures the Windows default playback endpoint only.
- Plays to the Windows default output endpoint only.
- No waveform trimming/editor yet.
- No microphone/system-audio mixing yet.
- No automatic clip-name extraction.
