using System;
using System.Collections.Generic;
using SabreTools.Library.Data;

namespace SabreTools.Library.DatItems
{
    /// <summary>
    /// Represents the information specific to a set/game/machine
    /// </summary>
    public class Machine : ICloneable
    {
        #region Publicly facing variables

        /// <summary>
        /// Name of the machine
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Additional notes
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Extended description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Year(s) of release/manufacture
        /// </summary>
        public string Year { get; set; }

        /// <summary>
        /// Manufacturer, if available
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// Publisher, if available
        /// </summary>
        public string Publisher { get; set; }

        /// <summary>
        /// fomof parent
        /// </summary>
        public string RomOf { get; set; }

        /// <summary>
        /// cloneof parent
        /// </summary>
        public string CloneOf { get; set; }

        /// <summary>
        /// sampleof parent
        /// </summary>
        public string SampleOf { get; set; }

        /// <summary>
        /// Support status
        /// </summary>
        /// <remarks>yes = true, partial = null, no = false</remarks>
        public bool? Supported { get; set; }

        /// <summary>
        /// Emulator source file related to the machine
        /// </summary>
        public string SourceFile { get; set; }

        /// <summary>
        /// Machine runnable status
        /// </summary>
        /// <remarks>yes = true, partial = null, no = false</remarks>
        public bool? Runnable { get; set; }

        /// <summary>
        /// Machine board name
        /// </summary>
        public string Board { get; set; }

        /// <summary>
        /// Rebuild location if different than machine name
        /// </summary>
        public string RebuildTo { get; set; }

        /// <summary>
        /// List of associated device names
        /// </summary>
        public List<string> Devices { get; set; }

        /// <summary>
        /// List of slot options
        /// </summary>
        public List<string> SlotOptions { get; set; }

        /// <summary>
        /// List of info items
        /// </summary>
        public List<Tuple<string, string>> Infos { get; set; }

        /// <summary>
        /// Type of the machine
        /// </summary>
        public MachineType MachineType { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Create a new Machine object
        /// </summary>
        public Machine()
        {
            Name = null;
            Comment = null;
            Description = null;
            Year = null;
            Manufacturer = null;
            Publisher = null;
            RomOf = null;
            CloneOf = null;
            SampleOf = null;
            Supported = true;
            SourceFile = null;
            Runnable = null;
            Board = null;
            RebuildTo = null;
            Devices = null;
            SlotOptions = null;
            Infos = null;
            MachineType = MachineType.NULL;
        }

        /// <summary>
        /// Create a new Machine object with the included information
        /// </summary>
        /// <param name="name">Name of the machine</param>
        /// <param name="description">Description of the machine</param>
        public Machine(string name, string description)
        {
            Name = name;
            Comment = null;
            Description = description;
            Year = null;
            Manufacturer = null;
            Publisher = null;
            RomOf = null;
            CloneOf = null;
            SampleOf = null;
            Supported = true;
            SourceFile = null;
            Runnable = null;
            Board = null;
            RebuildTo = null;
            Devices = null;
            SlotOptions = null;
            Infos = null;
            MachineType = MachineType.NULL;
        }

        #endregion

        #region Cloning methods

        /// <summary>
        /// Create a clone of the current machine
        /// </summary>
        /// <returns>New machine with the same values as the current one</returns>
        public object Clone()
        {
            return new Machine()
            {
                Name = this.Name,
                Comment = this.Comment,
                Description = this.Description,
                Year = this.Year,
                Manufacturer = this.Manufacturer,
                Publisher = this.Publisher,
                RomOf = this.RomOf,
                CloneOf = this.CloneOf,
                SampleOf = this.SampleOf,
                Supported = this.Supported,
                SourceFile = this.SourceFile,
                Runnable = this.Runnable,
                Board = this.Board,
                RebuildTo = this.RebuildTo,
                Devices = this.Devices,
                SlotOptions = this.SlotOptions,
                Infos = this.Infos,
                MachineType = this.MachineType,
            };
        }

        #endregion
    }
}
