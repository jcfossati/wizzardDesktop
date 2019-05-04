using SabreTools.Library.Data;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using Stream = System.IO.Stream;
#endif

namespace SabreTools.Library.FileTypes
{
    public class BaseFile
    {
        #region Publicly facing variables

        // TODO: Get all of these values automatically so there is no public "set"
        public FileType Type { get; protected set; }
        public string Filename { get; set; }
        public string Parent { get; set; }
        public string Date { get; set; }
        public long? Size { get; set; }
        public byte[] CRC { get; set; }
        public byte[] MD5 { get; set; }
        public byte[] SHA1 { get; set; }
        public byte[] SHA256 { get; set; }
        public byte[] SHA384 { get; set; }
        public byte[] SHA512 { get; set; }

        #endregion

        #region Construtors

        /// <summary>
        /// Create a new BaseFile with no base file
        /// </summary>
        public BaseFile()
        {
        }

        /// <summary>
        /// Create a new BaseFile from the given file
        /// </summary>
        /// <param name="filename">Name of the file to use</param>
        /// <param name="getHashes">True if hashes for this file should be calculated (default), false otherwise</param>
        public BaseFile(string filename, bool getHashes = true)
        {
            this.Filename = filename;

            if (getHashes)
            {
                BaseFile temp = Utilities.GetFileInfo(this.Filename);

                if (temp != null)
                {
                    this.Parent = temp.Parent;
                    this.Date = temp.Date;
                    this.CRC = temp.CRC;
                    this.MD5 = temp.MD5;
                    this.SHA1 = temp.SHA1;
                    this.SHA256 = temp.SHA256;
                    this.SHA384 = temp.SHA384;
                    this.SHA512 = temp.SHA512;
                }
            }
        }

        /// <summary>
        /// Create a new BaseFile from the given file
        /// </summary>
        /// <param name="filename">Name of the file to use</param>
        /// <param name="stream">Stream to populate information from</param>
        /// <param name="getHashes">True if hashes for this file should be calculated (default), false otherwise</param>
        public BaseFile(string filename, Stream stream, bool getHashes = true)
        {
            this.Filename = filename;

            if (getHashes)
            {
                BaseFile temp = Utilities.GetStreamInfo(stream, stream.Length);

                if(temp != null)
                {
                    this.Parent = temp.Parent;
                    this.Date = temp.Date;
                    this.CRC = temp.CRC;
                    this.MD5 = temp.MD5;
                    this.SHA1 = temp.SHA1;
                    this.SHA256 = temp.SHA256;
                    this.SHA384 = temp.SHA384;
                    this.SHA512 = temp.SHA512;
                }
            }
                
        }

        /// <summary>
        /// Create a new BaseFile from the given metadata
        /// </summary>
        /// <param name="filename">Name of the file to use</param>
        /// <param name="parent">Parent folder or archive</param>
        /// <param name="date">File date</param>
        /// <param name="crc">CRC hash as a byte array</param>
        /// <param name="md5">MD5 hash as a byte array</param>
        /// <param name="sha1">SHA-1 hash as a byte array</param>
        /// <param name="sha256">SHA-256 hash as a byte array</param>
        /// <param name="sha384">SHA-384 hash as a byte array</param>
        /// <param name="sha512">SHA-512 hash as a byte array</param>
        public BaseFile(string filename, string parent, string date, byte[] crc, byte[] md5, byte[] sha1, byte[] sha256, byte[] sha384, byte[] sha512)
        {
            this.Filename = filename;
            this.Parent = parent;
            this.Date = date;
            this.CRC = crc;
            this.MD5 = md5;
            this.SHA1 = sha1;
            this.SHA256 = sha256;
            this.SHA384 = sha384;
            this.SHA512 = sha512;
        }

        #endregion
    }
}
