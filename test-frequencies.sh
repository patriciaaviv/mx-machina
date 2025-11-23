#!/bin/bash

# Test script to play multiple hardcoded frequencies
# Usage: ./test-frequencies.sh

echo "Testing multiple frequencies on macOS"
echo "======================================"
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

# Array of test frequencies
FREQUENCIES=(200 440 800 1000 2000 5000)
DURATION=3  # 3 seconds per frequency

for freq in "${FREQUENCIES[@]}"; do
    echo "Playing ${freq} Hz for ${DURATION} seconds..."
    
    TEMP_FILE=$(mktemp /tmp/frequency_test_XXXXXX.wav)
    
    sox -n -r 44100 -b 16 -c 1 "$TEMP_FILE" synth ${DURATION} sine ${freq}
    
    if [ $? -eq 0 ]; then
        afplay "$TEMP_FILE"
        rm -f "$TEMP_FILE"
        echo "✅ ${freq} Hz completed"
    else
        echo "❌ Error generating ${freq} Hz"
        rm -f "$TEMP_FILE"
    fi
    
    echo ""
    sleep 0.5  # Small pause between frequencies
done

echo "All frequencies tested!"

