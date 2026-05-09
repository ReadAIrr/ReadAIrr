using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
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
            var evidence = _contributorEvidenceRepository.All().ToList();
            var aliases = _narratorIdentityLinkRepository.All().ToList();
            var bookFileIds = evidence.Where(x => x.BookFileId.HasValue).Select(x => x.BookFileId.Value).Distinct().ToList();
            var bookFiles = bookFileIds.Any() ? _mediaFileService.Get(bookFileIds) : new List<BookFile>();

            return NarratorEvidenceResourceMapper.ToResource(evidence, bookFiles, aliases, term?.Trim(), source?.Trim());
        }

        [HttpGet("{normalizedName}")]
        public ActionResult<NarratorEvidenceDetailResource> GetNarratorEvidenceDetail(string normalizedName, [FromQuery] string source = null)
        {
            var normalized = ContributorEvidence.NormalizeName(normalizedName);
            var evidence = _contributorEvidenceRepository.All().ToList();
            var aliases = _narratorIdentityLinkRepository.All().ToList();
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
            var model = existing ?? new NarratorIdentityLink();
            model.CanonicalName = canonicalName;
            model.CanonicalNormalizedName = canonicalNormalizedName;
            model.AliasName = aliasName;
            model.AliasNormalizedName = aliasNormalizedName;
            model.RelationshipType = resource.RelationshipType.IsNotNullOrWhiteSpace() ? resource.RelationshipType.Trim() : "alias";
            model.DisplayPreference = resource.DisplayPreference.IsNotNullOrWhiteSpace() ? resource.DisplayPreference.Trim() : "canonical";

            return existing == null ?
                _narratorIdentityLinkRepository.Insert(model).ToResource() :
                _narratorIdentityLinkRepository.Update(model).ToResource();
        }

        [HttpDelete("aliases/{id:int}")]
        public void UnlinkNarratorIdentity(int id)
        {
            _narratorIdentityLinkRepository.Delete(id);
        }
    }
}
