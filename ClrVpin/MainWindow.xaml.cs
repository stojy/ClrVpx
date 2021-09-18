﻿using System.Diagnostics;
using System.Text;
using System.Windows;
using ClrVpin.Shared;
using MaterialDesignThemes.Wpf;
using PropertyChanged;

namespace ClrVpin
{
    [AddINotifyPropertyChangedInterface]
    // ReSharper disable once UnusedMember.Global
    public partial class MainWindow
    {
        public MainWindow()
        {
            // initialise encoding to workaround the error "Windows -1252 is not supported encoding name"
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Model = new Model(this);
            DataContext = Model;

            InitializeComponent();

            Model.ScreenWorkArea = this.GetCurrentScreenWorkArea();

            Activated += (_, _) =>
            {
                if (Model.SettingsManager.WasReset && !_configWasResetHandled)
                {
                    _configWasResetHandled = true;
                    DialogHost.Show(new Message
                    {
                        Title = "Your settings have been reset",
                        Detail = "ClrVpin will now be restarted."
                    }).ContinueWith(_ => Dispatcher.Invoke(Restart));
                }
            };
        }
        
        private static void Restart()
        {
            Process.Start(Process.GetCurrentProcess().MainModule!.FileName!);
            Application.Current.Shutdown();
        }

        public Model Model { get; set; }
        private bool _configWasResetHandled;
    }
}