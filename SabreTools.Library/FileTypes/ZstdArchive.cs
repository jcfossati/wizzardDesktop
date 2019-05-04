using System;
using System.Collections.Generic;

using SabreTools.Library.Data;
using SabreTools.Library.DatItems;

#if MONO
using System.IO;
#else
using MemoryStream = System.IO.MemoryStream;
using Stream = System.IO.Stream;
#endif

namespace SabreTools.Library.FileTypes
{
    /// <summary>
    /// Represents a ZstdArchive archive for reading and writing
    /// </summary>
    /// TODO: Implement from source at https://github.com/skbkontur/ZstdNet
    public class ZstdArchive : BaseArchive
    {
        #region Constructors

        /// <summary>
        /// Create a new ZstdArchive with no base file
        /// </summary>
        public ZstdArchive()
            : base()
        {
            this.Type = FileType.ZstdArchive;
        }

        /// <summary>
        /// Create a new ZstdArchive from the given file
        /// </summary>
        /// <param name="filename">Name of the file to use as an archive</param>
        /// <param name="getHashes">True if hashes for this file should be calculated, false otherwise (default)</param>
        public ZstdArchive(string filename, bool getHashes)
            : base(filename, getHashes)
        {
            this.Type = FileType.ZstdArchive;
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Attempt to extract a file from an archive
        /// </summary>
        /// <param name="entryName">Name of the entry to be extracted</param>
        /// <param name="outDir">Output directory for archive extraction</param>
        /// <returns>Name of the extracted file, null on error</returns>
        public override string CopyToFile(string entryName, string outDir)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Attempt to extract a stream from an archive
        /// </summary>
        /// <param name="entryName">Name of the entry to be extracted</param>
        /// <param name="realEntry">Output representing the entry name that was found</param>
        /// <returns>MemoryStream representing the entry, null on error</returns>
        public override (MemoryStream, string) CopyToStream(string entryName)
        {
            throw new NotImplementedException();
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
        /// Write an input file to a torrent Zstd file
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Write an input stream to a torrent Zstd file
        /// </summary>
        /// <param name="inputStream">Input stream to be moved</param>
        /// <param name="outDir">Output directory to build to</param>
        /// <param name="rom">DatItem representing the new information</param>
        /// <param name="date">True if the date from the DAT should be used if available, false otherwise (default)</param>
        /// <param name="romba">True if files should be output in Romba depot folders, false otherwise</param>
        /// <returns>True if the write was a success, false otherwise</returns>
        /// <remarks>This works for now, but it can be sped up by using Ionic.Zip or another zlib wrapper that allows for header values built-in. See edc's code.</remarks>
        public override bool Write(Stream inputStream, string outDir, Rom rom, bool date = false, bool romba = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Write a set of input files to a torrent Zstd archive (assuming the same output archive name)
        /// </summary>
        /// <param name="inputFiles">Input files to be moved</param>
        /// <param name="outDir">Output directory to build to</param>
        /// <param name="rom">DatItem representing the new information</param>
        /// <param name="date">True if the date from the DAT should be used if available, false otherwise (default)</param>
        /// <param name="romba">True if files should be output in Romba depot folders, false otherwise</param>
        /// <returns>True if the archive was written properly, false otherwise</returns>
        public override bool Write(List<string> inputFiles, string outDir, List<Rom> roms, bool date = false, bool romba = false)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
