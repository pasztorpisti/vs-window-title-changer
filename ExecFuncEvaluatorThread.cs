using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Threading;
using VSWindowTitleChanger.ExpressionEvaluator;

namespace VSWindowTitleChanger
{
	// We can't simply execute processes on the main thread as the process
	// creation blocks and this could periodically hang the UI.
	class ExecFuncEvaluatorThread : ExecFuncEvaluator, IDisposable
	{
		static TimeSpan CACHE_ENTRY_DELETION_TIMEOUT = TimeSpan.FromMinutes(15);

		Thread m_ExecThread;

		AutoResetEvent m_WakeUpEvent = new AutoResetEvent(false);
		bool m_StopRequest = false;
		bool m_DebugMode = false;

		Dictionary<string, ExecInfo> m_ExecInfos = new Dictionary<string, ExecInfo>();

		public ExecFuncEvaluatorThread()
		{
			m_ExecThread = new Thread(ExecThread);
			m_ExecThread.Start();
		}

		public void Dispose()
		{
			lock (this)
			{
				m_StopRequest = true;
				m_WakeUpEvent.Set();
			}
			m_ExecThread.Join();
		}

		public bool DebugMode
		{
			set
			{
				lock (this)
				{
					m_DebugMode = value;
				}
			}
		}

		public string Evaluate(int exec_period_secs, string command, string workdir)
		{
			string id = GenerateUniqueIdFromExecParameters(command, workdir);
			ExecInfo exec_info;
			lock (this)
			{
				if (!m_ExecInfos.TryGetValue(id, out exec_info))
				{
					exec_info = new ExecInfo(exec_period_secs, command, workdir);
					m_ExecInfos.Add(id, exec_info);
				}
			}
			return exec_info.GetValue(exec_period_secs);
		}

		static string GenerateUniqueIdFromExecParameters(string command, string workdir)
		{
			return EscapeString(command) + "$" + EscapeString(workdir);
		}

		static string EscapeString(string s)
		{
			return s.Replace(@"\", @"\\").Replace(@"$", @"\$");
		}

		void ExecThread()
		{
			for (;;)
			{
				Dictionary<string, ExecInfo> exec_infos;
				bool debug_mode;

				lock (this)
				{
					if (m_StopRequest)
						break;
					debug_mode = m_DebugMode;
					exec_infos = new Dictionary<string, ExecInfo>(m_ExecInfos);
				}

				// Updating entries outside of the lock
				foreach (KeyValuePair<string, ExecInfo> entry in exec_infos)
					entry.Value.Update(debug_mode);

				// Removing entries that are out of use for some time.
				DateTime now = DateTime.Now;
				lock (this)
				{
					foreach (KeyValuePair<string, ExecInfo> entry in exec_infos)
					{
						if (now - entry.Value.LastExecTime >= CACHE_ENTRY_DELETION_TIMEOUT)
							m_ExecInfos.Remove(entry.Key);
					}
				}

				m_WakeUpEvent.WaitOne(200);
			}

			Cleanup();
		}

		void Cleanup()
		{
			Dictionary<string, ExecInfo> exec_infos;
			lock (this)
			{
				exec_infos = new Dictionary<string, ExecInfo>(m_ExecInfos);
			}

			foreach (KeyValuePair<string, ExecInfo> entry in exec_infos)
			{
				entry.Value.KillAndWaitIfNeeded();
			}
		}

		class ExecInfo
		{
			string m_Value = "";

			string m_Command;
			string m_Workdir;

			DateTime m_LastExecTime;
			DateTime m_NextUpdateTime;
			TimeSpan m_FollowingUpdateDelay;
			TimeSpan m_FollowingUpdateDelay2;

			public DateTime LastExecTime
			{
				get
				{
					lock (this)
					{
						return m_LastExecTime;
					}
				}
			}

			public ExecInfo(int allowed_value_age_secs, string command, string workdir)
			{
				m_LastExecTime = DateTime.Now;
				m_NextUpdateTime = m_LastExecTime;
				m_FollowingUpdateDelay = TimeSpan.FromSeconds(allowed_value_age_secs);
				m_FollowingUpdateDelay2 = m_FollowingUpdateDelay;
				m_Command = command;
				m_Workdir = workdir;
			}

			public string GetValue(int allowed_value_age_secs)
			{
				lock (this)
				{
					TimeSpan following_update_delay = TimeSpan.FromSeconds(allowed_value_age_secs);
					if (following_update_delay < m_FollowingUpdateDelay)
						m_FollowingUpdateDelay = following_update_delay;
					if (following_update_delay < m_FollowingUpdateDelay2)
						m_FollowingUpdateDelay2 = following_update_delay;
					DateTime next_update_time = m_LastExecTime + following_update_delay;
					if (next_update_time < m_NextUpdateTime)
						m_NextUpdateTime = next_update_time;
					return m_Value;
				}
			}

			void SetValue(string val)
			{
				lock (this)
				{
					m_Value = val;
					m_LastExecTime = DateTime.Now;
					m_NextUpdateTime = m_LastExecTime + m_FollowingUpdateDelay;
					m_FollowingUpdateDelay = m_FollowingUpdateDelay2;
					m_FollowingUpdateDelay2 = TimeSpan.FromDays(1000);
				}
			}

			void SetErrorMessageAndResetValue(string error_message)
			{
				Debug.WriteLine("EXEC ERROR: " + error_message);
				string val = m_DebugMode ? error_message.Replace("\r", "").Replace("\n", " ") : "";
				SetValue(val);
			}

			public void Update(bool debug_mode)
			{
				m_DebugMode = debug_mode;
				if (m_Process == null)
					LaunchProcessIfNeeded();
				else if (m_Process.HasExited)
					ProcessTerminated();
			}

			static string TransformOutput(string s)
			{
				s = s.Replace("\r", "");
				if (s.EndsWith("\n"))
					s = s.Substring(0, s.Length - 1);
				return s.Replace('\n', ' ');
			}

			void ProcessTerminated()
			{
				int exitcode = m_Process.ExitCode;
				Debug.WriteLine(string.Format("exec '{0}' '{1}' has finished with exitcode {2}", m_Command, m_Workdir, exitcode));

				if (exitcode == 0)
					SetValue(TransformOutput(m_StdOut.ToString()));
				else
					SetErrorMessageAndResetValue(string.Format("[[[exec exitcode={0} stdout={1} stderr={2}]]]", exitcode, m_StdOut.ToString(), m_StdErr.ToString()));

				m_StdOut = null;
				m_StdErr = null;
				m_Process = null;

				LaunchProcessIfNeeded();
			}

			void LaunchProcessIfNeeded()
			{
				DateTime next_update_time;
				lock (this)
				{
					next_update_time = m_NextUpdateTime;
				}
				if (DateTime.Now >= next_update_time)
					LaunchProcess();
			}

			bool m_DebugMode;
			Process m_Process;
			StringBuilder m_StdOut;
			StringBuilder m_StdErr;

			void LaunchProcess()
			{
				Process process = new Process();
				try
				{
					string comspec = Environment.GetEnvironmentVariable("COMSPEC");
					if (comspec == null)
						throw new Exception("Could not get the COMSPEC environment variable!");

					process.StartInfo.FileName = comspec;
					process.StartInfo.Arguments = "/C " + m_Command;

					if (m_Workdir.Length > 0)
					{
						if (!System.IO.Path.IsPathRooted(m_Workdir))
							throw new Exception("The workdir of exec commands must be either an empty string or an absolute path to an existing directory! workdir=" + m_Workdir);
						if (!System.IO.Directory.Exists(m_Workdir))
							throw new Exception("The exec workdir doesn't exist: " + m_Workdir);
						process.StartInfo.WorkingDirectory = m_Workdir;
					}
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardInput = false;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					process.StartInfo.ErrorDialog = false;
					process.StartInfo.CreateNoWindow = true;

					process.OutputDataReceived += process_OutputDataReceived;
					process.ErrorDataReceived += process_ErrorDataReceived;

					m_StdOut = new StringBuilder();
					m_StdErr = new StringBuilder();

					if (!process.Start())
					{
						SetErrorMessageAndResetValue("Error launching process!");
						return;
					}
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
				}
				catch (System.Exception ex)
				{
					SetErrorMessageAndResetValue("Process launch exception: " + ex.Message);
					return;
				}

				m_Process = process;
			}

			void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					m_StdOut.Append(e.Data);
					m_StdOut.Append('\n');
				}
			}

			void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					m_StdErr.Append(e.Data);
					m_StdErr.Append('\n');
				}
			}

			static void KillProcessAndChildren(int pid)
			{
				ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
				ManagementObjectCollection moc = searcher.Get();
				foreach (ManagementObject mo in moc)
				{
					KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
				}
				try
				{
					Process proc = Process.GetProcessById(pid);
					proc.Kill();
				}
				catch (Exception ex)
				{
					// An "Access is denied" message comes here when proc.kill() gets executed on
					// the "cmd /C" process but this is a known issue that doesn't cause any problems
					// because the child processes have already been successfully killed...
					Debug.WriteLine("Expected 'Access is denied' exception: " + ex.Message);
				}
			}

			public void KillAndWaitIfNeeded()
			{
				if (m_Process == null || m_Process.HasExited)
					return;

				// NOTE: m_Process.kill() unfortunately doesn't seem to work, others have also
				// found out that Process.kill() doesn't work for some reason if you start a
				// command with cmd using a commandline like "cmd /c mycommand"...
				// It seems that a Permission Denied error happens when we try to kill cmd.
				// Fortunately the recursive KillProcessAndChildren() function kills all of
				// its children successfully and then cmd also exits by itself.

				KillProcessAndChildren(m_Process.Id);
				m_Process.WaitForExit();
			}
		}
	}
}
