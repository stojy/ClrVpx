﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClrVpin.Controls;
using ClrVpin.Controls.FolderSelection;
using ClrVpin.Extensions;
using ClrVpin.Logging;
using ClrVpin.Models.Settings;
using ClrVpin.Models.Shared;
using ClrVpin.Models.Shared.Game;
using ClrVpin.Shared;
using ClrVpin.Shared.FeatureType;
using PropertyChanged;
using Utils;
using Utils.Extensions;

namespace ClrVpin.Merger;

[AddINotifyPropertyChangedInterface]
public class MergerViewModel : IShowViewModel
{
    public MergerViewModel() 
    {
        StartCommand = new ActionCommand(Start);
        DestinationContentTypeSelectedCommand = new ActionCommand(UpdateIsValid);

        CreateMatchCriteriaTypes();

        CreateIgnoreCriteria();

        MergeOptionsView = new ListCollectionView(CreateMergeOptions().ToList());

        SourceFolderModel = new FolderTypeModel("Source", Settings.Merger.SourceFolder, folder =>
        {
            Settings.Merger.SourceFolder = folder;
            TryUpdateDestinationFolder(folder);
        });

        _destinationContentTypes = Model.Settings.GetFixableContentTypes().Select(x => x.Description);
        DestinationContentTypes = new ObservableCollection<string>(_destinationContentTypes);

        IgnoreWordsString = string.Join(", ", Settings.Merger.IgnoreIWords);
        IgnoreWordsChangedCommand = new ActionCommand(IgnoreWordsChanged);

        UpdateIsValid();
    }

    public bool IsValid { get; set; }

    public ListCollectionView MergeOptionsView { get; }

    public FolderTypeModel SourceFolderModel { get; }
    public ObservableCollection<string> DestinationContentTypes { get; }

    public ICommand DestinationContentTypeSelectedCommand { get; set; }
    public ICommand StartCommand { get; }
    public Models.Settings.Settings Settings { get; } = Model.Settings;

    public string IgnoreWordsString { get; set; }
    public ICommand IgnoreWordsChangedCommand { get; set; }

    public FeatureType IgnoreIfNotNewerFeature { get; private set; }
    public FeatureType IgnoreIfSmallerFeature { get; private set; }
    public FeatureType IgnoreIfContainsWordsFeature { get; private set; }
    public FeatureType DeleteIgnoredFilesOptionFeature { get; private set; }
    public FeatureType IgnoreSelectClearAllFeature { get; private set; }

    public FeatureType MatchDuplicate { get; private set; }

    public FeatureType MatchFuzzy { get; private set; }

    public FeatureType MatchTableName { get; private set; }

    public FeatureType MatchWrongCase { get; private set; }
    public FeatureType MatchSelectClearAllFeature { get; private set; }

    public Window Show(Window parent)
    {
        _window = new MaterialWindowEx
        {
            Owner = parent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = this,
            Resources = parent.Resources,
            ContentTemplate = parent.FindResource("MergerTemplate") as DataTemplate,
            ResizeMode = ResizeMode.NoResize,
            Title = "Merger"
        };

        _window.Show();
        _window.Closed += (_, _) => Model.SettingsManager.Write();

        return _window;
    }

    private FeatureType CreateDeleteIgnoredFilesOption()
    {
        var feature = new FeatureType(-1)
        {
            Description = StaticSettings.DeleteIgnoredFilesOption.Description,
            Tip = StaticSettings.DeleteIgnoredFilesOption.Tip,
            IsSupported = true,
            IsActive = Settings.Merger.DeleteIgnoredFiles,
            SelectedCommand = new ActionCommand(() => Settings.Merger.DeleteIgnoredFiles = !Settings.Merger.DeleteIgnoredFiles)
        };

        return feature;
    }

    private void IgnoreWordsChanged()
    {
        Settings.Merger.IgnoreIWords = IgnoreWordsString == null ? new List<string>() : IgnoreWordsString.Split(",").Select(x => x.Trim().ToLower()).ToList();
    }

    private void UpdateIsValid() => IsValid = !string.IsNullOrEmpty(Settings.Merger.DestinationContentType);

    private void TryUpdateDestinationFolder(string folder)
    {
        // attempt to assign destination folder automatically based on the specified folder
        var contentType = _destinationContentTypes.FirstOrDefault(c => folder.ToLower().EndsWith(c.ToLower()));
        Settings.Merger.DestinationContentType = contentType;

        UpdateIsValid();
    }

    private void CreateMatchCriteriaTypes()
    {
        // show all match criteria types
        // - except for unknown and unsupported which are used 'under the hood' for subsequent reporting
        var featureTypesView = FeatureOptions.CreateFeatureOptionsSelectionsView(
            StaticSettings.MatchTypes.Where(x => !x.Enum.In(HitTypeEnum.CorrectName, HitTypeEnum.Unknown, HitTypeEnum.Unsupported)),
            Settings.Merger.SelectedMatchTypes);

        // create separate property for each so they can be referenced individually in the UI
        MatchWrongCase = featureTypesView.First(x => x.Id == (int)HitTypeEnum.WrongCase);
        MatchTableName = featureTypesView.First(x => x.Id == (int)HitTypeEnum.TableName);
        MatchDuplicate = featureTypesView.First(x => x.Id == (int)HitTypeEnum.DuplicateExtension);
        MatchFuzzy = featureTypesView.First(x => x.Id == (int)HitTypeEnum.Fuzzy);
        MatchSelectClearAllFeature = featureTypesView.First(x => x.Id == FeatureOptions.SelectAllId);
    }

    private void CreateIgnoreCriteria()
    {
        // create ignore criteria
        var ignoreFeatureTypesView = FeatureOptions.CreateFeatureOptionsSelectionsView(StaticSettings.IgnoreCriteria, Settings.Merger.SelectedIgnoreCriteria);

        // create separate property for each so they can be referenced individually in the UI
        IgnoreIfContainsWordsFeature = ignoreFeatureTypesView.First(x => x.Id == (int)IgnoreCriteriaEnum.IgnoreIfContainsWords);
        IgnoreIfSmallerFeature = ignoreFeatureTypesView.First(x => x.Id == (int)IgnoreCriteriaEnum.IgnoreIfSmaller);
        IgnoreIfNotNewerFeature = ignoreFeatureTypesView.First(x => x.Id == (int)IgnoreCriteriaEnum.IgnoreIfNotNewer);
        IgnoreSelectClearAllFeature = ignoreFeatureTypesView.First(x => x.Id == FeatureOptions.SelectAllId);

        // delete ignored isn't technically an ignored option.. but added here to keep it visually consistent
        DeleteIgnoredFilesOptionFeature = FeatureOptions.CreateFeatureType(StaticSettings.DeleteIgnoredFilesOption, Settings.Merger.DeleteIgnoredFiles);
        DeleteIgnoredFilesOptionFeature.SelectedCommand = new ActionCommand(() => Settings.Merger.DeleteIgnoredFiles = !Settings.Merger.DeleteIgnoredFiles);
        ignoreFeatureTypesView.AddNewItem(DeleteIgnoredFilesOptionFeature);
    }

    private IEnumerable<FeatureType> CreateMergeOptions()
    {
        // show all merge options
        var featureTypes = StaticSettings.MergeOptions.Select(mergeOption =>
        {
            var featureType = new FeatureType((int)mergeOption.Enum)
            {
                Description = mergeOption.Description,
                Tip = mergeOption.Tip,
                IsSupported = true,
                IsActive = Settings.Merger.SelectedMergeOptions.Contains(mergeOption.Enum),
                SelectedCommand = new ActionCommand(() => Settings.Merger.SelectedMergeOptions.Toggle(mergeOption.Enum))
            };

            return featureType;
        }).ToList();

        return featureTypes.Concat(new[] { FeatureOptions.CreateSelectAll(featureTypes) });
    }

    private async void Start()
    {
        Logger.Info($"\nMerger started, settings={JsonSerializer.Serialize(Settings)}");

        _window.Hide();
        Logger.Clear();

        var progress = new ProgressViewModel();
        progress.Show(_window);


        List<LocalGame> games;
        try
        {
            progress.Update("Loading Database");
            games = await TableUtils.ReadGamesFromDatabases(new List<ContentType> { Settings.GetSelectedDestinationContentType() });
            Logger.Info($"Loading database complete, duration={progress.Duration}", true);
        }
        catch (Exception)
        {
            progress.Close();
            _window.TryShow();
            return;
        }

        progress.Update("Checking and Matching Files");
        var unmatchedFiles = await MergerUtils.CheckAndMatchAsync(games, UpdateProgress);

        progress.Update("Merging Files");
        var gameFiles = await MergerUtils.MergeAsync(games, Settings.BackupFolder, UpdateProgress);

        progress.Update("Removing Unmatched Ignored Files");
        await MergerUtils.RemoveUnmatchedIgnoredAsync(unmatchedFiles, UpdateProgress);

        // delete empty backup folders - i.e. if there are no files (empty sub-directories are allowed)
        FileUtils.DeleteActiveBackupFolderIfEmpty();

        progress.Update("Preparing Results");
        await Task.Delay(1);
        _games = new ObservableCollection<LocalGame>(games);

        progress.Close();

        await ShowResults(gameFiles, unmatchedFiles, progress.Duration);

        void UpdateProgress(string detail, float ratioComplete) => progress.Update(null, ratioComplete, detail);
    }

    private async Task ShowResults(ICollection<FileDetail> gameFiles, ICollection<FileDetail> unmatchedFiles, TimeSpan duration)
    {
        var screenPosition = _window.GetCurrentScreenPosition();

        var statistics = new MergerStatisticsViewModel(_games, duration, gameFiles, unmatchedFiles);
        statistics.Show(_window, screenPosition.X + WindowMargin, WindowMargin);

        var width = Model.ScreenWorkArea.Width - statistics.Window.Width - WindowMargin;
        var mergerResults = new MergerResultsViewModel(_games, gameFiles, unmatchedFiles);
        var showTask = mergerResults.Show(_window, statistics.Window.Left + statistics.Window.Width + WindowMargin, WindowMargin, width);

        var logging = new LoggingViewModel();
        logging.Show(_window, mergerResults.Window.Left, mergerResults.Window.Top + mergerResults.Window.Height + WindowMargin, width);

        logging.Window.Closed += CloseWindows();
        mergerResults.Window.Closed += CloseWindows();
        statistics.Window.Closed += CloseWindows();

        EventHandler CloseWindows()
        {
            return (_, _) =>
            {
                mergerResults.Close();
                statistics.Window.Close();
                logging.Close();

                _window.Close();
            };
        }

        await showTask;
    }

    private readonly IEnumerable<string> _destinationContentTypes;
    private ObservableCollection<LocalGame> _games;
    private MaterialWindowEx _window;
    private const int WindowMargin = 0;
}