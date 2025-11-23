# Frequency Sound Test

A simple C# console application to test frequency sound playback on macOS.

## Requirements

- macOS
- .NET 8.0 SDK
- `sox` installed: `brew install sox`

## Usage

### Test all frequencies (default)
```bash
cd test-frequency
dotnet run
```

This will play 6 test frequencies: 200 Hz, 440 Hz, 800 Hz, 1000 Hz, 2000 Hz, 5000 Hz (3 seconds each)

### Test a specific frequency
```bash
dotnet run 440
```

### Test a specific frequency with custom duration
```bash
dotnet run 1000 10
```

This will play 1000 Hz for 10 seconds.

### Test white noise
```bash
dotnet run white 5
```

This will play white noise for 5 seconds. You can also use `whitenoise` or `noise` instead of `white`.

## What it does

1. Checks if running on macOS
2. Checks if `sox` is installed
3. Generates a WAV file with the specified frequency (or white noise) using `sox`
4. Plays it using `afplay` (macOS built-in)
5. Cleans up the temp file

## White Noise

White noise is generated using `sox`'s `whitenoise` synth option. It creates a random signal with equal energy at all frequencies, which is useful for:
- Focus and concentration
- Masking background noise
- Testing audio systems

## Example Output

```
Frequency Sound Test - macOS
============================

Testing 6 frequencies (3s each)...

Testing 200 Hz for 3 seconds...
  ✅ Generated: /var/folders/.../frequency_test_200Hz_xxx.wav
  🔊 Playing...
  ✅ 200 Hz playback completed

Testing 440 Hz for 3 seconds...
  ✅ Generated: /var/folders/.../frequency_test_440Hz_xxx.wav
  🔊 Playing...
  ✅ 440 Hz playback completed

Testing white noise for 5 seconds...
  ✅ Generated: /var/folders/.../whitenoise_test_xxx.wav
  🔊 Playing white noise...
  ✅ White noise playback completed
```

