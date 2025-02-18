using Shokofin.API.Info;
using Shokofin.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shokofin.Utils;

public static partial class Text
{
    private static readonly HashSet<char> PunctuationMarks = [
        // Common punctuation marks
        '.',   // period
        ',',   // comma
        ';',   // semicolon
        ':',   // colon
        '!',   // exclamation point
        '?',   // question mark
        ')',   // right parenthesis
        ']',   // right bracket
        '}',   // right brace
        '"',  // double quote
        '\'',   // single quote
        '，',  // Chinese comma
        '、',  // Chinese enumeration comma
        '！',  // Chinese exclamation point
        '？',  // Chinese question mark
        '“',  // Chinese double quote
        '”',  // Chinese double quote
        '‘',  // Chinese single quote
        '’',  // Chinese single quote
        '】',  // Chinese right bracket
        '》',  // Chinese right angle bracket
        '）',  // Chinese right parenthesis
        '・',  // Japanese middle dot

        // Less common punctuation marks
        '‽',    // interrobang
        '❞',   // double question mark
        '❝',   // double exclamation mark
        '⁇',   // question mark variation
        '⁈',   // exclamation mark variation
        '❕',   // white exclamation mark
        '❔',   // white question mark
        '⁉',   // exclamation mark
        '※',   // reference mark
        '⟩',   // right angle bracket
        '❯',   // right angle bracket
        '❭',   // right angle bracket
        '〉',   // right angle bracket
        '⌉',   // right angle bracket
        '⌋',   // right angle bracket
        '⦄',   // right angle bracket
        '⦆',   // right angle bracket
        '⦈',   // right angle bracket
        '⦊',   // right angle bracket
        '⦌',   // right angle bracket
        '⦎',   // right angle bracket
    ];

    internal static readonly HashSet<string> IgnoredSubTitles = new(StringComparer.InvariantCultureIgnoreCase) {
        "Complete Movie",
        "Music Video",
        "OAD",
        "OVA",
        "Short Movie",
        "Special",
        "TV Special",
        "Web",
    };

    /// <summary>
    /// Determines which provider to use to provide the descriptions.
    /// </summary>
    public enum DescriptionProvider {
        /// <summary>
        /// Provide the Shoko Group description for the show, if the show is
        /// constructed using Shoko's groups feature.
        /// </summary>
        Shoko = 1,

        /// <summary>
        /// Provide the description from AniDB.
        /// </summary>
        AniDB = 2,

        /// <summary>
        /// Deprecated, but kept until the next major release for backwards compatibility.
        /// TODO: REMOVE THIS IN 6.0
        /// </summary>
        TvDB = 3,

        /// <summary>
        /// Provide the description from TMDB.
        /// </summary>
        TMDB = 4
    }

    /// <summary>
    /// Determines which provider and method to use to look-up the title.
    /// </summary>
    public enum TitleProvider {
        /// <summary>
        /// Let Shoko decide what to display.
        /// </summary>
        Shoko_Default = 1,

        /// <summary>
        /// Use the default title as provided by AniDB.
        /// </summary>
        AniDB_Default = 2,

        /// <summary>
        /// Use the selected metadata language for the library as provided by
        /// AniDB.
        /// </summary>
        AniDB_LibraryLanguage = 3,

        /// <summary>
        /// Use the title in the origin language as provided by AniDB.
        /// </summary>
        AniDB_CountryOfOrigin = 4,

        /// <summary>
        /// Use the default title as provided by TMDB.
        /// </summary>
        TMDB_Default = 5,

        /// <summary>
        /// Use the selected metadata language for the library as provided by
        /// TMDB.
        /// </summary>
        TMDB_LibraryLanguage = 6,

        /// <summary>
        /// Use the title in the origin language as provided by TMDB.
        /// </summary>
        TMDB_CountryOfOrigin = 7,
    }

    /// <summary>
    /// Determines which type of title to look-up.
    /// </summary>
    public enum TitleProviderType {
        /// <summary>
        /// The main title used for metadata entries.
        /// </summary>
        Main = 0,

        /// <summary>
        /// The secondary title used for metadata entries.
        /// </summary>
        Alternate = 1,
    }

    public static string GetDescription(ShowInfo show, string? metadataLanguage)
        => GetDescriptionByDict(new() {
            {DescriptionProvider.Shoko, show.Shoko?.Description ?? show.DefaultSeason.Shoko.Description},
            {DescriptionProvider.AniDB, metadataLanguage is "en" ? show.DefaultSeason.AniDB.Description : null},
        });

    public static string GetDescription(SeasonInfo season, string? metadataLanguage)
        => GetDescriptionByDict(new() {
            {DescriptionProvider.Shoko, season.Shoko.Description},
            {DescriptionProvider.AniDB, metadataLanguage is "en" ? season.AniDB.Description : null},
        });

    public static string GetDescription(EpisodeInfo episode, string? metadataLanguage)
        => GetDescriptionByDict(new() {
            {DescriptionProvider.Shoko, episode.Shoko.Description},
            {DescriptionProvider.AniDB, metadataLanguage is "en" ? episode.AniDB.Description : null},
        });

    public static string GetDescription(IEnumerable<EpisodeInfo> episodeList, string? metadataLanguage)
        => JoinText(episodeList.Select(episode => GetDescription(episode, metadataLanguage))) ?? string.Empty;

    public static string GetMovieDescription(EpisodeInfo episode, SeasonInfo season, string? metadataLanguage)
    {
        // TODO: Actually implement actual movie descriptions from TMDB once it's made available in the plugin.
        bool isMultiEntry = season.Shoko.Sizes.Total.Episodes > 1;
        bool isMainEntry = episode.AniDB.Type == API.Models.EpisodeType.Normal && episode.Shoko.Name.Trim() == "Complete Movie";
        return isMultiEntry && !isMainEntry ? GetDescription(episode, metadataLanguage) : GetDescription(season, metadataLanguage);
    }

    /// <summary>
    /// Returns a list of the description providers to check, and in what order
    /// </summary>
    private static DescriptionProvider[] GetOrderedDescriptionProviders()
        => Plugin.Instance.Configuration.DescriptionSourceOrder.Where((t) => Plugin.Instance.Configuration.DescriptionSourceList.Contains(t)).ToArray();

    private static string GetDescriptionByDict(Dictionary<DescriptionProvider, string?> descriptions)
    {
        foreach (var provider in GetOrderedDescriptionProviders())
        {
            var overview = provider switch
            {
                DescriptionProvider.Shoko =>
                    descriptions.TryGetValue(DescriptionProvider.Shoko, out var desc) ? SanitizeAnidbDescription(desc ?? string.Empty) : null,
                DescriptionProvider.AniDB =>
                    descriptions.TryGetValue(DescriptionProvider.AniDB, out var desc) ? SanitizeAnidbDescription(desc ?? string.Empty) : null,
                _ => null
            };
            if (!string.IsNullOrEmpty(overview))
                return overview;
        }
        return string.Empty;
    }

    /// <summary>
    /// Sanitize the AniDB entry description to something usable by Jellyfin.
    /// </summary>
    /// <remarks>
    /// Based on ShokoMetadata's summary sanitizer which in turn is based on HAMA's summary sanitizer.
    /// </remarks>
    /// <param name="summary">The raw AniDB description.</param>
    /// <returns>The sanitized AniDB description.</returns>
    public static string SanitizeAnidbDescription(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return string.Empty;

        var config = Plugin.Instance.Configuration;
        if (config.SynopsisCleanLinks)
            summary = summary.Replace(SynopsisCleanLinks, match => $"[{match.Groups[2].Value}]({match.Groups[1].Value})");

        if (config.SynopsisCleanMiscLines)
            summary = summary.Replace(SynopsisCleanMiscLines, string.Empty);

        if (config.SynopsisRemoveSummary)
            summary = summary
                .Replace(SynopsisRemoveSummary1, match => $"**{match.Groups[1].Value}**: ")
                .Replace(SynopsisRemoveSummary2, string.Empty);

        if (config.SynopsisCleanMultiEmptyLines)
            summary = summary
                .Replace(SynopsisConvertNewLines, "\n")
                .Replace(SynopsisCleanMultiEmptyLines, "\n");

        return summary.Trim();
    }

    private static readonly Regex SynopsisCleanLinks = new(@"(https?:\/\/\w+.\w+(?:\/?\w+)?) \[([^\]]+)\]", RegexOptions.Compiled);

    private static readonly Regex SynopsisCleanMiscLines = new(@"^(\*|--|~)\s*", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex SynopsisRemoveSummary1 = new(@"\b(Note|Summary):\s*", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SynopsisRemoveSummary2 = new(@"\bSource: [^ ]+", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SynopsisConvertNewLines = new(@"\r\n|\r", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SynopsisCleanMultiEmptyLines = new(@"\n{2,}", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string? JoinText(IEnumerable<string?> textList)
    {
        var filteredList = textList
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title!.Trim())
            // We distinct the list because some episode entries contain the **exact** same description.
            .Distinct()
            .ToList();

        if (filteredList.Count == 0)
            return null;

        var index = 1;
        var outputText = filteredList[0];
        while (index < filteredList.Count) {
            var lastChar = outputText[^1];
            outputText += PunctuationMarks.Contains(lastChar) ? " " : ". ";
            outputText += filteredList[index++];
        }

        if (filteredList.Count > 1)
            outputText = outputText.TrimEnd();

        return outputText;
    }

    public static (string?, string?) GetEpisodeTitles(EpisodeInfo episodeInfo, SeasonInfo seasonInfo, string? metadataLanguage)
        => (
            GetEpisodeTitleByType(episodeInfo, seasonInfo, TitleProviderType.Main, metadataLanguage),
            GetEpisodeTitleByType(episodeInfo, seasonInfo, TitleProviderType.Alternate, metadataLanguage)
        );

    public static (string?, string?) GetSeasonTitles(SeasonInfo seasonInfo, string? metadataLanguage)
        => GetSeasonTitles(seasonInfo, 0, metadataLanguage);

    public static (string?, string?) GetSeasonTitles(SeasonInfo seasonInfo, int baseSeasonOffset, string? metadataLanguage)
    {
        var displayTitle = GetSeriesTitleByType(seasonInfo, seasonInfo.Shoko.Name, TitleProviderType.Main, metadataLanguage);
        var alternateTitle = GetSeriesTitleByType(seasonInfo, seasonInfo.Shoko.Name, TitleProviderType.Alternate, metadataLanguage);
        if (baseSeasonOffset > 0) {
            string type = string.Empty;
            switch (baseSeasonOffset) {
                default:
                    break;
                case 1:
                    type = "Alternate Version";
                    break;
            }
            if (!string.IsNullOrEmpty(type)) {
                displayTitle += $" ({type})";
                alternateTitle += $" ({type})";
            }
        }
        return (displayTitle, alternateTitle);
    }

    public static (string?, string?) GetShowTitles(ShowInfo showInfo, string? metadataLanguage)
        => (
            GetSeriesTitleByType(showInfo.DefaultSeason, showInfo.Name, TitleProviderType.Main, metadataLanguage),
            GetSeriesTitleByType(showInfo.DefaultSeason, showInfo.Name, TitleProviderType.Alternate, metadataLanguage)
        );

    public static (string?, string?) GetMovieTitles(EpisodeInfo episodeInfo, SeasonInfo seasonInfo, string? metadataLanguage)
        => (
            GetMovieTitleByType(episodeInfo, seasonInfo, TitleProviderType.Main, metadataLanguage),
            GetMovieTitleByType(episodeInfo, seasonInfo, TitleProviderType.Alternate, metadataLanguage)
        );

    /// <summary>
    /// Returns a list of the providers to check, and in what order
    /// </summary>
    private static TitleProvider[] GetOrderedTitleProvidersByType(TitleProviderType titleType)
        => titleType switch {
            TitleProviderType.Main =>
                Plugin.Instance.Configuration.TitleMainOrder.Where((t) => Plugin.Instance.Configuration.TitleMainList.Contains(t)).ToArray(),
            TitleProviderType.Alternate =>
                Plugin.Instance.Configuration.TitleAlternateOrder.Where((t) => Plugin.Instance.Configuration.TitleAlternateList.Contains(t)).ToArray(),
            _ => [],
        };

    private static string? GetMovieTitleByType(EpisodeInfo episodeInfo, SeasonInfo seasonInfo, TitleProviderType type, string? metadataLanguage)
    {
        var mainTitle = GetSeriesTitleByType(seasonInfo, seasonInfo.Shoko.Name, type, metadataLanguage);
        var subTitle = GetEpisodeTitleByType(episodeInfo, seasonInfo, type, metadataLanguage);

        if (!(string.IsNullOrEmpty(subTitle) || IgnoredSubTitles.Contains(subTitle)))
            return $"{mainTitle}: {subTitle}".Trim();
        return mainTitle?.Trim();
    }

    private static string? GetEpisodeTitleByType(EpisodeInfo episodeInfo, SeasonInfo seasonInfo, TitleProviderType type, string? metadataLanguage)
    {
        foreach (var provider in GetOrderedTitleProvidersByType(type)) {
            var title = provider switch {
                TitleProvider.Shoko_Default =>
                    episodeInfo.Shoko.Name,
                TitleProvider.AniDB_Default =>
                    episodeInfo.AniDB.Titles.FirstOrDefault(title => title.LanguageCode == "en")?.Value,
                TitleProvider.AniDB_LibraryLanguage =>
                    GetTitlesForLanguage(episodeInfo.AniDB.Titles, false, metadataLanguage),
                TitleProvider.AniDB_CountryOfOrigin =>
                    GetTitlesForLanguage(episodeInfo.AniDB.Titles, false, GuessOriginLanguage(GetMainLanguage(seasonInfo.AniDB.Titles))),
                _ => null,
            };
            if (!string.IsNullOrEmpty(title))
                return title.Trim();
        }
        return null;
    }

    private static string? GetSeriesTitleByType(SeasonInfo seasonInfo, string defaultName, TitleProviderType type, string? metadataLanguage)
    {
        foreach (var provider in GetOrderedTitleProvidersByType(type)) {
            var title = provider switch {
                TitleProvider.Shoko_Default =>
                    defaultName,
                TitleProvider.AniDB_Default =>
                    seasonInfo.AniDB.Titles.FirstOrDefault(title => title.Type == TitleType.Main)?.Value,
                TitleProvider.AniDB_LibraryLanguage =>
                    GetTitlesForLanguage(seasonInfo.AniDB.Titles, true, metadataLanguage),
                TitleProvider.AniDB_CountryOfOrigin =>
                    GetTitlesForLanguage(seasonInfo.AniDB.Titles, true, GuessOriginLanguage(GetMainLanguage(seasonInfo.AniDB.Titles))),
                _ => null,
            };
            if (!string.IsNullOrEmpty(title))
                return title.Trim();
        }
        return null;
    }

    [GeneratedRegex(@"^(?:Special|Episode) \d+$|^Part \d+ of \d+$|^Volume \d$|^(?:OVA|OAD|Movie|Complete Movie|Short Movie|TV Special|Music Video|Web|Volume)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex EpisodeNameRegex();

    /// <summary>
    /// Get the first title available for the language, optionally using types
    /// to filter the list in addition to the metadata languages provided.
    /// </summary>
    /// <param name="titles">Title list to search.</param>
    /// <param name="usingTypes">Search using titles</param>
    /// <param name="metadataLanguages">The metadata languages to search for.</param>
    /// <returns>The first found title in any of the provided metadata languages, or null.</returns>
    public static string? GetTitlesForLanguage(List<Title> titles, bool usingTypes, params string?[] metadataLanguages)
    {
        foreach (var lang in metadataLanguages) {
            if (string.IsNullOrEmpty(lang))
                continue;

            var titleList = titles.Where(t => t.LanguageCode == lang).ToList();
            if (titleList.Count == 0)
                continue;

            string? title = null;
            if (usingTypes) {
                title = titleList.FirstOrDefault(t => t.Type == TitleType.Official)?.Value;
                if (string.IsNullOrEmpty(title) && Plugin.Instance.Configuration.TitleAllowAny)
                    title = titleList.FirstOrDefault()?.Value;
            }
            else {
                title = titles.FirstOrDefault()?.Value;
            }
            if (!string.IsNullOrWhiteSpace(title) && !EpisodeNameRegex().IsMatch(title))
                return title;
        }
        return null;
    }

    /// <summary>
    /// Get the main title language from the title list.
    /// </summary>
    /// <param name="titles">Title list.</param>
    /// <returns>The main title language code.</returns>
    private static string GetMainLanguage(IEnumerable<Title> titles)
        => titles.FirstOrDefault(t => t?.Type == TitleType.Main)?.LanguageCode ?? titles.FirstOrDefault()?.LanguageCode ?? "x-other";

    /// <summary>
    /// Guess the origin language based on the main title language.
    /// </summary>
    /// <param name="langCode">The main title language code.</param>
    /// <returns>The list of origin language codes to try and use.</returns>
    private static string[] GuessOriginLanguage(string langCode)
        => langCode switch {
            "x-other" => ["ja"],
            "x-jat" => ["ja"],
            "x-zht" => ["zn-hans", "zn-hant", "zn-c-mcm", "zn"],
            _ => [langCode],
        };
}
