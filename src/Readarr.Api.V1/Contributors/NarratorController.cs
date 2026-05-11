using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using Readarr.Api.V1.BookFiles;
using Readarr.Http;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Readarr.Api.V1.Contributors
{
    [V1ApiController("narrator")]
    public class NarratorController : Controller
    {
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly INarratorIdentityLinkRepository _narratorIdentityLinkRepository;
        private readonly IMediaFileService _mediaFileService;
        private readonly IUnmappedIdentificationSuggestionService _unmappedIdentificationSuggestionService;
        private readonly IConfigService _configService;

        public NarratorController(IContributorEvidenceRepository contributorEvidenceRepository,
                                  INarratorIdentityLinkRepository narratorIdentityLinkRepository,
                                  IMediaFileService mediaFileService,
                                  IUnmappedIdentificationSuggestionService unmappedIdentificationSuggestionService,
                                  IConfigService configService)
        {
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _narratorIdentityLinkRepository = narratorIdentityLinkRepository;
            _mediaFileService = mediaFileService;
            _unmappedIdentificationSuggestionService = unmappedIdentificationSuggestionService;
            _configService = configService;
        }

        [HttpGet]
        public List<NarratorEvidenceResource> GetNarratorEvidence([FromQuery] string term = null, [FromQuery] string source = null)
        {
            var evidence = _contributorEvidenceRepository.GetNarratorEvidenceForIdentityIndex(term?.Trim(), source?.Trim());
            var aliases = _narratorIdentityLinkRepository.All().ToList();
            var bookFileIds = evidence.Where(x => x.BookFileId.HasValue).Select(x => x.BookFileId.Value).Distinct().ToList();
            var bookFiles = bookFileIds.Any() ? _mediaFileService.Get(bookFileIds) : new List<BookFile>();

            return NarratorEvidenceResourceMapper.ToResource(evidence, bookFiles, aliases, term?.Trim(), source?.Trim());
        }

        [HttpGet("paged")]
        public PagingResource<NarratorEvidenceResource> GetNarratorEvidencePaged([FromQuery] PagingRequestResource paging, [FromQuery] string term = null, [FromQuery] string source = null)
        {
            var pagingResource = new PagingResource<NarratorEvidenceResource>(paging);
            var evidence = _contributorEvidenceRepository.GetNarratorEvidenceForIdentityIndex(term?.Trim(), source?.Trim());
            var aliases = _narratorIdentityLinkRepository.All().ToList();
            var bookFileIds = evidence.Where(x => x.BookFileId.HasValue).Select(x => x.BookFileId.Value).Distinct().ToList();
            var bookFiles = bookFileIds.Any() ? _mediaFileService.Get(bookFileIds) : new List<BookFile>();
            var resources = NarratorEvidenceResourceMapper.ToResource(evidence, bookFiles, aliases, term?.Trim(), source?.Trim());
            var sortDirection = pagingResource.SortKey.IsNullOrWhiteSpace() || pagingResource.SortDirection == SortDirection.Default ? SortDirection.Ascending : pagingResource.SortDirection;
            var sorted = SortNarrators(resources, pagingResource.SortKey, sortDirection).ToList();
            var page = pagingResource.Page <= 0 ? 1 : pagingResource.Page;
            var pageSize = pagingResource.PageSize <= 0 ? 50 : pagingResource.PageSize;

            return new PagingResource<NarratorEvidenceResource>
            {
                Page = page,
                PageSize = pageSize,
                SortKey = pagingResource.SortKey ?? "displayName",
                SortDirection = sortDirection,
                TotalRecords = sorted.Count,
                Records = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            };
        }

        [HttpGet("{normalizedName}")]
        public ActionResult<NarratorEvidenceDetailResource> GetNarratorEvidenceDetail(string normalizedName, [FromQuery] string source = null)
        {
            var normalized = ContributorEvidence.NormalizeName(normalizedName);
            var aliases = _narratorIdentityLinkRepository.All().ToList();
            var aliasLinks = _narratorIdentityLinkRepository.GetByNormalizedName(normalized);
            var names = aliasLinks.SelectMany(x => new[] { x.CanonicalNormalizedName, x.AliasNormalizedName }).Append(normalized).Distinct().ToList();
            var evidence = _contributorEvidenceRepository.GetNarratorEvidenceByNames(names, source?.Trim());
            var bookFileIds = evidence.Where(x => x.BookFileId.HasValue).Select(x => x.BookFileId.Value).Distinct().ToList();
            var bookFiles = bookFileIds.Any() ? _mediaFileService.Get(bookFileIds) : new List<BookFile>();
            var resource = NarratorEvidenceResourceMapper.ToDetailResource(evidence, bookFiles, aliases, normalized, source?.Trim());

            if (resource == null)
            {
                return NotFound();
            }

            return resource;
        }

        [HttpPost("aliases")]
        public ActionResult<NarratorIdentityLinkResource> LinkNarratorIdentity([FromBody] NarratorIdentityLinkUpdateResource resource)
        {
            if (resource == null || resource.CanonicalName.IsNullOrWhiteSpace() || resource.AliasName.IsNullOrWhiteSpace())
            {
                return BadRequest("Canonical and alias narrator names are required.");
            }

            var canonicalName = resource.CanonicalName.Trim();
            var aliasName = resource.AliasName.Trim();
            var canonicalNormalizedName = ContributorEvidence.NormalizeName(canonicalName);
            var aliasNormalizedName = ContributorEvidence.NormalizeName(aliasName);

            if (canonicalNormalizedName.IsNullOrWhiteSpace() || aliasNormalizedName.IsNullOrWhiteSpace())
            {
                return BadRequest("Canonical and alias narrator names must contain at least one letter or number.");
            }

            if (canonicalNormalizedName == aliasNormalizedName)
            {
                return BadRequest("Canonical and alias narrator names must be different.");
            }

            var existing = _narratorIdentityLinkRepository.FindByAlias(aliasNormalizedName);
            if (existing != null)
            {
                return BadRequest("This narrator identity is already linked. Unlink it before creating a different link.");
            }

            var model = existing ?? new NarratorIdentityLink();
            model.CanonicalName = canonicalName;
            model.CanonicalNormalizedName = canonicalNormalizedName;
            model.AliasName = aliasName;
            model.AliasNormalizedName = aliasNormalizedName;
            model.RelationshipType = resource.RelationshipType.IsNotNullOrWhiteSpace() ? resource.RelationshipType.Trim() : "alias";
            model.DisplayPreference = resource.DisplayPreference.IsNotNullOrWhiteSpace() ? resource.DisplayPreference.Trim() : "canonical";

            return _narratorIdentityLinkRepository.Insert(model).ToResource();
        }

        [HttpDelete("aliases/{id:int}")]
        public void UnlinkNarratorIdentity(int id)
        {
            _narratorIdentityLinkRepository.Delete(id);
        }

        [HttpGet("unmatched/paged")]
        public PagingResource<NarratorUnmatchedResource> GetUnmatchedNarratorFilesPaged([FromQuery] PagingRequestResource paging, [FromQuery] string term = null)
        {
            var requestedPage = paging?.Page ?? 1;
            var requestedPageSize = paging?.PageSize ?? 50;
            var page = global::System.Math.Max(1, requestedPage);
            var pageSize = global::System.Math.Min(100, global::System.Math.Max(1, requestedPageSize));
            var cleanTerm = term?.Trim();
            var files = _mediaFileService.GetUnmappedFiles();
            var evidence = _contributorEvidenceRepository.GetByBookFileIds(files.Select(x => x.Id));
            var manualNarratorBookFileIds = evidence
                .Where(x => x.Source == "manual" && x.Role == "narrator" && x.BookFileId.HasValue)
                .Select(x => x.BookFileId.Value)
                .Distinct()
                .ToHashSet();

            var missingNarratorFiles = files
                .Where(x => !manualNarratorBookFileIds.Contains(x.Id))
                .Where(x => cleanTerm.IsNullOrWhiteSpace() || x.Path.Contains(cleanTerm, global::System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Path)
                .ToList();

            var groupedFiles = GroupLikelyMultipartBooks(missingNarratorFiles);
            var pageGroups = groupedFiles.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var resources = BuildUnmatchedResources(pageGroups, evidence);

            return new PagingResource<NarratorUnmatchedResource>
            {
                Page = page,
                PageSize = pageSize,
                SortKey = paging?.SortKey ?? "path",
                SortDirection = SortDirection.Ascending,
                TotalRecords = groupedFiles.Count,
                Records = resources
            };
        }

        [HttpPost("unmatched/scan")]
        public ActionResult<List<NarratorUnmatchedResource>> ScanUnmatchedNarratorFiles([FromBody] NarratorUnmatchedScanResource resource)
        {
            var bookFileIds = resource?.BookFileIds?.Where(x => x > 0).Distinct().ToList() ?? new List<int>();

            if (!bookFileIds.Any())
            {
                return BadRequest("bookFileIds must be provided.");
            }

            var files = _mediaFileService.Get(bookFileIds).Where(x => x.EditionId == 0).ToList();
            var fileGroups = GroupLikelyMultipartBooks(files);
            var primaryFiles = fileGroups.Select(x => x.OrderBy(file => file.Path).First()).ToList();
            var bookFileResources = primaryFiles.Select(x => x.ToResource()).ToList();
            var suggestions = _unmappedIdentificationSuggestionService.DeepIdentifyAudio(bookFileResources);

            if (resource?.AutoAccept != false)
            {
                foreach (var suggestion in suggestions)
                {
                    var group = fileGroups.FirstOrDefault(x => x.Any(file => string.Equals(file.Path, suggestion.Path, global::System.StringComparison.OrdinalIgnoreCase)));

                    if (group == null)
                    {
                        continue;
                    }

                    if (ShouldAutoAccept(suggestion))
                    {
                        foreach (var bookFile in group)
                        {
                            UpsertManualNarratorEvidence(bookFile, suggestion.Narrator, suggestion.Confidence, BuildAutoAcceptRawValue(suggestion));
                        }
                    }
                }
            }

            var evidence = _contributorEvidenceRepository.GetByBookFileIds(files.Select(x => x.Id));

            return Accepted(BuildUnmatchedResources(fileGroups, evidence));
        }

        [HttpPut("unmatched/confirm")]
        public ActionResult<NarratorUnmatchedResource> ConfirmUnmatchedNarrator([FromBody] NarratorUnmatchedConfirmResource resource)
        {
            var bookFileIds = resource?.BookFileIds?.Where(x => x > 0).Distinct().ToList() ?? new List<int>();

            if (resource != null && resource.BookFileId > 0 && !bookFileIds.Contains(resource.BookFileId))
            {
                bookFileIds.Add(resource.BookFileId);
            }

            if (!bookFileIds.Any())
            {
                return BadRequest("bookFileIds must be provided.");
            }

            if (resource.DisplayName.IsNullOrWhiteSpace())
            {
                return BadRequest("displayName must be provided.");
            }

            var bookFiles = _mediaFileService.Get(bookFileIds).Where(x => x.EditionId == 0).ToList();

            if (!bookFiles.Any())
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unmapped book file not found");
            }

            foreach (var bookFile in bookFiles)
            {
                UpsertManualNarratorEvidence(bookFile, resource.DisplayName, resource.Confidence, resource.RawValue);
            }

            var evidence = _contributorEvidenceRepository.GetByBookFileIds(bookFiles.Select(x => x.Id));

            return Accepted(BuildUnmatchedResources(GroupLikelyMultipartBooks(bookFiles), evidence).First());
        }

        private static IEnumerable<NarratorEvidenceResource> SortNarrators(List<NarratorEvidenceResource> resources, string sortKey, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Descending;

            return (sortKey ?? "displayName") switch
            {
                "evidenceCount" => descending ? resources.OrderByDescending(x => x.EvidenceCount) : resources.OrderBy(x => x.EvidenceCount),
                "workCount" => descending ? resources.OrderByDescending(x => x.WorkCount) : resources.OrderBy(x => x.WorkCount),
                "latestUpdated" or "updated" => descending ? resources.OrderByDescending(x => x.LatestUpdated) : resources.OrderBy(x => x.LatestUpdated),
                "providerEvidenceCount" => descending ? resources.OrderByDescending(x => x.ProviderEvidenceCount) : resources.OrderBy(x => x.ProviderEvidenceCount),
                _ => descending ? resources.OrderByDescending(x => x.DisplayName) : resources.OrderBy(x => x.DisplayName)
            };
        }

        private List<NarratorUnmatchedResource> BuildUnmatchedResources(List<List<BookFile>> fileGroups, List<ContributorEvidence> evidence)
        {
            var files = fileGroups.SelectMany(x => x).ToList();
            var fileResources = files.Select(x => x.ToResource()).ToList();
            var suggestions = _unmappedIdentificationSuggestionService.GetPersisted(fileResources);
            var suggestionByPath = suggestions
                .Where(x => x.Path.IsNotNullOrWhiteSpace())
                .GroupBy(x => x.Path, global::System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Updated).First(), global::System.StringComparer.OrdinalIgnoreCase);
            var evidenceByBookFileId = evidence
                .Where(x => x.BookFileId.HasValue)
                .GroupBy(x => x.BookFileId.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            return fileGroups.Select(group =>
            {
                var orderedFiles = group.OrderBy(x => x.Path).ToList();
                var primaryFile = orderedFiles.First();
                var groupEvidence = orderedFiles
                    .SelectMany(file =>
                    {
                        evidenceByBookFileId.TryGetValue(file.Id, out var fileEvidence);
                        return fileEvidence ?? new List<ContributorEvidence>();
                    })
                    .ToList();
                var suggestion = orderedFiles
                    .Select(file =>
                    {
                        suggestionByPath.TryGetValue(file.Path, out var fileSuggestion);
                        return fileSuggestion;
                    })
                    .Where(x => x != null)
                    .OrderByDescending(x => x.Updated)
                    .FirstOrDefault();

                var providerSupport = GetProviderSupport(suggestion?.Narrator);
                var manualEvidence = groupEvidence.FirstOrDefault(x => x.Source == "manual" && x.Role == "narrator");

                return new NarratorUnmatchedResource
                {
                    BookFileId = primaryFile.Id,
                    BookFileIds = orderedFiles.Select(x => x.Id).ToList(),
                    PartCount = orderedFiles.Count,
                    GroupKey = GetMultipartGroupKey(orderedFiles),
                    GroupTitle = GetGroupTitle(orderedFiles),
                    Path = primaryFile.Path,
                    Paths = orderedFiles.Select(x => x.Path).ToList(),
                    Size = orderedFiles.Sum(x => x.Size),
                    Modified = orderedFiles.Max(x => x.Modified),
                    Reviewed = orderedFiles.All(x => x.Reviewed),
                    SuggestedAuthor = suggestion?.LikelyAuthor,
                    SuggestedBook = suggestion?.LikelyBook,
                    SuggestedEdition = suggestion?.LikelyEdition,
                    SuggestedNarrator = suggestion?.Narrator,
                    ValidatedNarrator = suggestion?.ValidatedNarrator,
                    NarratorValidationStatus = suggestion?.NarratorValidationStatus,
                    NarratorValidationDetail = suggestion?.NarratorValidationDetail,
                    SuggestionConfidence = suggestion?.Confidence,
                    SuggestionStatus = suggestion?.Status,
                    SuggestionStage = suggestion?.Stage,
                    SuggestionExplanation = suggestion?.Explanation,
                    TranscriptExcerpt = suggestion?.TranscriptExcerpt,
                    ProviderSupported = providerSupport.ProviderSupported,
                    ProviderSupportLabel = providerSupport.ProviderSupportLabel,
                    ProviderEvidenceCount = providerSupport.ProviderEvidenceCount,
                    AutoAcceptThreshold = _configService.MinimumBookMatchSimilarity,
                    CanAutoAccept = suggestion != null && ShouldAutoAccept(suggestion),
                    IsAutoAccepted = manualEvidence != null,
                    ContributorEvidence = groupEvidence.Select(x => x.ToResource()).ToList()
                };
            }).ToList();
        }

        private static List<List<BookFile>> GroupLikelyMultipartBooks(List<BookFile> files)
        {
            var groups = files
                .GroupBy(x => GetCandidateDirectory(x.Path), global::System.StringComparer.OrdinalIgnoreCase)
                .SelectMany(directoryGroup =>
                {
                    var directoryFiles = directoryGroup.OrderBy(x => x.Path).ToList();

                    if (directoryFiles.Count <= 1 || !IsLikelyMultipartBookGroup(directoryGroup.Key, directoryFiles))
                    {
                        return directoryFiles.Select(x => new List<BookFile> { x });
                    }

                    return new[] { directoryFiles };
                })
                .OrderBy(x => x.First().Path)
                .ToList();

            return groups;
        }

        private static bool IsLikelyMultipartBookGroup(string directory, List<BookFile> files)
        {
            var folderTitle = NormalizeTitle(Path.GetFileName(directory));

            if (folderTitle.IsNullOrWhiteSpace())
            {
                return false;
            }

            var numberedCount = files.Count(file => LooksNumbered(Path.GetFileNameWithoutExtension(file.Path)));
            var folderTitleInNames = files.Count(file => NormalizeTitle(Path.GetFileNameWithoutExtension(file.Path)).Contains(folderTitle));

            return numberedCount >= 2 || folderTitleInNames >= global::System.Math.Min(2, files.Count);
        }

        private static bool LooksNumbered(string fileName)
        {
            return Regex.IsMatch(fileName ?? string.Empty, @"(^|[\s._-])((part|pt|disc|disk|cd|track|chapter|ch)\s*)?\d{1,4}([\s._-]|$)", RegexOptions.IgnoreCase);
        }

        private static string GetMultipartGroupKey(List<BookFile> files)
        {
            if (files.Count <= 1)
            {
                return $"file:{files.First().Id}";
            }

            return $"folder:{GetCandidateDirectory(files.First().Path)}";
        }

        private static string GetGroupTitle(List<BookFile> files)
        {
            var directory = GetCandidateDirectory(files.First().Path);
            var folder = Path.GetFileName(directory);

            if (folder.IsNotNullOrWhiteSpace())
            {
                return folder;
            }

            return Path.GetFileNameWithoutExtension(files.First().Path);
        }

        private static string GetCandidateDirectory(string path)
        {
            return Path.GetDirectoryName(path ?? string.Empty) ?? string.Empty;
        }

        private static string NormalizeTitle(string value)
        {
            return new string((value ?? string.Empty)
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }

        private ProviderSupportResult GetProviderSupport(string narrator)
        {
            var normalized = ContributorEvidence.NormalizeName(narrator);

            if (normalized.IsNullOrWhiteSpace())
            {
                return new ProviderSupportResult
                {
                    ProviderSupportLabel = "No narrator proposal yet"
                };
            }

            var providerEvidence = _contributorEvidenceRepository.GetNarratorEvidenceByNames(new[] { normalized }, "providerMetadata");

            return new ProviderSupportResult
            {
                ProviderSupported = providerEvidence.Any(),
                ProviderEvidenceCount = providerEvidence.Count,
                ProviderSupportLabel = providerEvidence.Any() ?
                    $"Metadata has {providerEvidence.Count} narrator match{(providerEvidence.Count == 1 ? string.Empty : "es")} for this name." :
                    "STT narrator proposal ready for confirmation; provider metadata has no narrator record to compare yet."
            };
        }

        private bool ShouldAutoAccept(global::Readarr.Api.V1.ManualImport.ManualImportIdentificationSuggestionResource suggestion)
        {
            return suggestion?.Narrator.IsNotNullOrWhiteSpace() == true &&
                   suggestion.Confidence.HasValue &&
                   suggestion.Confidence.Value >= _configService.MinimumBookMatchSimilarity;
        }

        private void UpsertManualNarratorEvidence(BookFile bookFile, string displayName, int? confidence, string rawValue)
        {
            var now = global::System.DateTime.UtcNow;
            var cleanDisplayName = displayName.Trim();

            _contributorEvidenceRepository.DeleteByBookFileIdSourceAndRole(bookFile.Id, "manual", "narrator");
            _contributorEvidenceRepository.Insert(new ContributorEvidence
            {
                BookFileId = bookFile.Id,
                Role = "narrator",
                DisplayName = cleanDisplayName,
                NormalizedName = ContributorEvidence.NormalizeName(cleanDisplayName),
                Source = "manual",
                Confidence = confidence,
                RawValue = rawValue.IsNotNullOrWhiteSpace() ? rawValue : cleanDisplayName,
                Created = now,
                Updated = now
            });
        }

        private string BuildAutoAcceptRawValue(global::Readarr.Api.V1.ManualImport.ManualImportIdentificationSuggestionResource suggestion)
        {
            return $"autoAcceptedFrom=sttTranscript;threshold={_configService.MinimumBookMatchSimilarity};confidence={suggestion.Confidence};narrator={suggestion.Narrator}";
        }

        private class ProviderSupportResult
        {
            public bool ProviderSupported { get; set; }
            public string ProviderSupportLabel { get; set; }
            public int ProviderEvidenceCount { get; set; }
        }
    }
}
