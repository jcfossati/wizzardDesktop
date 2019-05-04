using SabreTools.Library.Data;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using BinaryReader = System.IO.BinaryReader;
using SeekOrigin = System.IO.SeekOrigin;
using Stream = System.IO.Stream;
#endif

namespace SabreTools.Library.FileTypes
{
    /// <summary>
    /// This is code adapted from chd.h and chd.cpp in MAME
    /// Additional archival code from https://github.com/rtissera/libchdr/blob/master/src/chd.h
    /// </summary>
    /// <remarks>
    /// ----------------------------------------------
    /// Common CHD Header:
    /// 0x00-0x07 - CHD signature
    /// 0x08-0x0B - Header size
    /// 0x0C-0x0F - CHD version
    /// ----------------------------------------------
    /// CHD v1 header layout:
    /// 0x10-0x13 - Flags (1: Has parent MD5, 2: Disallow writes)
    /// 0x14-0x17 - Compression
    /// 0x18-0x1B - 512-byte sectors per hunk
    /// 0x1C-0x1F - Hunk count
    /// 0x20-0x23 - Hard disk cylinder count
    /// 0x24-0x27 - Hard disk head count
    /// 0x28-0x2B - Hard disk sector count
    /// 0x2C-0x3B - MD5
    /// 0x3C-0x4B - Parent MD5
    /// ----------------------------------------------
    /// CHD v2 header layout:
    /// 0x10-0x13 - Flags (1: Has parent MD5, 2: Disallow writes)
    /// 0x14-0x17 - Compression
    /// 0x18-0x1B - seclen-byte sectors per hunk
    /// 0x1C-0x1F - Hunk count
    /// 0x20-0x23 - Hard disk cylinder count
    /// 0x24-0x27 - Hard disk head count
    /// 0x28-0x2B - Hard disk sector count
    /// 0x2C-0x3B - MD5
    /// 0x3C-0x4B - Parent MD5
    /// 0x4C-0x4F - Number of bytes per sector (seclen)
    /// ----------------------------------------------
    /// CHD v3 header layout:
    /// 0x10-0x13 - Flags (1: Has parent SHA-1, 2: Disallow writes)
    /// 0x14-0x17 - Compression
    /// 0x18-0x1B - Hunk count
    /// 0x1C-0x23 - Logical Bytes
    /// 0x24-0x2C - Metadata Offset
    /// ...
    /// 0x4C-0x4F - Hunk Bytes
    /// 0x50-0x63 - SHA-1
    /// 0x64-0x77 - Parent SHA-1
    /// 0x78-0x87 - Map
    /// ----------------------------------------------
    /// CHD v4 header layout:
    /// 0x10-0x13 - Flags (1: Has parent SHA-1, 2: Disallow writes)
    /// 0x14-0x17 - Compression
    /// 0x18-0x1B - Hunk count
    /// 0x1C-0x23 - Logical Bytes
    /// 0x24-0x2C - Metadata Offset
    /// ...
    /// 0x2C-0x2F - Hunk Bytes
    /// 0x30-0x43 - SHA-1
    /// 0x44-0x57 - Parent SHA-1
    /// 0x58-0x6b - Raw SHA-1
    /// 0x6c-0x7b - Map
    /// ----------------------------------------------
    /// CHD v5 header layout:
    /// 0x10-0x13 - Compression format 1
    /// 0x14-0x17 - Compression format 2
    /// 0x18-0x1B - Compression format 3
    /// 0x1C-0x1F - Compression format 4
    /// 0x20-0x27 - Logical Bytes
    /// 0x28-0x2F - Map Offset
    /// 0x30-0x37 - Metadata Offset
    /// 0x38-0x3B - Hunk Bytes
    /// 0x3C-0x3F - Unit Bytes
    /// 0x40-0x53 - Raw SHA-1
    /// 0x54-0x67 - SHA-1
    /// 0x68-0x7b - Parent SHA-1
    /// ----------------------------------------------
    /// </remarks>
    public class CHDFile : BaseFile
    {
        #region Private instance variables

        // Core parameters from the header
        private byte[] m_signature;      // signature
        private uint m_headersize;       // size of the header
        private uint m_version;          // version of the header
        private ulong m_logicalbytes;    // logical size of the raw CHD data in bytes
        private ulong m_mapoffset;       // offset of map
        private ulong m_metaoffset;      // offset to first metadata bit
        private uint m_sectorsperhunk;   // number of sectors per hunk
        private uint m_hunkbytes;        // size of each raw hunk in bytes
        private ulong m_hunkcount;       // number of hunks represented
        private uint m_unitbytes;        // size of each unit in bytes
        private ulong m_unitcount;       // number of units represented
        private CHD_CODEC[] m_compression = new CHD_CODEC[4];   // array of compression types used

        // map information
        private uint m_mapentrybytes;    // length of each entry in a map

        // additional required vars
        private uint? _headerVersion;
        private BinaryReader m_br;       // Binary reader representing the CHD stream

        #endregion

        #region Pubically facing variables

        public uint? Version
        {
            get
            {
                if (_headerVersion == null)
                {
                    _headerVersion = ValidateHeaderVersion();
                }

                return _headerVersion;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Create a new, blank CHDFile
        /// </summary>
        public CHDFile()
        {
            this.Type = FileType.CHD;
        }

        /// <summary>
        /// Create a new CHDFile from an input file
        /// </summary>
        /// <param name="filename"></param>
        public CHDFile(string filename)
            : this(Utilities.TryOpenRead(filename))
        {
        }

        /// <summary>
        /// Create a new CHDFile from an input stream
        /// </summary>
        /// <param name="chdstream">Stream representing the CHD file</param>
        public CHDFile(Stream chdstream)
        {
            this.Type = FileType.CHD;
            m_br = new BinaryReader(chdstream);

            _headerVersion = ValidateHeaderVersion();

            if (_headerVersion != null)
            {
                byte[] hash = GetHashFromHeader();

                if (hash != null)
                {
                    if (hash.Length == Constants.MD5Length)
                    {
                        this.MD5 = hash;
                    }
                    else if (hash.Length == Constants.SHA1Length)
                    {
                        this.SHA1 = hash;
                    }
                }
            }
        }

        #endregion

        #region Header Parsing

        /// <summary>
        /// Validate the initial signature, version, and header size
        /// </summary>
        /// <returns>Unsigned int containing the version number, null if invalid</returns>
        private uint? ValidateHeaderVersion()
        {
            try
            {
                // Seek to the beginning to make sure we're reading the correct bytes
                m_br.BaseStream.Seek(0, SeekOrigin.Begin);

                // Read and verify the CHD signature
                m_signature = m_br.ReadBytes(8);

                // If no signature could be read, return null
                if (m_signature == null || m_signature.Length == 0)
                {
                    return null;
                }

                if (!m_signature.StartsWith(Constants.CHDSignature, exact: true))
                {
                    // throw CHDERR_INVALID_FILE;
                    return null;
                }

                // Get the header size and version
                m_headersize = m_br.ReadUInt32Reverse();
                m_version = m_br.ReadUInt32Reverse();

                // If we have an invalid combination of size and version
                if ((m_version == 1 && m_headersize != Constants.CHD_V1_HEADER_SIZE)
                    || (m_version == 2 && m_headersize != Constants.CHD_V2_HEADER_SIZE)
                    || (m_version == 3 && m_headersize != Constants.CHD_V3_HEADER_SIZE)
                    || (m_version == 4 && m_headersize != Constants.CHD_V4_HEADER_SIZE)
                    || (m_version == 5 && m_headersize != Constants.CHD_V5_HEADER_SIZE)
                    || (m_version < 1 || m_version > 5))
                {
                    // throw CHDERR_UNSUPPORTED_VERSION;
                    return null;
                }

                return m_version;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the internal MD5 (v1, v2) or SHA-1 (v3, v4, v5) from the CHD
        /// </summary>
        /// <returns>MD5 as a byte array, null on error</returns>
        private byte[] GetHashFromHeader()
        {
            // Validate the header by default just in case
            uint? version = ValidateHeaderVersion();

            // Now get the hash, if possible
            byte[] hash;

            // Now parse the rest of the header according to the version
            try
            {
                switch (version)
                {
                    case 1:
                        hash = ParseCHDv1Header();
                        break;
                    case 2:
                        hash = ParseCHDv2Header();
                        break;
                    case 3:
                        hash = ParseCHDv3Header();
                        break;
                    case 4:
                        hash = ParseCHDv4Header();
                        break;
                    case 5:
                        hash = ParseCHDv5Header();
                        break;
                    case null:
                    default:
                        // throw CHDERR_INVALID_FILE;
                        return null;
                }
            }
            catch
            {
                // throw CHDERR_INVALID_FILE;
                return null;
            }

            return hash;
        }

        /// <summary>
        /// Parse a CHD v1 header
        /// </summary>
        /// <returns>The extracted MD5 on success, null otherwise</returns>
        private byte[] ParseCHDv1Header()
        {
            // Seek to after the signature to make sure we're reading the correct bytes
            m_br.BaseStream.Seek(16, SeekOrigin.Begin);

            // Set the blank MD5 hash
            byte[] md5 = new byte[16];

            // Set offsets and defaults
            m_mapoffset = 0;
            m_mapentrybytes = 0;

            // Read the CHD flags
            uint flags = m_br.ReadUInt32Reverse();

            // Determine compression
            switch (m_br.ReadUInt32())
            {
                case 0: m_compression[0] = CHD_CODEC.NONE; break;
                case 1: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 2: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 3: m_compression[0] = CHD_CODEC.AVHUFF; break;
                default: /* throw CHDERR_UNKNOWN_COMPRESSION; */ return null;
            }

            m_compression[1] = m_compression[2] = m_compression[3] = CHD_CODEC.NONE;

            m_sectorsperhunk = m_br.ReadUInt32Reverse();
            m_hunkcount = m_br.ReadUInt32Reverse();
            m_br.ReadUInt32Reverse(); // Cylinder count
            m_br.ReadUInt32Reverse(); // Head count
            m_br.ReadUInt32Reverse(); // Sector count

            md5 = m_br.ReadBytes(16);
            m_br.ReadBytes(16); // Parent MD5

            return md5;
        }

        /// <summary>
        /// Parse a CHD v2 header
        /// </summary>
        /// <returns>The extracted MD5 on success, null otherwise</returns>
        private byte[] ParseCHDv2Header()
        {
            // Seek to after the signature to make sure we're reading the correct bytes
            m_br.BaseStream.Seek(16, SeekOrigin.Begin);

            // Set the blank MD5 hash
            byte[] md5 = new byte[16];

            // Set offsets and defaults
            m_mapoffset = 0;
            m_mapentrybytes = 0;

            // Read the CHD flags
            uint flags = m_br.ReadUInt32Reverse();

            // Determine compression
            switch (m_br.ReadUInt32())
            {
                case 0: m_compression[0] = CHD_CODEC.NONE; break;
                case 1: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 2: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 3: m_compression[0] = CHD_CODEC.AVHUFF; break;
                default: /* throw CHDERR_UNKNOWN_COMPRESSION; */ return null;
            }

            m_compression[1] = m_compression[2] = m_compression[3] = CHD_CODEC.NONE;

            m_sectorsperhunk = m_br.ReadUInt32Reverse();
            m_hunkcount = m_br.ReadUInt32Reverse();
            m_br.ReadUInt32Reverse(); // Cylinder count
            m_br.ReadUInt32Reverse(); // Head count
            m_br.ReadUInt32Reverse(); // Sector count

            md5 = m_br.ReadBytes(16);
            m_br.ReadBytes(16); // Parent MD5
            m_br.ReadUInt32Reverse(); // Sector size

            return md5;
        }

        /// <summary>
        /// Parse a CHD v3 header
        /// </summary>
        /// <returns>The extracted SHA-1 on success, null otherwise</returns>
        private byte[] ParseCHDv3Header()
        {
            // Seek to after the signature to make sure we're reading the correct bytes
            m_br.BaseStream.Seek(16, SeekOrigin.Begin);

            // Set the blank SHA-1 hash
            byte[] sha1 = new byte[20];

            // Set offsets and defaults
            m_mapoffset = 120;
            m_mapentrybytes = 16;

            // Read the CHD flags
            uint flags = m_br.ReadUInt32Reverse();

            // Determine compression
            switch (m_br.ReadUInt32())
            {
                case 0: m_compression[0] = CHD_CODEC.NONE; break;
                case 1: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 2: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 3: m_compression[0] = CHD_CODEC.AVHUFF; break;
                default: /* throw CHDERR_UNKNOWN_COMPRESSION; */ return null;
            }

            m_compression[1] = m_compression[2] = m_compression[3] = CHD_CODEC.NONE;

            m_hunkcount = m_br.ReadUInt32Reverse();
            m_logicalbytes = m_br.ReadUInt64Reverse();
            m_metaoffset = m_br.ReadUInt32Reverse();

            m_br.BaseStream.Seek(76, SeekOrigin.Begin);
            m_hunkbytes = m_br.ReadUInt32Reverse();

            m_br.BaseStream.Seek(Constants.CHDv3SHA1Offset, SeekOrigin.Begin);
            sha1 = m_br.ReadBytes(20);

            // guess at the units based on snooping the metadata
            // m_unitbytes = guess_unitbytes();
            m_unitcount = (m_logicalbytes + m_unitbytes - 1) / m_unitbytes;

            return sha1;
        }

        /// <summary>
        /// Parse a CHD v4 header
        /// </summary>
        /// <returns>The extracted SHA-1 on success, null otherwise</returns>
        private byte[] ParseCHDv4Header()
        {
            // Seek to after the signature to make sure we're reading the correct bytes
            m_br.BaseStream.Seek(16, SeekOrigin.Begin);

            // Set the blank SHA-1 hash
            byte[] sha1 = new byte[20];

            // Set offsets and defaults
            m_mapoffset = 108;
            m_mapentrybytes = 16;

            // Read the CHD flags
            uint flags = m_br.ReadUInt32Reverse();

            // Determine compression
            switch (m_br.ReadUInt32())
            {
                case 0: m_compression[0] = CHD_CODEC.NONE; break;
                case 1: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 2: m_compression[0] = CHD_CODEC.ZLIB; break;
                case 3: m_compression[0] = CHD_CODEC.AVHUFF; break;
                default: /* throw CHDERR_UNKNOWN_COMPRESSION; */ return null;
            }

            m_compression[1] = m_compression[2] = m_compression[3] = CHD_CODEC.NONE;

            m_hunkcount = m_br.ReadUInt32Reverse();
            m_logicalbytes = m_br.ReadUInt64Reverse();
            m_metaoffset = m_br.ReadUInt32Reverse();

            m_br.BaseStream.Seek(44, SeekOrigin.Begin);
            m_hunkbytes = m_br.ReadUInt32Reverse();

            m_br.BaseStream.Seek(Constants.CHDv4SHA1Offset, SeekOrigin.Begin);
            sha1 = m_br.ReadBytes(20);

            // guess at the units based on snooping the metadata
            // m_unitbytes = guess_unitbytes();
            m_unitcount = (m_logicalbytes + m_unitbytes - 1) / m_unitbytes;
            return sha1;
        }

        /// <summary>
        /// Parse a CHD v5 header
        /// </summary>
        /// <returns>The extracted SHA-1 on success, null otherwise</returns>
        private byte[] ParseCHDv5Header()
        {
            // Seek to after the signature to make sure we're reading the correct bytes
            m_br.BaseStream.Seek(16, SeekOrigin.Begin);

            // Set the blank SHA-1 hash
            byte[] sha1 = new byte[20];

            // Determine compression
            m_compression[0] = (CHD_CODEC)m_br.ReadUInt32Reverse();
            m_compression[1] = (CHD_CODEC)m_br.ReadUInt32Reverse();
            m_compression[2] = (CHD_CODEC)m_br.ReadUInt32Reverse();
            m_compression[3] = (CHD_CODEC)m_br.ReadUInt32Reverse();

            m_logicalbytes = m_br.ReadUInt64Reverse();
            m_mapoffset = m_br.ReadUInt64Reverse();
            m_metaoffset = m_br.ReadUInt64Reverse();
            m_hunkbytes = m_br.ReadUInt32Reverse();
            m_hunkcount = (m_logicalbytes + m_hunkbytes - 1) / m_hunkbytes;
            m_unitbytes = m_br.ReadUInt32Reverse();
            m_unitcount = (m_logicalbytes + m_unitbytes - 1) / m_unitbytes;

            // m_allow_writes = !compressed();

            // determine properties of map entries
            // m_mapentrybytes = compressed() ? 12 : 4;

            m_br.BaseStream.Seek(Constants.CHDv5SHA1Offset, SeekOrigin.Begin);
            sha1 = m_br.ReadBytes(20);
            return sha1;
        }

        #endregion
    }
}
