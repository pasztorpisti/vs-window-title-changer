using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSWindowTitleChanger
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	///
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the 
	/// IVsPackage interface and uses the registration attributes defined in the framework to 
	/// register itself and its components with the shell.
	/// </summary>
	// This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
	// a package.
	[PackageRegistration(UseManagedResourcesOnly = true)]
	// This attribute is used to register the informations needed to show the this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	[Guid(GuidList.guidVSWindowTitleChangerPkgString)]
	[ProvideOptionPage(typeof(ToolOptions), "VS Window Title Changer", "Settings", 0, 0, true)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.DesignMode)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.EmptySolution)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.NoSolution)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists)]
	public class VSWindowTitleChangerPackage : Package, IVsSolutionEvents, IVsDebuggerEvents
	{
		public VSWindowTitleChangerPackage()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
		}

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern bool SetWindowText(IntPtr hwnd, String lpString);

		private Dispatcher _uiThradDispatcher;
		private DispatcherTimer _dispatcherTimer;
		private bool _debug = false;

		private const int UPDATE_PERIOD_SECS = 5;
		private const int DEBUG_UPDATE_PERIOD_SECS = 1;


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
					slice_0_str = format.Substring(idx1+1);
				}
				else
				{
					// something like "1,5:4"
					slice_0_str = format.Substring(idx1+1, idx2-idx1-1);
					slice_1_str = format.Substring(idx2+1);
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

		private string TryMakeWindowTitleFromPattern(ToolOptions.WindowTitlePattern pattern, DBGMODE dbgmode, string solution_path)
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

		private void UpdateWindowTitle()
		{
			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			string solution_path, temp_solution_dir, temp_solution_options;
			if (VSConstants.S_OK != vs_solution.GetSolutionInfo(out temp_solution_dir, out solution_path, out temp_solution_options) ||
				solution_path == null)
				solution_path = "";

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			DBGMODE[] adbgmode = new DBGMODE[] { DBGMODE.DBGMODE_Design };
			if (VSConstants.S_OK != debugger.GetMode(adbgmode))
				adbgmode[0] = DBGMODE.DBGMODE_Design;
			DBGMODE dbgmode = adbgmode[0] & ~DBGMODE.DBGMODE_EncMask;

			ToolOptions options = (ToolOptions)GetDialogPage(typeof(ToolOptions));
			if (_debug != options.Debug)
			{
				_debug = options.Debug;
				_dispatcherTimer.Interval = new TimeSpan(0, 0, _debug ? DEBUG_UPDATE_PERIOD_SECS : UPDATE_PERIOD_SECS);
			}

			List<ToolOptions.WindowTitlePattern> patterns = options.WindowTitlePatterns;

			string title = null;
			foreach (ToolOptions.WindowTitlePattern pattern in patterns)
			{
				title = TryMakeWindowTitleFromPattern(pattern, dbgmode, solution_path);
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

			// This doesn't always return the actual VS main window handle, it seems to return
			// the handle of the currently active top level window of the process (if it has a taskbar button).
			//IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

			DTE dte = (DTE)GetService(typeof(DTE));
			IntPtr hwnd = (IntPtr)dte.MainWindow.HWnd;

			SetWindowText(hwnd, title);
		}

		void Schedule_UpdateWindowTitle()
		{
			_uiThradDispatcher.BeginInvoke(new Action(delegate() { UpdateWindowTitle(); }));
		}

		private uint _solutionEventsCookie;
		private uint _debuggerEventsCookie;

		protected override void Initialize()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
			base.Initialize();

			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			vs_solution.AdviseSolutionEvents(this, out _solutionEventsCookie);

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			debugger.AdviseDebuggerEvents(this, out _debuggerEventsCookie);

			_uiThradDispatcher = Dispatcher.CurrentDispatcher;

			_dispatcherTimer = new DispatcherTimer();
			_dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
			// Update every X seconds to handle unexpected window title changes
			_dispatcherTimer.Interval = new TimeSpan(0, 0, _debug ? DEBUG_UPDATE_PERIOD_SECS : UPDATE_PERIOD_SECS);
			_dispatcherTimer.Start();

			Schedule_UpdateWindowTitle();
		}

		private void dispatcherTimer_Tick(object sender, EventArgs e)
		{
			UpdateWindowTitle();
		}

		protected override void Dispose(bool disposing)
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Dispose() of: {0}", this.ToString()));
			_dispatcherTimer.Stop();

			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			vs_solution.UnadviseSolutionEvents(_solutionEventsCookie);

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			debugger.UnadviseDebuggerEvents(_debuggerEventsCookie);
		}

		// IVsSolutionEvents
		public virtual int OnAfterCloseSolution(object pUnkReserved)
		{
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		public virtual int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		{
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		public virtual int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnBeforeCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}
		// ~IVsSolutionEvents

		// IVsDebuggerEvents
		public virtual int OnModeChange(DBGMODE dbgmodeNew)
		{
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		// ~IVsDebuggerEvents
	}
}
