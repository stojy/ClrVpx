﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClrVpin.Logging;
using ClrVpin.Models.Shared;
using ClrVpin.Models.Shared.Database;
using ClrVpin.Models.Shared.Game;
using ClrVpin.Shared.Fuzzy;
using Utils;
using Utils.Extensions;
using Utils.Xml;

namespace ClrVpin.Shared
{
    public static class TableUtils
    {
        public static async Task<List<LocalGame>> ReadGamesFromDatabases(IEnumerable<ContentType> contentTypes)
        {
            try
            {
                return GetGamesFromDatabases(contentTypes);
            }
            catch (Exception e)
            {
                await Notification.ShowWarning("HomeDialog",
                    "Unable to read PinballY/PinballX database file",
                    "Please check the database xml file is well formatted, e.g. via https://codebeautify.org/xmlvalidator.\n\n" +
                    "Alternatively, log an issue via github and upload the xml file for review.",
                    $"{e.Message}\n\n" +
                    $"{e.StackTrace}\n\n" +
                    $"{e.InnerException?.Message}\n\n" +
                    $"{e.InnerException?.StackTrace}",
                    showCloseButton: true);
                throw;
            }
        }

        public static void WriteGamesToDatabase(IEnumerable<Game> games)
        {
            var gamesByDatabase = games.GroupBy(game => game.DatabaseFile);
            gamesByDatabase.ForEach(gamesGrouping => WriteGamesToDatabase(gamesGrouping, gamesGrouping.Key, "n/a", false));
        }

        public static void WriteGamesToDatabase(IEnumerable<Game> games, string file, string game, bool isNewEntry)
        {
            if (file != null && !Path.IsPathRooted(file))
            {
                var databaseContentType = Model.Settings.GetDatabaseContentType();
                file = Path.Combine(databaseContentType.Folder, file);
            }

            Logger.Info($"{(isNewEntry ? "Adding new" : "Updating existing")} table: '{game}', database: {file}");

            var menu = new Menu { Games = games.ToList() };

            // a new backup folder is designated for every backup so that we can keep a record of every file change
            FileUtils.SetActiveBackupFolder(Model.Settings.BackupFolder);
            FileUtils.Backup(file, "merged", ContentTypeEnum.Database.GetDescription(), null, true);

            menu.SerializeToXDocument().Cleanse().SerializeToFile(file);
        }

        public static IList<string> GetContentFileNames(ContentType contentType, string folder)
        {
            var supportedFiles = contentType.ExtensionsList.Select(ext => Directory.EnumerateFiles(folder, ext));

            return supportedFiles.SelectMany(x => x).ToList();
        }

        public static IEnumerable<FileDetail> GetNonContentFileDetails(ContentType contentType, string folder)
        {
            // return all files that don't match the supported file extensions
            var supportedExtensions = contentType.ExtensionsList.Select(x => x.TrimStart('*').ToLower()).ToList();
            var kindredExtensions = contentType.KindredExtensionsList.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.TrimStart('*').ToLower());
            supportedExtensions.AddRange(kindredExtensions);

            var allFiles = Directory.EnumerateFiles(folder).Select(x => x.ToLower());

            var unsupportedFiles = allFiles.Where(file => !supportedExtensions.Any(file.EndsWith));

            var unsupportedFixFiles = unsupportedFiles.Select(file => new FileDetail(contentType.Enum, HitTypeEnum.Unsupported, FixFileTypeEnum.Skipped, file, new FileInfo(file).Length));

            return unsupportedFixFiles.ToList();
        }

        public static IEnumerable<FileDetail> AddContentFilesToGames(IList<LocalGame> localGames, IEnumerable<string> contentFiles, ContentType contentType,
            Func<LocalGame, ContentHits> getContentHits, Action<string, int> updateProgress)
        {
            var unknownSupportedFiles = new List<FileDetail>();

            // for each file, associate it with a game or if one can't be found, then mark it as unknown
            // - ASSOCIATION IS DONE IRRESPECTIVE OF THE USER'S SELECTED PREFERENCE, I.E. THE USE SELECTIONS ARE CHECKED ELSEWHERE
            contentFiles.ForEach((contentFile, i) =>
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(contentFile);
                updateProgress(fileNameWithoutExtension, i + 1);

                LocalGame matchedLocalGame;

                // check for hit..
                // - only 1 hit per file.. but a game DB entry can have multiple file hits.. with a maximum of 1 valid hit, i.e. the others considered as duplicate, wrong case, fuzzy matched, etc.
                // - ignores the check criteria.. the check criteria is only used in the results (e.g. statistics)
                if ((matchedLocalGame = localGames.FirstOrDefault(game => Content.GetName(game, contentType.Category) == fileNameWithoutExtension)) != null)
                {
                    // if a match already exists, then assume this match is a duplicate name with wrong extension
                    // - file extension order is important as it determines the priority of the preferred extension
                    var contentHits = getContentHits(matchedLocalGame);
                    contentHits.Add(contentHits.Hits.Any(hit => hit.Type == HitTypeEnum.CorrectName) ? HitTypeEnum.DuplicateExtension : HitTypeEnum.CorrectName, contentFile);
                }
                else if ((matchedLocalGame = localGames.FirstOrDefault(localGame =>
                             string.Equals(Content.GetName(localGame, contentType.Category), fileNameWithoutExtension, StringComparison.CurrentCultureIgnoreCase))) != null)
                {
                    getContentHits(matchedLocalGame).Add(HitTypeEnum.WrongCase, contentFile);
                }
                else if (contentType.Category == ContentTypeCategoryEnum.Media && (matchedLocalGame = localGames.FirstOrDefault(localGame => localGame.Game.Name == fileNameWithoutExtension)) != null)
                {
                    getContentHits(matchedLocalGame).Add(HitTypeEnum.TableName, contentFile);
                }
                // fuzzy matching
                else
                {
                    var fuzzyFileNameDetails = Fuzzy.Fuzzy.GetTableDetails(contentFile, true);
                    (matchedLocalGame, var score, var isMatch) = localGames.MatchToLocalDatabase(fuzzyFileNameDetails);
                    if (isMatch)
                    {
                        getContentHits(matchedLocalGame).Add(HitTypeEnum.Fuzzy, contentFile, score);
                    }
                    else
                    {
                        // possible for..
                        // - table --> new table files added AND the database not updated yet
                        // - table support and media --> as per pinball OR extra/redundant files exist where there is no table (yet!)
                        unknownSupportedFiles.Add(new FileDetail(contentType.Enum, HitTypeEnum.Unknown, FixFileTypeEnum.Skipped, contentFile, new FileInfo(contentFile).Length));
                    }
                }
            });

            return unknownSupportedFiles;
        }

        public static async Task<List<FileDetail>> CheckAsync(List<LocalGame> games, Action<string, float> updateProgress, ContentType[] contentTypes, bool includeUnsupportedFiles)
        {
            var unmatchedFiles = await Task.Run(() => Check(games, updateProgress, contentTypes, includeUnsupportedFiles));
            return unmatchedFiles;
        }

        private static List<LocalGame> GetGamesFromDatabases(IEnumerable<ContentType> contentTypes)
        {
            var databaseContentType = Model.Settings.GetDatabaseContentType();

            // scan through all the databases in the folder
            var files = Directory.EnumerateFiles(databaseContentType.Folder, databaseContentType.Extensions);

            var localGames = new List<LocalGame>();

            files.ForEach(file =>
            {
                // explicitly open file as a stream so that the encoding can be specified to allow the non-standard characters to be read
                // - PinballY (and presumably PinballX) write the DB file as extended ASCII 'code page 1252', i.e. not utf-8 or utf-16
                // - despite 1252 being the default code page for windows, using .net5 it appears to 'code page 437'
                // - e.g. 153 (0x99)
                //   - code page 437 = Ö
                //   - code page 1252 = ™
                //   - utf-8 = �
                // - further reading.. https://en.wikipedia.org/wiki/Extended_ASCII, https://codepoints.net/U+2122?lang=en
                using var reader = new StreamReader(file, Encoding.GetEncoding("Windows-1252"));

                XDocument doc;
                try
                {
                    doc = XDocument.Load(reader);
                    if (doc.Root == null)
                        throw new Exception("Root element missing");
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to load database: '{file}'", e);
                }

                Menu menu;
                try
                {
                    menu = doc.Root.Deserialize<Menu>();
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to deserialize database: '{file}'", e);
                }

                var databaseLocalGames = menu.Games.Select(g => new LocalGame { Game = g }).ToList();

                var number = 1;
                databaseLocalGames.ForEach(localGame =>
                {
                    localGame.Init(number++);

                    localGame.Game.DatabaseFile = file;
                    localGame.ViewState.NavigateToIpdbCommand = new ActionCommand(() => NavigateToIpdb(localGame.Derived.IpdbUrl));
                    localGame.Content.Init(contentTypes);
                });

                localGames.AddRange(databaseLocalGames);
                LogDatabaseStatistics(databaseLocalGames, file);
            });

            LogDatabaseStatistics(localGames);

            return localGames;
        }

        private static List<FileDetail> Check(List<LocalGame> games, Action<string, float> updateProgress, IEnumerable<ContentType> checkContentTypes, bool includeUnsupportedFiles)
        {
            var unmatchedFiles = new List<FileDetail>();

            // retrieve all supported files
            // - for each content type, match files (from the configured content folder location) with the correct file extension(s) to a table
            var contentTypeSupportedFiles = checkContentTypes.Select(contentType => new
            {
                contentType,
                supportedFiles = GetContentFileNames(contentType, contentType.Folder).ToList()
            }).ToList();

            var totalFilesCount = contentTypeSupportedFiles.Sum(details => details.supportedFiles.Count);
            var fileCount = 0;
            contentTypeSupportedFiles.ForEach(details =>
            {
                var supportedFiles = details.supportedFiles;
                var contentType = details.contentType;

                var unknownFiles = AddContentFilesToGames(games, supportedFiles, contentType, game => game.Content.ContentHitsCollection.First(contentHits => contentHits.Enum == contentType.Enum),
                    (fileName, _) => updateProgress($"{contentType.Description}: {fileName}", ++fileCount / (float)totalFilesCount));
                unmatchedFiles.AddRange(unknownFiles);

                // identify any unsupported files, i.e. files in the directory that don't have a matching extension
                if (includeUnsupportedFiles)
                {
                    var unsupportedFiles = GetNonContentFileDetails(contentType, contentType.Folder);

                    // n/a for pinball - since it's expected that extra files will exist in same tables folder
                    // - e.g. vpx, directb2s, pov, ogg, txt, exe, etc
                    if (contentType.Category == ContentTypeCategoryEnum.Media)
                        unmatchedFiles.AddRange(unsupportedFiles);
                }
            });

            // update each table status as missing if their were no matches
            AddMissingStatus(games);

            // unmatchedFiles = unknownFiles + unsupportedFiles
            return unmatchedFiles;
        }

        private static void AddMissingStatus(List<LocalGame> games)
        {
            games.ForEach(game =>
            {
                // add missing content
                game.Content.ContentHitsCollection.ForEach(contentHitCollection =>
                {
                    if (!contentHitCollection.Hits.Any(hit => hit.Type == HitTypeEnum.CorrectName || hit.Type == HitTypeEnum.WrongCase))
                        contentHitCollection.Add(HitTypeEnum.Missing, Content.GetName(game, contentHitCollection.ContentType.Category));
                });
            });
        }


        private static void LogDatabaseStatistics(IReadOnlyCollection<LocalGame> localGames, string file = null)
        {
            Logger.Info(
                $"Local database {(file == null ? "total" : "file")}: count={localGames.Count} (manufactured={localGames.Count(onlineGame => !onlineGame.Derived.IsOriginal)}, original={localGames.Count(onlineGame => onlineGame.Derived.IsOriginal)})" +
                $"{(file == null ? "" : ", file: " + file)}");
        }

        private static void NavigateToIpdb(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}