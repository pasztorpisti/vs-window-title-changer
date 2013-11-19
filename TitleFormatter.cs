using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSWindowTitleChanger
{
	class TitleFormatter
	{
		private static int NormalizeIndex(int str_length, int index)
		{
			if (index < 0)
				return Math.Max(0, str_length + index);
			return Math.Min(str_length - 1, index);
		}

		private string ProcessComplexFormatSpecifier(string format, GroupCollection groups)
		{
			string group_index_str = null;
			string slice_0_str = null;
			string slice_1_str = null;
			int idx1 = format.IndexOf(',');
			if (idx1 < 0)
			{
				// something like "1"
				group_index_str = format;
			}
			else
			{
				group_index_str = format.Substring(0, idx1);
				slice_0_str = format.Substring(0, idx1);
				int idx2 = format.IndexOf(':');
				if (idx2 < 0)
				{
					// something like "1,5" or "1,-5"
					slice_0_str = format.Substring(idx1 + 1);
				}
				else
				{
					// something like "1,5:4"
					slice_0_str = format.Substring(idx1 + 1, idx2 - idx1 - 1);
					slice_1_str = format.Substring(idx2 + 1);
				}
			}

			int group_index = Convert.ToInt32(group_index_str);
			if (group_index < 0 || group_index >= groups.Count)
				return null;
			if (!groups[group_index].Success)
				return "";

			string s = groups[group_index].Value;
			if (slice_0_str == null)
				// something like "1"
				return s;

			int slice_0 = Convert.ToInt32(slice_0_str);
			if (slice_1_str == null)
			{
				// something like "1,5" or "1,-5"
				if (slice_0 < 0)
					return s.Substring(NormalizeIndex(s.Length, slice_0));
				return s.Substring(0, NormalizeIndex(s.Length, slice_0));
			}
			else
			{
				// something like "1,5:4"
				slice_0 = NormalizeIndex(s.Length, slice_0);
				int slice_1 = NormalizeIndex(s.Length, Convert.ToInt32(slice_1_str));
				if (slice_0 >= slice_1)
					return "";
				return s.Substring(slice_0, slice_1 - slice_0);
			}
		}

		private string CreateFormattedTitle(string title_pattern, GroupCollection groups)
		{
			int len = title_pattern.Length;
			string title = "";
			for (int i = 0; i < len; ++i)
			{
				if (title_pattern[i] != '$')
				{
					title += title_pattern[i];
					continue;
				}

				++i;
				if (i == len || title_pattern[i] == '$')
				{
					title += '$';
				}
				else if (title_pattern[i] >= '0' && title_pattern[i] <= '9')
				{
					int idx = title_pattern[i] - '0';
					if (idx >= groups.Count)
					{
						title += '$';
						title += title_pattern[i];
					}
					else
					{
						Group group = groups[idx];
						if (group.Success)
							title += group.Value;
					}
				}
				else if (title_pattern[i] == '{')
				{
					int idx2 = title_pattern.IndexOf('}', i + 1);
					if (idx2 < 0)
					{
						title += "${";
					}
					else
					{
						string formatted = null;
						try
						{
							formatted = ProcessComplexFormatSpecifier(title_pattern.Substring(i + 1, idx2 - i - 1), groups);
						}
						catch (System.Exception)
						{
						}

						if (formatted == null)
							title += title_pattern.Substring(i - 1, idx2 - i + 2);
						else
							title += formatted;

						i = idx2;
					}
				}
				else
				{
					title += '$';
					title += title_pattern[i];
				}
			}

			return title;
		}

		private string TryMakeWindowTitleFromPattern(WindowTitlePattern pattern, DBGMODE dbgmode, string solution_path)
		{
			Regex regex;
			try
			{
				RegexOptions regex_options = pattern.RegexIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
				regex = new Regex(pattern.Regex, regex_options);
			}
			catch (ArgumentException)
			{
				return null;
			}
			Match match = regex.Match(solution_path);
			if (!match.Success)
				return null;

			string title_pattern;
			switch (dbgmode)
			{
				case DBGMODE.DBGMODE_Break:
					title_pattern = pattern.TitlePatternBreakMode;
					break;
				case DBGMODE.DBGMODE_Run:
					title_pattern = pattern.TitlePatternRunningMode;
					break;
				default:
					title_pattern = pattern.TitlePattern;
					break;
			}

			return CreateFormattedTitle(title_pattern, match.Groups);
		}


		public struct Input
		{
			public string solution_path;
			public ToolOptions options;
			public DBGMODE dbgmode;
		}

		public string FormatTitle(ref Input input)
		{
			ToolOptions options = input.options;
			string solution_path = input.solution_path;

			string title = null;
			foreach (WindowTitlePattern pattern in options.WindowTitlePatterns)
			{
				title = TryMakeWindowTitleFromPattern(pattern, input.dbgmode, solution_path);
				if (title != null)
				{
					if (options.Debug)
						title += string.Format(" [Debug: pattern='{0}' solution_path={1}]", pattern.Name, solution_path);
					break;
				}
			}

			if (title == null)
			{
				// default title
				if (solution_path.Length != 0)
				{
					title = new FileInfo(solution_path).Name;
					title += " - Visual Studio";
				}
				else
				{
					title = "Visual Studio";
				}

				if (options.Debug)
					title += string.Format(" [Debug: pattern='<builtin default>' solution_path={0}]", solution_path);
			}

			return title;
		}
	}
}
