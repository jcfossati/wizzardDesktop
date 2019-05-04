﻿using System;
using System.Text;

using SabreTools.Library.Data;
using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using Alphaleonis.Win32.Filesystem;

using FileStream = System.IO.FileStream;
using StreamWriter = System.IO.StreamWriter;
#endif

namespace SabreTools.Library.Tools
{
    /// <summary>
    /// Log either to file or to the console
    /// </summary>
    public class Logger
    {
        // Private instance variables
        private bool _tofile;
        private bool _warnings;
        private bool _errors;
        private string _filename;
        private LogLevel _filter;
        private DateTime _start;
        private StreamWriter _log;
        private object _lock = new object(); // This is used during multithreaded logging

        // Private required variables
        private string _basepath = Path.Combine(Globals.ExeDir, "logs") + Path.DirectorySeparatorChar;

        /// <summary>
        /// Initialize a console-only logger object
        /// </summary>
        public Logger()
        {
            _tofile = false;
            _warnings = false;
            _errors = false;
            _filename = null;
            _filter = LogLevel.VERBOSE;

            Start();
        }

        /// <summary>
        /// Initialize a Logger object with the given information
        /// </summary>
        /// <param name="tofile">True if file should be written to instead of console</param>
        /// <param name="filename">Filename representing log location</param>
        /// <param name="filter">Highest filtering level to be kept, default VERBOSE</param>
        public Logger(bool tofile, string filename, LogLevel filter = LogLevel.VERBOSE)
        {
            _tofile = tofile;
            _warnings = false;
            _errors = false;
            _filename = Path.GetFileNameWithoutExtension(filename) + " (" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ")." + Utilities.GetExtension(filename);
            _filter = filter;

            if (!Directory.Exists(_basepath))
            {
                Directory.CreateDirectory(_basepath);
            }

            Start();
        }

        /// <summary>
        /// Start logging by opening output file (if necessary)
        /// </summary>
        /// <returns>True if the logging was started correctly, false otherwise</returns>
        public bool Start()
        {
            _start = DateTime.Now;
            if (!_tofile)
            {
                return true;
            }

            try
            {
                FileStream logfile = Utilities.TryCreate(Path.Combine(_basepath, _filename));
                _log = new StreamWriter(logfile, Encoding.UTF8, (int)(4 * Constants.KibiByte), true);
                _log.AutoFlush = true;

                _log.WriteLine("Logging started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                _log.WriteLine(string.Format("Command run: {0}", Globals.CommandLineArgs));
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// End logging by closing output file (if necessary)
        /// </summary>
        /// <param name="suppress">True if all ending output is to be suppressed, false otherwise (default)</param>
        /// <returns>True if the logging was ended correctly, false otherwise</returns>
        public bool Close(bool suppress = false)
        {
            if (!suppress)
            {
                if (_warnings)
                {
                    Console.WriteLine("There were warnings in the last run! Check the log for more details");
                }
                if (_errors)
                {
                    Console.WriteLine("There were errors in the last run! Check the log for more details");
                }

                TimeSpan span = DateTime.Now.Subtract(_start);

                // Special case for multi-day runs
                string total = "";
                if (span >= TimeSpan.FromDays(1))
                {
                    total = span.ToString(@"d\:hh\:mm\:ss");
                }
                else
                {
                    total = span.ToString(@"hh\:mm\:ss");
                }

                if (!_tofile)
                {
                    Console.WriteLine("Total runtime: " + total);
                    return true;
                }

                try
                {
                    _log.WriteLine("Logging ended " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    _log.WriteLine("Total runtime: " + total);
                    Console.WriteLine("Total runtime: " + total);
                    _log.Close();
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                try
                {
                    _log.Close();
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Write the given string to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="loglevel">Severity of the information being logged</param>
        /// <param name="appendPrefix">True if the level and datetime should be prepended to each statement, false otherwise</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        private bool Log(string output, LogLevel loglevel, bool appendPrefix)
        {
            // If the log level is less than the filter level, we skip it but claim we didn't
            if (loglevel < _filter)
            {
                return true;
            }

            // USER and ERROR writes to console
            if (loglevel == LogLevel.USER || loglevel == LogLevel.ERROR)
            {
                Console.WriteLine((loglevel == LogLevel.ERROR && appendPrefix ? loglevel.ToString() + " " : "") + output);
            }

            // If we're writing to file, use the existing stream
            if (_tofile)
            {
                try
                {
                    lock(_lock)
                    {
                        _log.WriteLine((appendPrefix ? loglevel.ToString() + " - " + DateTime.Now + " - " : "") + output);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Console.WriteLine("Could not write to log file!");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Write the given exact string to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="line">Line number to write out to</param>
        /// <param name="column">Column number to write out to</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool WriteExact(string output, int line, int column)
        {
            // Set the cursor position (if not being redirected)
            if (!Console.IsOutputRedirected)
            {
                Console.CursorTop = line;
                Console.CursorLeft = column;
            }

            // Write out to the console
            Console.Write(output);

            // If we're writing to file, use the existing stream
            if (_tofile)
            {
                try
                {
                    lock (_lock)
                    {
                        _log.Write(DateTime.Now + " - " + output);
                    }
                }
                catch
                {
                    Console.WriteLine("Could not write to log file!");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Write the given string as a verbose message to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>s
        public bool Verbose(string output, params object[] args)
        {
            return Log(args.Length == 0 ? output: string.Format(output, args), LogLevel.VERBOSE, true);
        }

        /// <summary>
        /// Write the given string as a verbose message to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="appendPrefix">True if the level and datetime should be prepended to each statement (default), false otherwise</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool Verbose(string output, bool appendPrefix = true, params object[] args)
        {
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.VERBOSE, appendPrefix);
        }

        /// <summary>
        /// Write the given string as a user message to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool User(string output, params object[] args)
        {
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.USER, true);
        }

        /// <summary>
        /// Write the given string as a user message to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="appendPrefix">True if the level and datetime should be prepended to each statement (default), false otherwise</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool User(string output, bool appendPrefix = true, params object[] args)
        {
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.USER, appendPrefix);
        }

        /// <summary>
        /// Write the given string as a warning to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool Warning(string output, params object[] args)
        {
            _warnings = true;
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.WARNING, true);
        }

        /// <summary>
        /// Write the given string as a warning to the log output
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="appendPrefix">True if the level and datetime should be prepended to each statement (default), false otherwise</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool Warning(string output, bool appendPrefix = true, params object[] args)
        {
            _warnings = true;
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.WARNING, appendPrefix);
        }

        /// <summary>
        /// Writes the given string as an error in the log
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool Error(string output, params object[] args)
        {
            _errors = true;
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.ERROR, true);
        }

        /// <summary>
        /// Writes the given string as an error in the log
        /// </summary>
        /// <param name="output">String to be written log</param>
        /// <param name="appendPrefix">True if the level and datetime should be prepended to each statement (default), false otherwise</param>
        /// <param name="args">Optional arguments for string formatting</param>
        /// <returns>True if the output could be written, false otherwise</returns>
        public bool Error(string output, bool appendPrefix = true, params object[] args)
        {
            _errors = true;
            return Log(args.Length == 0 ? output : string.Format(output, args), LogLevel.ERROR, appendPrefix);
        }

        /// <summary>
        /// Clear lines beneath the given line in the console
        /// </summary>
        /// <param name="line">Line number to clear beneath</param>
        /// <returns>True</returns>
        public bool ClearBeneath(int line)
        {
            if (!Console.IsOutputRedirected)
            {
                for (int i = line; i < Console.WindowHeight; i++)
                {
                    // http://stackoverflow.com/questions/8946808/can-console-clear-be-used-to-only-clear-a-line-instead-of-whole-console
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, i);
                }
            }
            return true;
        }
    }
}
