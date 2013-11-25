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
	public enum EExtensionActivationRule
	{
		AlwaysActive,
		AlwaysInactive,
		ActiveWithMultipleVSInstances,
		ActiveWithMultipleVSInstancesOfTheSameVersion,
	}

	public interface ISerializedOptions
	{
		bool Debug { get; set; }
		bool SlashPathSeparator { get; set; }
		EExtensionActivationRule ExtensionActivationRule { get; set; }
		List<WindowTitlePattern> WindowTitlePatterns { get; set; }
	}

	[ClassInterface(ClassInterfaceType.AutoDual)]
	[ComVisible(true)]
	public class ToolOptions : DialogPage, ISerializedOptions
	{
		private bool m_Debug;
		private bool m_SlashPathSeparator;
		private EExtensionActivationRule m_ExtensionActivationRule;
		private List<WindowTitlePattern> m_WindowTitlePatterns;

		[Category("Debugging")]
		[DisplayName("Debug Mode")]
		[Description("Shows additional debug info on the titlebar and the output window of Visual Studio.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Debug { get { return m_Debug; } set { m_Debug = value; } }

		[Category("Window Title Changer Options")]
		[DisplayName("Use '/' as Path Separator")]
		[Description("Replaces every backslashes to forward slashes in the pathnames of solution files and documents because matching backslashes results in uglier regular expressions.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool SlashPathSeparator { get { return m_SlashPathSeparator; } set { m_SlashPathSeparator = value; } }

		[Category("Window Title Changer Options")]
		[DisplayName("Extension Activation")]
		[Description("Turn on/off the extension. You can also ask it to be active only in case of multiple Visual Studio instances.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public EExtensionActivationRule ExtensionActivationRule { get { return m_ExtensionActivationRule; } set { m_ExtensionActivationRule = value; } }

		[Category("Window Title Changer Options")]
		[DisplayName("Solution Pathname and Window Title Patterns")]
		[Description("This collection contains the rules and patterns that are used to format the titlebar of the Visual Studio main window based on the state of the IDE.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public List<WindowTitlePattern> WindowTitlePatterns { get { return m_WindowTitlePatterns; } set { m_WindowTitlePatterns = value; } }


		public override void ResetSettings()
		{
			CopyOptions(new SerializedOptions(), this);
			Fixup();
		}

		public class SerializedOptions : ISerializedOptions
		{
			private bool m_Debug;
			private bool m_SlashPathSeparator = true;
			private EExtensionActivationRule m_ExtensionActivationRule;
			private List<WindowTitlePattern> m_WindowTitlePatterns;

			public bool Debug { get { return m_Debug; } set { m_Debug = value; } }
			public bool SlashPathSeparator { get { return m_SlashPathSeparator; } set { m_SlashPathSeparator = value; } }
			public EExtensionActivationRule ExtensionActivationRule { get { return m_ExtensionActivationRule; } set { m_ExtensionActivationRule = value; } }
			public List<WindowTitlePattern> WindowTitlePatterns { get { return m_WindowTitlePatterns; } set { m_WindowTitlePatterns = value; } }
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
					XmlWriterSettings settings = new XmlWriterSettings();
					settings.OmitXmlDeclaration = true;
					settings.Indent = false;
					settings.NewLineChars = "";
					settings.NewLineHandling = NewLineHandling.Entitize;
					settings.NewLineOnAttributes = false;
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


	//---------------------------------------------------------------------------------------------


	public class WindowTitlePattern
	{
		private string m_Name;
		private string m_Regex;
		private string[] m_ConditionalPatterns;

		public void Fixup()
		{
			if (m_Name == null)
				m_Name = "New Pattern";
			if (m_Regex == null)
				m_Regex = "";
			if (m_ConditionalPatterns == null)
				m_ConditionalPatterns = new string[0];
		}

		public WindowTitlePattern()
		{
			Fixup();
		}

		[Category("Organization")]
		[DisplayName("Name")]
		[Description("This is for here to make it easier for your to organize things. The extension doesn't use this.")]
		public string Name { get { return m_Name; } set { m_Name = value; } }

		[Category("Solution Specific Patterns")]
		[DisplayName("Solution File Path Regex")]
		[Description("A regex that is matched against the full pathname of the solution file. The captured groups will be available in the window title patterns as $sln_0, $sln_1, ...")]
		public string Regex
		{
			get { return m_Regex; }
			set
			{
				m_Regex = value == null ? "" : value;
				// This throws an exception if the regex is invalid and the collection editor
				// automatically shows a nice detailed error message (at least in VS2010).
				new Regex(value, RegexOptions.IgnoreCase);
			}
		}

		[Category("Solution Specific Patterns")]
		[DisplayName("Window Title Patterns")]
		[Description("A list of conditional and unconditional window title patterns. Conditional patters have the form 'IF condition_expr THEN window_title_expr'.")]
		public string[] ConditionalPatterns { get { return m_ConditionalPatterns; } set { m_ConditionalPatterns = value; } }

		public override string ToString()
		{
			return m_Name;
		}
	}
}
