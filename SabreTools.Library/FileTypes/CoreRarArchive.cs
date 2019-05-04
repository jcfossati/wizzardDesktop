using System;
using System.Collections.Generic;
using System.IO;
using SabreTools.Library.Data;
using SabreTools.Library.DatItems;

/// <summary>
/// This code is based on the header format described at http://www.rarlab.com/technote.htm#srvheaders
/// </summary>
/// <remarks>
/// ---------------------------------------------
/// vint
///
/// Variable length integer. Can include one or more bytes, where lower 7 bits of every byte contain integer data
/// and highest bit in every byte is the continuation flag.If highest bit is 0, this is the last byte in sequence.
/// So first byte contains 7 least significant bits of integer and continuation flag. Second byte, if present,
/// contains next 7 bits and so on.
///
/// Currently RAR format uses vint to store up to 64 bit integers, resulting in 10 bytes maximum. This value may
/// be increased in the future if necessary for some reason.
/// 
/// Sometimes RAR needs to pre-allocate space for vint before knowing its exact value. In such situation it can
/// allocate more space than really necessary and then fill several leading bytes with 0x80 hexadecimal, which means
/// 0 with continuation flag set.
/// ----------------------------------------------
/// General archive layout:
/// 
/// Self-extracting module(optional) (RAR assumes the maximum SFX module size to not exceed 1 MB, but this value
///		can be increased in the future.
/// RAR 5.0 signature (RAR 5.0 signature consists of 8 bytes: 0x52 0x61 0x72 0x21 0x1A 0x07 0x01 0x00.
///		You need to search for this signature in supposed archive from beginning and up to maximum SFX
///		module size. Just for comparison this is RAR 4.x 7 byte length signature: 0x52 0x61 0x72 0x21 0x1A 0x07 0x00.)
/// Archive encryption header(optional)
/// Main archive header
/// Archive comment service header(optional)
/// File header 1
/// Service headers(NTFS ACL, streams, etc.) for preceding file(optional).
/// ...
/// File header N
/// Service headers(NTFS ACL, streams, etc.) for preceding file(optional).
/// Recovery record(optional).
/// End of archive header.
/// ----------------------------------------------
/// General archive block format:
/// 
/// Header CRC32: uint32 (CRC32 of header data starting from Header size field and up to and including the optional extra area.)
/// Header size: vint (Size of header data starting from Header type field and up to and including the optional extra area.
///		This field must not be longer than 3 bytes in current implementation, resulting in 2 MB maximum header size.)
/// Header type: vint (Type of archive header. Possible values are: )
///		1   Main archive header.
///		2   File header.
///		3   Service header.
///		4   Archive encryption header.
///		5   End of archive header.
/// Header flags: vint (Flags common for all headers:)
///		0x0001   Extra area is present in the end of header. 
///		0x0002   Data area is present in the end of header.
///		0x0004   Blocks with unknown type and this flag must be skipped when updating an archive. 
///		0x0008   Data area is continuing from previous volume. 
///		0x0010   Data area is continuing in next volume. 
///		0x0020   Block depends on preceding file block. 
///		0x0040   Preserve a child block if host block is modified.
///	Extra area size: vint (Size of extra area. Optional field, present only if 0x0001 header flag is set.)
///	Data size: vint (Size of data area. Optional field, present only if 0x0002 header flag is set.)
///	...: ... (Fields specific for current block type. See concrete block type descriptions for details)
///	Extra data: ... (Optional area containing additional header fields, present only if 0x0001 header flag is set.)
///	Data area: vint (Optional data area, present only if 0x0002 header flag is set. Used to store large data amounts, such as
///		compressed file data. Not counted in Header CRC and Header size fields.
/// ----------------------------------------------
/// General extra area format
/// 
/// Size: vint (Size of record data starting from Type.)
/// Type: vint (Record type. Different archive blocks have different associated extra area record types. Read the
///		concrete archive block description for details. New record types can be added in the future, so unknown
///		record types need to be skipped without interrupting an operation.)
///	Data: ... (Record dependent data. May be missing if record consists only from size and type.)
/// ----------------------------------------------
/// Archive encryption header:
/// 
/// Header CRC32: uint32
/// Header size: vint
/// Header type: vint (4)
/// Header flags: vint
/// Encryption version: vint (Version of encryption algorithm. Now only 0 version(AES-256) is supported.)
/// Encryption flags: vint
///		0x0001   Password check data is present.
/// KDF count: 1 byte (Binary logarithm of iteration number for PBKDF2 function.RAR can refuse to process
///		KDF count exceeding some threshold. Concrete value of threshold is a version dependent.)
/// Salt: 16 bytes (Salt value used globally for all encrypted archive headers.)
/// Check value: 12 bytes (Value used to verify the password validity. Present only if 0x0001 encryption
///		flag is set.First 8 bytes are calculated using additional PBKDF2 rounds, 4 last bytes is the additional
///		checksum. Together with the standard header CRC32 we have 64 bit checksum to reliably verify this field
///		integrity and distinguish invalid password and damaged data. Further details can be found in UnRAR source code.)
/// ----------------------------------------------
/// Main archive header:
/// 
/// Header CRC32: uint32 (CRC32 of header data starting from Header size field and up to and including the optional extra area.)
/// Header size: vint (Size of header data starting from Header type field and up to and including the optional extra area. This field must not be longer than 3 bytes in current implementation, resulting in 2 MB maximum header size.)
/// Header type: vint (1)
/// Header flags: vint (Flags common for all headers)
/// Extra area size: vint (Size of extra area. Optional field, present only if 0x0001 header flag is set.)
/// Archive flags: vint 
///		0x0001   Volume.Archive is a part of multivolume set.
///		0x0002   Volume number field is present.This flag is present in all volumes except first.
///		0x0004   Solid archive.
///		0x0008   Recovery record is present.
///		0x0010   Locked archive.
/// Volume number: vint (Optional field, present only if 0x0002 archive flag is set. Not present for first volume,
///		1 for second volume, 2 for third and so on.)
/// Extra area: ... (Optional area containing additional header fields, present only if 0x0001 header flag is set.)
/// [Extra area of main archive header can contain following record types
/// Type Name    Description
/// 0x01	Locator Contains positions of different service blocks, so they can be accessed quickly, without scanning
///		the entire archive.This record is optional.If it is missing, it is still necessary to scan the entire archive
///		to verify presence of service blocks.]
/// ----------------------------------------------
/// </remarks>
namespace SabreTools.Library.FileTypes
{
    public class CoreRarArchive : BaseArchive
    {
        // SFX Module Information
        public byte[] SFX;

        // Standard Header Information
        public uint HeaderCRC32;
        public uint HeaderSize; // vint
        public RarHeaderFlags HeaderFlags; // vint
        public uint ExtraAreaSize; // vint
        public RarArchiveFlags ArchiveFlags; // vint
        public uint VolumeNumber; // vint
        public byte[] ExtraArea;

        // Encryption Header Information
        public uint EncryptionHeaderCRC32;
        public uint EncryptionHeaderSize; // vint
        public RarHeaderFlags EncryptionHeaderFlags; // vint
        public uint EncryptionVersion; // vint
        public uint EncryptionFlags; // vint
        public byte KDFCount;
        public byte[] Salt = new byte[16];
        public byte[] CheckValue = new byte[12];

        // Locator Information
        public uint LocatorSize; // vint
        public uint LocatorFlags; // vint
        public uint QuickOpenOffset; // vint
        public uint RecoveryRecordOffset; // vint

        // Entry Information
        public List<CoreRarArchiveEntry> Entries = new List<CoreRarArchiveEntry>();

        #region Unimplemented methods

        public override bool CopyAll(string outDir)
        {
            throw new NotImplementedException();
        }

        public override string CopyToFile(string entryName, string outDir)
        {
            throw new NotImplementedException();
        }

        public override (MemoryStream, string) CopyToStream(string entryName)
        {
            throw new NotImplementedException();
        }

        public override List<BaseFile> GetChildren(Hash omitFromScan = Hash.DeepHashes, bool date = false)
        {
            throw new NotImplementedException();
        }

        public override List<string> GetEmptyFolders()
        {
            throw new NotImplementedException();
        }

        public override bool IsTorrent()
        {
            throw new NotImplementedException();
        }

        public override bool Write(string inputFile, string outDir, Rom rom, bool date = false, bool romba = false)
        {
            throw new NotImplementedException();
        }

        public override bool Write(Stream inputStream, string outDir, Rom rom, bool date = false, bool romba = false)
        {
            throw new NotImplementedException();
        }

        public override bool Write(List<string> inputFiles, string outDir, List<Rom> roms, bool date = false, bool romba = false)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class CoreRarArchiveEntry
    {
        // Standard Entry Information
        public uint HeaderCRC32;
        public uint HeaderSize; // vint
        public RarHeaderType HeaderType; // vint
        public RarHeaderFlags HeaderFlags; // vint
        public uint ExtraAreaSize; // vint
        public uint DataAreaSize; // vint
        public RarFileFlags FileFlags; // vint
        public uint UnpackedSize; // vint
        public uint Attributes; // vint
        public uint mtime;
        public uint DataCRC32;
        public uint CompressionInformation; // vint
        public uint HostOS; // vint
        public uint NameLength; // vint
        public byte[] Name;
        public byte[] DataArea;

        // File Encryption Information
        public uint EncryptionSize; // vint
        public RarEncryptionFlags EncryptionFlags; // vint
        public byte KDFCount;
        public byte[] Salt = new byte[16];
        public byte[] IV = new byte[16];
        public byte[] CheckValue = new byte[12];

        // File Hash Information
        public uint HashSize; // vint
        public uint HashType; // vint
        public byte[] HashData = new byte[32];

        // File Time Information
        public uint TimeSize; // vint
        public RarTimeFlags TimeFlags; // vint
        public uint TimeMtime;
        public ulong TimeMtime64;
        public uint TimeCtime;
        public ulong TimeCtime64;
        public uint TimeLtime;
        public ulong TimeLtime64;

        // File Version Information
        public uint VersionSize; // vint
        public const uint VersionFlags = 0; // vint
        public uint VersionNumber; // vint

        // File System Redirection Record
        public uint RedirectionSize; // vint
        public RarRedirectionType RedirectionType; // vint
        public uint RedirectionFlags; // vint
        public uint RedirectionNameLength; // vint
        public byte[] RedirectionName;

        // Unix Owner Record
        public uint UnixOwnerSize; // vint
        public RarUnixOwnerRecordFlags UnixOwnerFlags; // vint
        public uint UnixOwnerUserNameLength; // vint
        public byte[] UnixOwnerUserName;
        public uint UnixOwnerGroupNameLength; // vint
        public byte[] UnixOwnerGroupName;
        public uint UnixOwnerUserId; // vint
        public uint UnixOwnerGroupId; // vint

        // Service Data Information
        public uint ServiceSize; // vint
        public byte[] ServiceData;
    }

    // BELOW ARE CONCRETE IMPLEMENTATIONS OF HEADER DETAILS

    /// <summary>
    /// General archive block format used by all RAR block types
    /// </summary>
    public class GeneralArchiveBlockFormat
    {
        public uint HeaderCRC32;
        public uint HeaderSize; // vint
        public HeaderType HeaderType;
        public HeaderFlags HeaderFlags;
        public ulong ExtraAreaSize; // vint
        public ulong DataAreaSize; // vint
        public byte[] ExtraArea;
        public byte[] DataArea;
    }

    /// <summary>
    /// General extra area format used by all RAR extra area records
    /// </summary>
    public class GeneralExtraAreaFormat
    {
        public ulong Size; // vint
        public ulong Type; // vint
        public byte[] Data;
    }

    /// <summary>
    /// Encryption header only present in encrypted archives
    /// 
    /// Every proceeding header is started from 16 byte AES-256
    /// initialization vectors followed by encrypted header data
    /// </summary>
    public class ArchiveEncryptionHeader : GeneralArchiveBlockFormat
    {
        public new HeaderType HeaderType = HeaderType.ArchiveEncryptionHeader;
        public ulong EncryptionVersion; // vint
        public ulong EncryptionFlags; // vint
    }

    /// <summary>
    /// Types of archive header
    /// </summary>
    public enum HeaderType : ulong // vint
    {
        MainArchiveHeader = 1,
        FileHeader = 2,
        ServiceHeader = 3,
        ArchiveEncryptionHeader = 4,
        EndOfArchiveHeader = 5,
    }

    /// <summary>
    /// Flags common for all headers
    /// </summary>
    [Flags]
    public enum HeaderFlags : ulong // vint
    {
        ExtraAreaIsPresentInEndOfHeader = 0x0001,
        DataAreaIsPresentInEndOfHeader = 0x0002,
        BlocksWithUnknownType = 0x0004, // this flag must be skipped when updating an archive
        DataAreaIsContinuingFromPreviousVolume = 0x0008,
        DataAreaIsContinuingInNextVolume = 0x0010,
        BlockDependsOnPrecedingFileBlock = 0x0020,
        PreserveChildBlockIfHostBlockIsModified = 0x0040,
    }
}
