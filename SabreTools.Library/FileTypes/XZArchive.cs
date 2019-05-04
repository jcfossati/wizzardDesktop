﻿using System;
using System.Collections.Generic;
using System.Linq;

using SabreTools.Library.Data;
using SabreTools.Library.DatItems;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using Alphaleonis.Win32.Filesystem;

using BinaryWriter = System.IO.BinaryWriter;
using EndOfStreamException = System.IO.EndOfStreamException;
using FileStream = System.IO.FileStream;
using MemoryStream = System.IO.MemoryStream;
using SeekOrigin = System.IO.SeekOrigin;
using Stream = System.IO.Stream;
#endif
using ROMVault2.SupportedFiles.Zip;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;

namespace SabreTools.Library.FileTypes
{
    /// <summary>
    /// Represents a TorrentXZ archive for reading and writing
    /// </summary>
    /// TODO: Wait for XZ write to be enabled by SevenZipSharp library
    public class XZArchive : BaseArchive
    {
        #region Constructors

        /// <summary>
        /// Create a new TorrentGZipArchive with no base file
        /// </summary>
        public XZArchive()
            : base()
        {
            this.Type = FileType.XZArchive;
        }

        /// <summary>
        /// Create a new TorrentGZipArchive from the given file
        /// </summary>
        /// <param name="filename">Name of the file to use as an archive</param>
        /// <param name="read">True for opening file as read, false for opening file as write</param>
        /// <param name="getHashes">True if hashes for this file should be calculated, false otherwise (default)</param>
        public XZArchive(string filename, bool getHashes = false)
            : base(filename, getHashes)
        {
            this.Type = FileType.XZArchive;
        }

        #endregion

        #region Extraction

        /// <summary>
        /// Attempt to extract a file as an archive
        /// </summary>
        /// <param name="outDir">Output directory for archive extraction</param>
        /// <returns>True if the extraction was a success, false otherwise</returns>
        public override bool CopyAll(string outDir)
        {
            bool encounteredErrors = true;

            try
            {
                // Create the temp directory
                Directory.CreateDirectory(outDir);

                // Extract all files to the temp directory
                SharpCompress.Archives.SevenZip.SevenZipArchive sza = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(Utilities.TryOpenRead(this.Filename));
                foreach (SevenZipArchiveEntry entry in sza.Entries)
                {
                    entry.WriteToDirectory(outDir, new SharpCompress.Common.ExtractionOptions { PreserveFileTime = true, ExtractFullPath = true, Overwrite = true });
                }
                encounteredErrors = false;
                sza.Dispose();
            }
            catch (EndOfStreamException)
            {
                // Catch this but don't count it as an error because SharpCompress is unsafe
            }
            catch (InvalidOperationException)
            {
                encounteredErrors = true;
            }
            catch (Exception ex)
            {
                Globals.Logger.Error(ex.ToString());
                encounteredErrors = true;
            }

            return encounteredErrors;
        }

        /// <summary>
        /// Attempt to extract a file from an archive
        /// </summary>
        /// <param name="entryName">Name of the entry to be extracted</param>
        /// <param name="outDir">Output directory for archive extraction</param>
        /// <returns>Name of the extracted file, null on error</returns>
        public override string CopyToFile(string entryName, string outDir)
        {
            // Try to extract a stream using the given information
            (MemoryStream ms, string realEntry) = CopyToStream(entryName);

            // If the memory stream and the entry name are both non-null, we write to file
            if (ms != null && realEntry != null)
            {
                realEntry = Path.Combine(outDir, realEntry);

                // Create the output subfolder now
                Directory.CreateDirectory(Path.GetDirectoryName(realEntry));

                // Now open and write the file if possible
                FileStream fs = Utilities.TryCreate(realEntry);
                if (fs != null)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    byte[] zbuffer = new byte[_bufferSize];
                    int zlen;
                    while ((zlen = ms.Read(zbuffer, 0, _bufferSize)) > 0)
                    {
                        fs.Write(zbuffer, 0, zlen);
                        fs.Flush();
                    }

                    ms?.Dispose();
                    fs?.Dispose();
                }
                else
                {
                    ms?.Dispose();
                    fs?.Dispose();
                    realEntry = null;
                }
            }

            return realEntry;
        }

        /// <summary>
        /// Attempt to extract a stream from an archive
        /// </summary>
        /// <param name="entryName">Name of the entry to be extracted</param>
        /// <param name="realEntry">Output representing the entry name that was found</param>
        /// <returns>MemoryStream representing the entry, null on error</returns>
        public override (MemoryStream, string) CopyToStream(string entryName)
        {
            MemoryStream ms = new MemoryStream();
            string realEntry = null;

            try
            {
                SharpCompress.Archives.SevenZip.SevenZipArchive sza = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(this.Filename, new ReaderOptions { LeaveStreamOpen = false, });
                foreach (SevenZipArchiveEntry entry in sza.Entries)
                {
                    if (entry != null && !entry.IsDirectory && entry.Key.Contains(entryName))
                    {
                        // Write the file out
                        realEntry = entry.Key;
                        entry.WriteTo(ms);
                        break;
                    }
                }
                sza.Dispose();
            }
            catch (Exception ex)
            {
                Globals.Logger.Error(ex.ToString());
                ms = null;
                realEntry = null;
            }

            return (ms, realEntry);
        }

        #endregion

        #region Information

        /// <summary>
        /// Generate a list of DatItem objects from the header values in an archive
        /// </summary>
        /// <param name="omitFromScan">Hash representing the hashes that should be skipped</param>
        /// <param name="date">True if entry dates should be included, false otherwise (default)</param>
        /// <returns>List of DatItem objects representing the found data</returns>
        /// <remarks>TODO: All instances of Hash.DeepHashes should be made into 0x0 eventually</remarks>
        public override List<BaseFile> GetChildren(Hash omitFromScan = Hash.DeepHashes, bool date = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generate a list of empty folders in an archive
        /// </summary>
        /// <param name="input">Input file to get data from</param>
        /// <returns>List of empty folders in the archive</returns>
        public override List<string> GetEmptyFolders()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Check whether the input file is a standardized format
        /// </summary>
        public override bool IsTorrent()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Writing

        /// <summary>
        /// Write an input file to a torrent XZ file
        /// </summary>
        /// <param name="inputFile">Input filename to be moved</param>
        /// <param name="outDir">Output directory to build to</param>
        /// <param name="rom">DatItem representing the new information</param>
        /// <param name="date">True if the date from the DAT should be used if available, false otherwise (default)</param>
        /// <param name="romba">True if files should be output in Romba depot folders, false otherwise</param>
        /// <returns>True if the write was a success, false otherwise</returns>
        /// <remarks>This works for now, but it can be sped up by using Ionic.Zip or another zlib wrapper that allows for header values built-in. See edc's code.</remarks>
        public override bool Write(string inputFile, string outDir, Rom rom, bool date = false, bool romba = false)
        {
            // Get the file stream for the file and write out
            return Write(Utilities.TryOpenRead(inputFile), outDir, rom, date: date);
        }

        /// <summary>
        /// Write an input file to a torrent XZ archive
        /// </summary>
        /// <param name="inputStream">Input stream to be moved</param>
        /// <param name="outDir">Output directory to build to</param>
        /// <param name="rom">DatItem representing the new information</param>
        /// <param name="date">True if the date from the DAT should be used if available, false otherwise (default)</param>
        /// <param name="romba">True if files should be output in Romba depot folders, false otherwise</param>
        /// <returns>True if the archive was written properly, false otherwise</returns>
        public override bool Write(Stream inputStream, string outDir, Rom rom, bool date = false, bool romba = false)
        {
            bool success = false;
            string tempFile = Path.Combine(outDir, "tmp" + Guid.NewGuid().ToString());

            // If either input is null or empty, return
            if (inputStream == null || rom == null || rom.Name == null)
            {
                return success;
            }

            // If the stream is not readable, return
            if (!inputStream.CanRead)
            {
                return success;
            }

            // Seek to the beginning of the stream
            inputStream.Seek(0, SeekOrigin.Begin);

            // Get the output archive name from the first rebuild rom
            string archiveFileName = Path.Combine(outDir, Utilities.RemovePathUnsafeCharacters(rom.MachineName) + (rom.MachineName.EndsWith(".xz") ? "" : ".xz"));

            // Set internal variables
            SevenZipBase.SetLibraryPath("7za.dll");
            SevenZipExtractor oldZipFile = null;
            SevenZipCompressor zipFile;

            try
            {
                // If the full output path doesn't exist, create it
                if (!Directory.Exists(Path.GetDirectoryName(tempFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
                }

                // If the archive doesn't exist, create it and put the single file
                if (!File.Exists(archiveFileName))
                {
                    zipFile = new SevenZipCompressor()
                    {
                        ArchiveFormat = OutArchiveFormat.XZ,
                        CompressionLevel = CompressionLevel.Normal,
                    };

                    // Create the temp directory
                    string tempPath = Path.Combine(outDir, Guid.NewGuid().ToString());
                    if (!Directory.Exists(tempPath))
                    {
                        Directory.CreateDirectory(tempPath);
                    }

                    // Create a stream dictionary
                    Dictionary<string, Stream> dict = new Dictionary<string, Stream>();
                    dict.Add(rom.Name, inputStream);

                    // Now add the stream
                    zipFile.CompressStreamDictionary(dict, tempFile);
                }

                // Otherwise, sort the input files and write out in the correct order
                else
                {
                    // Open the old archive for reading
                    using (oldZipFile = new SevenZipExtractor(archiveFileName))
                    {
                        // Map all inputs to index
                        Dictionary<string, int> inputIndexMap = new Dictionary<string, int>();

                        // If the old one doesn't contain the new file, then add it
                        if (!oldZipFile.ArchiveFileNames.Contains(rom.Name.Replace('\\', '/')))
                        {
                            inputIndexMap.Add(rom.Name.Replace('\\', '/'), -1);
                        }

                        // Then add all of the old entries to it too
                        for (int i = 0; i < oldZipFile.FilesCount; i++)
                        {
                            inputIndexMap.Add(oldZipFile.ArchiveFileNames[i], i);
                        }

                        // If the number of entries is the same as the old archive, skip out
                        if (inputIndexMap.Keys.Count <= oldZipFile.FilesCount)
                        {
                            success = true;
                            return success;
                        }

                        // Otherwise, process the old zipfile
                        zipFile = new SevenZipCompressor()
                        {
                            ArchiveFormat = OutArchiveFormat.XZ,
                            CompressionLevel = CompressionLevel.Normal,
                        };

                        // Get the order for the entries with the new file
                        List<string> keys = inputIndexMap.Keys.ToList();
                        keys.Sort(ZipFile.TorrentZipStringCompare);

                        // Copy over all files to the new archive
                        foreach (string key in keys)
                        {
                            // Get the index mapped to the key
                            int index = inputIndexMap[key];

                            // If we have the input file, add it now
                            if (index < 0)
                            {
                                // Create a stream dictionary
                                Dictionary<string, Stream> dict = new Dictionary<string, Stream>();
                                dict.Add(rom.Name, inputStream);

                                // Now add the stream
                                zipFile.CompressStreamDictionary(dict, tempFile);
                            }

                            // Otherwise, copy the file from the old archive
                            else
                            {
                                Stream oldZipFileEntryStream = new MemoryStream();
                                oldZipFile.ExtractFile(index, oldZipFileEntryStream);
                                oldZipFileEntryStream.Seek(0, SeekOrigin.Begin);

                                // Create a stream dictionary
                                Dictionary<string, Stream> dict = new Dictionary<string, Stream>();
                                dict.Add(oldZipFile.ArchiveFileNames[index], oldZipFileEntryStream);

                                // Now add the stream
                                zipFile.CompressStreamDictionary(dict, tempFile);
                                oldZipFileEntryStream.Dispose();
                            }

                            // After the first file, make sure we're in append mode
                            zipFile.CompressionMode = CompressionMode.Append;
                        }
                    }
                }

                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                success = false;
            }
            finally
            {
                inputStream?.Dispose();
            }

            // If the old file exists, delete it and replace
            if (File.Exists(archiveFileName))
            {
                Utilities.TryDeleteFile(archiveFileName);
            }
            File.Move(tempFile, archiveFileName);

            // Now make the file T7Z
            // TODO: Add ACTUAL T7Z compatible code

            BinaryWriter bw = new BinaryWriter(Utilities.TryOpenReadWrite(archiveFileName));
            bw.Seek(0, SeekOrigin.Begin);
            bw.Write(Constants.Torrent7ZipHeader);
            bw.Seek(0, SeekOrigin.End);

            using (oldZipFile = new SevenZipExtractor(Utilities.TryOpenReadWrite(archiveFileName)))
            {

                // Get the correct signature to use (Default 0, Unicode 1, SingleFile 2, StripFileNames 4)
                byte[] tempsig = Constants.Torrent7ZipSignature;
                if (oldZipFile.FilesCount > 1)
                {
                    tempsig[16] = 0x2;
                }
                else
                {
                    tempsig[16] = 0;
                }

                bw.Write(tempsig);
                bw.Flush();
                bw.Dispose();
            }

            return true;
        }

        /// <summary>
        /// Write a set of input files to a torrent XZ archive (assuming the same output archive name)
        /// </summary>
        /// <param name="inputFiles">Input files to be moved</param>
        /// <param name="outDir">Output directory to build to</param>
        /// <param name="rom">DatItem representing the new information</param>
        /// <param name="date">True if the date from the DAT should be used if available, false otherwise (default)</param>
        /// <param name="romba">True if files should be output in Romba depot folders, false otherwise</param>
        /// <returns>True if the archive was written properly, false otherwise</returns>
        public override bool Write(List<string> inputFiles, string outDir, List<Rom> roms, bool date = false, bool romba = false)
        {
            bool success = false;
            string tempFile = Path.Combine(outDir, "tmp" + Guid.NewGuid().ToString());

            // If either list of roms is null or empty, return
            if (inputFiles == null || roms == null || inputFiles.Count == 0 || roms.Count == 0)
            {
                return success;
            }

            // If the number of inputs is less than the number of available roms, return
            if (inputFiles.Count < roms.Count)
            {
                return success;
            }

            // If one of the files doesn't exist, return
            foreach (string file in inputFiles)
            {
                if (!File.Exists(file))
                {
                    return success;
                }
            }

            // Get the output archive name from the first rebuild rom
            string archiveFileName = Path.Combine(outDir, Utilities.RemovePathUnsafeCharacters(roms[0].MachineName) + (roms[0].MachineName.EndsWith(".xz") ? "" : ".xz"));

            // Set internal variables
            SevenZipBase.SetLibraryPath("7za.dll");
            SevenZipExtractor oldZipFile;
            SevenZipCompressor zipFile;

            try
            {
                // If the full output path doesn't exist, create it
                if (!Directory.Exists(Path.GetDirectoryName(archiveFileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(archiveFileName));
                }

                // If the archive doesn't exist, create it and put the single file
                if (!File.Exists(archiveFileName))
                {
                    zipFile = new SevenZipCompressor()
                    {
                        ArchiveFormat = OutArchiveFormat.XZ,
                        CompressionLevel = CompressionLevel.Normal,
                    };

                    // Map all inputs to index
                    Dictionary<string, int> inputIndexMap = new Dictionary<string, int>();
                    for (int i = 0; i < inputFiles.Count; i++)
                    {
                        inputIndexMap.Add(roms[i].Name.Replace('\\', '/'), i);
                    }

                    // Sort the keys in TZIP order
                    List<string> keys = inputIndexMap.Keys.ToList();
                    keys.Sort(ZipFile.TorrentZipStringCompare);

                    // Create the temp directory
                    string tempPath = Path.Combine(outDir, Guid.NewGuid().ToString());
                    if (!Directory.Exists(tempPath))
                    {
                        Directory.CreateDirectory(tempPath);
                    }

                    // Now add all of the files in order
                    foreach (string key in keys)
                    {
                        string newkey = Path.Combine(tempPath, key);

                        File.Move(inputFiles[inputIndexMap[key]], newkey);
                        zipFile.CompressFiles(tempFile, newkey);
                        File.Move(newkey, inputFiles[inputIndexMap[key]]);

                        // After the first file, make sure we're in append mode
                        zipFile.CompressionMode = CompressionMode.Append;
                    }

                    Utilities.CleanDirectory(tempPath);
                    Utilities.TryDeleteDirectory(tempPath);
                }

                // Otherwise, sort the input files and write out in the correct order
                else
                {
                    // Open the old archive for reading
                    using (oldZipFile = new SevenZipExtractor(archiveFileName))
                    {
                        // Map all inputs to index
                        Dictionary<string, int> inputIndexMap = new Dictionary<string, int>();
                        for (int i = 0; i < inputFiles.Count; i++)
                        {
                            // If the old one contains the new file, then just skip out
                            if (oldZipFile.ArchiveFileNames.Contains(roms[i].Name.Replace('\\', '/')))
                            {
                                continue;
                            }

                            inputIndexMap.Add(roms[i].Name.Replace('\\', '/'), -(i + 1));
                        }

                        // Then add all of the old entries to it too
                        for (int i = 0; i < oldZipFile.FilesCount; i++)
                        {
                            inputIndexMap.Add(oldZipFile.ArchiveFileNames[i], i);
                        }

                        // If the number of entries is the same as the old archive, skip out
                        if (inputIndexMap.Keys.Count <= oldZipFile.FilesCount)
                        {
                            success = true;
                            return success;
                        }

                        // Otherwise, process the old zipfile
                        zipFile = new SevenZipCompressor()
                        {
                            ArchiveFormat = OutArchiveFormat.XZ,
                            CompressionLevel = CompressionLevel.Normal,
                        };

                        // Get the order for the entries with the new file
                        List<string> keys = inputIndexMap.Keys.ToList();
                        keys.Sort(ZipFile.TorrentZipStringCompare);

                        // Copy over all files to the new archive
                        foreach (string key in keys)
                        {
                            // Get the index mapped to the key
                            int index = inputIndexMap[key];

                            // If we have the input file, add it now
                            if (index < 0)
                            {
                                FileStream inputStream = Utilities.TryOpenRead(inputFiles[-index - 1]);

                                // Create a stream dictionary
                                Dictionary<string, Stream> dict = new Dictionary<string, Stream>();
                                dict.Add(key, inputStream);

                                // Now add the stream
                                zipFile.CompressStreamDictionary(dict, tempFile);
                            }

                            // Otherwise, copy the file from the old archive
                            else
                            {
                                Stream oldZipFileEntryStream = new MemoryStream();
                                oldZipFile.ExtractFile(index, oldZipFileEntryStream);
                                oldZipFileEntryStream.Seek(0, SeekOrigin.Begin);

                                // Create a stream dictionary
                                Dictionary<string, Stream> dict = new Dictionary<string, Stream>();
                                dict.Add(oldZipFile.ArchiveFileNames[index], oldZipFileEntryStream);

                                // Now add the stream
                                zipFile.CompressStreamDictionary(dict, tempFile);
                                oldZipFileEntryStream.Dispose();
                            }

                            // After the first file, make sure we're in append mode
                            zipFile.CompressionMode = CompressionMode.Append;
                        }
                    }
                }

                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                success = false;
            }

            // If the old file exists, delete it and replace
            if (File.Exists(archiveFileName))
            {
                Utilities.TryDeleteFile(archiveFileName);
            }
            File.Move(tempFile, archiveFileName);

            // Now make the file T7Z
            // TODO: Add ACTUAL T7Z compatible code

            BinaryWriter bw = new BinaryWriter(Utilities.TryOpenReadWrite(archiveFileName));
            bw.Seek(0, SeekOrigin.Begin);
            bw.Write(Constants.Torrent7ZipHeader);
            bw.Seek(0, SeekOrigin.End);

            using (oldZipFile = new SevenZipExtractor(Utilities.TryOpenReadWrite(archiveFileName)))
            {
                // Get the correct signature to use (Default 0, Unicode 1, SingleFile 2, StripFileNames 4)
                byte[] tempsig = Constants.Torrent7ZipSignature;
                if (oldZipFile.FilesCount > 1)
                {
                    tempsig[16] = 0x2;
                }
                else
                {
                    tempsig[16] = 0;
                }

                bw.Write(tempsig);
                bw.Flush();
                bw.Dispose();
            }

            return true;
        }

        #endregion
    }
}
