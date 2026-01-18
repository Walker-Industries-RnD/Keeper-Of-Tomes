using KeeperOfTomes;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string directoryToScan = @"EnterNameHere";
        string snapshotPath = Path.Combine(Directory.GetCurrentDirectory(), "MakeSureThisNameMatchesTheDirectoryNameInDirectoryToScan");

        Console.WriteLine($"Scanning '{directoryToScan}'...");

        // First, save the snapshot
        await Functions.SaveDirectorySnapshot(directoryToScan, Directory.GetCurrentDirectory());
        Console.WriteLine($"Snapshot saved to '{snapshotPath}'");

        // Load snapshot
        if (File.Exists(snapshotPath))
        {
            var snapshotBytes = await File.ReadAllBytesAsync(snapshotPath);
            var snapshot = await BinaryConverter.NCByteArrayToObjectAsync<Functions.Snapshot>(snapshotBytes);

            Console.WriteLine($"Loaded snapshot of directory: {snapshot.DirectoryPath}");
            Console.WriteLine($"Total files: {snapshot.Data.Count}");
            Console.WriteLine("Sample files:");

            foreach (var file in snapshot.Data.Count > 5 ? snapshot.Data.GetRange(0, 5) : snapshot.Data)
            {
                Console.WriteLine($" - {file.Path} ({file.Size} bytes, last modified {file.LastWriteUtc})");
            }
        }
        else
        {
            Console.WriteLine("Snapshot file not found!");
        }

        Console.WriteLine("Test completed.");
    }
}
