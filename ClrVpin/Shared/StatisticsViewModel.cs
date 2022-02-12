﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ByteSizeLib;
using ClrVpin.Models;
using PropertyChanged;
using Utils;
using Utils.Extensions;

namespace ClrVpin.Shared
{
    [AddINotifyPropertyChangedInterface]
    public abstract class StatisticsViewModel
    {
        protected StatisticsViewModel(ObservableCollection<Game> games, TimeSpan elapsedTime, ICollection<FileDetail> gameFiles, ICollection<FileDetail> unmatchedFiles)
        {
            GameFiles = gameFiles;
            UnmatchedFiles = unmatchedFiles;

            ElapsedTime = elapsedTime;
            Games = games;

            Settings = Model.Settings;
        }

        protected ObservableCollection<Game> Games { get; }
        protected TimeSpan ElapsedTime { get; }

        public string Result { get; set; }
        public Window Window { get; protected set; }

        public IList<HitTypeEnum> SelectedFixHitTypes { get; set; }
        public IList<HitTypeEnum> SelectedCheckHitTypes { get; set; }
        public IList<HitType> SupportedHitTypes { get; set; }
        public IList<ContentType> SupportedContentTypes { get; set; }
        public IList<string> SelectedCheckContentTypes { get; set; }

        public int TotalCount { get; protected set; } // Games.Count;

        protected Models.Settings.Settings Settings { get; set; }

        protected void CreateStatistics()
        {
            Result =
                $"{CreateHitTypeStatistics()}\n" +
                $"{CreateTotalStatistics()}";
        }

        protected abstract string CreateTotalStatistics();

        protected static string CreateFileStatistic(IEnumerable<FileDetail> fileDetails)
        {
            var fileDetailsArray = fileDetails as FileDetail[] ?? fileDetails.ToArray();
            return CreateFileStatistic(fileDetailsArray.Length, fileDetailsArray.Sum(x => x.Size));
        }

        protected static string CreateFileStatistic(long count, long size) => $"{count} ({(size == 0 ? "0 B" : ByteSize.FromBytes(size).ToString("0.#"))})";

        private string CreateHitTypeStatistics()
        {
            // for every hit type, create stats against every content type
            var hitStatistics = SupportedHitTypes.Select(hitType =>
            {
                string contents;
                if (hitType.Enum.In(HitTypeEnum.Unknown, HitTypeEnum.Unsupported))
                {
                    // other files (unknown and unsupported) matches aren't attributed to a game.. so we treat them a little differently
                    contents = string.Join("\n", SupportedContentTypes.Select(contentType =>
                        $"- {contentType.Description,StatisticsKeyWidth + 2}{GetUnmatchedFilesContentStatistics(contentType.Enum, hitType.Enum)}"));
                }
                else
                {
                    // all known content has an associated game
                    contents = string.Join("\n", SupportedContentTypes.Select(contentType =>
                        $"- {contentType.Description,StatisticsKeyWidth + 2}{GetGameFilesContentStatistics(contentType.Enum, hitType.Enum)}"));
                }

                return $"{hitType.Description}\n{contents}";
            });

            return $"Criteria statistics for each content type\n\n{string.Join("\n\n", hitStatistics)}";
        }

        private string GetGameFilesContentStatistics(ContentTypeEnum contentType, HitTypeEnum hitType)
        {
            // identify stats belonging to criteria that were not selected for checking/fixing
            var prefix = "discovered";

            // discovered statistics - from the games list
            var discoveredStatistics = $"{prefix} {Games.Sum(g => g.Content.ContentHitsCollection.First(x => x.Enum == contentType).Hits.Count(hit => hit.Type == hitType))}/{TotalCount}";

            // file statistics - from the file list.. which is also stored in the games list, but more accessible via Games
            // - for n/a hit types (e.g. ignored) there will be no stats since there are no GameFiles :)
            var fileStatistics = CreateFileStatistics(GameFiles, contentType, hitType);

            return string.Join(": ", new[] {discoveredStatistics, fileStatistics}.Where(x => !string.IsNullOrEmpty(x)));
        }

        private string GetUnmatchedFilesContentStatistics(ContentTypeEnum contentType, HitTypeEnum hitType)
        {
            // identify stats belonging to criteria that were not selected for checking/fixing
            var prefix = "discovered";

            // discovered statistics - from the unknown files list
            var files = UnmatchedFiles.Where(x => x.ContentType == contentType && x.HitType == hitType).ToList();
            var discoveredStatistics = $"{prefix} {files.Count}";

            // file statistics - from the file list.. which is also stored in the games list, but more accessible via Games
            // - for n/a hit types (e.g. ignored) there will be no stats since there are no GameFiles :)
            var fileStatistics = CreateFileStatistics(UnmatchedFiles, contentType, hitType);

            return string.Join(": ", new[] { discoveredStatistics, fileStatistics }.Where(x => !string.IsNullOrEmpty(x)));
        }

        private static string CreateFileStatistics(IEnumerable<FileDetail> files, ContentTypeEnum contentType, HitTypeEnum hitType)
        {
            var fileStatistics = new List<string>();

            var matchedFiles = files.Where(x => x.ContentType == contentType && x.HitType == hitType).ToList();

            fileStatistics.Add(CreateFileStatistic("skipped", matchedFiles.Where(x => x.Skipped)));
            fileStatistics.Add(CreateFileStatistic("ignored", matchedFiles.Where(x => x.Ignored)));
            fileStatistics.Add(CreateFileStatistic("renamed", matchedFiles.Where(x => x.Renamed)));
            fileStatistics.Add(CreateFileStatistic("removed", matchedFiles.Where(x => x.Deleted)));
            fileStatistics.Add(CreateFileStatistic("merged", matchedFiles.Where(x => x.Merged)));

            return string.Join(", ", fileStatistics.Where(x=> x != null));
        }

        private static string CreateFileStatistic(string prefix, IEnumerable<FileDetail> removedFiles, bool includeEmpty = false)
        {
            var fileDetails = removedFiles as FileDetail[] ?? removedFiles.ToArray();
            return fileDetails.Any() || includeEmpty ? $"{prefix} {CreateFileStatistic(fileDetails.Length, fileDetails.Sum(x => x.Size))}" : null;
        }

        protected readonly ICollection<FileDetail> GameFiles;
        protected readonly ICollection<FileDetail> UnmatchedFiles;
        public const int StatisticsKeyWidth = -26;
    }
}