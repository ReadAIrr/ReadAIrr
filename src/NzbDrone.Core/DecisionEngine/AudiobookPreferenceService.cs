using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IAudiobookPreferenceService
    {
        bool HasPreferences(QualityProfile profile);
        int Compare(QualityProfile profile, IEnumerable<BookFile> currentFiles, RemoteBook candidate);
        AudiobookPreferenceDescriptor Describe(IEnumerable<BookFile> files);
        AudiobookPreferenceDescriptor Describe(RemoteBook candidate);
    }

    public class AudiobookPreferenceService : IAudiobookPreferenceService
    {
        private static readonly Regex PartTotalRegex = new Regex(@"(?:^|[^\d])(?<part>\d{1,4})\s*(?:of|/)\s*(?<total>\d{1,4})(?:[^\d]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CountRegex = new Regex(@"(?:^|[^\d])(?<count>\d{1,4})\s*(?:files?|parts?|tracks?|chapters?)(?:[^\w]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MultiFileHintRegex = new Regex(@"(?:multi[\s-]?file|multi[\s-]?part|part\s+\d{1,4}|disc\s*\d{1,3}|cd\s*\d{1,3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SingleFileHintRegex = new Regex(@"(?:single[\s-]?file|one[\s-]?file|unabridged\s+m4b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool HasPreferences(QualityProfile profile)
        {
            return profile.AudiobookLayoutPreference != AudiobookLayoutPreference.NoPreference ||
                   profile.AudiobookFormatPreference != AudiobookFormatPreference.NoPreference ||
                   profile.AudiobookFileCountPreference != AudiobookFileCountPreference.NoPreference;
        }

        public int Compare(QualityProfile profile, IEnumerable<BookFile> currentFiles, RemoteBook candidate)
        {
            if (!HasPreferences(profile))
            {
                return 0;
            }

            var current = Describe(currentFiles);
            var proposed = Describe(candidate);

            return Compare(profile, current, proposed);
        }

        public AudiobookPreferenceDescriptor Describe(IEnumerable<BookFile> files)
        {
            var audioFiles = files.Where(IsAudioFile).ToList();

            if (audioFiles.Empty())
            {
                return AudiobookPreferenceDescriptor.Unknown;
            }

            var formats = audioFiles.Select(GetAudioFormat).Where(x => x != AudiobookFormat.Unknown).Distinct().ToList();

            return new AudiobookPreferenceDescriptor
            {
                Layout = audioFiles.Count == 1 ? AudiobookLayout.SingleFile : AudiobookLayout.MultiFile,
                Format = formats.Count == 1 ? formats.Single() : AudiobookFormat.Unknown,
                FileCount = audioFiles.Count
            };
        }

        public AudiobookPreferenceDescriptor Describe(RemoteBook candidate)
        {
            var quality = candidate.ParsedBookInfo?.Quality?.Quality ?? Quality.Unknown;
            var title = string.Join(" ", new[]
            {
                candidate.Release?.Title,
                candidate.ParsedBookInfo?.ReleaseTitle,
                candidate.ParsedBookInfo?.BookTitle
            }.Where(x => x.IsNotNullOrWhiteSpace()));

            var format = GetAudioFormat(quality, title);
            var fileCount = GetFileCount(title);
            var layout = GetLayout(quality, title, fileCount);

            return new AudiobookPreferenceDescriptor
            {
                Layout = layout,
                Format = format,
                FileCount = fileCount
            };
        }

        private static int Compare(QualityProfile profile, AudiobookPreferenceDescriptor current, AudiobookPreferenceDescriptor proposed)
        {
            var compare = 0;

            if (profile.AudiobookLayoutPreference == AudiobookLayoutPreference.PreferSingleFile)
            {
                compare += CompareKnown(current.Layout, proposed.Layout, AudiobookLayout.SingleFile);
            }
            else if (profile.AudiobookLayoutPreference == AudiobookLayoutPreference.PreferMultiFile)
            {
                compare += CompareKnown(current.Layout, proposed.Layout, AudiobookLayout.MultiFile);
            }

            if (profile.AudiobookFormatPreference == AudiobookFormatPreference.PreferM4B)
            {
                compare += CompareKnown(current.Format, proposed.Format, AudiobookFormat.M4B);
            }
            else if (profile.AudiobookFormatPreference == AudiobookFormatPreference.PreferMP3)
            {
                compare += CompareKnown(current.Format, proposed.Format, AudiobookFormat.MP3);
            }

            if (profile.AudiobookFileCountPreference == AudiobookFileCountPreference.PreferFewerParts &&
                proposed.FileCount.HasValue &&
                current.FileCount.HasValue)
            {
                compare += current.FileCount.Value.CompareTo(proposed.FileCount.Value);
            }

            return compare.CompareTo(0);
        }

        private static int CompareKnown<T>(T current, T proposed, T preferred)
            where T : struct
        {
            if (current.Equals(default(T)) || proposed.Equals(default(T)))
            {
                return 0;
            }

            var currentMatches = current.Equals(preferred);
            var proposedMatches = proposed.Equals(preferred);

            return proposedMatches.CompareTo(currentMatches);
        }

        private static bool IsAudioFile(BookFile file)
        {
            if (file == null)
            {
                return false;
            }

            if (MediaFileExtensions.AudioExtensions.Contains(Path.GetExtension(file.Path)))
            {
                return true;
            }

            var quality = file.Quality?.Quality;

            return quality == Quality.MP3 || quality == Quality.M4B || quality == Quality.FLAC || quality == Quality.UnknownAudio;
        }

        private static AudiobookFormat GetAudioFormat(BookFile file)
        {
            var extension = Path.GetExtension(file.Path);

            if (extension.Equals(".m4b", StringComparison.OrdinalIgnoreCase))
            {
                return AudiobookFormat.M4B;
            }

            if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".mp2", StringComparison.OrdinalIgnoreCase))
            {
                return AudiobookFormat.MP3;
            }

            return GetAudioFormat(file.Quality?.Quality ?? Quality.Unknown, file.Path);
        }

        private static AudiobookFormat GetAudioFormat(Quality quality, string text)
        {
            if (quality == Quality.M4B || ContainsToken(text, "m4b"))
            {
                return AudiobookFormat.M4B;
            }

            if (quality == Quality.MP3 || ContainsToken(text, "mp3"))
            {
                return AudiobookFormat.MP3;
            }

            return AudiobookFormat.Unknown;
        }

        private static AudiobookLayout GetLayout(Quality quality, string text, int? fileCount)
        {
            if (fileCount.HasValue)
            {
                return fileCount.Value == 1 ? AudiobookLayout.SingleFile : AudiobookLayout.MultiFile;
            }

            if (SingleFileHintRegex.IsMatch(text) || quality == Quality.M4B || ContainsToken(text, "m4b"))
            {
                return AudiobookLayout.SingleFile;
            }

            if (MultiFileHintRegex.IsMatch(text))
            {
                return AudiobookLayout.MultiFile;
            }

            return AudiobookLayout.Unknown;
        }

        private static int? GetFileCount(string text)
        {
            if (text.IsNullOrWhiteSpace())
            {
                return null;
            }

            var countMatch = CountRegex.Match(text);

            if (countMatch.Success && int.TryParse(countMatch.Groups["count"].Value, out var count) && count > 0)
            {
                return count;
            }

            var totalMatch = PartTotalRegex.Match(text);

            if (totalMatch.Success && int.TryParse(totalMatch.Groups["total"].Value, out var total) && total > 0)
            {
                return total;
            }

            return null;
        }

        private static bool ContainsToken(string text, string token)
        {
            return text.IsNotNullOrWhiteSpace() &&
                   Regex.IsMatch(text, $@"(?:^|[^\w]){Regex.Escape(token)}(?:[^\w]|$)", RegexOptions.IgnoreCase);
        }
    }

    public class AudiobookPreferenceDescriptor
    {
        public static AudiobookPreferenceDescriptor Unknown => new AudiobookPreferenceDescriptor();

        public AudiobookLayout Layout { get; set; }
        public AudiobookFormat Format { get; set; }
        public int? FileCount { get; set; }
    }

    public enum AudiobookLayout
    {
        Unknown = 0,
        SingleFile = 1,
        MultiFile = 2
    }

    public enum AudiobookFormat
    {
        Unknown = 0,
        M4B = 1,
        MP3 = 2
    }
}
