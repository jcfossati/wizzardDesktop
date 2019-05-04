﻿using SabreTools.Library.Data;

namespace SabreTools.Library.DatItems
{
    /// <summary>
    /// Represents a (usually WAV-formatted) sample to be included for use in the set
    /// </summary>
    public class Sample : DatItem
    {
        #region Constructors

        /// <summary>
        /// Create a default, empty Sample object
        /// </summary>
        public Sample()
        {
            this.Name = "";
            this.ItemType = ItemType.Sample;
        }

        #endregion

        #region Cloning Methods

        public override object Clone()
        {
            return new Sample()
            {
                Name = this.Name,
                ItemType = this.ItemType,
                DupeType = this.DupeType,

                Supported = this.Supported,
                Publisher = this.Publisher,
                Infos = this.Infos,
                PartName = this.PartName,
                PartInterface = this.PartInterface,
                Features = this.Features,
                AreaName = this.AreaName,
                AreaSize = this.AreaSize,

                MachineName = this.MachineName,
                Comment = this.Comment,
                MachineDescription = this.MachineDescription,
                Year = this.Year,
                Manufacturer = this.Manufacturer,
                RomOf = this.RomOf,
                CloneOf = this.CloneOf,
                SampleOf = this.SampleOf,
                SourceFile = this.SourceFile,
                Runnable = this.Runnable,
                Board = this.Board,
                RebuildTo = this.RebuildTo,
                Devices = this.Devices,
                MachineType = this.MachineType,

                SystemID = this.SystemID,
                System = this.System,
                SourceID = this.SourceID,
                Source = this.Source,
            };
        }

        #endregion

        #region Comparision Methods

        public override bool Equals(DatItem other)
        {
            // If we don't have a sample, return false
            if (this.ItemType != other.ItemType)
            {
                return false;
            }

            // Otherwise, treat it as a sample
            Sample newOther = (Sample)other;

            // If the archive information matches
            return (this.Name == newOther.Name);
        }

        #endregion
    }
}
