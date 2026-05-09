using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using Readarr.Http;

namespace Readarr.Api.V1.Contributors
{
    [V1ApiController("narrator")]
    public class NarratorController : Controller
    {
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly INarratorIdentityLinkRepository _narratorIdentityLinkRepository;
        private readonly IMediaFileService _mediaFileService;

        public NarratorController(IContributorEvidenceRepository contributorEvidenceRepository,
                                  INarratorIdentityLinkRepository narratorIdentityLinkRepository,
                                  IMediaFileService mediaFileService)
        {
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _narratorIdentityLinkRepository = narratorIdentityLinkRepository;
            _mediaFileService = mediaFileService;
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
    }
}
