﻿// This file is a part of MPDN Extensions.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mpdn.Extensions.Framework;

namespace Mpdn.Extensions.PlayerExtensions
{
    public class YoutubeDlExtension : PlayerExtension
    {
        public static readonly DirectoryInfo TEMP_PATH = new DirectoryInfo(Path.Combine(Path.GetTempPath(), @"MPDNTemp"));

        public override ExtensionUiDescriptor Descriptor
        {
            get
            {
                return new ExtensionUiDescriptor
                {
                    Guid = new Guid("062842A1-F991-4556-9706-4268F59F4880"),
                    Name = "Test",
                    Description = "Test",
                    Copyright = ""
                };
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            Media.Loading += MediaLoading;
            Player.StateChanged += MediaClosed;
        }

        public override void Destroy()
        {
            Media.Loading -= MediaLoading;
            Player.StateChanged -= MediaClosed;

            base.Destroy();
        }

        private void MediaClosed(object sender, PlayerStateEventArgs e)
        {
            if (e.NewState == PlayerState.Closed && TEMP_PATH.Exists)
                foreach (FileInfo file in TEMP_PATH.GetFiles())
                    try { file.Delete(); }
                    catch (IOException) { }
        }

        private void MediaLoading(object sender, MediaLoadingEventArgs e)
        {
            if (IsValidUrl(e.Filename))
            {
                try
                {
                    var video = new YoutubeVideo(e.Filename);
                    e.Filename = video.Filename;
                }
                catch (Exception ex)
                {
                    // User may not have youtube-dl installed
                    Trace.WriteLine(ex);
                }
            }
        }

        private bool IsValidUrl(string uriName)
        {
            Uri uriResult;
            return Uri.TryCreate(uriName, UriKind.Absolute, out uriResult)
                && (   uriResult.Scheme == Uri.UriSchemeHttp
                    || uriResult.Scheme == Uri.UriSchemeHttps)
                && !Path.HasExtension(uriResult.AbsolutePath);
        }

        public override IList<Verb> Verbs
        {
            get { return new Verb[0]; }
        }

        private class YoutubeVideo 
        {
            private readonly string m_DownloadHash;
            private Process m_Downloader;

            private void PlayFile(string file)
            {
                Player.ActiveForm.Invoke((Action)(() => PlayerControl.OpenMedia(file)));
            }
            
            private void OnDownloaded(object sender, EventArgs e)
            {
                PlayFile(Filename);

                DisposeHelper.Dispose(ref m_Downloader);
            }

            private void OnWrite(object sender, DataReceivedEventArgs e)
            {
                Player.ActiveForm.Invoke((Action)(() => Player.OsdText.Show(e.Data)));
            }

            public string Filename
            {
                get
                {
                    if (TEMP_PATH.Exists)
                    {
                        var file = TEMP_PATH.EnumerateFiles().FirstOrDefault(f => f.Name.Contains(m_DownloadHash));
                        if (file != null)
                            return file.FullName;
                    }

                    return null;
                }
            }

            public YoutubeVideo(string url)
            {
                m_DownloadHash = url.GetHashCode().ToString("X8");
                var path = Path.Combine(TEMP_PATH.FullName, @"%(title)s_[" + m_DownloadHash + @"]_%(format_id)s.%(ext)s");

                m_Downloader = new Process();
                m_Downloader.StartInfo = new ProcessStartInfo
                {
                    FileName = "youtube-dl",
                    Arguments = String.Format("-f bestvideo[height<=?1080]+bestaudio/best \"{0}\" -o \"{1}\" --no-playlist", url, path),
                    //CreateNoWindow = true,
                    //RedirectStandardOutput = true,
                    //RedirectStandardError = true,
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false
                };
                //m_Downloader.EnableRaisingEvents = true;
                //m_Downloader.Exited += OnDownloaded;
                //m_Downloader.OutputDataReceived += OnWrite;

                m_Downloader.Start();
                //m_Downloader.BeginOutputReadLine();
                m_Downloader.WaitForExit();

                if (m_Downloader.ExitCode != 0)
                    throw new InvalidOperationException("Couldn't load url.");
            }


            public void Dispose()
            {
                DisposeHelper.Dispose(m_Downloader);

                var file = new FileInfo(Filename);
                try { file.Delete(); }
                catch (IOException) { }
            }
        }
    }
}