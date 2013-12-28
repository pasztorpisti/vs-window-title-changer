using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

#if VS2010_AND_LATER
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
#endif

namespace VSWindowTitleChanger
{
	// Prevents Visual Studio from actually setting the window title. This class hooks the main window
	// and stores the original title but sets the actual window title only when this plugin asks.
	class VSMainWindow : NativeWindow
	{
		private IntPtr m_MainHWND;
		private bool m_SetTextEnabled;
		private string m_CurrentTitle;
		private string m_OriginalTitle;
		private bool m_IsAppActive;

		public bool IsAppActive { get { return m_IsAppActive; } }

		void SetIsAppActive(bool app_active)
		{
			if (m_IsAppActive == app_active)
				return;
			m_IsAppActive = app_active;
			if (OnWindowTitleUpdateNeeded != null)
				OnWindowTitleUpdateNeeded();
		}

		public string OriginalTitle { get { return m_OriginalTitle; } }

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		private static string GetWindowText(IntPtr hWnd)
		{
			int length = GetWindowTextLength(hWnd);
			StringBuilder sb = new StringBuilder(length + 1);
			GetWindowText(hWnd, sb, sb.Capacity);
			return sb.ToString();
		}

		[DllImport("user32.dll")]
		static extern IntPtr GetActiveWindow();

		public void Initialize(IntPtr main_hwnd)
		{
			m_MainHWND = main_hwnd;
			m_OriginalTitle = GetWindowText(main_hwnd);
			m_IsAppActive = GetActiveWindow() != IntPtr.Zero;
#if VS2010_AND_LATER
			TryfindTitleTextBlock();
#endif
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

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsIconic(IntPtr hWnd);

		public bool Minimized
		{
			get
			{
				return IsIconic(m_MainHWND);
			}
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		public bool IsForegroundWindow()
		{
			return m_MainHWND == GetForegroundWindow();
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
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
#if VS2010_AND_LATER
				if (m_TitleTextBlock != null)
					m_TitleTextBlock.Text = title;
#endif
			}
			finally
			{
				m_SetTextEnabled = false;
			}
		}

		[DllImport("user32.dll")]
		static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[DllImport("kernel32.dll")]
		static extern uint GetCurrentProcessId();

		// It seems that sometimes we don't receive WM_ACTIVATEAPP(1) when the debugged program quits and
		// this plugin thinks that this app is still inactive. We help on that by periodically polling this state.
		public void UpdateAppActive()
		{
			bool app_active = false;
			IntPtr foreground_wnd = GetForegroundWindow();
			if (foreground_wnd != null)
			{
				uint foreground_process_id;
				if (0 != GetWindowThreadProcessId(foreground_wnd, out foreground_process_id))
					app_active = foreground_process_id == GetCurrentProcessId();
			}
			SetIsAppActive(app_active);
		}

		public delegate void WindowTitleUpdateNeededHandler();
		public event WindowTitleUpdateNeededHandler OnWindowTitleUpdateNeeded;

		protected override void WndProc(ref Message m)
		{
			//System.Diagnostics.Debug.WriteLine(m.ToString());

			const int WM_SETTEXT = 0xC;
			const int WM_WINDOWPOSCHANGED = 0x47;
			const int WM_ACTIVATEAPP = 0x1C;
			const int WM_ACTIVATE = 6;

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
				switch (m.Msg)
				{
					case WM_ACTIVATEAPP:
						SetIsAppActive(m.WParam != IntPtr.Zero);
						break;

					case WM_WINDOWPOSCHANGED:
					case WM_ACTIVATE:
						if (OnWindowTitleUpdateNeeded != null)
							OnWindowTitleUpdateNeeded();
						break;
				}
				base.WndProc(ref m);
			}
		}

#if VS2010_AND_LATER
		TextBlock m_TitleTextBlock;

		void TryfindTitleTextBlock()
		{
			DependencyObject root = HwndSource.FromHwnd(m_MainHWND).RootVisual as DependencyObject;
			if (root != null)
			{
				DependencyObject titlebar = FindInSubtreeByClassNamePostfix(root, ".MainWindowTitleBar") as DependencyObject;
				if (titlebar != null)
				{
					DependencyObject dock_panel = FindChildByClassNamePostfix(titlebar, ".DockPanel");
					if (dock_panel != null)
					{
						m_TitleTextBlock = FindChildByClassNamePostfix(dock_panel, ".TextBlock") as TextBlock;
					}
				}
			}
		}

		DependencyObject FindChildByClassNamePostfix(DependencyObject parent, string postfix)
		{
			for (int i = 0, e = VisualTreeHelper.GetChildrenCount(parent); i < e; ++i)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child != null)
				{
					string type_name = child.GetType().FullName;
					if (type_name.EndsWith(postfix))
						return child;
				}
			}
			return null;
		}

		DependencyObject FindInSubtreeByClassNamePostfix(DependencyObject root, string postfix)
		{
			for (int i = 0, e = VisualTreeHelper.GetChildrenCount(root); i < e; ++i)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				if (child != null)
				{
					string type_name = child.GetType().FullName;
					if (type_name.EndsWith(postfix))
						return child;
					DependencyObject res = FindInSubtreeByClassNamePostfix(child, postfix);
					if (res != null)
						return res;
				}
			}
			return null;
		}
#endif
	}
}
