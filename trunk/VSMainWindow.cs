using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VSWindowTitleChanger
{
	class VSMainWindow : NativeWindow
	{
		private IntPtr m_MainHWND;
		private bool m_SetTextEnabled;
		private string m_CurrentTitle;
		private string m_OriginalTitle;

		public string OriginalTitle { get { return m_OriginalTitle; } }

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		private static string GetWindowText(IntPtr hWnd)
		{
			int length = GetWindowTextLength(hWnd);
			StringBuilder sb = new StringBuilder(length + 1);
			GetWindowText(hWnd, sb, sb.Capacity);
			return sb.ToString();
		}

		public void Initialize(IntPtr main_hwnd)
		{
			m_MainHWND = main_hwnd;
			m_OriginalTitle = GetWindowText(main_hwnd);
			AssignHandle(main_hwnd);
		}

		public void Deinitialize()
		{
			if (m_MainHWND != default(IntPtr))
			{
				ReleaseHandle();
				m_MainHWND = default(IntPtr);
			}
		}

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern bool SetWindowText(IntPtr hwnd, String lpString);

		public void SetTitle(string title)
		{
			if (m_CurrentTitle != null && m_CurrentTitle == title)
				return;
			m_CurrentTitle = title;
			m_SetTextEnabled = true;
			try
			{
				SetWindowText(m_MainHWND, title);
			}
			finally
			{
				m_SetTextEnabled = false;
			}
		}

		protected override void WndProc(ref Message m)
		{
			//System.Diagnostics.Debug.WriteLine(m.ToString());

			const int WM_SETTEXT = 0xC;

			if (m.Msg == WM_SETTEXT)
			{
				if (m_SetTextEnabled)
				{
					base.WndProc(ref m);
				}
				else
				{
					m_OriginalTitle = Marshal.PtrToStringUni(m.LParam);
					m.Result = (IntPtr)1;
				}
			}
			else
			{
				base.WndProc(ref m);
			}
		}
	}
}
