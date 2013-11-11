using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shell;

namespace VSWindowTitleChanger
{
	[ClassInterface(ClassInterfaceType.AutoDual)]
	[ComVisible(true)]
	class ToolOptions : DialogPage
	{
		[Category("Solution File Pathname Matching")]
		[DisplayName("Debug Mode")]
		public bool Debug { get; set; }

		[Serializable]
		public class WindowTitlePattern
		{
			private string name;
			private bool regexIgnoreCase = true;
			private string regex;
			private string titlePattern;
			private string titlePatternBreakMode;
			private string titlePatternRunningMode;

			public void Fixup()
			{
				if (name == null)
					name = "New Pattern";
				if (regex == null)
					regex = @".+\\([^\\]+)\.sln$";
				if (titlePattern == null)
					titlePattern = "$1 - Visual Studio";
				if (titlePatternBreakMode == null)
					titlePatternBreakMode = "$1 - Visual Studio (Debugging)";
				if (titlePatternRunningMode == null)
					titlePatternRunningMode = "$1 - Visual Studio (Running)";
			}

			public WindowTitlePattern()
			{
				Fixup();
			}

			[Category("Organization")]
			[DisplayName("Name")]
			public string Name { get { return name; } set { name = value; } }

			[Category("Solution File Pathname Matching")]
			[DisplayName("Case Insensitive Regex")]
			public bool RegexIgnoreCase { get { return regexIgnoreCase; } set { regexIgnoreCase = value; } }

			[Category("Solution File Pathname Matching")]
			[DisplayName("Solution File Pathname Regex")]
			public string Regex
			{
				get { return regex; }
				set
				{
					regex = value;
					// This throws an exception if the regex is invalid and the
					// collection editor automatically shows a nice detailed error message.
					new Regex(value);
				}
			}

			[Category("Solution File Pathname Matching")]
			[DisplayName("Window Title Pattern")]
			public string TitlePattern { get { return titlePattern; } set { titlePattern = value; } }

			[Category("Solution File Pathname Matching")]
			[DisplayName("Window Title Pattern in Break Mode")]
			public string TitlePatternBreakMode { get { return titlePatternBreakMode; } set { titlePatternBreakMode = value; } }

			[Category("Solution File Pathname Matching")]
			[DisplayName("Window Title Pattern in Running Mode")]
			public string TitlePatternRunningMode { get { return titlePatternRunningMode; } set { titlePatternRunningMode = value; } }

			public override string ToString()
			{
				return name;
			}
		}

		private List<WindowTitlePattern> windowTitlePatterns = new List<WindowTitlePattern>();

		[Category("Window Title Changer Options")]
		[DisplayName("Window Title Patterns")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public List<WindowTitlePattern> WindowTitlePatterns
		{
			get { return windowTitlePatterns; }
			set { windowTitlePatterns = value; }
		}

		// The below invisible WindowTitlePatternsString property and its binary/base64 serialization is
		// there because I couldn't get VS to serialize the WindowTitlePatterns collection property. For this
		// reason the WindowTitlePatterns property is used only by the designer and the invisible
		// WindowTitlePatternsString is used for its serialization...
		private sealed class MySerializationBinder : System.Runtime.Serialization.SerializationBinder
		{
			public override Type BindToType(string assemblyName, string typeName)
			{
				return Type.GetType(String.Format("{0}, {1}", typeName, Assembly.GetExecutingAssembly().FullName));
			}
		}

		[Browsable(false)]
		public string WindowTitlePatternsString
		{
			get
			{
				BinaryFormatter bf = new BinaryFormatter();
				using (MemoryStream mem_stream = new MemoryStream())
				{
					foreach (WindowTitlePattern wtp in windowTitlePatterns)
						bf.Serialize(mem_stream, wtp);
					return Convert.ToBase64String(mem_stream.ToArray());
				}
			}
			set
			{
				List<WindowTitlePattern> wtps = new List<WindowTitlePattern>();
				try
				{
					BinaryFormatter bf = new BinaryFormatter();
					bf.Binder = new MySerializationBinder();
					using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(value)))
					{
						while (ms.Position != ms.Length)
						{
							WindowTitlePattern wtp = (WindowTitlePattern)bf.Deserialize(ms);
							wtp.Fixup();
							wtps.Add(wtp);
						}
					}
					windowTitlePatterns = wtps;
				}
				catch (System.Exception)
				{
				}
			}
		}
	}
}
