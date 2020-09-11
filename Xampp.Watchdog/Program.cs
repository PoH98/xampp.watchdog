using System;
using System.Diagnostics;
using System.IO;
using WUApiLib;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Net.Sockets;
using IniParser;
using System.ComponentModel;

namespace Xampp.Watchdog
{
    class Program
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;
        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    KillAll(true);
                    if (!IsAdministrator())
                    {
                        Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]:   This action need Admin to be disabled!");
                        var exeName = Process.GetCurrentProcess().MainModule.FileName;
                        ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
                        startInfo.Verb = "runas";
                        startInfo.Arguments = "--EnableUpdate";
                        Process.Start(startInfo);
                        Environment.Exit(0);
                    }
                    ProcessStartInfo startUpd = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c sc config wuauserv start=auto",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true
                    };
                    Process.Start(startUpd);
                    startUpd.Arguments = "/c net start wuauserv";
                    Process.Start(startUpd);
                    return true;
                default:
                    return false;
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
        private static readonly AutomaticUpdates auc = new AutomaticUpdates();
        public static readonly ProcessStartInfo apache_start = new ProcessStartInfo { FileName= "apache\\bin\\httpd.exe", CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden};
        public static readonly ProcessStartInfo mysql_start = new ProcessStartInfo { FileName = "mysql\\bin\\mysqld.exe", Arguments= "--defaults-file=mysql\\bin\\my.ini --standalone", CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        public static Process apache, mysql;
        private static long byteswritten = 0;
        private static bool Debug;
        static void Main(string[] args)
        {
            if (!File.Exists("watchdog.ini"))
            {
                File.WriteAllText("watchdog.ini", "[Watch]\nport=80");
            }
            FileIniDataParser parser = new FileIniDataParser();
            var data = parser.ReadFile("watchdog.ini");
            var port = Convert.ToInt32(data["Watch"]["port"]);
            if(args.Length > 0)
            {
                if (args[0].Contains("--D"))
                {
                    Debug = true;
                }
                else if (args[0] == "--EnableUpdate" && IsAdministrator())
                {
                    ProcessStartInfo startUpd = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c sc config wuauserv start=auto",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true
                    };
                    Process.Start(startUpd);
                    startUpd.Arguments = "/c net start wuauserv";
                    Process.Start(startUpd);
                    Environment.Exit(0);
                }
            }
            try
            {
                if (Process.GetProcessesByName("Xampp.Watchdog").Length > 1)
                {
                    Environment.Exit(0);
                }
                foreach(var proc in Process.GetProcessesByName("httpd"))
                {
                    proc.Kill();
                }
                foreach (var proc in Process.GetProcessesByName("mysqld"))
                {
                    proc.Kill();
                }
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
                _handler += new EventHandler(Handler);
                SetConsoleCtrlHandler(_handler, true);
                if (!Debug)
                {
                    string StartUp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "xampp.watchdog.lnk");
                    if (!File.Exists(StartUp))
                    {
                        IShellLink link = (IShellLink)new ShellLink();
                        link.SetDescription("Auto restart xampp, written by PoH98");
                        link.SetPath(Path.Combine(Environment.CurrentDirectory, "xampp.watchdog.exe"));
                        link.SetWorkingDirectory(Environment.CurrentDirectory);
                        IPersistFile file = (IPersistFile)link;
                        file.Save(StartUp, false);
                    }
                }
                apache = Process.Start(apache_start);
                apache.EnableRaisingEvents = true;
                mysql = Process.Start(mysql_start);
                mysql.EnableRaisingEvents = true;
                apache.Exited += Apache_Exited;
                mysql.Exited += Mysql_Exited;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Apache started with id " + apache.Id);
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: MySQL started with id " + mysql.Id);
                DateTime LastBackUp = DateTime.MinValue;
                string ip;
                do
                {
                    using (WebClient webclient = new WebClient())
                    {
                        ip = webclient.DownloadString("http://bot.whatismyipaddress.com/");
                    }
                    Console.ResetColor();
                    Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: IP Address detected as " + ip);
                    CheckWindowsUpdate();
                    try
                    {
                        if(!IsPortOpen(ip, port, new TimeSpan(0, 0, 5)))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]:Server check ERROR on address " + ip + ":" + port + "!");
                            KillAll(false);
                            apache = Process.Start(apache_start);
                            apache.EnableRaisingEvents = true;
                            mysql = Process.Start(mysql_start);
                            mysql.EnableRaisingEvents = true;
                            apache.Exited += Apache_Exited;
                            mysql.Exited += Mysql_Exited;
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Apache started with id " + apache.Id);
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: MySQL started with id " + mysql.Id);
                            Thread.Sleep(30000);
                        }
                        else
                        {
                            Thread.Sleep(30000);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]:Server check SUCCESS on address " + ip + ":" + port + "!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(ex.Message);
                    }
                    if (!Debug)
                    {
                        if (LastBackUp.Date != DateTime.Now.Date && DateTime.Now.Hour == 0)
                        {
                            KillAll(false);
                            BackUp(Path.Combine(Environment.CurrentDirectory, "mysql\\data"));
                            foreach (var folder in Directory.GetDirectories(Path.Combine(Environment.CurrentDirectory, "mysql\\data")))
                            {
                                BackUp(folder);
                            }
                            LastBackUp = DateTime.Now.Date;
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]:Database Backup success! Written " + SizeSuffix(byteswritten) + " total! ");
                            byteswritten = 0;
                            apache = Process.Start(apache_start);
                            mysql = Process.Start(mysql_start);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Apache started with id " + apache.Id);
                            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: MySQL started with id " + mysql.Id);

                        }
                    }
                }
                while (true);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }
        }

        private static void Mysql_Exited(object sender, EventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: MySQL had been killed unexpected! Restarting now!");
            Thread.Sleep(2000);
            mysql = Process.Start(mysql_start);
            mysql.EnableRaisingEvents = true;
            mysql.Exited += Mysql_Exited;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: MySQL started with id " + mysql.Id);
        }

        private static void Apache_Exited(object sender, EventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Apache had been killed unexpected! Restarting now!");
            Thread.Sleep(2000);
            apache = Process.Start(apache_start);
            apache.EnableRaisingEvents = true;
            apache.Exited += Apache_Exited;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Apache started with id " + apache.Id);
        }

        public static void KillAll(bool exiting)
        {
            apache.Exited -= Apache_Exited;
            mysql.Exited -= Mysql_Exited;
            if (!apache.HasExited)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Apache killed");
                apache.Kill();
            }
            if (!mysql.HasExited)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: MySQL killed");
                mysql.Kill();
            }
            if (File.Exists("mysql\\data\\"+ Environment.MachineName+".pid"))
            {
                File.Delete("mysql\\data\\" + Environment.MachineName + ".pid");
            }
            if (File.Exists("apache\\logs\\httpd.pid"))
            {
                File.Delete("apache\\logs\\httpd.pid");
            }
            if(!exiting)
                Thread.Sleep(5000);
        }

        private static Task ConsumeOutput(TextReader reader, Action<object> callback)
        {
            return new Task().AddTask(callback, reader.ReadToEnd());
        }

        private static void BackUp(string folder)
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                var destination = file.Replace("\\data\\", "\\backup\\");
                if (!Directory.Exists(destination.Substring(0, destination.LastIndexOf("\\"))))
                {
                    Directory.CreateDirectory(destination.Substring(0, destination.LastIndexOf("\\")));
                }
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
                FileInfo info = new FileInfo(file);
                byteswritten += info.Length;
                File.Copy(file, destination);
            }
        }

        static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void CheckWindowsUpdate()
        {
            bool isWUEnabled = auc.ServiceEnabled;
            if (isWUEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Windows Update is Enabled");
                if (!IsAdministrator())
                {
                    Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]:   This action need Admin to be disabled!");
                    var exeName = Process.GetCurrentProcess().MainModule.FileName;
                    ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
                    startInfo.Verb = "runas";
                    Process.Start(startInfo);
                    Environment.Exit(0);
                }
                UpdateSession uSession = new UpdateSession();
                IUpdateSearcher uSearcher = uSession.CreateUpdateSearcher();
                uSearcher.Online = false;
                try
                {
                    ISearchResult sResult = uSearcher.Search("IsInstalled=0 And IsHidden=0");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Found " + sResult.Updates.Count + " updates pending for install");
                    foreach (IUpdate update in sResult.Updates)
                    {
                        Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]:   " + update.Title);
                    }
                    Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Who the fuck cares what update is havent installed?");
                }
                catch (Exception ex)
                {
                    if (ex.ToString().ToLower().Contains("disabled"))
                    {
                        return;
                    }
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Something went wrong: " + ex.Message);
                }
                auc.Settings.NotificationLevel = AutomaticUpdatesNotificationLevel.aunlNotifyBeforeDownload;
                auc.Settings.Save();
                ProcessStartInfo killUpd = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c net stop wuauserv",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true
                };
                var winUpd = Process.Start(killUpd);
                Task task3 = ConsumeOutput(winUpd.StandardOutput, s =>
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(s);
                });
                winUpd.WaitForExit();
                killUpd.Arguments = "/c sc config wuauserv start= disabled";
                winUpd = Process.Start(killUpd);
                Task task4 = ConsumeOutput(winUpd.StandardOutput, s =>
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(s);
                });
                winUpd.WaitForExit();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Disabled Windows Update");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "]: Windows Update is Disabled");
            }
        }

        private static bool IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeout);
                    client.EndConnect(result);
                    return success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
