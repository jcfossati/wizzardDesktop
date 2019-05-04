using System;
using System.Collections.Generic;
using System.Text;

using SabreTools.Library.Data;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using Alphaleonis.Win32.Filesystem;

using BinaryReader = System.IO.BinaryReader;
using BinaryWriter = System.IO.BinaryWriter;
using IOException = System.IO.IOException;
using MemoryStream = System.IO.MemoryStream;
using PathTooLongException = System.IO.PathTooLongException;
using Stream = System.IO.Stream;
#endif

namespace ROMVault2.SupportedFiles.Zip
{
	/// <remarks>
	/// Based on work by GordonJ for RomVault
	/// https://github.com/gjefferyes/RomVault/blob/master/ROMVault2/SupportedFiles/Zip/zipFile.cs
	/// </remarks>
	public class ZipFile : IDisposable
	{
		#region Private instance variables

		private FileInfo _zipFileInfo;
		private ulong _centerDirStart;
		private ulong _centerDirSize;
		private ulong _endOfCenterDir64;
		private byte[] _fileComment;
		private Stream _zipstream;
		private uint _entriesCount;
		private readonly List<ZipFileEntry> _entries = new List<ZipFileEntry>();
		private ZipStatus _zipStatus;
		private bool _zip64;
		private ZipOpenType _zipOpen;
		private int _readIndex;

		#endregion

		#region Public facing variables

		public string ZipFilename
		{
			get { return (_zipFileInfo != null ? _zipFileInfo.FullName : ""); }
		}
		public long TimeStamp
		{
			get { return (_zipFileInfo != null ? _zipFileInfo.LastWriteTime.Ticks : 0); }
		}
		public ZipOpenType ZipOpen
		{
			get { return _zipOpen; }
			set { _zipOpen = value; }
		}
		public ZipStatus ZipStatus
		{
			get { return _zipStatus; }
		}
		public List<ZipFileEntry> Entries
		{
			get { return _entries; }
		}
		public int EntriesCount
		{
			get { return _entries.Count; }
		}
		public string Filename(int i)
		{
			return _entries[i].FileName;
		}
		public ulong UncompressedSize(int i)
		{
			return _entries[i].UncompressedSize;
		}
		public ulong? LocalHeader(int i)
		{
			return ((_entries[i].GeneralPurposeBitFlag & GeneralPurposeBitFlag.LanguageEncodingFlag) == 0
				? (ulong?)_entries[i].RelativeOffset
				: null);
		}
		public ZipReturn FileStatus(int i)
		{
			return _entries[i].FileStatus;
		}
		public byte[] CRC32(int i)
		{
			return _entries[i].CRC;
		}
		public byte[] MD5(int i)
		{
			return _entries[i].MD5;
		}
		public byte[] SHA1(int i)
		{
			return _entries[i].SHA1;
		}
		public bool Contains(string n)
		{
			return _entries.Contains(new ZipFileEntry(new MemoryStream(), n));
		}

		#endregion

		#region Destructors

		~ZipFile()
		{
			Dispose();
		}

		public void Dispose()
		{
			if (_zipstream != null)
			{
				_zipstream.Close();
				_zipstream.Dispose();
			}
		}

		#endregion

		#region Central Directory

		/// <summary>
		/// Find the end of the central directory signature
		/// </summary>
		/// <returns>Status of the given stream</returns>
		private ZipReturn FindEndOfCentralDirSignature()
		{
			long fileSize = _zipstream.Length;
			long maxBackSearch = 0xffff;

			if (_zipstream.Length < maxBackSearch)
			{
				maxBackSearch = _zipstream.Length;
			}

			const long buffsize = 0x400;
			byte[] buffer = new byte[buffsize + 4];

			long backPosition = 4;
			while (backPosition < maxBackSearch)
			{
				backPosition += buffsize;
				if (backPosition > maxBackSearch) backPosition = maxBackSearch;

				long readSize = backPosition > (buffsize + 4) ? (buffsize + 4) : backPosition;

				_zipstream.Position = fileSize - backPosition;

				_zipstream.Read(buffer, 0, (int)readSize);


				for (long i = readSize - 4; i >= 0; i--)
				{
					if ((buffer[i] != 0x50) || (buffer[i + 1] != 0x4b) || (buffer[i + 2] != 0x05) || (buffer[i + 3] != 0x06))
					{
						continue;
					}

					_zipstream.Position = (fileSize - backPosition) + i;
					return ZipReturn.ZipGood;
				}
			}
			return ZipReturn.ZipCentralDirError;
		}

		/// <summary>
		/// Read the end of the central directory
		/// </summary>
		/// <returns>Status of the given stream</returns>
		private ZipReturn ReadEndOfCentralDir()
		{
			// Open the stream for reading
			BinaryReader br = new BinaryReader(_zipstream);

			// If the stream doesn't start with the correct signature, return
			uint thisSignature = br.ReadUInt32();
			if (thisSignature != Constants.EndOfCentralDirSignature)
			{
				return ZipReturn.ZipEndOfCentralDirectoryError;
			}

			// If this is part of a spanned archive, return
			ushort tushort = br.ReadUInt16(); // NumberOfThisDisk
			if (tushort != 0)
			{
				return ZipReturn.ZipEndOfCentralDirectoryError;
			}
			tushort = br.ReadUInt16(); // NumberOfThisDiskCenterDir
			if (tushort != 0)
			{
				return ZipReturn.ZipEndOfCentralDirectoryError;
			}

			// If the number of entries in the current disk doesn't match up with the total entries, return
			_entriesCount = br.ReadUInt16(); // TotalNumberOfEntriesDisk
			tushort = br.ReadUInt16(); // TotalNumber of entries in the central directory 
			if (tushort != _entriesCount)
			{
				return ZipReturn.ZipEndOfCentralDirectoryError;
			}

			_centerDirSize = br.ReadUInt32(); // SizeOfCenteralDir
			_centerDirStart = br.ReadUInt32(); // Offset

			// Get the file comment
			ushort zipFileCommentLength = br.ReadUInt16();
			_fileComment = br.ReadBytes(zipFileCommentLength);

			// If there's extra data past the comment, flag that we have extra data
			if (_zipstream.Position != _zipstream.Length)
			{
				_zipStatus |= ZipStatus.ExtraData;
			}

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Write the end of the central directory
		/// </summary>
		private void WriteEndOfCentralDir()
		{
			// Open the stream for writing
			BinaryWriter bw = new BinaryWriter(_zipstream);

			// Now write out all of the data
			bw.Write(Constants.EndOfCentralDirSignature);
			bw.Write((ushort)0); // NumberOfThisDisk
			bw.Write((ushort)0); // NumberOfThisDiskCenterDir
			bw.Write((ushort)(_entries.Count >= 0xffff ? 0xffff : _entries.Count));  // TotalNumberOfEnteriesDisk
			bw.Write((ushort)(_entries.Count >= 0xffff ? 0xffff : _entries.Count));  // TotalNumber of enteries in the central directory 
			bw.Write((uint)(_centerDirSize >= 0xffffffff ? 0xffffffff : _centerDirSize));
			bw.Write((uint)(_centerDirStart >= 0xffffffff ? 0xffffffff : _centerDirStart));
			bw.Write((ushort)_fileComment.Length);
			bw.Write(_fileComment, 0, _fileComment.Length);
		}

		#endregion

		#region Zip64 Central Directory

		/// <summary>
		/// Read the end of the Zip64 central directory
		/// </summary>
		/// <returns>Status of the given stream</returns>
		private ZipReturn ReadZip64EndOfCentralDir()
		{
			// Set the type of the archive to Zip64
			_zip64 = true;

			// Open the stream for reading
			BinaryReader br = new BinaryReader(_zipstream);

			// If the signature doesn't match, then return
			uint thisSignature = br.ReadUInt32();
			if (thisSignature != Constants.Zip64EndOfCentralDirSignature)
			{
				return ZipReturn.ZipEndOfCentralDirectoryError;
			}

			// If the size of the central dir record isn't right, return
			ulong tulong = br.ReadUInt64(); // Size of zip64 end of central directory record
			if (tulong != 44)
			{
				return ZipReturn.Zip64EndOfCentralDirError;
			}

			br.ReadUInt16(); // version made by

			// If the version needed to extract isn't correct, return
			ushort tushort = br.ReadUInt16(); // version needed to extract
			if (tushort != (ushort)ArchiveVersion.TorrentZip64)
			{
				return ZipReturn.Zip64EndOfCentralDirError;
			}

			// If this is part of a spanned archive, return
			uint tuint = br.ReadUInt32(); // number of this disk
			if (tuint != 0)
			{
				return ZipReturn.Zip64EndOfCentralDirError;
			}
			tuint = br.ReadUInt32(); // number of the disk with the start of the central directory
			if (tuint != 0)
			{
				return ZipReturn.Zip64EndOfCentralDirError;
			}

			// If the number of entries in the current disk doesn't match up with the total entries, return
			_entriesCount = (uint)br.ReadUInt64(); // total number of entries in the central directory on this disk
			tulong = br.ReadUInt64(); // total number of entries in the central directory
			if (tulong != _entriesCount)
			{
				return ZipReturn.Zip64EndOfCentralDirError;
			}

			_centerDirSize = br.ReadUInt64(); // size of central directory
			_centerDirStart = br.ReadUInt64(); // offset of start of central directory with respect to the starting disk number

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Write the end of the Zip64 central directory
		/// </summary>
		private void WriteZip64EndOfCentralDir()
		{
			// Open the stream for writing
			BinaryWriter bw = new BinaryWriter(_zipstream);

			// Now write out all of the data
			bw.Write(Constants.Zip64EndOfCentralDirSignature);
			bw.Write((ulong)44); // Size of zip64 end of central directory record
			bw.Write((ushort)ArchiveVersion.TorrentZip64); // version made by
			bw.Write((ushort)ArchiveVersion.TorrentZip64); // version needed to extract
			bw.Write((uint)0); // number of this disk
			bw.Write((uint)0); // number of the disk with the start of the central directroy
			bw.Write((ulong)_entries.Count); // total number of entries in the central directory on this disk
			bw.Write((ulong)_entries.Count); // total number of entries in the central directory
			bw.Write(_centerDirSize);  // size of central directory
			bw.Write(_centerDirStart); // offset of start of central directory with respect to the starting disk number
		}

		/// <summary>
		/// Read the end of the Zip64 central directory locator
		/// </summary>
		/// <returns></returns>
		private ZipReturn ReadZip64EndOfCentralDirectoryLocator()
		{
			// Set the current archive type to Zip64
			_zip64 = true;

			// Open the stream for reading
			BinaryReader br = new BinaryReader(_zipstream);

			// If the signature doesn't match, return
			uint thisSignature = br.ReadUInt32();
			if (thisSignature != Constants.Zip64EndOfCentralDirectoryLocator)
			{
				return ZipReturn.ZipEndOfCentralDirectoryError;
			}

			// If the disk isn't the first and only, then return
			uint tuint = br.ReadUInt32();  // number of the disk with the start of the zip64 end of centeral directory
			if (tuint != 0)
			{
				return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;
			}

			_endOfCenterDir64 = br.ReadUInt64(); // relative offset of the zip64 end of central directory record

			tuint = br.ReadUInt32();  // total number of disks
			if (tuint != 1)
			{
				return ZipReturn.Zip64EndOfCentralDirectoryLocatorError;
			}

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Write the end of the Zip64 central directory locator
		/// </summary>
		private void WriteZip64EndOfCentralDirectoryLocator()
		{
			// Open the stream for writing
			BinaryWriter bw = new BinaryWriter(_zipstream);

			// Now write the data
			bw.Write(Constants.Zip64EndOfCentralDirectoryLocator);
			bw.Write((uint)0); // number of the disk with the start of the zip64 end of centeral directory
			bw.Write(_endOfCenterDir64); // relative offset of the zip64 end of central directroy record
			bw.Write((uint)1); // total number of disks
		}

		#endregion

		#region Open, Create, Close

		/// <summary>
		/// Open a new file as an archive
		/// </summary>
		/// <param name="filename">Name of the new file to open</param>
		/// <param name="timestamp">Timestamp the file should have</param>
		/// <param name="readHeaders">True if file headers should be read, false otherwise</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn Open(string filename, long timestamp, bool readHeaders)
		{
			// If a stream already exists, close it
			Close();

			// Now, reset the archive information
			_zipStatus = ZipStatus.None;
			_zip64 = false;
			_centerDirStart = 0;
			_centerDirSize = 0;
			_zipFileInfo = null;

			// Then, attempt to open the file and get information from it
			try
			{
				// If the input file doesn't exist, close the stream and return
				if (!File.Exists(filename))
				{
					Close();
					return ZipReturn.ZipErrorFileNotFound;
				}

				// Get the fileinfo object
				_zipFileInfo = new FileInfo(filename);

				// If the timestamps don't match, close the stream and return
				if (_zipFileInfo.LastWriteTime.Ticks != timestamp)
				{
					Close();
					return ZipReturn.ZipErrorTimeStamp;
				}

				// Now try to open the file for reading
				_zipstream = Utilities.TryOpenRead(filename);
				int read = _zipstream.Read(new byte[1], 0, 1);
				if (read != 1)
				{
					Close();
					return ZipReturn.ZipErrorOpeningFile;
				}
				_zipstream.Position = 0;
			}
			catch (PathTooLongException)
			{
				Close();
				return ZipReturn.ZipFileNameToLong;
			}
			catch (IOException)
			{
				Close();
				return ZipReturn.ZipErrorOpeningFile;
			}

			// If we succeeded, set the flag for read
			_zipOpen = ZipOpenType.OpenRead;

			// If we're not reading the headers, return
			if (!readHeaders)
			{
				return ZipReturn.ZipGood;
			}

			//Otherwise, we want to get all of the archive information
			try
			{
				// First, try to get the end of the central directory
				ZipReturn zr = FindEndOfCentralDirSignature();
				if (zr != ZipReturn.ZipGood)
				{
					Close();
					return zr;
				}

				// Now read the end of the central directory
				long eocd = _zipstream.Position;
				zr = ReadEndOfCentralDir();
				if (zr != ZipReturn.ZipGood)
				{
					Close();
					return zr;
				}

				// If we have any indicators of Zip64, check for the Zip64 EOCD
				if (_centerDirStart == 0xffffffff || _centerDirSize == 0xffffffff || _entriesCount == 0xffff)
				{
					_zip64 = true;

					// Check for the Zip64 EOCD locator
					_zipstream.Position = eocd - 20;
					zr = ReadZip64EndOfCentralDirectoryLocator();
					if (zr != ZipReturn.ZipGood)
					{
						Close();
						return zr;
					}

					// If it was found, read the Zip64 EOCD
					_zipstream.Position = (long)_endOfCenterDir64;
					zr = ReadZip64EndOfCentralDir();
					if (zr != ZipReturn.ZipGood)
					{
						Close();
						return zr;
					}
				}

				// Now that we have the rest of the information, check for TorrentZip
				bool torrentZip = false;
				if (_fileComment.Length == 22)
				{
					if (Encoding.ASCII.GetString(_fileComment).Substring(0, 14) == "TORRENTZIPPED-")
					{
						// First get to the right part of the stream
						OptimizedCRC ocrc = new OptimizedCRC();
						byte[] buffer = new byte[_centerDirSize];
						_zipstream.Position = (long)_centerDirStart;

						// Then read in the central directory and hash
						BinaryReader br = new BinaryReader(_zipstream);
						buffer = br.ReadBytes((int)_centerDirSize);
						ocrc.Update(buffer, 0, (int)_centerDirSize);
						string calculatedCrc = ocrc.Value.ToString("X8");

						// If the hashes match, then we have a torrentzip file
						string extractedCrc = Encoding.ASCII.GetString(_fileComment).Substring(14, 8);
						if (String.Equals(calculatedCrc, extractedCrc, StringComparison.Ordinal))
						{
							torrentZip = true;
						}
					}
				}

				// With potential torrentzip out of the way, read the central directory
				_zipstream.Position = (long)_centerDirStart;

				// Remove any entries already listed in the archive
				_entries.Clear();
				_entries.Capacity = (int)_entriesCount;

				// Now populate the entries from the central directory
				for (int i = 0; i < _entriesCount; i++)
				{
					ZipFileEntry zfe = new ZipFileEntry(_zipstream);
					zr = zfe.ReadCentralDirectory();
					
					// If we get any errors, close and return
					if (zr != ZipReturn.ZipGood)
					{
						Close();
						return zr;
					}

					// If we have a Zip64 entry, make sure the archive is
					_zip64 |= zfe.Zip64;

					// Now add the entry to the archive
					_entries.Add(zfe);
				}

				// Now that the entries are populated, verify against the actual headers
				for (int i = 0; i < _entriesCount; i++)
				{
					zr = _entries[i].ReadHeader();

					// If we get any errors, close and return
					if (zr != ZipReturn.ZipGood)
					{
						Close();
						return zr;
					}

					// If we have a torrentzipped entry, make sure the archive is
					torrentZip &= _entries[i].TorrentZip;
				}

				// If we have a torrentzipped file, check the file order
				if (torrentZip)
				{
					for (int i = 0; i < _entriesCount - 1; i++)
					{
						if (TorrentZipStringCompare(_entries[i].FileName, _entries[i + 1].FileName) < 0)
						{
							continue;
						}
						torrentZip = false;
						break;
					}
				}

				// Now check for torrentzipped directories if we still have a torrentZip file
				if (torrentZip)
				{
					for (int i = 0; i < _entriesCount - 1; i++)
					{
						// See if we found a directory
						string filename0 = _entries[i].FileName;
						if (filename0.Substring(filename0.Length - 1, 1) != "/")
						{
							continue;
						}

						// See if the next file is in that directory
						string filename1 = _entries[i + 1].FileName;
						if (filename1.Length <= filename0.Length)
						{
							continue;
						}
						if (TorrentZipStringCompare(filename0, filename1.Substring(0, filename0.Length)) == 0)
						{
							continue;
						}

						// If we found a file in the directory, then we don't need the directory entry
						torrentZip = false;
						break;
					}
				}

				// If we still have torrentzip, say the archive is too
				if (torrentZip)
				{
					_zipStatus |= ZipStatus.TorrentZip;
				}

				return ZipReturn.ZipGood;
			}
			catch
			{
				Close();
				return ZipReturn.ZipErrorReadingFile;
			}
		}

		/// <summary>
		/// Create a new file as an archive
		/// </summary>
		/// <param name="filename">Name of the new file to create</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn Create(string filename)
		{
			// If the file is already open, return
			if (_zipOpen != ZipOpenType.Closed)
			{
				return ZipReturn.ZipFileAlreadyOpen;
			}

			// Otherwise, create the directory for the file
			Directory.CreateDirectory(Path.GetDirectoryName(filename));
			_zipFileInfo = new FileInfo(filename);

			// Now try to open the file
			_zipstream = File.Open(filename, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite);
			ZipOpen = ZipOpenType.OpenWrite;
			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Close the file that the stream refers to
		/// </summary>
		public void Close()
		{
			// If the stream is already closed, then just return
			if (_zipOpen == ZipOpenType.Closed)
			{
				return;
			}

			// If the stream is opened for read, close it
			if (_zipOpen == ZipOpenType.OpenRead)
			{
				Dispose();
				_zipOpen = ZipOpenType.Closed;
				return;
			}

			// Now, the only other choice is open for writing so we check everything is correct
			_zip64 = false;
			bool torrentZip = true;

			// Check the central directory
			_centerDirStart = (ulong)_zipstream.Position;
			if (_centerDirStart >= 0xffffffff)
			{
				_zip64 = true;
			}

			// Now loop through and add all of the central directory entries
			foreach (ZipFileEntry zfe in _entries)
			{
				zfe.WriteCentralDirectory(_zipstream);
				_zip64 |= zfe.Zip64;
				torrentZip &= zfe.TorrentZip;
			}

			_centerDirSize = (ulong)_zipstream.Position - _centerDirStart;

			// Then get the central directory hash
			OptimizedCRC ocrc = new OptimizedCRC();
			byte[] buffer = new byte[_centerDirSize];
			long currentPosition = _zipstream.Position;
			_zipstream.Position = (long)_centerDirStart;

			// Then read in the central directory and hash
			BinaryReader br = new BinaryReader(_zipstream);
			buffer = br.ReadBytes((int)_centerDirSize);
			ocrc.Update(buffer, 0, (int)_centerDirSize);
			string calculatedCrc = ocrc.Value.ToString("X8");

			// Finally get back to the original position
			_zipstream.Position = currentPosition;

			// Now set more of the information
			_fileComment = (torrentZip ? Encoding.ASCII.GetBytes(("TORRENTZIPPED-" + calculatedCrc).ToCharArray()) : new byte[0]);
			_zipStatus = (torrentZip ? ZipStatus.TorrentZip : ZipStatus.None);
			
			// If we have a Zip64 archive, write the correct information
			if (_zip64)
			{
				_endOfCenterDir64 = (ulong)_zipstream.Position;
				WriteZip64EndOfCentralDir();
				WriteZip64EndOfCentralDirectoryLocator();
			}

			// Now write out the end of the central directory
			WriteEndOfCentralDir();

			// Finally, close and dispose of the stream
			_zipstream.SetLength(_zipstream.Position);
			_zipstream.Flush();
			_zipstream.Close();
			_zipstream.Dispose();

			// Get the new file information
			_zipFileInfo = new FileInfo(_zipFileInfo.FullName);

			// And set the stream to closed
			_zipOpen = ZipOpenType.Closed;
		}

		/// <summary>
		/// Close a failed stream
		/// </summary>
		public void CloseFailed()
		{
			// If the stream is already closed, return
			if (_zipOpen == ZipOpenType.Closed)
			{
				return;
			}

			// If we're open for read, close the underlying stream
			if (_zipOpen == ZipOpenType.OpenRead)
			{
				Dispose();
				_zipOpen = ZipOpenType.Closed;
				return;
			}

			// Otherwise, we only have an open for write left
			_zipstream.Flush();
			_zipstream.Close();
			_zipstream.Dispose();

			// Delete the failed file
			Utilities.TryDeleteFile(_zipFileInfo.FullName);
			_zipFileInfo = null;
			_zipOpen = ZipOpenType.Closed;
		}

		#endregion

		#region Read and Write

		/// <summary>
		/// Open the read file stream
		/// </summary>
		/// <param name="index">Index of entry to read</param>
		/// <param name="raw">If compression mode is deflate, use the zipstream as is, otherwise decompress</param>
		/// <param name="stream">Output stream representing the correctly compressed stream</param>
		/// <param name="streamSize">Size of the stream regardless of compression</param>
		/// <param name="compressionMethod">Compression method to compare against</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn OpenReadStream(int index, bool raw, out Stream stream, out ulong streamSize, out CompressionMethod compressionMethod, out uint lastMod)
		{
			// Set all of the defaults
			streamSize = 0;
			compressionMethod = CompressionMethod.Stored;
			lastMod = 0;
			_readIndex = index;
			stream = null;

			// If the file isn't open for read, return
			if (_zipOpen != ZipOpenType.OpenRead)
			{
				return ZipReturn.ZipReadingFromOutputFile;
			}

			// Now try to read the local file header
			ZipReturn zr = _entries[index].ReadHeader();
			if (zr != ZipReturn.ZipGood)
			{
				Close();
				return zr;
			}

			// Now return the results of opening the local file
			return _entries[index].OpenReadStream(raw, out stream, out streamSize, out compressionMethod, out lastMod);
		}

		/// <summary>
		/// Open the read file stream wihtout verification, if possible
		/// </summary>
		/// <param name="index">Index of entry to read</param>
		/// <param name="raw">If compression mode is deflate, use the zipstream as is, otherwise decompress</param>
		/// <param name="stream">Output stream representing the correctly compressed stream</param>
		/// <param name="streamSize">Size of the stream regardless of compression</param>
		/// <param name="compressionMethod">Compression method to compare against</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn OpenReadStreamQuick(ulong pos, bool raw, out Stream stream, out ulong streamSize, out CompressionMethod compressionMethod, out uint lastMod)
		{
			// Get the temporary entry based on the defined position
			ZipFileEntry tempEntry = new ZipFileEntry(_zipstream);
			tempEntry.RelativeOffset = pos;

			// Clear the local files and add this file instead
			_entries.Clear();
			_entries.Add(tempEntry);

			// Now try to read the header quickly
			ZipReturn zr = tempEntry.ReadHeaderQuick();
			if (zr != ZipReturn.ZipGood)
			{
				stream = null;
				streamSize = 0;
				compressionMethod = CompressionMethod.Stored;
				lastMod = 0;
				return zr;
			}
			_readIndex = 0;

			// Return the file stream if it worked
			return tempEntry.OpenReadStream(raw, out stream, out streamSize, out compressionMethod, out lastMod);
		}

		/// <summary>
		/// Close the read file stream
		/// </summary>
		/// <returns></returns>
		public ZipReturn CloseReadStream()
		{
			return _entries[_readIndex].CloseReadStream();
		}

		/// <summary>
		/// Open the write file stream
		/// </summary>
		/// <param name="raw">If compression mode is deflate, use the zipstream as is, otherwise decompress</param>
		/// <param name="torrentZip">True if outputted stream should be torrentzipped, false otherwise</param>
		/// <param name="uncompressedSize">Uncompressed size of the stream</param>
		/// <param name="compressionMethod">Compression method to compare against</param>
		/// <param name="stream">Output stream representing the correctly compressed stream</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn OpenWriteStream(bool raw, bool torrentZip, string filename, ulong uncompressedSize,
			CompressionMethod compressionMethod, out Stream stream, uint lastMod = Constants.TorrentZipFileDateTime)
		{
			// Check to see if the stream is writable
			stream = null;
			if (_zipOpen != ZipOpenType.OpenWrite)
			{
				return ZipReturn.ZipWritingToInputFile;
			}

			// Open the entry stream based on the current position
			ZipFileEntry zfe = new ZipFileEntry(_zipstream, filename, lastMod: lastMod);
			ZipReturn zr = zfe.OpenWriteStream(raw, torrentZip, uncompressedSize, compressionMethod, out stream);
			_entries.Add(zfe);

			return zr;
		}

		/// <summary>
		/// Close the write file stream
		/// </summary>
		/// <param name="crc32">CRC to assign to the current stream</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn CloseWriteStream(uint crc32)
		{
			return _entries[_entries.Count - 1].CloseWriteStream(crc32);
		}

		/// <summary>
		/// Remove the last added entry, if possible
		/// </summary>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn RollBack()
		{
			// If the stream isn't writable, return
			if (_zipOpen != ZipOpenType.OpenWrite)
			{
				return ZipReturn.ZipWritingToInputFile;
			}

			// Otherwise, make sure there are entries to roll back
			int fileCount = _entries.Count;
			if (fileCount == 0)
			{
				return ZipReturn.ZipErrorRollBackFile;
			}

			// Get the last added entry and remove
			ZipFileEntry zfe = _entries[fileCount - 1];
			_entries.RemoveAt(fileCount - 1);
			_zipstream.Position = (long)zfe.RelativeOffset;
			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Add a directory marking to a local file
		/// </summary>
		public void AddDirectory()
		{
			_entries[_entries.Count - 1].AddDirectory();
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Scan every individual entry for validity
		/// </summary>
		public void DeepScan()
		{
			foreach (ZipFileEntry zfe in _entries)
			{
				zfe.Check();
			}
		}

		/// <summary>
		/// Get the text associated with a return status
		/// </summary>
		/// <param name="zr">ZipReturn status to parse</param>
		/// <returns>String associated with the ZipReturn</returns>
		public static string ZipErrorMessageText(ZipReturn zr)
		{
			string ret = "Unknown";
			switch (zr)
			{
				case ZipReturn.ZipGood:
					ret = "";
					break;
				case ZipReturn.ZipFileCountError:
					ret = "The number of file in the Zip does not mach the number of files in the Zips Centeral Directory";
					break;
				case ZipReturn.ZipSignatureError:
					ret = "An unknown Signature Block was found in the Zip";
					break;
				case ZipReturn.ZipExtraDataOnEndOfZip:
					ret = "Extra Data was found on the end of the Zip";
					break;
				case ZipReturn.ZipUnsupportedCompression:
					ret = "An unsupported Compression method was found in the Zip, if you recompress this zip it will be usable";
					break;
				case ZipReturn.ZipLocalFileHeaderError:
					ret = "Error reading a zipped file header information";
					break;
				case ZipReturn.ZipCentralDirError:
					ret = "There is an error in the Zip Centeral Directory";
					break;
				case ZipReturn.ZipReadingFromOutputFile:
					ret = "Trying to write to a Zip file open for output only";
					break;
				case ZipReturn.ZipWritingToInputFile:
					ret = "Tring to read from a Zip file open for input only";
					break;
				case ZipReturn.ZipErrorGettingDataStream:
					ret = "Error creating Data Stream";
					break;
				case ZipReturn.ZipCRCDecodeError:
					ret = "CRC error";
					break;
				case ZipReturn.ZipDecodeError:
					ret = "Error unzipping a file";
					break;
			}

			return ret;
		}

		/// <summary>
		/// Compare two strings in TorrentZip format
		/// </summary>
		/// <param name="string1"></param>
		/// <param name="string2"></param>
		/// <returns></returns>
		public static int TorrentZipStringCompare(string string1, string string2)
		{
			char[] bytes1 = string1.ToCharArray();
			char[] bytes2 = string2.ToCharArray();

			int pos1 = 0;
			int pos2 = 0;

			for (;;)
			{
				if (pos1 == bytes1.Length)
				{
					return ((pos2 == bytes2.Length) ? 0 : -1);
				}
				if (pos2 == bytes2.Length)
				{
					return 1;
				}

				int byte1 = bytes1[pos1++];
				int byte2 = bytes2[pos2++];

				if (byte1 >= 65 && byte1 <= 90)
				{
					byte1 += 0x20;
				}
				if (byte2 >= 65 && byte2 <= 90)
				{
					byte2 += 0x20;
				}

				if (byte1 < byte2)
				{
					return -1;
				}
				if (byte1 > byte2)
				{
					return 1;
				}
			}
		}

		#endregion
	}
}
