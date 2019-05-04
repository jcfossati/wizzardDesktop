using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using SabreTools.Library.Data;
using SabreTools.Library.Tools;

using Ionic.Zlib;

namespace ROMVault2.SupportedFiles.Zip
{
	/// <remarks>
	/// Based on work by GordonJ for RomVault
	/// https://github.com/gjefferyes/RomVault/blob/master/ROMVault2/SupportedFiles/Zip/zipFile.cs
	/// </remarks>
	public class ZipFileEntry : IEquatable<ZipFileEntry>
	{
		#region Private instance variables

		private readonly Stream _zipstream;
		private Stream _readStream;
		private Stream _writeStream;
		private string _fileName;
		private CompressionMethod _compressionMethod;
		private ArchiveVersion _versionMadeBy;
		private ArchiveVersion _versionNeeded;
		private GeneralPurposeBitFlag _generalPurposeBitFlag;
		private uint _lastMod;
		private uint _crc;
		private ulong _compressedSize;
		private ulong _uncompressedSize;
		private byte[] _extraField;
		private byte[] _comment;
		private InternalFileAttributes _internalFileAttributes;
		private uint _externalFileAttributes;
		private ulong _relativeOffset;
		private ulong _crc32Location;
		private ulong _extraLocation;
		private ulong _dataLocation;
		private bool _zip64;
		private bool _torrentZip;
		private byte[] _md5;
		private byte[] _sha1;
		private ZipReturn _fileStatus = ZipReturn.ZipUntested;

		#endregion

		#region Public facing variables

		public string FileName
		{
			get { return _fileName; }
			private set { _fileName = value; }
		}
		public GeneralPurposeBitFlag GeneralPurposeBitFlag
		{
			get { return _generalPurposeBitFlag; }
			private set { _generalPurposeBitFlag = value; }
		}
		public uint LastMod
		{
			get { return _lastMod; }
			set { _lastMod = value; }
		}
		public byte[] CRC
		{
			get { return BitConverter.GetBytes(_crc); }
			private set { _crc = BitConverter.ToUInt32(value, 0); }
		}
		public ulong UncompressedSize
		{
			get { return _uncompressedSize; }
			private set { _uncompressedSize = value; }
		}
		public string ExtraField
		{
			get { return Encoding.GetEncoding(858).GetString(_extraField); }
			set { _extraField = Utilities.StringToByteArray(Utilities.ConvertAsciiToHex(value)); }
		}
		public string Comment
		{
			get { return Encoding.GetEncoding(858).GetString(_comment); }
			set { _comment = Utilities.StringToByteArray(Utilities.ConvertAsciiToHex(value)); }
		}
		public ulong RelativeOffset
		{
			get { return _relativeOffset; }
			set { _relativeOffset = value; }
		}
		public bool Zip64
		{
			get { return _zip64; }
			private set { _zip64 = value; }
		}
		public bool TorrentZip
		{
			get { return _torrentZip; }
			private set { _torrentZip = value; }
		}
		public byte[] MD5
		{
			get { return _md5; }
			private set { _md5 = value; }
		}
		public byte[] SHA1
		{
			get { return _sha1; }
			private set { _sha1 = value; }
		}
		public ZipReturn FileStatus
		{
			get { return _fileStatus; }
			set { _fileStatus = value; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Create a new ZipFileEntry using just a stream
		/// </summary>
		/// <param name="zipstream">Stream representing the entry</param>
		public ZipFileEntry(Stream zipstream)
		{
			_zipstream = zipstream;
		}

		/// <summary>
		/// Create a new ZipFileEntry from a stream and a filename
		/// </summary>
		/// <param name="zipstream">Stream representing the entry</param>
		/// <param name="filename">Internal filename to use</param>
		public ZipFileEntry(Stream zipstream, string filename, uint lastMod = Constants.TorrentZipFileDateTime)
		{
			_zip64 = false;
			_zipstream = zipstream;
			_generalPurposeBitFlag = GeneralPurposeBitFlag.DeflatingMaximumCompression;
			_compressionMethod = CompressionMethod.Deflated;
			_lastMod = lastMod;

			FileName = filename;
		}

		#endregion

		#region Central Directory

		/// <summary>
		/// Read the central directory entry from the input stream
		/// </summary>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn ReadCentralDirectory()
		{
			try
			{
				// Open the stream for reading
				BinaryReader br = new BinaryReader(_zipstream);
				
				// If the first bytes aren't a central directory header, log and return
				if (br.ReadUInt32() != Constants.CentralDirectoryHeaderSignature)
				{
					return ZipReturn.ZipCentralDirError;
				}

				// Now read in available information, skipping the unnecessary
				_versionMadeBy = (ArchiveVersion)br.ReadUInt16();
				_versionNeeded = (ArchiveVersion)br.ReadUInt16();
				_generalPurposeBitFlag = (GeneralPurposeBitFlag)br.ReadUInt16();
				_compressionMethod = (CompressionMethod)br.ReadUInt16();

				// If we have an unsupported compression method, log and return
				if (_compressionMethod != CompressionMethod.Stored && _compressionMethod != CompressionMethod.Deflated)
				{
					return ZipReturn.ZipCentralDirError;
				}

				// Keep reading available information, skipping the unnecessary
				_lastMod = br.ReadUInt32();
				_crc = br.ReadUInt32();
				_compressedSize = br.ReadUInt32();
				_uncompressedSize = br.ReadUInt32();

				// Now store some temp vars to find the filename, extra field, and comment
				ushort fileNameLength = br.ReadUInt16();
				ushort extraFieldLength = br.ReadUInt16();
				ushort fileCommentLength = br.ReadUInt16();

				// Even more reading available information, skipping the unnecessary
				br.ReadUInt16(); // Disk number start
				_internalFileAttributes = (InternalFileAttributes)br.ReadUInt16();
				_externalFileAttributes = br.ReadUInt32();
				_relativeOffset = br.ReadUInt32();
				byte[] fileNameBytes = br.ReadBytes(fileNameLength);
				_fileName = ((_generalPurposeBitFlag & GeneralPurposeBitFlag.LanguageEncodingFlag) == 0
					? Encoding.GetEncoding(858).GetString(fileNameBytes)
					: Encoding.UTF8.GetString(fileNameBytes, 0, fileNameLength));
				_extraField = br.ReadBytes(extraFieldLength);
				_comment = br.ReadBytes(fileCommentLength);

				/*
				Full disclosure: this next section is in GordonJ's work but I honestly
				have no idea everything that it does. It seems to do something to figure
				out if it's Zip64, or possibly check for random things but it uses the
				extra field for this, which I do not fully understand. It's copied in
				its entirety below in the hope that it makes things better...
				*/

				int pos = 0;
				while (extraFieldLength > pos)
				{
					ushort type = BitConverter.ToUInt16(_extraField, pos);
					pos += 2;
					ushort blockLength = BitConverter.ToUInt16(_extraField, pos);
					pos += 2;
					switch (type)
					{
						case 0x0001:
							Zip64 = true;
							if (UncompressedSize == 0xffffffff)
							{
								UncompressedSize = BitConverter.ToUInt64(_extraField, pos);
								pos += 8;
							}
							if (_compressedSize == 0xffffffff)
							{
								_compressedSize = BitConverter.ToUInt64(_extraField, pos);
								pos += 8;
							}
							if (_relativeOffset == 0xffffffff)
							{
								_relativeOffset = BitConverter.ToUInt64(_extraField, pos);
								pos += 8;
							}
							break;
						case 0x7075:
							//byte version = extraField[pos];
							pos += 1;
							uint nameCRC32 = BitConverter.ToUInt32(_extraField, pos);
							pos += 4;

							CRC32 crcTest = new CRC32();
							crcTest.SlurpBlock(fileNameBytes, 0, fileNameLength);
							uint fCRC = (uint)crcTest.Crc32Result;

							if (nameCRC32 != fCRC)
							{
								return ZipReturn.ZipCentralDirError;
							}

							int charLen = blockLength - 5;

							_fileName = Encoding.UTF8.GetString(_extraField, pos, charLen);
							pos += charLen;

							break;
						default:
							pos += blockLength;
							break;
					}
				}
			}
			catch
			{
				return ZipReturn.ZipCentralDirError;
			}

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Write the central directory entry from the included stream
		/// </summary>
		/// <param name="output">Write out the data from the internal stream to the output stream</param>
		public void WriteCentralDirectory(Stream output)
		{
			// Open the output stream for writing
			BinaryWriter bw = new BinaryWriter(output);
			
			// Create an empty extra field to start out with
			List<byte> extraField = new List<byte>();

			// Now get the uncompressed size (for Zip64 compatibility)
			uint uncompressedSize32;
			if (_uncompressedSize >= 0xffffffff)
			{
				_zip64 = true;
				uncompressedSize32 = 0xffffffff;
				extraField.AddRange(BitConverter.GetBytes(_uncompressedSize));
			}
			else
			{
				uncompressedSize32 = (uint)_uncompressedSize;
			}

			// Now get the compressed size (for Zip64 compatibility)
			uint compressedSize32;
			if (_compressedSize >= 0xffffffff)
			{
				_zip64 = true;
				compressedSize32 = 0xffffffff;
				extraField.AddRange(BitConverter.GetBytes(_compressedSize));
			}
			else
			{
				compressedSize32 = (uint)_compressedSize;
			}

			// Now get the relative offset (for Zip64 compatibility)
			uint relativeOffset32;
			if (_relativeOffset >= 0xffffffff)
			{
				_zip64 = true;
				relativeOffset32 = 0xffffffff;
				extraField.AddRange(BitConverter.GetBytes(_relativeOffset));
			}
			else
			{
				relativeOffset32 = (uint)_relativeOffset;
			}

			// If we wrote anything to the extra field, set the flag and size
			if (extraField.Count > 0)
			{
				ushort extraFieldLengthInternal = (ushort)extraField.Count;
				extraField.InsertRange(0, BitConverter.GetBytes((ushort)0x0001)); // id
				extraField.InsertRange(2, BitConverter.GetBytes(extraFieldLengthInternal)); // data length
			}
			ushort extraFieldLength = (ushort)extraField.Count;

			// Now check for a unicode filename and set the flag accordingly
			byte[] fileNameBytes;
			if (Utilities.IsUnicode(_fileName))
			{
				_generalPurposeBitFlag |= GeneralPurposeBitFlag.LanguageEncodingFlag;
				fileNameBytes = Encoding.UTF8.GetBytes(_fileName);
			}
			else
			{
				fileNameBytes = Encoding.GetEncoding(858).GetBytes(_fileName);
			}
			ushort fileNameLength = (ushort)fileNameBytes.Length;

			// Set the version needed to extract according to if it's Zip64
			ushort versionNeededToExtract = (ushort)(_zip64 ? ArchiveVersion.TorrentZip64 : ArchiveVersion.TorrentZip);

			// Now, write all of the data to the stream
			bw.Write(Constants.CentralDirectoryHeaderSignature);
			bw.Write((ushort)ArchiveVersion.MSDOSandOS2);
			bw.Write(versionNeededToExtract);
			bw.Write((ushort)_generalPurposeBitFlag);
			bw.Write((ushort)_compressionMethod);
			bw.Write(_lastMod);
			bw.Write(_crc);
			bw.Write(compressedSize32);
			bw.Write(uncompressedSize32);
			bw.Write(fileNameLength);
			bw.Write(extraFieldLength);
			bw.Write((ushort)0); // File comment length
			bw.Write((ushort)0); // Disk number start
			bw.Write((ushort)0); // Internal file attributes
			bw.Write((uint)0); // External file attributes
			bw.Write(relativeOffset32);
			bw.Write(fileNameBytes, 0, fileNameLength); // Only write first bytes if longer than allowed
			bw.Write(extraField.ToArray(), 0, extraFieldLength); // Only write the first bytes if longer than allowed
			// We have no file comment, so we don't have to write more
		}

		#endregion

		#region Header

		/// <summary>
		/// Read the local file header from the input stream
		/// </summary>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn ReadHeader()
		{
			try
			{
				// We assume that the file is torrentzip until proven otherwise
				_torrentZip = true;

				// Open the stream for reading
				BinaryReader br = new BinaryReader(_zipstream);

				// Set the position of the writer based on the entry information
				br.BaseStream.Seek((long)_relativeOffset, SeekOrigin.Begin);

				// If the first bytes aren't a local file header, log and return
				if (br.ReadUInt32() != Constants.LocalFileHeaderSignature)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

				// Now read in available information, comparing to the known data
				if (br.ReadUInt16() != (ushort)_versionNeeded)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				if (br.ReadUInt16() != (ushort)_generalPurposeBitFlag)
				{
					_torrentZip = false;
				}
				if (br.ReadUInt16() != (ushort)_compressionMethod)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				if (br.ReadUInt32() != _lastMod)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				if ((_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == 0 && br.ReadUInt32() != _crc)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

				uint readCompressedSize = br.ReadUInt32();
				// If we have Zip64, the compressed size should be 0xffffffff
				if (_zip64 && readCompressedSize != 0xffffffff && readCompressedSize != _compressedSize)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				// If we have the zeroed flag set, then no size should be included
				if ((_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == GeneralPurposeBitFlag.ZeroedCRCAndSize && readCompressedSize != 0)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				// If we don't have the zeroed flag set, then the size should match
				if (!_zip64 && (_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == 0 && readCompressedSize != _compressedSize)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

				uint readUncompressedSize = br.ReadUInt32();
				// If we have Zip64, the uncompressed size should be 0xffffffff
				if (_zip64 && readUncompressedSize != 0xffffffff && readUncompressedSize != _compressedSize)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				// If we have the zeroed flag set, then no size should be included
				if ((_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == GeneralPurposeBitFlag.ZeroedCRCAndSize && readUncompressedSize != 0)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				// If we don't have the zeroed flag set, then the size should match
				if (!_zip64 && (_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == 0 && readUncompressedSize != _uncompressedSize)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

				ushort fileNameLength = br.ReadUInt16();
				ushort extraFieldLength = br.ReadUInt16();

				byte[] fileNameBytes = br.ReadBytes(fileNameLength);
				string tempFileName = ((_generalPurposeBitFlag & GeneralPurposeBitFlag.LanguageEncodingFlag) == 0
					? Encoding.GetEncoding(858).GetString(fileNameBytes)
					: Encoding.UTF8.GetString(fileNameBytes, 0, fileNameLength));

				byte[] extraField = br.ReadBytes(extraFieldLength);

				/*
				Full disclosure: this next section is in GordonJ's work but I honestly
				have no idea everything that it does. It seems to do something to figure
				out if it's Zip64, or possibly check for random things but it uses the
				extra field for this, which I do not fully understand. It's copied in
				its entirety below in the hope that it makes things better...
				*/

				_zip64 = false;
				int pos = 0;
				while (extraFieldLength > pos)
				{
					ushort type = BitConverter.ToUInt16(extraField, pos);
					pos += 2;
					ushort blockLength = BitConverter.ToUInt16(extraField, pos);
					pos += 2;
					switch (type)
					{
						case 0x0001:
							Zip64 = true;
							if (readUncompressedSize == 0xffffffff)
							{
								ulong tLong = BitConverter.ToUInt64(extraField, pos);
								if (tLong != UncompressedSize)
								{
									return ZipReturn.ZipLocalFileHeaderError;
								}
								pos += 8;
							}
							if (readCompressedSize == 0xffffffff)
							{
								ulong tLong = BitConverter.ToUInt64(extraField, pos);
								if (tLong != _compressedSize)
								{
									return ZipReturn.ZipLocalFileHeaderError;
								}
								pos += 8;
							}
							break;
						case 0x7075:
							//byte version = extraField[pos];
							pos += 1;
							uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
							pos += 4;

							CRC32 crcTest = new CRC32();
							crcTest.SlurpBlock(fileNameBytes, 0, fileNameLength);
							uint fCRC = (uint)crcTest.Crc32Result;

							if (nameCRC32 != fCRC)
							{
								return ZipReturn.ZipLocalFileHeaderError;
							}

							int charLen = blockLength - 5;

							tempFileName = Encoding.UTF8.GetString(extraField, pos, charLen);
							pos += charLen;

							break;
						default:
							pos += blockLength;
							break;
					}
				}

				// Back to code I understand
				if (!String.Equals(_fileName, tempFileName, StringComparison.InvariantCulture))
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

				// Set the position of the data
				_dataLocation = (ulong)_zipstream.Position;

				// Now if no other data should be after the data, return
				if((_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == 0)
				{
					return ZipReturn.ZipGood;
				}

				// Otherwise, compare the data after the file too
				_zipstream.Position += (long)_compressedSize;

				// If there's no subheader, read the next thing as crc
				uint tempCrc = br.ReadUInt32();
				if (tempCrc != Constants.EndOfLocalFileHeaderSignature)
				{
					tempCrc = br.ReadUInt32();
				}

				if (tempCrc != _crc)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				if (br.ReadUInt32() != _compressedSize)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
				if (br.ReadUInt32() != _uncompressedSize)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}
			}
			catch
			{
				return ZipReturn.ZipLocalFileHeaderError;
			}

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Read the local file header from the input stream, assuming correctness
		/// </summary>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn ReadHeaderQuick()
		{
			try
			{
				// We assume that the file is torrentzip until proven otherwise
				_torrentZip = true;

				// Open the stream for reading
				BinaryReader br = new BinaryReader(_zipstream);

				// Set the position of the writer based on the entry information
				br.BaseStream.Seek((long)_relativeOffset, SeekOrigin.Begin);

				// If the first bytes aren't a local file header, log and return
				if (br.ReadUInt32() != Constants.LocalFileHeaderSignature)
				{
					return ZipReturn.ZipLocalFileHeaderError;
				}

				// Now read in available information, ignoring unneeded
				_versionNeeded = (ArchiveVersion)br.ReadUInt16();
				_generalPurposeBitFlag = (GeneralPurposeBitFlag)br.ReadUInt16();

				// If the flag says there's no hash data, then we can't use quick mode
				if ((_generalPurposeBitFlag & GeneralPurposeBitFlag.ZeroedCRCAndSize) == GeneralPurposeBitFlag.ZeroedCRCAndSize)
				{
					return ZipReturn.ZipCannotFastOpen;
				}

				_compressionMethod = (CompressionMethod)br.ReadUInt16();
				_lastMod = br.ReadUInt32();
				_crc = br.ReadUInt32();
				_compressedSize = br.ReadUInt32();
				_uncompressedSize = br.ReadUInt32();

				ushort fileNameLength = br.ReadUInt16();
				ushort extraFieldLength = br.ReadUInt16();

				byte[] fileNameBytes = br.ReadBytes(fileNameLength);
				_fileName = ((_generalPurposeBitFlag & GeneralPurposeBitFlag.LanguageEncodingFlag) == 0
					? Encoding.GetEncoding(858).GetString(fileNameBytes)
					: Encoding.UTF8.GetString(fileNameBytes, 0, fileNameLength));

				byte[] extraField = br.ReadBytes(extraFieldLength);

				/*
				Full disclosure: this next section is in GordonJ's work but I honestly
				have no idea everything that it does. It seems to do something to figure
				out if it's Zip64, or possibly check for random things but it uses the
				extra field for this, which I do not fully understand. It's copied in
				its entirety below in the hope that it makes things better...
				*/

				_zip64 = false;
				int pos = 0;
				while (extraFieldLength > pos)
				{
					ushort type = BitConverter.ToUInt16(extraField, pos);
					pos += 2;
					ushort blockLength = BitConverter.ToUInt16(extraField, pos);
					pos += 2;
					switch (type)
					{
						case 0x0001:
							Zip64 = true;
							if (_uncompressedSize == 0xffffffff)
							{
								_uncompressedSize = BitConverter.ToUInt64(extraField, pos);
								pos += 8;
							}
							if (_compressedSize == 0xffffffff)
							{
								_compressedSize = BitConverter.ToUInt64(extraField, pos);
								pos += 8;
							}
							break;
						case 0x7075:
							pos += 1;
							uint nameCRC32 = BitConverter.ToUInt32(extraField, pos);
							pos += 4;

							CRC32 crcTest = new CRC32();
							crcTest.SlurpBlock(fileNameBytes, 0, fileNameLength);
							uint fCRC = (uint)crcTest.Crc32Result;

							if (nameCRC32 != fCRC)
							{
								return ZipReturn.ZipLocalFileHeaderError;
							}

							int charLen = blockLength - 5;

							FileName = Encoding.UTF8.GetString(extraField, pos, charLen);

							pos += charLen;

							break;
						default:
							pos += blockLength;
							break;
					}
				}

				// Set the position of the data
				_dataLocation = (ulong)_zipstream.Position;
			}
			catch
			{
				return ZipReturn.ZipLocalFileHeaderError;
			}

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Write the local file header entry to the included stream
		/// </summary>
		public void WriteHeader()
		{
			// Open the stream for writing
			BinaryWriter bw = new BinaryWriter(_zipstream);

			// Create an empty extra field to start out with
			List<byte> extraField = new List<byte>();

			// Figure out if we're in Zip64 based on the size
			_zip64 = _uncompressedSize >= 0xffffffff;

			// Now check for a unicode filename and set the flag accordingly
			byte[] fileNameBytes;
			if (Utilities.IsUnicode(_fileName))
			{
				_generalPurposeBitFlag |= GeneralPurposeBitFlag.LanguageEncodingFlag;
				fileNameBytes = Encoding.UTF8.GetBytes(_fileName);
			}
			else
			{
				fileNameBytes = Encoding.GetEncoding(858).GetBytes(_fileName);
			}

			// Set the version needed to extract according to if it's Zip64
			ushort versionNeededToExtract = (ushort)(_zip64 ? ArchiveVersion.TorrentZip64 : ArchiveVersion.TorrentZip);

			// Now save the relative offset and write
			_relativeOffset = (ulong)_zipstream.Position;
			bw.Write(Constants.LocalFileHeaderSignature);
			bw.Write(versionNeededToExtract);
			bw.Write((ushort)_generalPurposeBitFlag);
			bw.Write((ushort)_compressionMethod);
			bw.Write(_lastMod);

			_crc32Location = (ulong)_zipstream.Position;

			// Now, write dummy bytes for crc, compressed size, and uncompressed size
			bw.Write(0xffffffff);
			bw.Write(0xffffffff);
			bw.Write(0xffffffff);

			// If we have Zip64, add the right things to the extra field
			if (_zip64)
			{
				for (int i = 0; i < 20; i++)
				{
					extraField.Add(0);
				}
			}

			// Write out the lengths and their associated fields
			ushort fileNameLength = (ushort)fileNameBytes.Length;
			bw.Write(fileNameLength);

			ushort extraFieldLength = (ushort)extraField.Count;
			bw.Write(extraFieldLength);

			bw.Write(fileNameBytes, 0, fileNameLength);

			_extraLocation = (ulong)_zipstream.Position;
			bw.Write(extraField.ToArray(), 0, extraFieldLength);
		}

		#endregion

		#region Read and Write

		/// <summary>
		/// Open the read file stream
		/// </summary>
		/// <param name="raw">If compression mode is deflate, use the zipstream as is, otherwise decompress</param>
		/// <param name="stream">Output stream representing the correctly compressed stream</param>
		/// <param name="streamSize">Size of the stream regardless of compression</param>
		/// <param name="compressionMethod">Compression method to compare against</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn OpenReadStream(bool raw, out Stream stream, out ulong streamSize, out CompressionMethod compressionMethod, out uint lastMod)
		{
			streamSize = 0;
			compressionMethod = _compressionMethod;
			lastMod = _lastMod;

			_readStream = null;
			_zipstream.Seek((long)_dataLocation, SeekOrigin.Begin);

			switch (_compressionMethod)
			{
				case CompressionMethod.Deflated:
					if (raw)
					{
						_readStream = _zipstream;
						streamSize = _compressedSize;
					}
					else
					{
						_readStream = new DeflateStream(_zipstream, CompressionMode.Decompress, true);
						streamSize = _uncompressedSize;
					}
					break;
				case CompressionMethod.Stored:
					_readStream = _zipstream;
					streamSize = _compressedSize;
					break;
			}
			stream = _readStream;
			return (stream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood);
		}

		/// <summary>
		/// Close the read file stream
		/// </summary>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn CloseReadStream()
		{
			DeflateStream dfStream = _readStream as DeflateStream;
			if (dfStream != null)
			{
				dfStream.Close();
				dfStream.Dispose();
			}
			else
			{
				FileStream fsStream = _readStream as FileStream;
				if (fsStream != null)
				{
					fsStream.Close();
					fsStream.Dispose();
				}
			}
			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Open the write file stream
		/// </summary>
		/// <param name="raw">If compression mode is deflate, use the zipstream as is, otherwise decompress</param>
		/// <param name="torrentZip">True if outputted stream should be torrentzipped, false otherwise</param>
		/// <param name="uncompressedSize">Uncompressed size of the stream</param>
		/// <param name="compressionMethod">Compression method to compare against</param>
		/// <param name="stream">Output stream representing the correctly compressed stream</param>
		/// <param name="tzip">True if the file should use the TorrentZip date (default), false otherwise</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn OpenWriteStream(bool raw, bool torrentZip, ulong uncompressedSize, CompressionMethod compressionMethod, out Stream stream)
		{
			_uncompressedSize = uncompressedSize;
			_compressionMethod = compressionMethod;

			WriteHeader();
			_dataLocation = (ulong)_zipstream.Position;

			if (raw)
			{
				_writeStream = _zipstream;
				_torrentZip = torrentZip;
			}
			else
			{
				if (compressionMethod == CompressionMethod.Stored)
				{
					_writeStream = _zipstream;
					_torrentZip = false;
				}
				else
				{
					_writeStream = new DeflateStream(_zipstream, CompressionMode.Compress, CompressionLevel.BestCompression, true);
					_torrentZip = true;
				}
			}

			stream = _writeStream;
			return (stream == null ? ZipReturn.ZipErrorGettingDataStream : ZipReturn.ZipGood);
		}

		/// <summary>
		/// Close the write file stream
		/// </summary>
		/// <param name="crc32">CRC to assign to the current stream</param>
		/// <returns>Status of the underlying stream</returns>
		public ZipReturn CloseWriteStream(uint crc32)
		{
			DeflateStream dfStream = _writeStream as DeflateStream;
			if (dfStream != null)
			{
				dfStream.Flush();
				dfStream.Close();
				dfStream.Dispose();
			}

			_compressedSize = (ulong)_zipstream.Position - _dataLocation;

			if (_compressedSize == 0 && _uncompressedSize == 0)
			{
				AddDirectory();
				_compressedSize = (ulong)_zipstream.Position - _dataLocation;
			}

			_crc = crc32;
			WriteCompressedSize();

			return ZipReturn.ZipGood;
		}

		/// <summary>
		/// Write out the compressed size of the stream
		/// </summary>
		private void WriteCompressedSize()
		{
			// Save the current position before seeking
			long posNow = _zipstream.Position;
			_zipstream.Seek((long)_crc32Location, SeekOrigin.Begin);

			// Open the stream for writing
			BinaryWriter bw = new BinaryWriter(_zipstream);

			// Get the 32-bit compatible sizes
			uint compressedSize32;
			uint uncompressedSize32;
			if (_zip64)
			{
				compressedSize32 = 0xffffffff;
				uncompressedSize32 = 0xffffffff;
			}
			else
			{
				compressedSize32 = (uint)_compressedSize;
				uncompressedSize32 = (uint)_uncompressedSize;
			}

			// Now write the data
			bw.Write(_crc);
			bw.Write(compressedSize32);
			bw.Write(uncompressedSize32);

			// If we have Zip64, write additional data
			if (_zip64)
			{
				_zipstream.Seek((long)_extraLocation, SeekOrigin.Begin);
				bw.Write((ushort)0x0001); // id
				bw.Write((ushort)16); // data length
				bw.Write(_uncompressedSize);
				bw.Write(_compressedSize);
			}

			// Now seek back to the original position
			_zipstream.Seek(posNow, SeekOrigin.Begin);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Get the data from the current file, if not already checked
		/// </summary>
		public void Check()
		{
			// If the file has been tested or has an error, return
			if (_fileStatus != ZipReturn.ZipUntested)
			{
				return;
			}

			try
			{
				Stream stream = null;
				_zipstream.Seek((long)_dataLocation, SeekOrigin.Begin);

				switch (_compressionMethod)
				{
					case CompressionMethod.Deflated:
						stream = new DeflateStream(_zipstream, CompressionMode.Decompress, true);
						break;
					case CompressionMethod.Stored:
						stream = _zipstream;
						break;
				}

				if (stream == null)
				{
					_fileStatus = ZipReturn.ZipErrorGettingDataStream;
					return;
				}

				// Create the hashers
				uint tempCrc;
				OptimizedCRC crc = new OptimizedCRC();
				MD5 md5 = System.Security.Cryptography.MD5.Create();
				SHA1 sha1 = System.Security.Cryptography.SHA1.Create();

				// Now get the hash of the stream
				BinaryReader fs = new BinaryReader(stream);

				byte[] buffer = new byte[1024];
				int read;
				while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
				{
					crc.Update(buffer, 0, read);
					md5.TransformBlock(buffer, 0, read, buffer, 0);
					sha1.TransformBlock(buffer, 0, read, buffer, 0);
				}

				crc.Update(buffer, 0, 0);
				md5.TransformFinalBlock(buffer, 0, 0);
				sha1.TransformFinalBlock(buffer, 0, 0);

				tempCrc = crc.UnsignedValue;
				_md5 = md5.Hash;
				_sha1 = sha1.Hash;

				// Dispose of the hashers
				crc.Dispose();
				md5.Dispose();
				sha1.Dispose();

				if (_compressionMethod == CompressionMethod.Deflated)
				{
					stream.Close();
					stream.Dispose();
				}

				_fileStatus = (_crc == tempCrc ? ZipReturn.ZipGood : ZipReturn.ZipCRCDecodeError);
			}
			catch
			{
				_fileStatus = ZipReturn.ZipDecodeError;
			}
		}

		/// <summary>
		/// Add a directory marking to a local file
		/// </summary>
		public void AddDirectory()
		{
			Stream ds = _zipstream;
			ds.WriteByte(03);
			ds.WriteByte(00);
		}

		/// <summary>
		/// Check if an entry equals another (use only name for now)
		/// </summary>
		/// <param name="zfe"></param>
		/// <returns></returns>
		public bool Equals(ZipFileEntry zfe)
		{
			return (String.Equals(_fileName, zfe.FileName, StringComparison.InvariantCultureIgnoreCase));
		}

		#endregion
	}
}
