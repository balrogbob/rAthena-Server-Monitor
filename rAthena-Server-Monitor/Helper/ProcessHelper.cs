namespace rAthena_Server_Monitor.Helper;

public class ProcessHelper
{
    public async Task<ProcessResult> RunWithRedirect(string command)
    {
        var result = new ProcessResult();

        // Do not dispose early; keep process alive for continuous monitoring
        var process = new Process();

        var normalizedCommand = command.Trim().Trim('"');
        if (!Path.IsPathRooted(normalizedCommand))
        {
            normalizedCommand = Path.GetFullPath(normalizedCommand);
        }
        var workingDirectory = Path.GetDirectoryName(normalizedCommand) ?? Environment.CurrentDirectory;

        process.StartInfo.FileName = normalizedCommand;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.EnableRaisingEvents = true;

        var outputBuilder = new StringBuilder();
        var outputClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += ProcDataReceived;
        process.ErrorDataReceived += ProcDataReceived;
        process.Exited += ProcHasExited;

        void ProcDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                // individual stream closes generate null; mark appropriately
                // (both may get set multiple times, harmless)
                outputClosed.TrySetResult(true);
                errorClosed.TrySetResult(true);
                return;
            }
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                var proc = (Process)sender;
                var lower = proc.ProcessName.ToLower();
                LoginLog(sender, e, lower);
                CharLog(sender, e, lower);
                MapLog(sender, e, lower);
                WebLog(sender, e, lower);
                outputBuilder.AppendLine(e.Data);
            }
        }

        void LoginLog(object sender, DataReceivedEventArgs e, string lower)
        {
            var procLogin = ProcNameCfg(PublicClass.MySettings.LoginExePath);
            if (lower == procLogin.ToLower())
            {
                if (e.Data.Contains("[Error]"))
                {
                    UpCount(MyEnum.LogType.ErrorLogin);
                    UpLogs(MyEnum.LogType.ErrorLogin, e.Data);
                }
                else if (e.Data.Contains("set users"))
                {
                    try
                    {
                        var playerCount = e.Data.Split(':');
                        Program.FrmMain.Online = int.Parse(playerCount[2]);
                    }
                    catch { Program.FrmMain.Online = 0; }
                }
                OtherLog(sender, e);
                WriteCategorizedLogs(MyEnum.LogType.ErrorLogin, e.Data);
            }
        }

        void CharLog(object sender, DataReceivedEventArgs e, string lower)
        {
            var procChar = ProcNameCfg(PublicClass.MySettings.CharExePath);
            if (lower == procChar.ToLower())
            {
                if (e.Data.Contains("[Error]"))
                {
                    UpCount(MyEnum.LogType.ErrorChar);
                    UpLogs(MyEnum.LogType.ErrorChar, e.Data);
                }
                OtherLog(sender, e);
                WriteCategorizedLogs(MyEnum.LogType.ErrorChar, e.Data);
            }
        }

        void MapLog(object sender, DataReceivedEventArgs e, string lower)
        {
            var procMap = ProcNameCfg(PublicClass.MySettings.MapExePath);
            if (lower == procMap.ToLower())
            {
                if (e.Data.Contains("[Error]") || e.Data.Contains("script error"))
                {
                    UpCount(MyEnum.LogType.ErrorMap);
                    UpLogs(MyEnum.LogType.ErrorMap, e.Data);
                }
                OtherLog(sender, e);
                WriteCategorizedLogs(MyEnum.LogType.ErrorMap, e.Data);
            }
        }

        void WebLog(object sender, DataReceivedEventArgs e, string lower)
        {
            var procMap = ProcNameCfg(PublicClass.MySettings.WebExePath);
            if (lower == procMap.ToLower())
            {
                WriteCategorizedLogs(MyEnum.LogType.Web, e.Data);
            }
        }

        void OtherLog(object sender, DataReceivedEventArgs e)
        {
            if (e.Data.Contains("[Debug]")) { UpCount(MyEnum.LogType.Debug); UpLogs(MyEnum.LogType.Debug, e.Data); }
            else if (e.Data.Contains("[SQL]")) { UpCount(MyEnum.LogType.SQL); UpLogs(MyEnum.LogType.SQL, e.Data); }
            else if (e.Data.Contains("[Warning]")) { UpCount(MyEnum.LogType.Warning); UpLogs(MyEnum.LogType.Warning, e.Data); }
        }

        void WriteCategorizedLogs(MyEnum.LogType logType, string line)
        {
            if (line.Contains("[Status]")) WriteLogs(logType, MyEnum.ConsoleType.Status, line);
            else if (line.Contains("[Info]")) WriteLogs(logType, MyEnum.ConsoleType.Info, line);
            else if (line.Contains("[Notice]")) WriteLogs(logType, MyEnum.ConsoleType.Notice, line);
            else if (line.Contains("[Warning]")) WriteLogs(logType, MyEnum.ConsoleType.Warning, line);
            else if (line.Contains("[Error]")) WriteLogs(logType, MyEnum.ConsoleType.Error, line);
            else if (line.Contains("[SQL]")) WriteLogs(logType, MyEnum.ConsoleType.Sql, line);
            else if (line.Contains("[Debug]")) WriteLogs(logType, MyEnum.ConsoleType.Debug, line);
            else WriteLogs(logType, MyEnum.ConsoleType.Other, line);
        }

        try
        {
            process.Start();
        }
        catch (Exception error)
        {
            result.Completed = true;
            result.ExitCode = -1;
            result.Output = error.Message;
            return result;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Await natural exit; no artificial timeout so output continues indefinitely
        await process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(outputClosed.Task, errorClosed.Task).ConfigureAwait(false);

        result.Completed = true;
        result.ExitCode = process.ExitCode;
        if (process.ExitCode != 0)
        {
            result.Output = outputBuilder.ToString();
        }

        return result;
    }

    private void ProcHasExited(object sender, EventArgs e)
    {
        var proLogin = ProcNameCfg(PublicClass.MySettings.LoginExePath);
        var proChar = ProcNameCfg(PublicClass.MySettings.CharExePath);
        var proMap = ProcNameCfg(PublicClass.MySettings.MapExePath);
        var proWeb = ProcNameCfg(PublicClass.MySettings.WebExePath);

        var proc = (Process)sender;
        var lower = proc.ProcessName.ToLower();

        if (lower == proLogin.ToLower())
        {
            Program.FrmMain.txtLogin.Invoke(delegate { Program.FrmMain.txtLogin.AppendText(">>Login Server - stopped<<" + Environment.NewLine); });
        }
        if (lower == proChar.ToLower())
        {
            Program.FrmMain.txtChar.Invoke(delegate { Program.FrmMain.txtChar.AppendText(">>Char Server - stopped<<" + Environment.NewLine); });
        }
        if (lower == proMap.ToLower())
        {
            Program.FrmMain.txtMap.Invoke(delegate { Program.FrmMain.txtMap.AppendText(">>Map Server - stopped<<" + Environment.NewLine); });
        }
        if (lower == proWeb.ToLower())
        {
            Program.FrmMain.txtWeb.Invoke(delegate { Program.FrmMain.txtWeb.AppendText(">>Web Server - stopped<<" + Environment.NewLine); });
        }
    }

    public struct ProcessResult
    {
        public bool Completed;
        public int? ExitCode;
        public string Output;
    }

    public static void KillProcess(string processName)
    {
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.ProcessName.IndexOf(ProcNameCfg(processName), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    p.Kill();
                }
            }
            catch (Exception ex)
            {
                MyMessage.MsgShowError(ex.Message + "\n" + ex.StackTrace);
            }
        }
    }

    public static string ProcNameCfg(string cfgName)
    {
        var normalized = cfgName.Trim().Trim('"');
        if (!Path.IsPathRooted(normalized)) { try { normalized = Path.GetFullPath(normalized); } catch { } }
        return Path.GetFileNameWithoutExtension(normalized) ?? cfgName;
    }

    private void UpCount(MyEnum.LogType type)
    {
        switch (type)
        {
            case MyEnum.LogType.ErrorChar: Program.FrmMain.ErrorAll += 1; Program.FrmMain.ErrorChar += 1; break;
            case MyEnum.LogType.ErrorMap: Program.FrmMain.ErrorAll += 1; Program.FrmMain.ErrorMap += 1; break;
            case MyEnum.LogType.ErrorLogin: Program.FrmMain.ErrorAll += 1; Program.FrmMain.ErrorLogin += 1; break;
            case MyEnum.LogType.Warning: Program.FrmMain.Warning += 1; break;
            case MyEnum.LogType.SQL: Program.FrmMain.Sql += 1; break;
            case MyEnum.LogType.Debug: Program.FrmMain.IDebug += 1; break;
        }
    }

    private void UpLogs(MyEnum.LogType type, string value)
    {
        var result = value + Environment.NewLine;
        switch (type)
        {
            case MyEnum.LogType.ErrorChar: PublicClass.LogErrorAll += result; PublicClass.LogErrorChar += result; break;
            case MyEnum.LogType.ErrorMap: PublicClass.LogErrorAll += result; PublicClass.LogErrorMap += result; break;
            case MyEnum.LogType.ErrorLogin: PublicClass.LogErrorAll += result; PublicClass.LogErrorLogin += result; break;
            case MyEnum.LogType.Warning: PublicClass.LogWarning += result; break;
            case MyEnum.LogType.SQL: PublicClass.LogSql += result; break;
            case MyEnum.LogType.Debug: PublicClass.LogDebug += result; break;
        }
    }

    private static void WriteLogs(MyEnum.LogType type, MyEnum.ConsoleType consoleType, string value)
    {
        if (value.Contains("Loading")) return;
        var txtLog = type switch
        {
            MyEnum.LogType.ErrorChar => Program.FrmMain.txtChar,
            MyEnum.LogType.ErrorMap => Program.FrmMain.txtMap,
            MyEnum.LogType.ErrorLogin => Program.FrmMain.txtLogin,
            MyEnum.LogType.Web => Program.FrmMain.txtWeb,
            _ => new RichTextBox()
        };
        var color = consoleType switch
        {
            MyEnum.ConsoleType.Status => PublicClass.MySettings.Status,
            MyEnum.ConsoleType.Info => PublicClass.MySettings.Info,
            MyEnum.ConsoleType.Notice => PublicClass.MySettings.Notice,
            MyEnum.ConsoleType.Warning => PublicClass.MySettings.Warning,
            MyEnum.ConsoleType.Sql => PublicClass.MySettings.Sql,
            MyEnum.ConsoleType.Debug => PublicClass.MySettings.Debug,
            MyEnum.ConsoleType.Error => PublicClass.MySettings.Error,
            _ => Color.Gainsboro
        };
        if (consoleType != MyEnum.ConsoleType.Other)
        {
            var header = $"[{consoleType}]";
            var newEData = value.Remove(0, header.Length);
            txtLog.Invoke(delegate { txtLog.AppendText(header, color); txtLog.AppendText(newEData + Environment.NewLine); });
            return;
        }
        txtLog.Invoke(delegate { txtLog.AppendText(value + Environment.NewLine); });
    }
}