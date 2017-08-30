// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Mpdn.Extensions.Framework;
using Mpdn.Extensions.PlayerExtensions.DisplayChangerNativeMethods;

namespace Mpdn.Extensions.PlayerExtensions
{
    public class DisplayChanger : PlayerExtension<DisplayChangerSettings, DisplayChangerConfigDialog>
    {
        private Screen m_RestoreScreen;
        private int m_RestoreFrequency;

        private readonly List<int> m_AllRefreshRates = new List<int>();

        public override ExtensionUiDescriptor Descriptor
        {
            get
            {
                return new ExtensionUiDescriptor
                {
                    Guid = new Guid("9C1BBA5B-B956-43E1-9A91-58B72571EF82"),
                    Name = "Display Changer",
                    Description = "Changes display refresh rate based on video"
                };
            }
        }

        protected override string ConfigFileName
        {
            get { return "Example.DisplayChanger"; }
        }

        public override void Initialize()
        {
            base.Initialize();

            Player.StateChanged += PlayerStateChanged;
            Player.Closed += FormClosed;
            foreach (var screen in Screen.AllScreens)
            {
                m_AllRefreshRates.Add(GetRefreshRate(screen));
            }
        }

        public override void Destroy()
        {
            base.Destroy();

            Player.StateChanged -= PlayerStateChanged;
            Player.Closed -= FormClosed;
        }

        private void FormClosed(object sender, EventArgs eventArgs)
        {
            RestoreSettings();

            if (Settings.RestoreOnExit)
            {
                for (int i = 0; i < m_AllRefreshRates.Count; i++)
                {
                    var rate = m_AllRefreshRates[i];
                    ChangeRefreshRate(Screen.AllScreens[i], rate);
                }
            }
        }

        private void PlayerStateChanged(object sender, PlayerStateEventArgs e)
        {
            if (Media.VideoInfo == null) return;

            if (e.NewState == PlayerState.Closed)
            {
                RestoreSettings();
                return;
            }

            if (e.OldState != PlayerState.Closed)
                return;

            if (!Activated())
                return;

            m_RestoreFrequency = 0;

            var screen = Screen.FromControl(Gui.VideoBox);

            var frequencies = GetFrequencies(screen);

            if (!frequencies.Any())
                return;

            var timePerFrame = Media.VideoInfo.AvgTimePerFrame;
            if (Math.Abs(timePerFrame) < 1)
                return;

            var fps = Math.Round(1000000 / timePerFrame, 3);
            if (Math.Abs(fps) < 1) 
                return;

            // Find the highest frequency that matches fps
            var frequenciesDescending = frequencies.OrderByDescending(f => f).ToArray();
            var frequency = frequenciesDescending.FirstOrDefault(f => MatchFrequencies(f, fps));
            if (frequency == 0)
            {
                // Exact match (23 for 23.976) not found
                // Find the closest one that matches fps (e.g. 24 for 23.976)
                frequency = frequenciesDescending.FirstOrDefault(f => MatchRoundedFrequencies(f, fps));
            }
            if (frequency == 0)
            {
                // Still couldn't find a match
                if (!Settings.HighestRate)
                    return;

                // Use the highest rate available
                frequency = frequenciesDescending.First();
            }
            
            ChangeRefreshRate(screen, frequency);
        }

        private static bool MatchRoundedFrequencies(int f, double fps)
        {
            return f % (int) Math.Round(fps) == 0;
        }

        private static bool MatchFrequencies(int f, double fps)
        {
            var multiples = Math.Round(f/fps);
            fps *= multiples;

            return f % (int) fps == 0;
        }

        private bool Activated()
        {
            if (!Settings.Activate)
                return false;

            if (!Settings.Restricted) 
                return true;

            // Parse video types
            var videoTypes = Settings.VideoTypes.ToLowerInvariant().Split(' ');
            return videoTypes.Any(VideoSpecifier.Match);
        }

        private void RestoreSettings()
        {
            if (m_RestoreFrequency == 0)
                return;

            if (Settings.Restore)
            {
                ChangeRefreshRate(m_RestoreScreen, m_RestoreFrequency);
            }
            m_RestoreFrequency = 0;
        }

        private void ChangeRefreshRate(Screen screen, int frequency)
        {
            bool wasFullScreen = false;
            if (Player.FullScreenMode.Active)
            {
                wasFullScreen = true;
                // We can't change frequency in exclusive mode
                Player.FullScreenMode.Active = false;
            }

            var dm = NativeMethods.CreateDevmode(screen.DeviceName);
            if (GetSettings(ref dm, screen.DeviceName) == 0)
                return;

            if (dm.dmDisplayFrequency == frequency)
                return;

            var oldFreq = dm.dmDisplayFrequency;
            m_RestoreScreen = screen;

            dm.dmFields = (int) DM.DisplayFrequency;
            dm.dmDisplayFrequency = frequency;
            dm.dmDeviceName = screen.DeviceName;
            bool continuePlaying = false;
            if (Player.State == PlayerState.Playing)
            {
                continuePlaying = true;
                Media.Pause(false);
            }
            if (ChangeSettings(dm))
            {
                m_RestoreFrequency = oldFreq;
            }
            if (continuePlaying)
            {
                Media.Play(false);
            }
            if (wasFullScreen)
            {
                Player.FullScreenMode.Active = true;
            }
        }

        private static IList<int> GetFrequencies(Screen screen)
        {
            var frequencies = new List<int>();
            int index = 0;
            while (true)
            {
                var dm = NativeMethods.CreateDevmode(screen.DeviceName);
                if (GetSettings(ref dm, screen.DeviceName, index++) == 0)
                    break;

                if (dm.dmBitsPerPel != 32)
                    continue;

                if (dm.dmDisplayFlags != 0) // Only want progressive modes (1: DM_GRAYSCALE, 2: DM_INTERLACED)
                    continue;

                if (dm.dmPelsWidth != screen.Bounds.Width)
                    continue;

                if (dm.dmPelsHeight != screen.Bounds.Height)
                    continue;

                frequencies.Add(dm.dmDisplayFrequency);
            }

            return frequencies;
        }

        private static int GetRefreshRate(Screen screen)
        {
            var dm = NativeMethods.CreateDevmode(screen.DeviceName);
            return GetSettings(ref dm, screen.DeviceName) == 0 ? 0 : dm.dmDisplayFrequency;
        }

        private static bool ChangeSettings(DEVMODE dm)
        {
            if (NativeMethods.ChangeDisplaySettingsEx(dm.dmDeviceName, ref dm, IntPtr.Zero,
                    NativeMethods.CDS_UPDATEREGISTRY, IntPtr.Zero) == NativeMethods.DISP_CHANGE_SUCCESSFUL)
            {
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, NativeMethods.WM_DISPLAYCHANGE,
                    new IntPtr(NativeMethods.SPI_SETNONCLIENTMETRICS), IntPtr.Zero);
                return true;
            }
            Trace.Write("Failed to change display refresh rate");
            return false;
        }

        private static int GetSettings(ref DEVMODE dm, string deviceName)
        {
            // helper to obtain current settings
            return GetSettings(ref dm, deviceName, NativeMethods.ENUM_CURRENT_SETTINGS);
        }

        private static int GetSettings(ref DEVMODE dm, string deviceName, int iModeNum)
        {
            // helper to wrap EnumDisplaySettings Win32 API
            return NativeMethods.EnumDisplaySettings(deviceName, iModeNum, ref dm);
        }
    }

    public class DisplayChangerSettings
    {
        public DisplayChangerSettings()
        {
            Activate = false;
            Restore = false;
            RestoreOnExit = false;
            HighestRate = false;
            Restricted = false;
        }

        public bool Activate { get; set; }
        public bool Restore { get; set; }
        public bool RestoreOnExit { get; set; }
        public bool HighestRate { get; set; }
        public bool Restricted { get; set; }
        public string VideoTypes { get; set; }
    }
}
