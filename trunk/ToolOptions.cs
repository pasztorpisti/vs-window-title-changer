using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.Shell;

namespace VSWindowTitleChanger
{
	public interface ISerializedOptions
	{
		bool Debug { get; set; }
		List<WindowTitlePattern> WindowTitlePatterns { get; set; }
	}

	[ClassInterface(ClassInterfaceType.AutoDual)]
	[ComVisible(true)]
	public class ToolOptions : DialogPage, ISerializedOptions
	{
		[Category("Solution File Pathname Matching")]
		[DisplayName("Debug Mode")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Debug { get; set; }

		[Category("Window Title Changer Options")]
		[DisplayName("Window Title Patterns")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public List<WindowTitlePattern> WindowTitlePatterns { get; set; }

		public override void ResetSettings()
		{
			CopyOptions(new SerializedOptions(), this);
			Fixup();
		}

		public class SerializedOptions : ISerializedOptions
		{
			public bool Debug { get; set; }
			public List<WindowTitlePattern> WindowTitlePatterns { get; set; }
		}

		private static void CopyOptions(ISerializedOptions src, ISerializedOptions dest)
		{
			foreach (PropertyInfo pi in typeof(ISerializedOptions).GetProperties())
				pi.SetValue(dest, pi.GetValue(src, null), null);
		}

		[Browsable(false)]
		public string WindowTitlePatternsString
		{
			get
			{
				try
				{
					SerializedOptions options = new SerializedOptions();
					CopyOptions(this, options);
					XmlSerializer xs = new XmlSerializer(typeof(SerializedOptions));
					StringWriter sw = new StringWriter();
					XmlWriterSettings settings = new XmlWriterSettings()
					{
						OmitXmlDeclaration = true,
						Indent = false,
						NewLineChars = "",
						NewLineHandling = NewLineHandling.Entitize,
						NewLineOnAttributes = false,
					};
					XmlWriter xtw = XmlTextWriter.Create(sw, settings);
					xs.Serialize(xtw, options);
					return sw.ToString();
				}
				catch (System.Exception)
				{
					return "";
				}
			}
			set
			{
				try
				{
					if (string.IsNullOrEmpty(value))
					{
						ResetSettings();
						return;
					}
					XmlSerializer xs = new XmlSerializer(typeof(SerializedOptions));
					StringReader sr = new StringReader(value);
					SerializedOptions options = (SerializedOptions)xs.Deserialize(sr);
					CopyOptions(options, this);
					Fixup();
				}
				catch (System.Exception)
				{
				}
			}
		}

		private void Fixup()
		{
			if (WindowTitlePatterns == null)
			{
				WindowTitlePatterns = new List<WindowTitlePattern>();
			}
			else
			{
				foreach (WindowTitlePattern wtp in WindowTitlePatterns)
					wtp.Fixup();
			}
		}

		public ToolOptions()
		{
			Fixup();
		}

	}



	public class WindowTitlePattern
	{
		private string m_Name;
		private bool m_RegexIgnoreCase = true;
		private string m_Regex;
		private string m_TitlePattern;
		private string m_TitlePatternBreakMode;
		private string m_TitlePatternRunningMode;

		public void Fixup()
		{
			if (m_Name == null)
				m_Name = "New Pattern";
			if (m_Regex == null)
				m_Regex = @".+\\([^\\]+)\.sln$";
			if (m_TitlePattern == null)
				m_TitlePattern = "$1 - Visual Studio";
			if (m_TitlePatternBreakMode == null)
				m_TitlePatternBreakMode = "$1 - Visual Studio (Debugging)";
			if (m_TitlePatternRunningMode == null)
				m_TitlePatternRunningMode = "$1 - Visual Studio (Running)";
		}

		public WindowTitlePattern()
		{
			Fixup();
		}

		[Category("Organization")]
		[DisplayName("Name")]
		public string Name { get { return m_Name; } set { m_Name = value; } }

		[Category("Solution File Pathname Matching")]
		[DisplayName("Case Insensitive Regex")]
		public bool RegexIgnoreCase { get { return m_RegexIgnoreCase; } set { m_RegexIgnoreCase = value; } }

		[Category("Solution File Pathname Matching")]
		[DisplayName("Solution File Pathname Regex")]
		public string Regex
		{
			get { return m_Regex; }
			set
			{
				m_Regex = value;
				// This throws an exception if the regex is invalid and the
				// collection editor automatically shows a nice detailed error message.
				new Regex(value);
			}
		}

		[Category("Solution File Pathname Matching")]
		[DisplayName("Window Title Pattern")]
		public string TitlePattern { get { return m_TitlePattern; } set { m_TitlePattern = value; } }

		[Category("Solution File Pathname Matching")]
		[DisplayName("Window Title Pattern in Break Mode")]
		public string TitlePatternBreakMode { get { return m_TitlePatternBreakMode; } set { m_TitlePatternBreakMode = value; } }

		[Category("Solution File Pathname Matching")]
		[DisplayName("Window Title Pattern in Running Mode")]
		public string TitlePatternRunningMode { get { return m_TitlePatternRunningMode; } set { m_TitlePatternRunningMode = value; } }

		public override string ToString()
		{
			return m_Name;
		}
	}
}
