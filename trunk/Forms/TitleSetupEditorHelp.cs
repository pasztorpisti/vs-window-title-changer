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
			helpBrowser.Navigating += new WebBrowserNavigatingEventHandler(helpBrowser_Navigating);

			FormClosing += new FormClosingEventHandler(TitleSetupEditorHelp_FormClosing);
		}

		void TitleSetupEditorHelp_FormClosing(object sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
			Visible = false;
		}

		private void helpBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
		{
			e.Cancel = true;
			Process.Start(e.Url.ToString());
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