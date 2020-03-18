using System;
using System.IO;
using System.Linq;
using DpkRepacker.Models;
using Komponent.IO;
using Komponent.IO.Streams;

namespace DpkRepacker
{
    // Developer for FF1 port on 3DS: Square Enix
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintHelp();
                return;
            }

            if (args[0] != "-h" && args[0] != "-c" && args[0] != "-x")
            {
                PrintHelp();
                return;
            }

            if (args[0] == "-h")
            {
                PrintHelp();
                return;
            }

            if (args[0] == "-x" && args.Length < 2)
            {
                PrintHelp();
                return;
            }

            if (args[0] == "-c" && args.Length < 3)
            {
                PrintHelp();
                return;
            }

            if (args[0] == "-c")
                RepackDpk(args[1], args[2]);
            else
                DepackDpk(args[1]);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Following modes are usable:");
            Console.WriteLine(" -h\t Prints this help text");
            Console.WriteLine(" -x [dpk path]\tExtracts a given dpk file");
            Console.WriteLine(" -c [folder with files] [new dpk name]\tCreates a new dpk file from a folder of files");
        }

        private static void RepackDpk(string inputFolder, string dpkFile)
        {
            if (!Directory.Exists(inputFolder))
            {
                Console.WriteLine($"Folder {inputFolder} does not exist.");
                return;
            }

            var wp16 = Kompression.Implementations.Compressions.Wp16.Build();

            var dpkStream = File.Create(dpkFile);
            using (var bw = new BinaryWriterX(dpkStream))
            {
                var files = Directory.GetFiles(inputFolder);

                // Write compressed file data
                var filesOffset = 0x80 + files.Length * 0x80;
                var fileEntries = new FileEntry[files.Length];
                var fileEntryPosition = 0;
                var fileNr = 1;
                foreach (var file in files)
                {
                    var nameWithoutPath = Path.GetFileName(file);
                    var fileName = nameWithoutPath.Substring(0, Math.Min(0x16, nameWithoutPath.Length));
                    var fileNameSum = fileName.Sum(x => (byte)x);

                    var fileStream = File.OpenRead(file);
                    var ms = new MemoryStream();

                    Console.Write($"File {fileNr++:000}/{fileEntries.Length:000} - ");
                    Console.WriteLine(nameWithoutPath);

                    wp16.Compress(fileStream, ms);

                    ms.Position = 0;

                    var fileEntry = new FileEntry
                    {
                        fileName = fileName,
                        nameSum = (short)fileNameSum,
                        fileOffset = filesOffset,
                        uncompressedSize = (int)fileStream.Length,
                        compressedSize = (int)ms.Length
                    };
                    fileEntries[fileEntryPosition++] = fileEntry;

                    bw.BaseStream.Position = filesOffset;
                    ms.CopyTo(bw.BaseStream);
                    bw.WriteAlignment(0x80);
                    filesOffset = (int)bw.BaseStream.Position;
                }

                // Write file entries
                bw.BaseStream.Position = 0x80;
                foreach (var fileEntry in fileEntries.OrderBy(x => x.nameSum))
                {
                    if (fileEntry.fileName.Length < 0x16)
                        fileEntry.fileName = fileEntry.fileName.PadRight(0x16, '\0');

                    bw.WriteType(fileEntry);
                    bw.WriteAlignment(0x80);
                }

                // Header
                bw.BaseStream.Position = 0;
                bw.Write(files.Length);
                bw.Write((int)bw.BaseStream.Length);
            }

            dpkStream.Close();
        }

        private static void DepackDpk(string dpkFile)
        {
            if (!File.Exists(dpkFile))
            {
                Console.WriteLine($"File {dpkFile} does not exist.");
                return;
            }

            var folder = Path.Combine(Path.GetDirectoryName(dpkFile), Path.GetFileNameWithoutExtension(dpkFile));
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var dpkStream = File.OpenRead(dpkFile);
            using (var br = new BinaryReaderX(dpkStream))
            {
                var fileCount = br.ReadInt32();
                var archiveSize = br.ReadInt32();

                if (dpkStream.Length != archiveSize)
                    throw new InvalidOperationException("Archive size doesn't match.");

                var baseOffset = 0x80;
                var fileEntries = new FileEntry[fileCount];
                for (var i = 0; i < fileCount; i++)
                {
                    br.BaseStream.Position = baseOffset + i * baseOffset;
                    fileEntries[i] = br.ReadType<FileEntry>();
                    fileEntries[i].fileName = fileEntries[i].fileName.TrimEnd('\0');
                }

                var wp16 = Kompression.Implementations.Compressions.Wp16.Build();

                var fileNr = 1;
                foreach (var fileEntry in fileEntries)
                {
                    Console.Write($"File {fileNr++:000}/{fileEntries.Length:000} - ");
                    Console.WriteLine(fileEntry.fileName);
                    if (fileEntry.compressedSize != fileEntry.uncompressedSize)
                    {
                        var destFile = File.Create(Path.Combine(folder, fileEntry.fileName));
                        var compressedStream = new SubStream(dpkStream, fileEntry.fileOffset, fileEntry.compressedSize);
                        wp16.Decompress(compressedStream, destFile);
                        destFile.Close();
                    }
                    else
                    {
                        var destFile = File.Create(Path.Combine(folder, fileEntry.fileName));
                        new SubStream(dpkStream, fileEntry.fileOffset, fileEntry.uncompressedSize).CopyTo(destFile);
                        destFile.Close();
                    }
                }
            }
        }
    }
}
