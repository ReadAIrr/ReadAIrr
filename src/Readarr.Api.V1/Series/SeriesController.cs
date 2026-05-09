using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using Readarr.Http;

namespace Readarr.Api.V1.Series
{
    [V1ApiController]
    public class SeriesController : Controller
    {
        protected readonly ISeriesService _seriesService;
        protected readonly ISeriesBookLinkService _seriesBookLinkService;
        protected readonly IAuthorIdentityLinkService _authorIdentityLinkService;
        protected readonly IMediaFileService _mediaFileService;

        public SeriesController(ISeriesService seriesService,
                                ISeriesBookLinkService seriesBookLinkService,
                                IAuthorIdentityLinkService authorIdentityLinkService,
                                IMediaFileService mediaFileService)
        {
            _seriesService = seriesService;
            _seriesBookLinkService = seriesBookLinkService;
            _authorIdentityLinkService = authorIdentityLinkService;
            _mediaFileService = mediaFileService;
        }

        [HttpGet]
        public List<SeriesResource> GetSeries(int? authorId, bool includeLinkedAuthors = false)
        {
            var series = authorId.HasValue ?
                includeLinkedAuthors ?
                    _seriesService.GetByAuthorIds(_authorIdentityLinkService.GetAuthorIdentityIds(authorId.Value)) :
                    _seriesService.GetByAuthorId(authorId.Value) :
                _seriesService.All();
            var links = series.ToDictionary(x => x.Id, x => _seriesBookLinkService.GetLinksBySeries(x.Id));
            var files = links.SelectMany(x => x.Value)
                .Select(x => x.BookId)
                .Distinct()
                .SelectMany(x => _mediaFileService.GetFilesByBook(x))
                .ToList();

            return series.ToResource(links, files);
        }

        [HttpGet("{id:int}")]
        public SeriesResource GetSeriesById(int id)
        {
            var series = _seriesService.Get(id);
            var links = _seriesBookLinkService.GetLinksBySeries(id);
            var files = links.Select(x => x.BookId)
                .Distinct()
                .SelectMany(x => _mediaFileService.GetFilesByBook(x))
                .ToList();

            return series.ToResource(links, files);
        }
    }
}
