﻿using FFmpegView.Bass;
using System.Windows;

namespace FFmpegView.WpfDemo2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Core.Initialize();
            BassCore.Initialize();
        }
    }
}