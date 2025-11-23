#!/bin/bash

# Test script to play hardcoded frequencies on macOS
# Usage: ./test-frequency.sh [frequency] [duration]

FREQUENCY=${1:-440}  # Default: 440 Hz (A4 note)
DURATION=${2:-5}     # Default: 5 seconds

echo "Testing frequency playback on macOS"
echo "Frequency: ${FREQUENCY} Hz"
echo "Duration: ${DURATION} seconds"
echo ""

# Check if sox is installed
if ! command -v sox &> /dev/null; then
    echo "❌ Error: sox is not installed"
    echo "Install it with: brew install sox"
    exit 1
fi

# Check if we're on macOS
if [[ "$OSTYPE" != "darwin"* ]]; then
    echo "❌ Error: This script only works on macOS"
    exit 1
fi

# Create temp file
TEMP_FILE=$(mktemp /tmp/frequency_test_XXXXXX.wav)

echo "Generating tone at ${FREQUENCY} Hz..."
sox -n -r 44100 -b 16 -c 1 "$TEMP_FILE" synth ${DURATION} sine ${FREQUENCY}

if [ $? -eq 0 ]; then
    echo "✅ Audio file generated: $TEMP_FILE"
    echo "Playing audio..."
    afplay "$TEMP_FILE"
    
    if [ $? -eq 0 ]; then
        echo "✅ Playback completed"
    else
        echo "❌ Error during playback"
    fi
else
    echo "❌ Error generating audio file"
fi

# Clean up
rm -f "$TEMP_FILE"
echo "Cleaned up temp file"

