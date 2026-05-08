using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MediaFiles;
using Readarr.Http;

namespace Readarr.Api.V1.Contributors
{
    [V1ApiController("narrator")]
    public class NarratorController : Controller
    {
        private readonly IContributorEvidenceRepository _contributorEvidenceRepository;
        private readonly IMediaFileService _mediaFileService;

        public NarratorController(IContributorEvidenceRepository contributorEvidenceRepository,
                                  IMediaFileService mediaFileService)
        {
            _contributorEvidenceRepository = contributorEvidenceRepository;
            _mediaFileService = mediaFileService;
        }

        [HttpGet]
        public List<NarratorEvidenceResource> GetNarratorEvidence([FromQuery] string term = null, [FromQuery] string source = null)
        {
            var evidence = _contributorEvidenceRepository.All().ToList();
            var bookFileIds = evidence.Where(x => x.BookFileId.HasValue).Select(x => x.BookFileId.Value).Distinct().ToList();
            var bookFiles = bookFileIds.Any() ? _mediaFileService.Get(bookFileIds) : new List<BookFile>();

            return NarratorEvidenceResourceMapper.ToResource(evidence, bookFiles, term?.Trim(), source?.Trim());
        }
    }
}
