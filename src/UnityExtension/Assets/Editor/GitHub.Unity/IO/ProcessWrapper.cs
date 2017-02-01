using System;
using System.Diagnostics;
using GitHub.Unity.Logging;
using System.Text;
using System.ComponentModel;

namespace GitHub.Unity
{
    class ProcessWrapper : IProcess
    {
        private static readonly ILogger logger = Logger.GetLogger<ProcessWrapper>();

        public event Action<string> OnOutputData;
        public event Action<string> OnErrorData;
        public event Action<IProcess> OnExit;

        private Process process;
        private ProcessState state;

        public ProcessWrapper(ProcessStartInfo psi)
        {
            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (s, e) =>
            {
                if (process.HasExited)
                {
                    state = ProcessState.Finished;
                }
                logger.Debug("Output - \"" + e.Data + "\" exited:" + process.HasExited);
                OnOutputData.SafeInvoke(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (process.HasExited)
                {
                    state = ProcessState.Finished;
                }

                if (e.Data == null) return;

                logger.Debug("Error - \"" + e.Data + "\" exited:" + process.HasExited);

                OnErrorData.SafeInvoke(e.Data);
                if (process.HasExited)
                {
                    OnExit.SafeInvoke(this);
                }
            };
            process.Exited += (s, e) =>
            {
                state = ProcessState.Finished;
                logger.Debug("Exit");
                OnExit.SafeInvoke(this);
            };
        }

        public void Run()
        {
            logger.Debug("Run: ");

            try
            {
                process.Start();
                state = ProcessState.Running;
            }
            catch (Win32Exception ex)
            {
                state = ProcessState.Exception;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Error code " + ex.NativeErrorCode);
                if (ex.NativeErrorCode == 2)
                {
                    sb.AppendLine("The system cannot find the file specified.");
                }
                foreach (string env in process.StartInfo.EnvironmentVariables.Keys)
                {
                    sb.AppendFormat("{0}:{1}", env, process.StartInfo.EnvironmentVariables[env]);
                    sb.AppendLine();
                }
                OnErrorData.SafeInvoke(String.Format("{0} {1}", ex.Message, sb.ToString()));
                OnExit.SafeInvoke(this);
                return;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public bool WaitForExit(int milliseconds)
        {
            logger.Debug("WaitForExit - time: " + milliseconds + "ms");

            // Workaround for a bug in which some data may still be processed AFTER this method returns true, thus losing the data.
            // http://connect.microsoft.com/VisualStudio/feedback/details/272125/waitforexit-and-waitforexit-int32-provide-different-and-undocumented-implementations
            bool waitSucceeded = process.WaitForExit(milliseconds);
            if (waitSucceeded)
            {
                process.WaitForExit();
            }
            return waitSucceeded;
        }

        public void WaitForExit()
        {
            logger.Debug("WaitForExit");
            process.WaitForExit();
        }

        public void Close()
        {
            process.Close();
        }

        public void Kill()
        {
            process.Kill();
        }

        public int Id { get { return process.Id; } }

        public bool HasExited { get { return state == ProcessState.Finished || state == ProcessState.Exception; } }

        enum ProcessState
        {
            NotRunning,
            Running,
            Finished,
            Exception
        }
    }
}