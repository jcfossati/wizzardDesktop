﻿using System;
using System.Diagnostics;

namespace SabreTools.Helper
{
	public class Build
	{
		/// <summary>
		/// Returns true if running in a Mono environment
		/// </summary>
		public static bool MonoEnvironment
		{
			get { return (Type.GetType("Mono.Runtime") != null); }
		}

		/// <summary>
		/// Readies the console and outputs the header
		/// </summary>
		/// <param name="name">The name to be displayed as the program</param>
		/// <remarks>Adapted from http://stackoverflow.com/questions/8200661/how-to-align-string-in-fixed-length-string</remarks>
		public static void Start(string name)
		{
			// Dynamically create the header string
			string border = "+-----------------------------------------------------------------------------+";
			string mid = name + " " + Constants.Version;
			mid = "|" + mid.PadLeft(((77 - mid.Length) / 2) + mid.Length).PadRight(77) + "|";

			// If we're outputting to console, do fancy things
			if (!Console.IsOutputRedirected)
			{
				// Set the console to ready state
				ConsoleColor formertext = ConsoleColor.White;
				ConsoleColor formerback = ConsoleColor.Black;
				if (!MonoEnvironment)
				{
					Console.SetBufferSize(Console.BufferWidth, 999);
					formertext = Console.ForegroundColor;
					formerback = Console.BackgroundColor;
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.BackgroundColor = ConsoleColor.Blue;
				}

				Console.Title = "SabreTools-" + name + " " + Constants.Version;

				// Output the header
				Console.WriteLine(border);
				Console.WriteLine(mid);
				Console.WriteLine(border);
				Console.WriteLine();

				// Return the console to the original text and background colors
				if (!MonoEnvironment)
				{
					Console.ForegroundColor = formertext;
					Console.BackgroundColor = formerback;
				}
			}
		}

		/// <summary>
		/// Show the help dialog for a given class
		/// </summary>
		public static void Help()
		{
			//http://stackoverflow.com/questions/14849367/how-to-determine-calling-method-and-class-name
			StackTrace st = new StackTrace();
			string className = st.GetFrame(1).GetMethod().ReflectedType.Name;

			switch (className)
			{
				case "DATabase":
					Console.Write(@"
DATabase - Import and Generate DAT files
-----------------------------------------
Usage: DATabase [option] [filename|dirname] ...

Options:
  -?, -h, --help	Show this help
  -a, --add		Add a new system or source to the database
	-manu=			Manufacturer name (system only)
	-system=			System name (system only)
	-source=			Source name (source only)
	-url=			URL (source only)
  -es, --ext-split		Split a DAT by two file extensions
	-exta=			First extension to split by
	-extb=			Second extension to split by
	-out=			Output directory
  -g, --generate	Start tool in generate mode
	-system=			System ID to generate from
	-nr, --no-rename	Don't auto-rename games
	-o, --old		Output DAT in CMP format instead of XML
  -ga, --generate-all	Start tool in generate all mode
	-nr, --no-rename	Don't auto-rename games
	-o, --old		Output DAT in CMP format instead of XML
  -hs, --hash-split		Split a DAT or folder by best-available hashes
	-out=			Output directory
  -i, --import		Start tool in import mode
	-ig, --ignore		Don't prompt for new sources
  -lso, --list-sources	List all sources (id <= name)
  -lsy, --list-systems	List all systems (id <= name)
  -m, --merge		Merge one or more DATs
	-di, --diff		Output all diffdats (merge flag not required)
		-c, --cascade		Enable cascaded diffing
			-ip, --inplace		Enable inplace, cascaded diffing
	-dd, --dedup		Enable deduping in the created DAT
	-b, --bare		Don't include date in file name
	-u, --unzip		Force unzipping in created DAT
	-o, --old		Output DAT in CMP format instead of XML
	-clean			Clean game names according to WoD standards
	-out=			Output directory (overridden by --inplace)
	-sd, --superdat		Enable SuperDAT creation
	-n=, --name=		Set the internal name of the DAT
	-de=, --desc=		Set the filename and description of the DAT
	-ca=, --category=	Set the category of the DAT
	-v=, --version=		Set the version of the DAT
	-au=, --author=		Set the author of the DAT
  -rm, --remove		Remove a system or source from the database
	-system=		System ID
	-source=		Source ID
  -st, --stats		Get statistics on all input DATs
	-si, --single		Show individual statistics
  -tm, --trim-merge	Consolidate DAT into a single game and trim entries
	-rd=, --root-dir=	Set the root directory for trimming calculation
	-nr, --no-rename	Keep game names instead of using '!'
	-df, --disable-force	Disable forceunzipping
  -ud, --update		Update a DAT file
	-oc, --output-cmp	Output in CMP format
	-om, --output-miss	Output in Missfile format
	  -r, --roms		Output roms to miss instead of sets
	  -gp, --game-prefix	Add game name as a prefix to each item
	  -pre=, --prefix=	Set prefix to be printed in front of all lines
	  -post=, --postfix=	Set postfix to be printed behind all lines
	  -q, --quotes		Put double-quotes around each item
	  -ae=, --add-ext=	Add an extension to each item
	  -re=, --rep-ext=	Replace all extensions with specified
	  -ro, --romba		Output roms in Romba format (requires SHA-1)
	  -tsv, --tsv		Output roms in Tab-Separated Value format
	-or, --output-rc	Output in RomCenter format
	-os, --output-sd	Output in SabreDAT format
	-ox, --output-xml	Output in Logiqx XML format
	-f=, --filename=	Set a new filename
	-n=, --name=		Set a new internal name
	-de=, --desc=		Set a new description
	-ca=, --category=	Set a new category
	-v=, --version=		Set a new version
	-da=, --date=		Set a new date
	-au=, --author=		Set a new author
	-em=, --email=		Set a new email
	-hp=, --homepage=	Set a new homepage
	-u=, --url=		Set a new URL
	-co=, --comment=	Set a new comment
	-h=, --header=		Set a new header skipper
	-sd=, --superdat	Set SuperDAT type
	-fm=, --forcemerge=	Set force merging
		Supported values are:
		- None
		- Split
		- Full
	-fn=, --forcend=	Set force nodump
		Supported values are:
		- None
		- Obsolete
		- Required
		- Ignore
	-fp=, --forcepack=	Set force packing
		Supported values are:
		- None
		- Zip
		- Unzip
	-clean			Clean game names according to WoD standards
	-out=			Output directory

Filenames and directories can't start with a reserved string
unless prefixed by 'input='
");
					break;
				case "Headerer":
					Console.WriteLine(@"Headerer - Remove and restore rom headers
-----------------------------------------
Usage: Headerer [option] [filename|dirname]

Options:
  -e			Detect and remove mode
  -r			Restore header to file based on SHA-1");
					break;
				case "DATFromDir":
					Console.WriteLine(@"DATFromDir - Create a DAT file from a directory
-----------------------------------------
Usage: DATFromDir [options] [filename|dirname] <filename|dirname> ...

Options:
  -h, -?, --help	Show this help dialog
  -m, --noMD5		Don't include MD5 in output
  -s, --noSHA1		Don't include SHA1 in output
  -b, --bare		Don't include date in file name
  -u, --unzip		Force unzipping in created DAT
  -f, --files		Treat archives as files
  -o, --old		Output DAT in CMP format instead of XML
  -gz, --gz-files	Allow reading of GZIP files as archives
  -ro, --romba		Read files from a Romba input
  -n=, --name=		Set the internal name of the DAT
  -d=, --desc=		Set the filename and description of the DAT
  -c=, --cat=		Set the category of the DAT
  -v=, --version=	Set the version of the DAT
  -au=, --author=	Set the author of the DAT
  -sd, --superdat	Enable SuperDAT creation
  -t=, --temp=		Set the temporary directory to use");
					break;
				case "OfflineMerge":
					Console.WriteLine(@"OfflineMerge - Update DATS for offline arrays
-----------------------------------------
Usage: OfflineMerge [options] [inputs]

Options:
  -h, -?, --help	Show this help dialog
  -f, --fake		Replace all hashes and sizes by the default

Inputs:
  -com=			Complete current DAT
  -fix=			Complete current Missing
  -new=			New Complete DAT

This program will output the following DATs:
  (a) Net New - (NewComplete)-(Complete)
  (b) Unneeded - (Complete)-(NewComplete)
  (c) New Missing - (Net New)+(Missing-(Unneeded))
  (d) Have - (NewComplete)-(New Missing)
        OR (Complete or NewComplete)-(Missing) if one is missing");
					break;
				case "Filter":
					Console.WriteLine(@"Filter - Filter DATs by inputted criteria
-----------------------------------------
Usage: Filter [options] [inputs]

Options:
  -h, -?, --help	Show this help dialog
  -out=, --out=		Output directory
  -gn=, --game-name=	Game name to be filtered on
  -rn=, --rom-name=	Rom name to be filtered on
  -rt=, --rom-type=	Rom type to be filtered on
  -sgt=, --greater=	Size greater than or equal to
  -slt=, --less=	Size less than or equal to
  -seq=, --equal=	Size equal to
  -crc=, --crc=		CRC to be filtered on
  -md5=, --md5=		MD5 to be filtered on
  -sha1=, --sha1=	SHA-1 to be filtered on
  -nd, --nodump		Only match nodump roms
  -nnd, --not-nodump	Exclude all nodump roms

Game name, Rom name, CRC, MD5, SHA-1 can do partial matches
using asterisks as follows (case insensitive):
    *00 means ends with '00'
    00* means starts with '00'
    *00* means contains '00'
    00 means exactly equals '00'");
					break;
				default:
					Console.Write("This is the default help output");
					break;
			}
		}

		public static void Credits()
		{
			Console.WriteLine(@"-----------------------------------------
Credits
-----------------------------------------

Programmer / Lead:	Matt Nadareski (darksabre76)
Additional code:	emuLOAD, @tractivo
Testing:		emuLOAD, @tractivo, Kludge, Obiwantje, edc
Suggestions:		edc, AcidX, Amiga12, EliUmniCk
Based on work by:	The Wizard of DATz");
		}
	}
}