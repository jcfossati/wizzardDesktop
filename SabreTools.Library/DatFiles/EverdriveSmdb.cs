﻿using System;
using System.Collections.Generic;
using System.Text;
using SabreTools.Library.Data;
using SabreTools.Library.DatItems;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using Alphaleonis.Win32.Filesystem;

using FileStream = System.IO.FileStream;
using StreamReader = System.IO.StreamReader;
using StreamWriter = System.IO.StreamWriter;
#endif
using NaturalSort;

namespace SabreTools.Library.DatFiles
{
    /// <summary>
    /// Represents parsing and writing of an Everdrive SMDB file
    /// </summary>
    internal class EverdriveSMDB : DatFile
    {
        /// <summary>
        /// Constructor designed for casting a base DatFile
        /// </summary>
        /// <param name="datFile">Parent DatFile to copy from</param>
        public EverdriveSMDB(DatFile datFile)
            : base(datFile, cloneHeader: false)
        {
        }

        /// <summary>
        /// Parse an Everdrive SMDB file and return all found games within
        /// </summary>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="sysid">System ID for the DAT</param>
        /// <param name="srcid">Source ID for the DAT</param>
        /// <param name="keep">True if full pathnames are to be kept, false otherwise (default)</param>
        /// <param name="clean">True if game names are sanitized, false otherwise (default)</param>
        /// <param name="remUnicode">True if we should remove non-ASCII characters from output, false otherwise (default)</param>
        public override void ParseFile(
            // Standard Dat parsing
            string filename,
            int sysid,
            int srcid,

            // Miscellaneous
            bool keep,
            bool clean,
            bool remUnicode)
        {
            // Open a file reader
            Encoding enc = Utilities.GetEncoding(filename);
            StreamReader sr = new StreamReader(Utilities.TryOpenRead(filename), enc);

            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();

                /*
                The gameinfo order is as follows
                0 - SHA-256
                1 - Machine Name/Filename
                2 - SHA-1
                3 - MD5
                4 - CRC32
                */

                string[] gameinfo = line.Split('\t');
                string[] fullname = gameinfo[1].Split('/');

                Rom rom = new Rom
                {
                    Name = gameinfo[1].Substring(fullname[0].Length + 1),
                    Size = -1, // No size provided, but we don't want the size being 0
                    CRC = Utilities.CleanHashData(gameinfo[4], 8),
                    MD5 = Utilities.CleanHashData(gameinfo[3], 32),
                    SHA1 = Utilities.CleanHashData(gameinfo[2], 40),
                    SHA256 = Utilities.CleanHashData(gameinfo[0], 64),
                    ItemStatus = ItemStatus.None,

                    MachineName = fullname[0],
                    MachineDescription = fullname[0],
                };

                // Now process and add the rom
                ParseAddHelper(rom, clean, remUnicode);
            }

            sr.Dispose();
        }

        /// <summary>
        /// Create and open an output file for writing direct from a dictionary
        /// </summary>
        /// <param name="outfile">Name of the file to write to</param>
        /// <param name="ignoreblanks">True if blank roms should be skipped on output, false otherwise (default)</param>
        /// <returns>True if the DAT was written correctly, false otherwise</returns>
        public override bool WriteToFile(string outfile, bool ignoreblanks = false)
        {
            try
            {
                Globals.Logger.User("Opening file for writing: {0}", outfile);
                FileStream fs = Utilities.TryCreate(outfile);

                // If we get back null for some reason, just log and return
                if (fs == null)
                {
                    Globals.Logger.Warning("File '{0}' could not be created for writing! Please check to see if the file is writable", outfile);
                    return false;
                }

                StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(false));

                // Get a properly sorted set of keys
                List<string> keys = Keys;
                keys.Sort(new NaturalComparer());

                foreach (string key in keys)
                {
                    List<DatItem> roms = this[key];

                    // Resolve the names in the block
                    roms = DatItem.ResolveNames(roms);

                    for (int index = 0; index < roms.Count; index++)
                    {
                        DatItem item = roms[index];

                        // There are apparently times when a null rom can skip by, skip them
                        if (item.Name == null || item.MachineName == null)
                        {
                            Globals.Logger.Warning("Null rom found!");
                            continue;
                        }

                        // If we have a "null" game (created by DATFromDir or something similar), log it to file
                        if (item.ItemType == ItemType.Rom
                            && ((Rom)item).Size == -1
                            && ((Rom)item).CRC == "null")
                        {
                            Globals.Logger.Verbose("Empty folder found: {0}", item.MachineName);

                            item.Name = (item.Name == "null" ? "-" : item.Name);
                            ((Rom)item).Size = Constants.SizeZero;
                        }

                        WriteDatItem(sw, item, ignoreblanks);
                    }
                }

                Globals.Logger.Verbose("File written!" + Environment.NewLine);
                sw.Dispose();
                fs.Dispose();
            }
            catch (Exception ex)
            {
                Globals.Logger.Error(ex.ToString());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Write out Game start using the supplied StreamWriter
        /// </summary>
        /// <param name="sw">StreamWriter to output to</param>
        /// <param name="rom">DatItem object to be output</param>
        /// <returns>True if the data was written, false on error</returns>
        private bool WriteDatItem(StreamWriter sw, DatItem rom, bool ignoreblanks = false)
        {
            // If we are in ignore blanks mode AND we have a blank (0-size) rom, skip
            if (ignoreblanks
                && (rom.ItemType == ItemType.Rom
                && (((Rom)rom).Size == 0 || ((Rom)rom).Size == -1)))
            {
                return true;
            }

            try
            {
                // No game should start with a path separator
                if (rom.MachineName.StartsWith(Path.DirectorySeparatorChar.ToString()))
                    rom.MachineName = rom.MachineName.Substring(1);

                // If the DatItem isn't a rom, we don't output it
                if (rom.ItemType != ItemType.Rom)
                    return true;

                Rom temp = rom as Rom;
                string state = (!ExcludeFields[(int)Field.SHA256] ? temp.SHA256 : "") + "\t"
                            + (!ExcludeFields[(int)Field.MachineName] ? temp.MachineName + "/" : "")
                            + temp.Name + "\t"
                            + (!ExcludeFields[(int)Field.SHA1] ? temp.SHA1 : "") + "\t"
                            + (!ExcludeFields[(int)Field.MD5] ? temp.MD5 : "") + "\t"
                            + (!ExcludeFields[(int)Field.CRC] ? temp.CRC : "") + "\n";

                sw.Write(state);
                sw.Flush();
            }
            catch (Exception ex)
            {
                Globals.Logger.Error(ex.ToString());
                return false;
            }

            return true;
        }
    }
}
