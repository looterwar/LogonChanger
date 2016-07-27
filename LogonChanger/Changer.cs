﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LogonEditor;
using NDesk.Options;
using SettingsVault;
using HelperLib = HelperLibrary.HelperLib;

namespace LogonChanger
{
    public static class Changer
    {

        public enum Mode
        {
            Local,
            Remote,
            Bing
        }

        public static void Update()
        {
            ConfigureWallpaperDir();

            var fileName = Settings.Default.Get<string>(Config.WallpaperDir) + Util.GenerateFileTimeStamp() + ".jpg";
            var result = false;

            switch (Settings.Default.Get<Mode>(Config.Mode, Mode.Bing))
            {
                case Mode.Bing:
                    var bingResource = new BingWebResource();
                    result = bingResource.GetResource(null, fileName);
                    break;
                case Mode.Remote:
                    var remoteResource = new WebResource();
                    result = remoteResource.GetResource(new Uri(Settings.Default.Get<string>(Config.Url)), fileName);
                    break;
                case Mode.Local:
                    var localResource = new LocalResource();
                    result = localResource.GetResource("");
                    break;
            }
            

            // If we failed to download the image just return
            if (!result)
                return;
            
            TakeOwnershipPri();
            UpdatePri(fileName);

        }

        public static void InitialiseSettings(string[] args = null)
        {
            Settings.Init(Config.SettingsFilePath);

            // Initalise empty settings file
            if (!File.Exists(Config.SettingsFilePath))
            {
                Settings.Default.Set(Config.Interval, 3600000); // 1 hour
                Settings.Default.Set(Config.WallpaperDir, @"C:\LogonWallpapers\");
                Settings.Default.Set(Config.Verbose, false);
                Settings.Default.Set(Config.Mode, Mode.Bing);

                Settings.Default.Save();
            }

            // Parse any arguments
            if (args?.Length > 0)
            {
                var p = new OptionSet()
                {
                    {"i|interval:", (int v) => Settings.Default.Set(Config.Interval, v)},
                    {"d|dir:", v => Settings.Default.Set(Config.WallpaperDir, v)},
                    {"u|url:", v => Settings.Default.Set(Config.Url, v)},
                    {"v", v => Settings.Default.Set(Config.Verbose, (v != null))},
                };

                try
                {
                    p.Parse(args);
                }
                catch (Exception ex)
                {
                    Logger.WriteError("Argument Failure: \n\n", ex);
                }

            }

            Logger.Verbose = Settings.Default.Get<bool>(Config.Verbose,false);
            Settings.Default.Save();
        }

        private static void UpdatePri(string resourceFileName)
        {
            if (File.Exists(Config.NewPriPath))
                File.Delete(Config.NewPriPath);

            LogonPriEditor.ModifyLogonPri(Config.TempPriPath, Config.NewPriPath, resourceFileName);
            File.Copy(Config.NewPriPath, Config.PriFileLocation,true);

            Logger.WriteInformation("Wallpaper Set!", true);
        }

        private static void ConfigureWallpaperDir()
        {
            if (!Directory.Exists(Settings.Default.Get<string>(Config.WallpaperDir)))
                Directory.CreateDirectory(Settings.Default.Get<string>(Config.WallpaperDir));
        }

        private static void TakeOwnershipPri()
        {
            HelperLib.TakeOwnership(Config.LogonFolder);
            HelperLib.TakeOwnership(Config.PriFileLocation);

            if (!File.Exists(Config.BakPriFileLocation))
            {
                Logger.WriteWarning("Could not find Windows.UI.Logon.pri.bak file. Creating new.");
                File.Copy(Config.PriFileLocation, Config.BakPriFileLocation);
            }

            HelperLib.TakeOwnership(Config.BakPriFileLocation);

            File.Copy(Config.BakPriFileLocation, Config.TempPriPath, true);

            if (File.Exists(Config.CurrentImageLocation))
            {
                var temp = Path.GetTempFileName();
                File.Copy(Config.CurrentImageLocation, temp, true);

                File.Delete(Config.CurrentImageLocation);
            }
        }
    }
}
