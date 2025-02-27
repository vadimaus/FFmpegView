using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FFmpegView.AvaloniaDemo.Models;
using FFmpegView.Bass;
using System;

namespace FFmpegView.AvaloniaDemo
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            BassCore.Initialize();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow()
                {
                    DataContext = new MainWindowViewModel()
                };
            }

            Console.WriteLine(FontManager.Current.DefaultFontFamilyName);
            Console.WriteLine(FontManager.Current.PlatformImpl.GetDefaultFontFamilyName());
            Console.WriteLine(string.Join(';', FontManager.Current.PlatformImpl.GetInstalledFontFamilyNames()));
            base.OnFrameworkInitializationCompleted();
        }

        public static Window? MainWindow
        {
            get
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }

                return null;
            }
        }

        public static MainWindowViewModel? ViewModel
        {
            get
            {
                return MainWindow?.DataContext as MainWindowViewModel;
            }
        }
    }
}