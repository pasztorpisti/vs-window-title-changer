using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms.Design;
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
		EExtensionActivationRule ExtensionActivationRule { get; set; }
		TitleSetup TitleSetup { get; set; }
	}

	[ClassInterface(ClassInterfaceType.AutoDual)]
	[ComVisible(true)]
	public class ToolOptions : DialogPage, ISerializedOptions
	{
		private bool m_Debug;
		private EExtensionActivationRule m_ExtensionActivationRule;
		private TitleSetup m_TitleSetup;

		[Category("Debugging")]
		[DisplayName("Debug Mode")]
		[Description("Shows additional debug info on the titlebar and the output window of Visual Studio.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Debug { get { return m_Debug; } set { m_Debug = value; } }

		[Category("Window Title Changer Options")]
		[DisplayName("Extension Activation")]
		[Description("Turn on/off the extension. You can also ask it to be active only in case of multiple Visual Studio instances.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public EExtensionActivationRule ExtensionActivationRule { get { return m_ExtensionActivationRule; } set { m_ExtensionActivationRule = value; } }

		[Category("Window Title Changer Options")]
		[DisplayName("Window Title Setup")]
		[Description("A simple or complex expression that evaluates into the title text of the Visual Studio main window.")]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public TitleSetup TitleSetup { get { return m_TitleSetup; } set { m_TitleSetup = value; } }


		public override void ResetSettings()
		{
			CopyOptions(new SerializedOptions(), this);
			Fixup();
		}

		public class SerializedOptions : ISerializedOptions
		{
			private bool m_Debug;
			private EExtensionActivationRule m_ExtensionActivationRule;
			private TitleSetup m_TitleSetup;

			public bool Debug { get { return m_Debug; } set { m_Debug = value; } }
			public EExtensionActivationRule ExtensionActivationRule { get { return m_ExtensionActivationRule; } set { m_ExtensionActivationRule = value; } }
			public TitleSetup TitleSetup { get { return m_TitleSetup; } set { m_TitleSetup = value; } }
		}

		private static void CopyOptions(ISerializedOptions src, ISerializedOptions dest)
		{
			foreach (PropertyInfo pi in typeof(ISerializedOptions).GetProperties())
				pi.SetValue(dest, pi.GetValue(src, null), null);
		}

		[Browsable(false)]
		public string TitleSetupString
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
			if (m_TitleSetup == null)
				m_TitleSetup = new TitleSetup();
			else
				m_TitleSetup.Fixup();
		}

		public ToolOptions()
		{
			Fixup();
		}

	}


	//---------------------------------------------------------------------------------------------

	[Editor(typeof(TitleSetupUITypeEditor), typeof(System.Drawing.Design.UITypeEditor))]
	[Serializable]
	public class TitleSetup : IComparable<TitleSetup>
	{
		string m_TitleExpression;
		public string TitleExpression { get { return m_TitleExpression; } set { m_TitleExpression = value; } }
		
		public void Fixup()
		{
			if (TitleExpression == null)
				TitleExpression = "orig_title";
		}

		public TitleSetup()
		{
			Fixup();
		}

		public override string ToString()
		{
			return GetType().Name;
		}

		public virtual int CompareTo(TitleSetup other)
		{
			return string.Compare(m_TitleExpression, other.m_TitleExpression, true);
		}
	}

	//---------------------------------------------------------------------------------------------

	public class TitleSetupUITypeEditor : UITypeEditor
	{
		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (context != null && context.Instance != null && provider != null)
			{
				IWindowsFormsEditorService edsvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
				if (edsvc != null)
				{
					// Intentionally showing a non-modal dialog to allow the user to
					// play around with the IDE/ while changing the plugin settings.
					PackageGlobals.Instance().ShowTitleExpressionEditor();
					return value;
				}
			}
			return value;
		}

		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}
	}

}
