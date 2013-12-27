using System.Diagnostics;
using System.Windows.Forms;

namespace VSWindowTitleChanger
{
	public partial class TitleSetupEditorHelp : Form
	{
		public TitleSetupEditorHelp()
		{
			InitializeComponent();

			helpBrowser.DocumentText = TitleSetupEditorHelpResources.TitleSetupEditorHelp;
			helpBrowser.Navigating += helpBrowser_Navigating;

			FormClosing += TitleSetupEditorHelp_FormClosing;
		}

		void TitleSetupEditorHelp_FormClosing(object sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
			Visible = false;
		}

		private void helpBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
		{
			if (e.Url.Host.Length != 0)
			{
				e.Cancel = true;
				Process.Start(e.Url.ToString());
			}
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				Close();
				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}
	}
}