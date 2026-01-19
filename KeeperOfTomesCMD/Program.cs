using KeeperOfTomes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // 1️ Create a temporary test directory
        string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "TempTestDir");
        Directory.CreateDirectory(tempDir);

        // 2️ Create 5 temporary text files
        string[] files = Enumerable.Range(1, 5)
            .Select(i => Path.Combine(tempDir, $"file{i}.txt"))
            .ToArray();

        for (int i = 0; i < files.Length; i++)
        {
            await File.WriteAllTextAsync(files[i], $"Initial content for file {i + 1}");
        }
        Console.WriteLine("Created 5 temporary files.");

        // 3️ Take the initial snapshot using Keeper
        var snapshotRoot = Directory.GetCurrentDirectory();
        var snapshotInfo = await Keeper.SnapshotDirectory(tempDir, snapshotRoot);
        Console.WriteLine("Initial snapshot saved.");

        // 4️ Edit 3 files
        for (int i = 0; i < 3; i++)
        {
            await File.AppendAllTextAsync(files[i], "\nEdited content");
        }
        Console.WriteLine("Edited 3 files.");

        // 5️ Rename one file
        string renamedFile = Path.Combine(tempDir, "file1_renamed.txt");
        File.Move(files[0], renamedFile);
        Console.WriteLine($"Renamed '{files[0]}' → '{renamedFile}'");

        // 6️ Update snapshot again using Keeper
        snapshotInfo = await Keeper.SnapshotDirectory(tempDir, snapshotRoot);
        Console.WriteLine("\nSnapshot update results:");
        Console.WriteLine($"Added files: {string.Join(", ", snapshotInfo.AddedFiles)}");
        Console.WriteLine($"Removed files: {string.Join(", ", snapshotInfo.RemovedFiles)}");
        Console.WriteLine($"Updated files: {string.Join(", ", snapshotInfo.UpdatedFiles)}");

        if (snapshotInfo.UpdatedFileDetails.Count > 0)
        {
            Console.WriteLine("Renamed/moved files:");
            foreach (var (oldPath, newPath) in snapshotInfo.UpdatedFileDetails)
            {
                Console.WriteLine($" - {oldPath} → {newPath}");
            }
        }

        // 7️ Load snapshot to inspect
        string snapshotPath = Path.Combine(snapshotRoot, Path.GetFileName(tempDir) + ".snapshot");
        if (File.Exists(snapshotPath))
        {
            var snapshotBytes = await File.ReadAllBytesAsync(snapshotPath);
            var snapshot = await BinaryConverter.NCByteArrayToObjectAsync<Functions.Snapshot>(snapshotBytes);

            Console.WriteLine($"\nLoaded snapshot of directory: {snapshot.DirectoryPath}");
            Console.WriteLine($"Total files: {snapshot.Data.Count}");
            Console.WriteLine("Sample files:");
            foreach (var file in snapshot.Data.Count > 5 ? snapshot.Data.GetRange(0, 5) : snapshot.Data)
            {
                Console.WriteLine($" - {file.Path} ({file.Size} bytes, last modified {file.LastWriteUtc})");
            }
        }

        Console.WriteLine("\nTest completed.");
    }
}
