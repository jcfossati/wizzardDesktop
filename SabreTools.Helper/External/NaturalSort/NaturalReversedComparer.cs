﻿/*
 * 
 * Links for info and original source code:
 * 
 * https://blog.codinghorror.com/sorting-for-humans-natural-sort-order/
 * http://www.codeproject.com/Articles/22517/Natural-Sort-Comparer
 *
 * Exact code implementation used with permission, originally by motoschifo
 * 
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SabreTools.Helper
{
	public class NaturalReversedComparer : Comparer<string>, IDisposable
	{
		private Dictionary<string, string[]> table;

		public NaturalReversedComparer()
		{
			table = new Dictionary<string, string[]>();
		}

		public void Dispose()
		{
			table.Clear();
			table = null;
		}

		public override int Compare(string x, string y)
		{
			if (x == y)
			{
				return 0;
			}
			string[] x1, y1;
			if (!table.TryGetValue(x, out x1))
			{
				//x1 = Regex.Split(x.Replace(" ", ""), "([0-9]+)");
				x1 = Regex.Split(x, "([0-9]+)");
				table.Add(x, x1);
			}
			if (!table.TryGetValue(y, out y1))
			{
				//y1 = Regex.Split(y.Replace(" ", ""), "([0-9]+)");
				y1 = Regex.Split(y, "([0-9]+)");
				table.Add(y, y1);
			}

			for (int i = 0; i < x1.Length && i < y1.Length; i++)
			{
				if (x1[i] != y1[i])
				{
					return PartCompare(x1[i], y1[i]);
				}
			}
			if (y1.Length > x1.Length)
			{
				return 1;
			}
			else if (x1.Length > y1.Length)
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}

		private static int PartCompare(string left, string right)
		{
			int x, y;
			if (!int.TryParse(left, out x))
			{
				return right.CompareTo(left);
			}

			if (!int.TryParse(right, out y))
			{
				return right.CompareTo(left);
			}

			return -x.CompareTo(y);
		}
	}
}
