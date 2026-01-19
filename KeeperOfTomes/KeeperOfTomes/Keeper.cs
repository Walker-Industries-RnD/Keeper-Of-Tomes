using Org.BouncyCastle.Pqc.Crypto.Lms;
using Pariah_Cybersecurity;
using Standart.Hash.xxHash;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace KeeperOfTomes
{

    public static class Keeper
    {

        public static async Task<Functions.SnapshotInfo> SnapshotDirectory(
            string directoryToScan,
            string snapshotRoot,
            string? snapshotId = null)
        {
            directoryToScan = Path.GetFullPath(directoryToScan);
            Directory.CreateDirectory(snapshotRoot);

            var name = snapshotId ?? Path.GetFileName(directoryToScan);
            var snapshotFile = Path.Combine(snapshotRoot, name + ".snapshot");

            if (!File.Exists(snapshotFile))
            {
                await Functions.SaveDirectorySnapshot(
                    directoryToScan,
                    snapshotRoot
                );

                return new Functions.SnapshotInfo();
            }

            return await Functions.UpdateDirectorySnapshot(snapshotFile);
        }
    
    
    }

    public class Functions
    {

        public class FileEntry
        {
            public string Path { get; set; }
            public long Size { get; set; }
            public DateTime LastWriteUtc { get; set; }
            public ulong Hash { get; set; }

            public FileEntry() { } // Required for serializers

            public FileEntry(string path, long size, DateTime lastWriteUtc, ulong hash)
            {
                Path = path;
                Size = size;
                LastWriteUtc = lastWriteUtc;
                Hash = hash;
            }
        }

        public class Snapshot
        {
            public Dictionary<string, DateTime> LastScan { get; set; } = new();
            public List<FileEntry> Data { get; set; } = new();
            public string DirectoryPath { get; set; } = string.Empty;

            public Snapshot() { } // Required for serializers

            public Snapshot(Dictionary<string, DateTime> lastScan, List<FileEntry> data, string directoryPath)
            {
                LastScan = lastScan;
                Data = data;
                DirectoryPath = directoryPath;
            }
        }



        public struct SnapshotInfo
        {
            public HashSet<string> AddedFiles;
            public HashSet<string> RemovedFiles;
            public HashSet<string> UpdatedFiles;
            public HashSet<(string, string)> UpdatedFileDetails;
        }


        //For directories which haven't been saved before
        public static async Task SaveDirectorySnapshot(string directoryPath, string pathToSaveTo)
        {
            var fileList = new List<FileEntry>();
            var lastScanData = new Dictionary<string, DateTime>();

            var endPath = Path.Combine(pathToSaveTo, Path.GetFileName(directoryPath));

            const int CHUNK_SIZE = 50;
            var fileEnumerator = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories);

            foreach (var chunk in fileEnumerator.Chunk(CHUNK_SIZE))
            {
                var fileEntries = await SaveDirectoryChunk(chunk);

                fileList.AddRange(fileEntries.Item1);

                foreach (var kvp in fileEntries.Item2)
                {
                    lastScanData[kvp.Key] = kvp.Value;
                }
            }

            var dataToWrite = new Snapshot(lastScanData, fileList, directoryPath);

            var byteData = await BinaryConverter.NCObjectToByteArrayAsync<Snapshot>(dataToWrite);

            var tempFile = endPath + ".snapshot.tmp";
            await File.WriteAllBytesAsync(tempFile, byteData);
            if (File.Exists(endPath + ".snapshot"))
            {
                File.Replace(tempFile, endPath + ".snapshot", null);
            }
            else
            {
                File.Move(tempFile, endPath + ".snapshot");
            }
        }
        static async Task<(List<FileEntry>, Dictionary<string, DateTime>)> SaveDirectoryChunk(IReadOnlyList<string> files)
        {
            uint seed = 0;
            var fileList = new List<FileEntry>();
            var lastScanData = new Dictionary<string, DateTime>();

            foreach (var filePathRaw in files)
            {
                var path = Path.GetFullPath(filePathRaw);

                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var fileInfo = new FileInfo(path);
                    ulong hash = await xxHash64.ComputeHashAsync(stream, bufferSize: 81920, seed: seed);
                    var entry = new FileEntry(path, fileInfo.Length, fileInfo.LastWriteTimeUtc, hash);
                    fileList.Add(entry);
                    lastScanData[path] = fileInfo.LastWriteTimeUtc;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipped '{path}'; {ex.GetType().Name}: {ex.Message}");
                }
            }

            return (fileList, lastScanData);
        }

        //For updating existing directories
        public static async Task<SnapshotInfo> UpdateDirectorySnapshot(string SnapshotFilePath)
        {
            var returnVal = new SnapshotInfo
            {
                AddedFiles = new HashSet<string>(),
                RemovedFiles = new HashSet<string>(),
                UpdatedFiles = new HashSet<string>(),
                UpdatedFileDetails = new HashSet<(string, string)>()
            };

            var snapShotBytes = await File.ReadAllBytesAsync(SnapshotFilePath);
            var snapShotData = await BinaryConverter.NCByteArrayToObjectAsync<Snapshot>(snapShotBytes);

            var directoryToScan = snapShotData.DirectoryPath;
            var chronoFilter = snapShotData.LastScan;

            var oldSnapshot = snapShotData;

            var oldHashLookup = oldSnapshot.Data
                .GroupBy(f => f.Hash)
                .ToDictionary(g => g.Key, g => g.ToList());

            var fileListBag = new ConcurrentBag<FileEntry>();
            var lastScanBag = new ConcurrentDictionary<string, DateTime>();
            var livePaths = new ConcurrentDictionary<string, byte>();

            const int CHUNK_SIZE = 50;
            var fileEnumerator = Directory.EnumerateFiles(directoryToScan, "*", SearchOption.AllDirectories);

            foreach (var chunk in fileEnumerator.Chunk(CHUNK_SIZE))
            {
                var tasks = chunk.Select(async pathRaw =>
                {
                    var path = Path.GetFullPath(pathRaw);
                    livePaths.TryAdd(path, 0);

                    bool isInOld = chronoFilter.TryGetValue(path, out var lastScanTime);

                    try
                    {
                        using var stream = new FileStream(
                            path,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite,
                            81920,
                            FileOptions.Asynchronous | FileOptions.SequentialScan
                        );

                        var fileInfo = new FileInfo(path);
                        ulong hash = await xxHash64.ComputeHashAsync(stream, bufferSize: 81920, seed: 0);

                        var entry = new FileEntry(path, fileInfo.Length, fileInfo.LastWriteTimeUtc, hash);
                        fileListBag.Add(entry);
                        lastScanBag[path] = fileInfo.LastWriteTimeUtc;

                        if (isInOld)
                        {
                            bool updated = fileInfo.LastWriteTimeUtc > lastScanTime;
                            if (updated)
                            {
                                if (oldHashLookup.TryGetValue(hash, out var oldEntries))
                                {
                                    //SUPER important time tolerance
                                    var matchedOld = oldEntries.FirstOrDefault(f =>
                      f.Size == fileInfo.Length &&
                      Math.Abs((f.LastWriteUtc - fileInfo.LastWriteTimeUtc).TotalSeconds) < 1);


                                    if (matchedOld != null && matchedOld.Path != path)
                                    {
                                        lock (returnVal.UpdatedFileDetails)
                                            returnVal.UpdatedFileDetails.Add((matchedOld.Path, path));
                                        lock (returnVal.UpdatedFiles)
                                            returnVal.UpdatedFiles.Add(path);
                                        Console.WriteLine($"Renamed: '{matchedOld.Path}' → '{path}'");
                                    }
                                    else
                                    {
                                        lock (returnVal.UpdatedFiles)
                                            returnVal.UpdatedFiles.Add(path);
                                    }
                                }
                                else
                                {
                                    lock (returnVal.UpdatedFiles)
                                        returnVal.UpdatedFiles.Add(path);
                                }
                            }
                        }
                        else
                        {
                            if (oldHashLookup.TryGetValue(hash, out var oldEntries))
                            {
                                var matchedOld = oldEntries.FirstOrDefault(f =>
                                    f.Size == fileInfo.Length &&
                                    f.LastWriteUtc == fileInfo.LastWriteTimeUtc);

                                if (matchedOld != null)
                                {
                                    lock (returnVal.UpdatedFileDetails)
                                        returnVal.UpdatedFileDetails.Add((matchedOld.Path, path));
                                    lock (returnVal.UpdatedFiles)
                                        returnVal.UpdatedFiles.Add(path);
                                    Console.WriteLine($"Renamed (new path detected): '{matchedOld.Path}' → '{path}'");
                                }
                                else
                                {
                                    lock (returnVal.AddedFiles)
                                        returnVal.AddedFiles.Add(path);
                                }
                            }
                            else
                            {
                                lock (returnVal.AddedFiles)
                                    returnVal.AddedFiles.Add(path);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Skipped '{path}'; {ex.GetType().Name}: {ex.Message}");
                    }
                });

                await Task.WhenAll(tasks);
            }

            foreach (var oldEntry in oldSnapshot.Data)
            {
                var normalizedOldPath = Path.GetFullPath(oldEntry.Path);
                if (!livePaths.ContainsKey(normalizedOldPath))
                {
                    lock (returnVal.RemovedFiles)
                        returnVal.RemovedFiles.Add(oldEntry.Path);
                }
            }

            var updatedSnapshot = new Snapshot(
                new Dictionary<string, DateTime>(lastScanBag),
                fileListBag.ToList(),
                directoryToScan
            );

            var tempFile = SnapshotFilePath + ".tmp";
            var byteData = await BinaryConverter.NCObjectToByteArrayAsync(updatedSnapshot);
            await File.WriteAllBytesAsync(tempFile, byteData);
            File.Replace(tempFile, SnapshotFilePath, null);

            return returnVal;
        }

        //For updating single files

        public static async Task<SnapshotInfo> UpdateSingleFile(string filePath, string snapshotFilePath)
        {
            filePath = Path.GetFullPath(filePath);

            var returnVal = new SnapshotInfo
            {
                AddedFiles = new HashSet<string>(),
                RemovedFiles = new HashSet<string>(),
                UpdatedFiles = new HashSet<string>(),
                UpdatedFileDetails = new HashSet<(string, string)>()
            };

            var snapshotBytes = await File.ReadAllBytesAsync(snapshotFilePath);
            var snapshot = await BinaryConverter.NCByteArrayToObjectAsync<Snapshot>(snapshotBytes);

            var oldHashLookup = snapshot.Data
                .GroupBy(f => f.Hash)
                .ToDictionary(g => g.Key, g => g.ToList());

            var chronoFilter = snapshot.LastScan;
            var livePaths = new HashSet<string>(chronoFilter.Keys.Select(p => Path.GetFullPath(p)));

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var fileInfo = new FileInfo(filePath);
                ulong hash = await xxHash64.ComputeHashAsync(stream, bufferSize: 81920, seed: 0);

                var entry = new FileEntry(filePath, fileInfo.Length, fileInfo.LastWriteTimeUtc, hash);

                bool existedBefore = chronoFilter.TryGetValue(filePath, out var lastScanTime);

                if (existedBefore)
                {
                    bool updated = fileInfo.LastWriteTimeUtc > lastScanTime;
                    if (updated)
                    {
                        returnVal.UpdatedFiles.Add(filePath);
                    }
                }
                else
                {
                    if (oldHashLookup.TryGetValue(hash, out var oldEntries))
                    {
                        var matchedOld = oldEntries.FirstOrDefault(f => f.Size == fileInfo.Length);
                        if (matchedOld != null && matchedOld.Path != filePath)
                        {
                            returnVal.UpdatedFileDetails.Add((matchedOld.Path, filePath));
                            returnVal.UpdatedFiles.Add(filePath);
                        }
                        else
                        {
                            returnVal.AddedFiles.Add(filePath);
                        }
                    }
                    else
                    {
                        returnVal.AddedFiles.Add(filePath);
                    }
                }

                var newData = snapshot.Data.Where(f => f.Path != filePath).ToList();
                newData.Add(entry);

                var newLastScan = new Dictionary<string, DateTime>(snapshot.LastScan);
                newLastScan[filePath] = fileInfo.LastWriteTimeUtc;

                var updatedSnapshot = new Snapshot(newLastScan, newData, snapshot.DirectoryPath);

                var tempFile = snapshotFilePath + ".tmp";
                var byteData = await BinaryConverter.NCObjectToByteArrayAsync(updatedSnapshot);
                await File.WriteAllBytesAsync(tempFile, byteData);
                File.Replace(tempFile, snapshotFilePath, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Skipped '{filePath}'; {ex.GetType().Name}: {ex.Message}");
            }

            return returnVal;
        }



    }








}
