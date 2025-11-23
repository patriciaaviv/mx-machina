# Thought Deletion Test

A standalone test program to verify thought deletion functionality and persistence.

## Prerequisites

- .NET 8.0 SDK
- The main plugin project must be built first

## Running the Test

From the `TestThoughtDeletion` directory:

```bash
dotnet run
```

Or from the project root:

```bash
cd TestThoughtDeletion
dotnet run
```

## What It Tests

1. **Creating Thoughts**: Creates 5 test thoughts and verifies they're stored
2. **Verifying Storage**: Checks that thoughts are properly stored with IDs and timestamps
3. **Single Deletion**: Tests deleting a single thought and verifies it's removed
4. **Multiple Deletion**: Tests deleting multiple thoughts at once
5. **Persistence**: Verifies that deletions persist across sessions (by creating a new service instance)
6. **Categorized Thoughts**: Tests that categorized thoughts work correctly after deletions

## Expected Output

The test will:
- Create test thoughts
- Verify they're stored correctly
- Delete thoughts one by one and in batches
- Verify deletions persist when reloading the service
- Show categorized thoughts after deletions

## Notes

- This test uses the actual storage system (thoughts.json file)
- Test thoughts will be created and deleted during the test
- The test verifies that deleted thoughts are permanently removed and won't appear in new sessions

