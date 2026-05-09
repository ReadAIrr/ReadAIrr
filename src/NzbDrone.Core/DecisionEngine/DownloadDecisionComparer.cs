using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public class DownloadDecisionComparer : IComparer<DownloadDecision>
    {
        private static readonly Regex AudioBitrateRegex = new Regex(@"(?<bitrate>\d{2,4})\s*(?:k(?:bps|bit|b/s)?|kbps|cbr|abr|vbr)(?:[^\w]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IQualityDefinitionService _qualityDefinitionService;

        public delegate int CompareDelegate(DownloadDecision x, DownloadDecision y);
        public delegate int CompareDelegate<TSubject, TValue>(DownloadDecision x, DownloadDecision y);

        public DownloadDecisionComparer(IConfigService configService, IDelayProfileService delayProfileService, IQualityDefinitionService qualityDefinitionService)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
            _qualityDefinitionService = qualityDefinitionService;
        }

        public int Compare(DownloadDecision x, DownloadDecision y)
        {
            var comparers = new List<CompareDelegate>
            {
                CompareQuality,
                CompareCustomFormatScore,
                CompareQualityBitratePreference,
                CompareQualitySizePreference,
                CompareProtocol,
                CompareIndexerPriority,
                ComparePeersIfTorrent,
                CompareBookCount,
                CompareAgeIfUsenet,
                CompareSize
            };

            return comparers.Select(comparer => comparer(x, y)).FirstOrDefault(result => result != 0);
        }

        private int CompareBy<TSubject, TValue>(TSubject left, TSubject right, Func<TSubject, TValue> funcValue)
            where TValue : IComparable<TValue>
        {
            var leftValue = funcValue(left);
            var rightValue = funcValue(right);

            return leftValue.CompareTo(rightValue);
        }

        private int CompareByReverse<TSubject, TValue>(TSubject left, TSubject right, Func<TSubject, TValue> funcValue)
            where TValue : IComparable<TValue>
        {
            return CompareBy(left, right, funcValue) * -1;
        }

        private int CompareAll(params int[] comparers)
        {
            return comparers.Select(comparer => comparer).FirstOrDefault(result => result != 0);
        }

        private int CompareIndexerPriority(DownloadDecision x, DownloadDecision y)
        {
            return CompareByReverse(x.RemoteBook.Release, y.RemoteBook.Release, release => release.IndexerPriority);
        }

        private int CompareQuality(DownloadDecision x, DownloadDecision y)
        {
            if (_configService.DownloadPropersAndRepacks == ProperDownloadTypes.DoNotPrefer)
            {
                return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.Author.QualityProfile.Value.GetIndex(remoteBook.ParsedBookInfo.Quality.Quality));
            }

            return CompareAll(CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.Author.QualityProfile.Value.GetIndex(remoteBook.ParsedBookInfo.Quality.Quality)),
                           CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.ParsedBookInfo.Quality.Revision));
        }

        private int CompareCustomFormatScore(DownloadDecision x, DownloadDecision y)
        {
            return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.CustomFormatScore);
        }

        private int CompareQualitySizePreference(DownloadDecision x, DownloadDecision y)
        {
            if (x.RemoteBook.ParsedBookInfo.Quality.Quality != y.RemoteBook.ParsedBookInfo.Quality.Quality)
            {
                return 0;
            }

            var qualityDefinition = _qualityDefinitionService.Get(x.RemoteBook.ParsedBookInfo.Quality.Quality);

            if (qualityDefinition == null ||
                qualityDefinition.SizePreference == QualitySizePreference.NoPreference ||
                x.RemoteBook.Release.Size <= 0 ||
                y.RemoteBook.Release.Size <= 0)
            {
                return 0;
            }

            if (qualityDefinition.SizePreference == QualitySizePreference.PreferSmaller)
            {
                return CompareByReverse(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.Release.Size);
            }

            if (qualityDefinition.SizePreference == QualitySizePreference.PreferLarger)
            {
                return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.Release.Size);
            }

            if (!qualityDefinition.TargetSize.HasValue || qualityDefinition.TargetSize.Value <= 0)
            {
                return 0;
            }

            var targetSize = qualityDefinition.TargetSize.Value.Kilobits();

            return CompareByReverse(x.RemoteBook, y.RemoteBook, remoteBook => Math.Abs(remoteBook.Release.Size - targetSize));
        }

        private int CompareQualityBitratePreference(DownloadDecision x, DownloadDecision y)
        {
            if (x.RemoteBook.ParsedBookInfo.Quality.Quality != y.RemoteBook.ParsedBookInfo.Quality.Quality)
            {
                return 0;
            }

            var qualityDefinition = _qualityDefinitionService.Get(x.RemoteBook.ParsedBookInfo.Quality.Quality);

            if (qualityDefinition == null ||
                qualityDefinition.BitratePreference == QualityBitratePreference.NoPreference)
            {
                return 0;
            }

            var leftBitrate = GetAudioBitrate(x.RemoteBook);
            var rightBitrate = GetAudioBitrate(y.RemoteBook);

            if (!leftBitrate.HasValue || !rightBitrate.HasValue || leftBitrate.Value == rightBitrate.Value)
            {
                return 0;
            }

            if (qualityDefinition.BitratePreference == QualityBitratePreference.PreferHigher)
            {
                return leftBitrate.Value.CompareTo(rightBitrate.Value);
            }

            if (qualityDefinition.BitratePreference == QualityBitratePreference.PreferLower)
            {
                return rightBitrate.Value.CompareTo(leftBitrate.Value);
            }

            return 0;
        }

        private static int? GetAudioBitrate(RemoteBook remoteBook)
        {
            var text = string.Join(" ", new[]
            {
                remoteBook.Release?.Title,
                remoteBook.Release?.Codec,
                remoteBook.Release?.Container,
                remoteBook.ParsedBookInfo?.ReleaseTitle,
                remoteBook.ParsedBookInfo?.BookTitle
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            var match = AudioBitrateRegex.Match(text);

            if (!match.Success || !int.TryParse(match.Groups["bitrate"].Value, out var bitrate) || bitrate <= 0)
            {
                return null;
            }

            return bitrate;
        }

        private int CompareProtocol(DownloadDecision x, DownloadDecision y)
        {
            var result = CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
            {
                var delayProfile = _delayProfileService.BestForTags(remoteBook.Author.Tags);
                var downloadProtocol = remoteBook.Release.DownloadProtocol;
                return downloadProtocol == delayProfile.PreferredProtocol;
            });

            return result;
        }

        private int CompareBookCount(DownloadDecision x, DownloadDecision y)
        {
            var discographyCompare = CompareBy(x.RemoteBook,
                y.RemoteBook,
                remoteBook => remoteBook.ParsedBookInfo.Discography);

            if (discographyCompare != 0)
            {
                return discographyCompare;
            }

            return CompareByReverse(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.Books.Count);
        }

        private int ComparePeersIfTorrent(DownloadDecision x, DownloadDecision y)
        {
            // Different protocols should get caught when checking the preferred protocol,
            // since we're dealing with the same series in our comparisions
            if (x.RemoteBook.Release.DownloadProtocol != DownloadProtocol.Torrent ||
                y.RemoteBook.Release.DownloadProtocol != DownloadProtocol.Torrent)
            {
                return 0;
            }

            return CompareAll(
                CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
                {
                    var seeders = TorrentInfo.GetSeeders(remoteBook.Release);

                    return seeders.HasValue && seeders.Value > 0 ? Math.Round(Math.Log10(seeders.Value)) : 0;
                }),
                CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
                {
                    var peers = TorrentInfo.GetPeers(remoteBook.Release);

                    return peers.HasValue && peers.Value > 0 ? Math.Round(Math.Log10(peers.Value)) : 0;
                }));
        }

        private int CompareAgeIfUsenet(DownloadDecision x, DownloadDecision y)
        {
            if (x.RemoteBook.Release.DownloadProtocol != DownloadProtocol.Usenet ||
                y.RemoteBook.Release.DownloadProtocol != DownloadProtocol.Usenet)
            {
                return 0;
            }

            return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook =>
            {
                var ageHours = remoteBook.Release.AgeHours;
                var age = remoteBook.Release.Age;

                if (ageHours < 1)
                {
                    return 1000;
                }

                if (ageHours <= 24)
                {
                    return 100;
                }

                if (age <= 7)
                {
                    return 10;
                }

                return 1;
            });
        }

        private int CompareSize(DownloadDecision x, DownloadDecision y)
        {
            // TODO: Is smaller better? Smaller for usenet could mean no par2 files.
            return CompareBy(x.RemoteBook, y.RemoteBook, remoteBook => remoteBook.Release.Size.Round(200.Megabytes()));
        }
    }
}
