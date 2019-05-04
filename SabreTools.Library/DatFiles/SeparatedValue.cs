﻿using System;
using System.Collections.Generic;
using System.Text;
using SabreTools.Library.Data;
using SabreTools.Library.DatItems;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using FileStream = System.IO.FileStream;
using StreamReader = System.IO.StreamReader;
using StreamWriter = System.IO.StreamWriter;
#endif
using NaturalSort;

namespace SabreTools.Library.DatFiles
{
    /// <summary>
    /// Represents parsing and writing of a value-separated DAT
    /// </summary>
    internal class SeparatedValue : DatFile
    {
        // Private instance variables specific to Separated Value DATs
        char _delim;

        /// <summary>
        /// Constructor designed for casting a base DatFile
        /// </summary>
        /// <param name="datFile">Parent DatFile to copy from</param>
        /// <param name="delim">Delimiter for parsing individual lines</param>
        public SeparatedValue(DatFile datFile, char delim)
            : base(datFile, cloneHeader: false)
        {
            _delim = delim;
        }

        /// <summary>
        /// Parse a character-separated value DAT and return all found games and roms within
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

            // Create an empty list of columns to parse though
            List<string> columns = new List<string>();

            long linenum = -1;
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                linenum++;

                // Parse the first line, getting types from the column names
                if (linenum == 0)
                {
                    string[] parsedColumns = line.Split(_delim);
                    foreach (string parsed in parsedColumns)
                    {
                        switch (parsed.ToLowerInvariant().Trim('"'))
                        {
                            case "file":
                            case "filename":
                            case "file name":
                                columns.Add("DatFile.FileName");
                                break;
                            case "internal name":
                                columns.Add("DatFile.Name");
                                break;
                            case "description":
                            case "dat description":
                                columns.Add("DatFile.Description");
                                break;
                            case "game name":
                            case "game":
                            case "machine":
                                columns.Add("Machine.Name");
                                break;
                            case "game description":
                                columns.Add("Description");
                                break;
                            case "type":
                                columns.Add("DatItem.Type");
                                break;
                            case "rom":
                            case "romname":
                            case "rom name":
                            case "name":
                                columns.Add("Rom.Name");
                                break;
                            case "disk":
                            case "diskname":
                            case "disk name":
                                columns.Add("Disk.Name");
                                break;
                            case "size":
                                columns.Add("DatItem.Size");
                                break;
                            case "crc":
                            case "crc hash":
                                columns.Add("DatItem.CRC");
                                break;
                            case "md5":
                            case "md5 hash":
                                columns.Add("DatItem.MD5");
                                break;
                            case "sha1":
                            case "sha-1":
                            case "sha1 hash":
                            case "sha-1 hash":
                                columns.Add("DatItem.SHA1");
                                break;
                            case "sha256":
                            case "sha-256":
                            case "sha256 hash":
                            case "sha-256 hash":
                                columns.Add("DatItem.SHA256");
                                break;
                            case "sha384":
                            case "sha-384":
                            case "sha384 hash":
                            case "sha-384 hash":
                                columns.Add("DatItem.SHA384");
                                break;
                            case "sha512":
                            case "sha-512":
                            case "sha512 hash":
                            case "sha-512 hash":
                                columns.Add("DatItem.SHA512");
                                break;
                            case "nodump":
                            case "no dump":
                            case "status":
                            case "item status":
                                columns.Add("DatItem.Nodump");
                                break;
                            case "date":
                                columns.Add("DatItem.Date");
                                break;
                            default:
                                columns.Add("INVALID");
                                break;
                        }
                    }

                    continue;
                }

                // Otherwise, we want to split the line and parse
                string[] parsedLine = line.Split(_delim);

                // If the line doesn't have the correct number of columns, we log and skip
                if (parsedLine.Length != columns.Count)
                {
                    Globals.Logger.Warning("Malformed line found in '{0}' at line {1}", filename, linenum);
                    continue;
                }

                // Set the output item information
                string machineName = null, machineDesc = null, name = null, crc = null, md5 = null, sha1 = null,
                    sha256 = null, sha384 = null, sha512 = null, date = null;
                long size = -1;
                ItemType itemType = ItemType.Rom;
                ItemStatus status = ItemStatus.None;

                // Now we loop through and get values for everything
                for (int i = 0; i < columns.Count; i++)
                {
                    string value = parsedLine[i].Trim('"');
                    switch (columns[i])
                    {
                        case "DatFile.FileName":
                            FileName = (String.IsNullOrWhiteSpace(FileName) ? value : FileName);
                            break;
                        case "DatFile.Name":
                            Name = (String.IsNullOrWhiteSpace(Name) ? value : Name);
                            break;
                        case "DatFile.Description":
                            Description = (String.IsNullOrWhiteSpace(Description) ? value : Description);
                            break;
                        case "Machine.Name":
                            machineName = value;
                            break;
                        case "Description":
                            machineDesc = value;
                            break;
                        case "DatItem.Type":
                            itemType = Utilities.GetItemType(value) ?? ItemType.Rom;
                            break;
                        case "Rom.Name":
                        case "Disk.Name":
                            name = String.IsNullOrWhiteSpace(value) ? name : value;
                            break;
                        case "DatItem.Size":
                            if (!Int64.TryParse(value, out size))
                            {
                                size = -1;
                            }
                            break;
                        case "DatItem.CRC":
                            crc = Utilities.CleanHashData(value, Constants.CRCLength);
                            break;
                        case "DatItem.MD5":
                            md5 = Utilities.CleanHashData(value, Constants.MD5Length);
                            break;
                        case "DatItem.SHA1":
                            sha1 = Utilities.CleanHashData(value, Constants.SHA1Length);
                            break;
                        case "DatItem.SHA256":
                            sha256 = Utilities.CleanHashData(value, Constants.SHA256Length);
                            break;
                        case "DatItem.SHA384":
                            sha384 = Utilities.CleanHashData(value, Constants.SHA384Length);
                            break;
                        case "DatItem.SHA512":
                            sha512 = Utilities.CleanHashData(value, Constants.SHA512Length);
                            break;
                        case "DatItem.Nodump":
                            status = Utilities.GetItemStatus(value);
                            break;
                        case "DatItem.Date":
                            date = value;
                            break;
                    }
                }

                // And now we populate and add the new item
                switch (itemType)
                {
                    case ItemType.Archive:
                        Archive archive = new Archive()
                        {
                            Name = name,

                            MachineName = machineName,
                            MachineDescription = machineDesc,
                        };

                        ParseAddHelper(archive, clean, remUnicode);
                        break;
                    case ItemType.BiosSet:
                        BiosSet biosset = new BiosSet()
                        {
                            Name = name,

                            MachineName = machineName,
                            Description = machineDesc,
                        };

                        ParseAddHelper(biosset, clean, remUnicode);
                        break;
                    case ItemType.Disk:
                        Disk disk = new Disk()
                        {
                            Name = name,
                            MD5 = md5,
                            SHA1 = sha1,
                            SHA256 = sha256,
                            SHA384 = sha384,
                            SHA512 = sha512,

                            MachineName = machineName,
                            MachineDescription = machineDesc,

                            ItemStatus = status,
                        };

                        ParseAddHelper(disk, clean, remUnicode);
                        break;
                    case ItemType.Release:
                        Release release = new Release()
                        {
                            Name = name,

                            MachineName = machineName,
                            MachineDescription = machineDesc,
                        };

                        ParseAddHelper(release, clean, remUnicode);
                        break;
                    case ItemType.Rom:
                        Rom rom = new Rom()
                        {
                            Name = name,
                            Size = size,
                            CRC = crc,
                            MD5 = md5,
                            SHA1 = sha1,
                            SHA256 = sha256,
                            SHA384 = sha384,
                            SHA512 = sha512,
                            Date = date,

                            MachineName = machineName,
                            MachineDescription = machineDesc,

                            ItemStatus = status,
                        };

                        ParseAddHelper(rom, clean, remUnicode);
                        break;
                    case ItemType.Sample:
                        Sample sample = new Sample()
                        {
                            Name = name,

                            MachineName = machineName,
                            MachineDescription = machineDesc,
                        };

                        ParseAddHelper(sample, clean, remUnicode);
                        break;
                }
            }
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

                // Write out the header
                WriteHeader(sw);

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
                        DatItem rom = roms[index];

                        // There are apparently times when a null rom can skip by, skip them
                        if (rom.Name == null || rom.MachineName == null)
                        {
                            Globals.Logger.Warning("Null rom found!");
                            continue;
                        }

                        // If we have a "null" game (created by DATFromDir or something similar), log it to file
                        if (rom.ItemType == ItemType.Rom
                            && ((Rom)rom).Size == -1
                            && ((Rom)rom).CRC == "null")
                        {
                            Globals.Logger.Verbose("Empty folder found: {0}", rom.MachineName);
                        }

                        // Now, output the rom data
                        WriteDatItem(sw, rom, ignoreblanks);
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
        /// Write out DAT header using the supplied StreamWriter
        /// </summary>
        /// <param name="sw">StreamWriter to output to</param>
        /// <returns>True if the data was written, false on error</returns>
        private bool WriteHeader(StreamWriter sw)
        {
            try
            {
                string header = string.Format("\"File Name\"{0}\"Internal Name\"{0}\"Description\"{0}\"Game Name\"{0}\"Game Description\"{0}\"Type\"{0}\"" +
                                "Rom Name\"{0}\"Disk Name\"{0}\"Size\"{0}\"CRC\"{0}\"MD5\"{0}\"SHA1\"{0}\"SHA256\"{0}\"Nodump\"\n", _delim);

                // Write the header out
                sw.Write(header);
                sw.Flush();
            }
            catch (Exception ex)
            {
                Globals.Logger.Error(ex.ToString());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Write out DatItem using the supplied StreamWriter
        /// </summary>
        /// <param name="sw">StreamWriter to output to</param>
        /// <param name="rom">DatItem object to be output</param>
        /// <param name="ignoreblanks">True if blank roms should be skipped on output, false otherwise (default)</param>
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
                // Initialize all strings
                string state = "",
                    pre = "",
                    post = "",
                    type = "",
                    romname = "",
                    diskname = "",
                    size = "",
                    crc = "",
                    md5 = "",
                    sha1 = "",
                    sha256 = "",
                    sha384 = "",
                    sha512 = "",
                    status = "";

                // Separated values should only output Rom and Disk
                if (rom.ItemType != ItemType.Disk && rom.ItemType != ItemType.Rom)
                {
                    return true;
                }

                if (rom.ItemType == ItemType.Rom)
                {
                    type = "rom";
                    romname = rom.Name;
                    size = ((Rom)rom).Size.ToString();
                    crc = ((Rom)rom).CRC;
                    md5 = ((Rom)rom).MD5;
                    sha1 = ((Rom)rom).SHA1;
                    sha256 = ((Rom)rom).SHA256;
                    sha384 = ((Rom)rom).SHA384;
                    sha512 = ((Rom)rom).SHA512;
                    status = (((Rom)rom).ItemStatus != ItemStatus.None ? "\"" + ((Rom)rom).ItemStatus.ToString() + "\"" : "\"\"");
                }
                else if (rom.ItemType == ItemType.Disk)
                {
                    type = "disk";
                    diskname = rom.Name;
                    md5 = ((Disk)rom).MD5;
                    sha1 = ((Disk)rom).SHA1;
                    sha256 = ((Disk)rom).SHA256;
                    sha384 = ((Disk)rom).SHA384;
                    sha512 = ((Disk)rom).SHA512;
                    status = (((Disk)rom).ItemStatus != ItemStatus.None ? "\"" + ((Disk)rom).ItemStatus.ToString() + "\"" : "\"\"");
                }

                pre = CreatePrefixPostfix(rom, true);
                post = CreatePrefixPostfix(rom, false);
                string inline = string.Format("\"" + FileName + "\""
                    + "{0}\"" + Name + "\""
                    + "{0}\"" + Description + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.MachineName] ? rom.MachineName : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.Description] ? rom.MachineDescription : "") + "\""
                    + "{0}\"" + type + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.Name] ? romname : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.Name] ? diskname : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.Size] ? size : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.CRC] ? crc : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.MD5] ? md5 : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.SHA1] ? sha1 : "") + "\""
                    + "{0}\"" + (!ExcludeFields[(int)Field.SHA256] ? sha256 : "") + "\""
                    // + "{0}\"" + (!ExcludeFields[(int)Field.SHA384] ? sha384 : "") + "\""
                    // + "{0}\"" + (!ExcludeFields[(int)Field.SHA512] ? sha512 : "") + "\""
                    + "{0}" + status, _delim);
                state += pre + inline + post + "\n";

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
