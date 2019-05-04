﻿using System;
using System.Collections.Generic;
using System.IO;

using SabreTools.Library.Data;

namespace SabreTools.Library.Help
{
    public class Feature
    {
        #region Private instance variables

        private FeatureType _featureType;
        private bool _foundOnce = false;

        // Specific value types
        private bool _valueBool = false;
        private int _valueInt32 = Int32.MinValue;
        private long _valueInt64 = Int64.MinValue;
        private string _valueString = null;
        private List<string> _valueList = null;

        #endregion

        #region Publicly facing variables

        public string Name { get; private set; }
        public List<string> Flags { get; private set; }
        public string Description { get; private set; }
        public string LongDescription { get; private set; } // TODO: Use this to generate README.1ST?
        public Dictionary<string, Feature> Features { get; private set; }

        #endregion

        #region Constructors

        public Feature()
        {
            this.Name = null;
            this.Flags = new List<string>();
            this.Description = null;
            this.LongDescription = null;
            this._featureType = FeatureType.Flag;
            this.Features = new Dictionary<string, Feature>();
        }

        public Feature(string name, string flag, string description, FeatureType featureType, string longDescription = null)
        {
            this.Name = name;
            this.Flags = new List<string>();
            this.Flags.Add(flag);
            this.Description = description;
            this.LongDescription = longDescription;
            this._featureType = featureType;
            this.Features = new Dictionary<string, Feature>();
        }

        public Feature(string name, List<string> flags, string description, FeatureType featureType, string longDescription = null)
        {
            this.Name = name;
            this.Flags = flags;
            this.Description = description;
            this.LongDescription = longDescription;
            this._featureType = featureType;
            this.Features = new Dictionary<string, Feature>();
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Directly address a given subfeature
        /// </summary>
        public Feature this[string name]
        {
            get { return this.Features[name]; }
            set { this.Features[name] = value; }
        }

        /// <summary>
        /// Directly address a given subfeature
        /// </summary>
        public Feature this[Feature subfeature]
        {
            get { return this.Features[subfeature.Name]; }
            set { this.Features[subfeature.Name] = value; }
        }

        /// <summary>
        /// Add a new feature for this feature
        /// </summary>
        /// <param name="feature"></param>
        public void AddFeature(Feature feature)
        {
            if (this.Features == null)
                this.Features = new Dictionary<string, Feature>();

            lock(this.Features)
            {
                this.Features[feature.Name] = feature;
            }
        }

        /// <summary>
        /// Add a new flag for this feature
        /// </summary>
        /// <param name="flag">Flag to add for this feature</param>
        public void AddFlag(string flag)
        {
            if (this.Flags == null)
                this.Flags = new List<string>();

            lock (this.Flags)
            {
                this.Flags.Add(flag);
            }
        }

        /// <summary>
        /// Add a set of new flags for this feature
        /// </summary>
        /// <param name="flags">List of flags to add to this feature</param>
        public void AddFlags(List<string> flags)
        {
            if (this.Flags == null)
                this.Flags = new List<string>();

            lock (this.Flags)
            {
                this.Flags.AddRange(flags);
            }
        }

        /// <summary>
        /// Returns if a flag exists for the current feature
        /// </summary>
        /// <param name="name">Name of the flag to check</param>
        /// <returns>True if the flag was found, false otherwise</returns>
        public bool ContainsFlag(string name)
        {
            bool success = false;

            // Loop through the flags
            foreach (string flag in this.Flags)
            {
                if (flag == name)
                {
                    success = true;
                    break;
                }
                else if (flag.TrimStart('-') == name)
                {
                    success = true;
                    break;
                }
            }

            return success;
        }

        /// <summary>
        /// Returns if the feature contains a flag that starts with the given character
        /// </summary>
        /// <param name="c">Character to check against</param>
        /// <returns>True if the flag was found, false otherwise</returns>
        public bool StartsWith(char c)
        {
            bool success = false;

            // Loop through the flags
            foreach (string flag in this.Flags)
            {
                if (flag.TrimStart('-').ToLowerInvariant()[0] == c)
                {
                    success = true;
                    break;
                }
            }

            return success;
        }

        #endregion

        #region Instance Methods

        /// <summary>
        /// Output this feature only
        /// </summary>
        /// <param name="pre">Positive number representing number of spaces to put in front of the feature</param>
        /// <param name="midpoint">Positive number representing the column where the description should start</param>
        /// <param name="includeLongDescription">True if the long description should be formatted and output, false otherwise</param>
        public List<string> Output(int pre = 0, int midpoint = 0, bool includeLongDescription = false)
        {
            // Create the output list
            List<string> outputList = new List<string>();

            // Build the output string first
            string output = "";

            // Add the pre-space first
            output += CreatePadding(pre);

            // Preprocess the flags, if necessary
            string[] newflags = new string[this.Flags.Count];
            this.Flags.CopyTo(newflags);
            switch (_featureType)
            {
                case FeatureType.Int32:
                case FeatureType.Int64:
                case FeatureType.List:
                case FeatureType.String:
                    for (int i = 0; i < newflags.Length; i++)
                    {
                        newflags[i] += "=";
                    }
                    break;
                case FeatureType.Flag:
                default:
                    // No-op
                    break;
            }

            // Now add all flags
            output += String.Join(", ", newflags);

            // If we have a midpoint set, check to see if the string needs padding
            if (midpoint > 0 && output.Length < midpoint)
            {
                output += CreatePadding(midpoint - output.Length);
            }
            else
            {
                output += " ";
            }

            // Append the description
            output += this.Description;

            // Now append it to the list
            outputList.Add(output);

            // If we are outputting the long description, format it and then add it as well
            if (includeLongDescription)
            {
                // Get the width of the console for wrapping reference
                int width = Console.WindowWidth - 1;

                // Prepare the output string
                output = CreatePadding(pre + 4);

                // Now split the input description and start processing
                string[] split = this.LongDescription.Split(' ');
                for (int i = 0; i < split.Length; i++)
                {
                    // If we have a newline character, reset the line and continue
                    if (split[i].Contains("\n"))
                    {
                        string[] subsplit = split[i].Replace("\r", "").Split('\n');
                        for (int j = 0; j < subsplit.Length - 1; j++)
                        {
                            // Add the next word only if the total length doesn't go above the width of the screen
                            if (output.Length + subsplit[j].Length < width)
                            {
                                output += (output.Length == pre + 4 ? "" : " ") + subsplit[j];
                            }
                            // Otherwise, we want to cache the line to output and create a new blank string
                            else
                            {
                                outputList.Add(output);
                                output = CreatePadding(pre + 4);
                                output += (output.Length == pre + 4 ? "" : " ") + subsplit[j];
                            }

                            outputList.Add(output);
                            output = CreatePadding(pre + 4);
                        }

                        output += subsplit[subsplit.Length - 1];
                        continue;
                    }

                    // Add the next word only if the total length doesn't go above the width of the screen
                    if (output.Length + split[i].Length < width)
                    {
                        output += (output.Length == pre + 4 ? "" : " ") + split[i];
                    }
                    // Otherwise, we want to cache the line to output and create a new blank string
                    else
                    {
                        outputList.Add(output);
                        output = CreatePadding(pre + 4);
                        output += (output.Length == pre + 4 ? "" : " ") + split[i];
                    }
                }

                // Add the last created output and a blank line for clarity
                outputList.Add(output);
                outputList.Add("");
            }

            return outputList;
        }

        /// <summary>
        /// Create a padding space based on the given length
        /// </summary>
        /// <param name="spaces">Number of padding spaces to add</param>
        /// <returns>String with requested number of blank spaces</returns>
        private string CreatePadding(int spaces)
        {
            string blank = "";
            for (int i = 0; i < spaces; i++)
            {
                blank += " ";
            }
            return blank;
        }

        /// <summary>
        /// Output this feature and all subfeatures
        /// </summary>
        /// <param name="tabLevel">Level of indentation for this feature</param>
        /// <param name="pre">Positive number representing number of spaces to put in front of the feature</param>
        /// <param name="midpoint">Positive number representing the column where the description should start</param>
        /// <param name="includeLongDescription">True if the long description should be formatted and output, false otherwise</param>
        public List<string> OutputRecursive(int tabLevel, int pre = 0, int midpoint = 0, bool includeLongDescription = false)
        {
            // Create the output list
            List<string> outputList = new List<string>();

            // Build the output string first
            string output = "";

            // Normalize based on the tab level
            int preAdjusted = pre;
            int midpointAdjusted = midpoint;
            if (tabLevel > 0)
            {
                preAdjusted += 4 * tabLevel;
                midpointAdjusted += 4 * tabLevel;
            }

            // Add the pre-space first
            output += CreatePadding(preAdjusted);

            // Preprocess the flags, if necessary
            string[] newflags = new string[this.Flags.Count];
            this.Flags.CopyTo(newflags);
            switch (_featureType)
            {
                case FeatureType.Int32:
                case FeatureType.Int64:
                case FeatureType.List:
                case FeatureType.String:
                    for (int i = 0; i < newflags.Length; i++)
                    {
                        newflags[i] += "=";
                    }
                    break;
                case FeatureType.Flag:
                default:
                    // No-op
                    break;
            }

            // Now add all flags
            output += String.Join(", ", newflags);

            // If we have a midpoint set, check to see if the string needs padding
            if (midpoint > 0 && output.Length < midpointAdjusted)
            {
                output += CreatePadding(midpointAdjusted - output.Length);
            }
            else
            {
                output += " ";
            }

            // Append the description
            output += this.Description;

            // Now append it to the list
            outputList.Add(output);

            // If we are outputting the long description, format it and then add it as well
            if (includeLongDescription)
            {
                // Get the width of the console for wrapping reference
                int width = Console.WindowWidth - 1;

                // Prepare the output string
                output = CreatePadding(preAdjusted + 4);

                // Now split the input description and start processing
                string[] split = this.LongDescription.Split(' ');
                for (int i = 0; i < split.Length; i++)
                {
                    // If we have a newline character, reset the line and continue
                    if (split[i].Contains("\n"))
                    {
                        string[] subsplit = split[i].Replace("\r", "").Split('\n');
                        for (int j = 0; j < subsplit.Length - 1; j++)
                        {
                            // Add the next word only if the total length doesn't go above the width of the screen
                            if (output.Length + subsplit[j].Length < width)
                            {
                                output += (output.Length == preAdjusted + 4 ? "" : " ") + subsplit[j];
                            }
                            // Otherwise, we want to cache the line to output and create a new blank string
                            else
                            {
                                outputList.Add(output);
                                output = CreatePadding(preAdjusted + 4);
                                output += (output.Length == preAdjusted + 4 ? "" : " ") + subsplit[j];
                            }

                            outputList.Add(output);
                            output = CreatePadding(preAdjusted + 4);
                        }

                        output += subsplit[subsplit.Length - 1];
                        continue;
                    }

                    // Add the next word only if the total length doesn't go above the width of the screen
                    if (output.Length + split[i].Length < width)
                    {
                        output += (output.Length == preAdjusted + 4 ? "" : " ") + split[i];
                    }
                    // Otherwise, we want to cache the line to output and create a new blank string
                    else
                    {
                        outputList.Add(output);
                        output = CreatePadding(preAdjusted + 4);
                        output += (output.Length == preAdjusted + 4 ? "" : " ") + split[i];
                    }
                }

                // Add the last created output and a blank line for clarity
                outputList.Add(output);
                outputList.Add("");
            }

            // Now let's append all subfeatures
            foreach (string feature in this.Features.Keys)
            {
                outputList.AddRange(this.Features[feature].OutputRecursive(tabLevel + 1, pre, midpoint, includeLongDescription));
            }

            return outputList;
        }

        /// <summary>
        /// Validate whether a flag is valid for this feature or not
        /// </summary>
        /// <param name="input">Input to check against</param>
        /// <param name="exact">True if just this feature should be checked, false if all subfeatures are checked as well</param>
        /// <param name="ignore">True if the existing flag should be ignored, false otherwise</param>
        /// <returns>True if the flag was valid, false otherwise</returns>
        public bool ValidateInput(string input, bool exact = false, bool ignore = false)
        {
            bool valid = false;

            // Determine what we should be looking for
            switch (_featureType)
            {
                // If we have a flag, make sure it doesn't have an equal sign in it 
                case FeatureType.Flag:
                    valid = !input.Contains("=") && this.Flags.Contains(input);
                    if (valid)
                    {
                        _valueBool = true;

                        // If we've already found this feature before
                        if (_foundOnce && !ignore)
                        {
                            valid = false;
                        }

                        _foundOnce = true;
                    }
                    break;
                // If we have an Int32, try to parse it if at all possible
                case FeatureType.Int32:
                    valid = input.Contains("=") && this.Flags.Contains(input.Split('=')[0]);
                    if (valid)
                    {
                        if (!Int32.TryParse(input.Split('=')[1], out int value))
                        {
                            value = Int32.MinValue;
                        }
                        _valueInt32 = value;

                        // If we've already found this feature before
                        if (_foundOnce && !ignore)
                        {
                            valid = false;
                        }

                        _foundOnce = true;
                    }
                    break;
                // If we have an Int32, try to parse it if at all possible
                case FeatureType.Int64:
                    valid = input.Contains("=") && this.Flags.Contains(input.Split('=')[0]);
                    if (valid)
                    {
                        if (!Int64.TryParse(input.Split('=')[1], out long value))
                        {
                            value = Int64.MinValue;
                        }
                        _valueInt64 = value;

                        // If we've already found this feature before
                        if (_foundOnce && !ignore)
                        {
                            valid = false;
                        }

                        _foundOnce = true;
                    }
                    break;
                // If we have an input, make sure it has an equals sign in it
                case FeatureType.List:
                    valid = input.Contains("=") && this.Flags.Contains(input.Split('=')[0]);
                    if (valid)
                    {
                        if (_valueList == null)
                        {
                            _valueList = new List<string>();
                        }

                        _valueList.Add(input.Split('=')[1]);
                    }
                    break;
                case FeatureType.String:
                    valid = input.Contains("=") && this.Flags.Contains(input.Split('=')[0]);
                    if (valid)
                    {
                        _valueString = input.Split('=')[1];

                        // If we've already found this feature before
                        if (_foundOnce && !ignore)
                        {
                            valid = false;
                        }

                        _foundOnce = true;
                    }
                    break;
            }

            // If we haven't found a valid flag and we're not looking for just this feature, check to see if any of the subfeatures are valid
            if (!valid && !exact)
            {
                foreach (string feature in this.Features.Keys)
                {
                    valid = this.Features[feature].ValidateInput(input);

                    // If we've found a valid feature, we break out
                    if (valid)
                    {
                        break;
                    }
                }
            }

            // If we're not valid at this point, we want to check if this flag is a file or a folder
            if (!valid)
            {
                valid = File.Exists(input) || Directory.Exists(input);
            }

            return valid;
        }

        /// <summary>
        /// Get the proper value associated with this feature
        /// </summary>
        /// <returns>Value associated with this feature</returns>
        public object GetValue()
        {
            switch (_featureType)
            {
                case FeatureType.Flag:
                    return _valueBool;
                case FeatureType.Int32:
                    return _valueInt32;
                case FeatureType.Int64:
                    return _valueInt64;
                case FeatureType.List:
                    return _valueList;
                case FeatureType.String:
                    return _valueString;
            }

            return null;
        }

        /// <summary>
        /// Returns if this feature has a valid value or not
        /// </summary>
        /// <returns>True if the feature is enabled, false otherwise</returns>
        public bool IsEnabled()
        {
            object obj = GetValue();

            switch (_featureType)
            {
                case FeatureType.Flag:
                    return (bool)obj;
                case FeatureType.Int32:
                    return (int)obj != Int32.MinValue;
                case FeatureType.Int64:
                    return (long)obj != Int64.MinValue;
                case FeatureType.List:
                    return obj != null;
                case FeatureType.String:
                    return obj != null;
            }

            return false;
        }

        #endregion
    }
}
