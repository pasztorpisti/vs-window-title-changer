﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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
			}
			finally
			{
				m_SetTextEnabled = false;
			}
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
						m_IsAppActive = m.WParam != IntPtr.Zero;
						if (OnWindowTitleUpdateNeeded != null)
							OnWindowTitleUpdateNeeded();
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
	}
}
