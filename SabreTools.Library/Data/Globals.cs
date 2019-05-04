using System;
using System.Reflection;
using System.Threading.Tasks;

using SabreTools.Library.Tools;

#if MONO
using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif

namespace SabreTools.Library.Data
{
    /// <summary>
    /// Globally-accessible objects for the library
    /// </summary>
    public class Globals
    {
        #region Private implementations

        private static Logger _logger = null;
        private static int _maxDegreeOfParallelism = System.Environment.ProcessorCount;
        private static string _exeName = new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath;
        private static string _exeDir = Path.GetDirectoryName(_exeName);
        private static string _args = string.Join(" ", Environment.GetCommandLineArgs());

        #endregion

        #region Public accessors

        public static Logger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = new Logger();
                }
                return _logger;
            }
            set { _logger = value; }
        }
        public static int MaxThreads
        {
            get { return _maxDegreeOfParallelism; }
            set { _maxDegreeOfParallelism = value; }
        }
        public static ParallelOptions ParallelOptions
        {
            get
            {
                return new ParallelOptions()
                {
                    MaxDegreeOfParallelism = _maxDegreeOfParallelism
                };
            }
        }
        public static string ExeName
        {
            get
            {
                return _exeName;
            }
        }
        public static string ExeDir
        {
            get
            {
                return _exeDir;
            }
        }
        public static string CommandLineArgs
        {
            get
            {
                return _args;
            }
        }

        #endregion
    }
}
