﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClrVpin.Models;
using ClrVpin.Models.Settings;
using ClrVpin.Shared;
using MaterialDesignExtensions.Controls;
using PropertyChanged;
using Utils;

namespace ClrVpin.Scanner
{
    [AddINotifyPropertyChangedInterface]
    public class ScannerViewModel
    {
        public ScannerViewModel()
        {
            StartCommand = new ActionCommand(Start);
            //ConfigureCheckContentTypesCommand = new ActionCommand<string>(ConfigureCheckContentTypes);
            CheckContentTypesView = new ListCollectionView(CreateCheckContentTypes().ToList());

            CheckHitTypesView = new ListCollectionView(CreateCheckHitTypes().ToList());

            _fixHitTypes = CreateFixHitTypes();
            FixHitTypesView = new ListCollectionView(_fixHitTypes.ToList());
        }

        public ListCollectionView CheckContentTypesView { get; set; }
        public ListCollectionView CheckHitTypesView { get; set; }
        public ListCollectionView FixHitTypesView { get; set; }

        public ObservableCollection<Game> Games { get; set; }
        public ICommand StartCommand { get; set; }
        public Config Config { get; } = Model.Config;

        public void Show(Window parent)
        {
            _scannerWindow = new MaterialWindow
            {
                Owner = parent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                //SizeToContent = SizeToContent.WidthAndHeight,
                Width = 400,
                Height = 465,
                Content = this,
                Resources = parent.Resources,
                ContentTemplate = parent.FindResource("ScannerTemplate") as DataTemplate,
                ResizeMode = ResizeMode.NoResize,
                Title = "Scanner"
            };

            _scannerWindow.Show();
            parent.Hide();

            _scannerWindow.Closed += (_, _) =>
            {
                Model.Config.Save();
                parent.Show();
            };
        }

        private static IEnumerable<FeatureType> CreateCheckContentTypes()
        {
            // show all hit types
            var featureTypes = Config.ContentTypes.Select(contentType =>
            {
                var featureType = new FeatureType((int)contentType.Enum)
                {
                    Description = contentType.Description,
                    Tip = contentType.Tip,
                    IsSupported = true,
                    IsActive = Model.Config.SelectedCheckContentTypes.Contains(contentType.Description),
                    SelectedCommand = new ActionCommand(() => Model.Config.SelectedCheckContentTypes.Toggle(contentType.Description))
                };

                return featureType;
            });

            return featureTypes.ToList();
        }

        private IEnumerable<FeatureType> CreateCheckHitTypes()
        {
            // show all hit types
            var featureTypes = Config.HitTypes.Select(hitType =>
            {
                var featureType = new FeatureType((int)hitType.Enum)
                {
                    Description = hitType.Description,
                    Tip = hitType.Tip,
                    IsSupported = true,
                    IsActive = Model.Config.SelectedCheckHitTypes.Contains(hitType.Enum)
                };

                featureType.SelectedCommand = new ActionCommand(() =>
                {
                    Model.Config.SelectedCheckHitTypes.Toggle(hitType.Enum);

                    // toggle the fix hit type checked & enabled
                    var fixHitType = _fixHitTypes.First(x => x.Description == featureType.Description);
                    fixHitType.IsSupported = featureType.IsActive && !fixHitType.IsNeverSupported;
                    if (!featureType.IsActive)
                    {
                        fixHitType.IsActive = false;
                        Model.Config.SelectedFixHitTypes.ToggleOff(hitType.Enum);
                    }
                });

                return featureType;
            });

            return featureTypes.ToList();
        }

        private static IEnumerable<FeatureType> CreateFixHitTypes()
        {
            // show all hit types, but allow them to be enabled and selected indirectly via the check hit type
            var contentTypes = Config.HitTypes.Select(hitType => new FeatureType((int)hitType.Enum)
            {
                Description = hitType.Description,
                Tip = hitType.Tip,
                IsNeverSupported = hitType.Enum == HitTypeEnum.Missing,
                IsSupported = Model.Config.SelectedCheckHitTypes.Contains(hitType.Enum) && hitType.Enum != HitTypeEnum.Missing,
                IsActive = Model.Config.SelectedFixHitTypes.Contains(hitType.Enum) && hitType.Enum != HitTypeEnum.Missing,
                SelectedCommand = new ActionCommand(() => Model.Config.SelectedFixHitTypes.Toggle(hitType.Enum))
            });

            return contentTypes.ToList();
        }

        private async void Start()
        {
            _scannerWindow.Hide();
            Logging.Logger.Clear();

            var progress = new ProgressViewModel();
            progress.Show(_scannerWindow);
            
            // todo; retrieve 'missing games' from spreadsheet

            progress.Update("Loading Database", 0);
            var games = TableUtils.GetGamesFromDatabases();
            
            progress.Update("Checking Files", 30);
            var unknownFiles = ScannerUtils.Check(games);

            progress.Update("Fixing Files", 60);
            var gameFiles = await ScannerUtils.FixAsync(games, Settings.BackupFolder);

            progress.Update("Removing Unknown Files", 90);
            await ScannerUtils.RemoveAsync(unknownFiles);

            progress.Update("Preparing Results", 100);
            await Task.Delay(10);
            Games = new ObservableCollection<Game>(games);
            
            // todo; remove concat.. for statistics!!
            ShowResults(gameFiles, unknownFiles, progress.Duration);
         
            progress.Close();
        }

        private void ShowResults(ICollection<FileDetail> gameFiles, ICollection<FileDetail> unknownFiles, TimeSpan duration)
        {
            var scannerStatistics = new ScannerStatisticsViewModel(Games, duration, gameFiles, unknownFiles);
            scannerStatistics.Show(_scannerWindow, WindowMargin, WindowMargin);

            var scannerResults = new ScannerResultsViewModel(Games);
            scannerResults.Show(_scannerWindow, scannerStatistics.Window.Left + scannerStatistics.Window.Width + WindowMargin, scannerStatistics.Window.Top);

            var scannerExplorer = new ScannerExplorerViewModel(Games);
            scannerExplorer.Show(_scannerWindow, scannerResults.Window.Left, scannerResults.Window.Top + scannerResults.Window.Height + WindowMargin);

            _loggingWindow = new Logging.Logging();
            _loggingWindow.Show(_scannerWindow, scannerExplorer.Window.Left, scannerExplorer.Window.Top + scannerExplorer.Window.Height + WindowMargin);

            scannerStatistics.Window.Closed += (_, _) =>
            {
                scannerResults.Close();
                scannerExplorer.Close();
                _loggingWindow.Close();
                _scannerWindow.Show();
            };
        }

        private readonly IEnumerable<FeatureType> _fixHitTypes;
        private Window _scannerWindow;
        private Logging.Logging _loggingWindow;
        private const int WindowMargin = 5;
        public Models.Settings.Settings Settings { get; } = SettingsManager.Settings;
    }
}