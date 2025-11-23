# Thought Categorization Test

A standalone test program to verify OpenAI API integration for thought categorization.

## Prerequisites

- .NET 8.0 SDK
- `secrets.json` file in the project root (`/Users/giursan/Documents/Coding/hackatum/mx-machina/secrets.json`) with your OpenAI API key

## Running the Test

From the `TestThoughtCategorization` directory:

```bash
dotnet run
```

Or from the project root:

```bash
cd TestThoughtCategorization
dotnet run
```

## What It Tests

1. **API Key Loading**: Verifies that the OpenAI API key can be loaded from `secrets.json`
2. **Thought Categorization**: Tests categorizing multiple sample thoughts using the OpenAI API

## Expected Output

The test will:
- Load your OpenAI API key from `secrets.json`
- Test categorization of 7 sample thoughts
- Display the category assigned to each thought (Work, Personal, Urgent, Shopping, Health, Other)

## Notes

- The test makes real API calls to OpenAI, so it will use your API credits
- Each thought categorization takes about 0.5-1 second
- If the API key is missing or invalid, the test will fail early with a clear error message

