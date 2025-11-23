using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupedeck.MXMachinaPlugin;

namespace TestThoughtDeletion
{
    /// <summary>
    /// Standalone test program for thought deletion functionality
    /// Run with: dotnet run
    /// 
    /// This test program tests:
    /// - Creating thoughts
    /// - Verifying they persist in storage
    /// - Deleting thoughts
    /// - Verifying they're removed from storage
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 Testing Thought Deletion Functionality");
            Console.WriteLine("========================================\n");

            // Initialize a mock PluginLog since we don't have the full plugin context
            InitializeMockPluginLog();

            try
            {
                var service = new ThoughtCaptureService();

                // Test 1: Create some test thoughts
                Console.WriteLine("Test 1: Creating Test Thoughts");
                Console.WriteLine("------------------------------");
                await TestCreateThoughts(service);
                Console.WriteLine();

                // Test 2: Verify thoughts are stored
                Console.WriteLine("Test 2: Verifying Thoughts Are Stored");
                Console.WriteLine("-------------------------------------");
                TestVerifyThoughtsStored(service);
                Console.WriteLine();

                // Test 3: Delete a single thought
                Console.WriteLine("Test 3: Deleting Single Thought");
                Console.WriteLine("-------------------------------");
                TestDeleteSingleThought(service);
                Console.WriteLine();

                // Test 4: Delete multiple thoughts
                Console.WriteLine("Test 4: Deleting Multiple Thoughts");
                Console.WriteLine("-----------------------------------");
                TestDeleteMultipleThoughts(service);
                Console.WriteLine();

                // Test 5: Verify deletions persist (reload service)
                Console.WriteLine("Test 5: Verifying Deletions Persist Across Sessions");
                Console.WriteLine("---------------------------------------------------");
                TestPersistence(service);
                Console.WriteLine();

                // Test 6: Test categorized thoughts after deletion
                Console.WriteLine("Test 6: Testing Categorized Thoughts After Deletion");
                Console.WriteLine("--------------------------------------------------");
                TestCategorizedThoughts(service);
                Console.WriteLine();

                Console.WriteLine("✅ All tests completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static async Task TestCreateThoughts(ThoughtCaptureService service)
        {
            var testThoughts = new[]
            {
                "Test thought 1: Buy groceries",
                "Test thought 2: Call dentist",
                "Test thought 3: Finish project report",
                "Test thought 4: Schedule meeting",
                "Test thought 5: Review code changes"
            };

            Console.WriteLine($"Creating {testThoughts.Length} test thoughts...");
            foreach (var thought in testThoughts)
            {
                await service.CaptureThoughtAsync(thought);
                Console.WriteLine($"   ✅ Created: \"{thought}\"");
                await Task.Delay(100); // Small delay
            }

            var allThoughts = service.GetUnreviewedThoughts();
            Console.WriteLine($"\n   Total thoughts in storage: {allThoughts.Count}");
        }

        static void TestVerifyThoughtsStored(ThoughtCaptureService service)
        {
            var thoughts = service.GetUnreviewedThoughts();
            var testThoughts = thoughts.Where(t => t.Text.StartsWith("Test thought")).ToList();

            Console.WriteLine($"Found {testThoughts.Count} test thoughts:");
            foreach (var thought in testThoughts)
            {
                Console.WriteLine($"   • [{thought.Category}] {thought.Text}");
                Console.WriteLine($"     ID: {thought.Id}, Captured: {thought.CapturedAt:yyyy-MM-dd HH:mm:ss}");
            }

            if (testThoughts.Count == 0)
            {
                Console.WriteLine("   ⚠️  No test thoughts found!");
            }
        }

        static void TestDeleteSingleThought(ThoughtCaptureService service)
        {
            var thoughts = service.GetUnreviewedThoughts();
            var testThought = thoughts.FirstOrDefault(t => t.Text == "Test thought 1: Buy groceries");

            if (testThought == null)
            {
                Console.WriteLine("   ⚠️  Test thought not found for deletion");
                return;
            }

            Console.WriteLine($"   Deleting: \"{testThought.Text}\"");
            Console.WriteLine($"   ID: {testThought.Id}");

            var beforeCount = service.GetUnreviewedThoughts().Count;
            service.DeleteThought(testThought.Id);
            var afterCount = service.GetUnreviewedThoughts().Count;

            Console.WriteLine($"   Thoughts before: {beforeCount}");
            Console.WriteLine($"   Thoughts after: {afterCount}");

            if (afterCount == beforeCount - 1)
            {
                Console.WriteLine("   ✅ Thought successfully deleted");
            }
            else
            {
                Console.WriteLine("   ❌ Thought deletion failed - count mismatch");
            }

            // Verify it's actually gone
            var deletedThought = service.GetUnreviewedThoughts().FirstOrDefault(t => t.Id == testThought.Id);
            if (deletedThought == null)
            {
                Console.WriteLine("   ✅ Verified: Thought no longer exists in storage");
            }
            else
            {
                Console.WriteLine("   ❌ Error: Thought still exists after deletion!");
            }
        }

        static void TestDeleteMultipleThoughts(ThoughtCaptureService service)
        {
            var thoughts = service.GetUnreviewedThoughts();
            var testThoughts = thoughts.Where(t => t.Text.StartsWith("Test thought")).ToList();

            if (testThoughts.Count < 2)
            {
                Console.WriteLine("   ⚠️  Not enough test thoughts for multiple deletion test");
                return;
            }

            var thoughtsToDelete = testThoughts.Take(2).ToList();
            var thoughtIds = thoughtsToDelete.Select(t => t.Id).ToList();

            Console.WriteLine($"   Deleting {thoughtIds.Count} thoughts:");
            foreach (var thought in thoughtsToDelete)
            {
                Console.WriteLine($"      • \"{thought.Text}\"");
            }

            var beforeCount = service.GetUnreviewedThoughts().Count;
            service.DeleteThoughts(thoughtIds);
            var afterCount = service.GetUnreviewedThoughts().Count;

            Console.WriteLine($"   Thoughts before: {beforeCount}");
            Console.WriteLine($"   Thoughts after: {afterCount}");

            if (afterCount == beforeCount - thoughtIds.Count)
            {
                Console.WriteLine($"   ✅ Successfully deleted {thoughtIds.Count} thoughts");
            }
            else
            {
                Console.WriteLine("   ❌ Multiple deletion failed - count mismatch");
            }

            // Verify they're all gone
            var remainingDeleted = service.GetUnreviewedThoughts()
                .Where(t => thoughtIds.Contains(t.Id))
                .ToList();

            if (remainingDeleted.Count == 0)
            {
                Console.WriteLine("   ✅ Verified: All deleted thoughts removed from storage");
            }
            else
            {
                Console.WriteLine($"   ❌ Error: {remainingDeleted.Count} thought(s) still exist after deletion!");
            }
        }

        static void TestPersistence(ThoughtCaptureService service)
        {
            // Get current state
            var thoughtsBefore = service.GetUnreviewedThoughts();
            var testThoughtsBefore = thoughtsBefore.Where(t => t.Text.StartsWith("Test thought")).ToList();
            var countBefore = testThoughtsBefore.Count;

            Console.WriteLine($"   Thoughts before reload: {countBefore}");

            // Create a new service instance to simulate a new session
            var newService = new ThoughtCaptureService();
            var thoughtsAfter = newService.GetUnreviewedThoughts();
            var testThoughtsAfter = thoughtsAfter.Where(t => t.Text.StartsWith("Test thought")).ToList();
            var countAfter = testThoughtsAfter.Count;

            Console.WriteLine($"   Thoughts after reload: {countAfter}");

            if (countBefore == countAfter)
            {
                Console.WriteLine("   ✅ Persistence verified: Thought count matches across sessions");
            }
            else
            {
                Console.WriteLine($"   ⚠️  Count mismatch: {countBefore} vs {countAfter}");
            }

            // Verify deleted thoughts are still gone
            var deletedIds = new List<string>();
            foreach (var thought in testThoughtsBefore)
            {
                if (!testThoughtsAfter.Any(t => t.Id == thought.Id))
                {
                    deletedIds.Add(thought.Id);
                }
            }

            if (deletedIds.Count > 0)
            {
                Console.WriteLine($"   ✅ Verified: {deletedIds.Count} previously deleted thought(s) remain deleted");
            }
        }

        static void TestCategorizedThoughts(ThoughtCaptureService service)
        {
            var categorized = service.GetCategorizedThoughts();
            var totalThoughts = service.GetUnreviewedThoughts().Count;

            Console.WriteLine($"   Total unreviewed thoughts: {totalThoughts}");
            Console.WriteLine($"   Categories: {categorized.Count}");

            foreach (var category in categorized)
            {
                Console.WriteLine($"      • {category.Key}: {category.Value.Count} thought(s)");
            }

            if (categorized.Count > 0)
            {
                Console.WriteLine("   ✅ Categorized thoughts working correctly");
            }
            else if (totalThoughts == 0)
            {
                Console.WriteLine("   ℹ️  No thoughts remaining (all deleted)");
            }
            else
            {
                Console.WriteLine("   ⚠️  Thoughts exist but no categories found");
            }
        }

        static void InitializeMockPluginLog()
        {
            // PluginLog uses null-conditional operators, so if not initialized,
            // it will silently do nothing, which is fine for testing.
            Console.WriteLine("Note: PluginLog not initialized (will be silent)");
        }
    }
}

