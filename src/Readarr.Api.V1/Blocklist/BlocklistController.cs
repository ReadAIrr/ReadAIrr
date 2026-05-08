using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.Extensions;

namespace Readarr.Api.V1.Blocklist
{
    [V1ApiController]
    public class BlocklistController : Controller
    {
        private readonly IBlocklistService _blocklistService;
        private readonly ICustomFormatCalculationService _formatCalculator;

        public BlocklistController(IBlocklistService blocklistService,
                                   ICustomFormatCalculationService formatCalculator)
        {
            _blocklistService = blocklistService;
            _formatCalculator = formatCalculator;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<BlocklistResource> GetBlocklist([FromQuery] PagingRequestResource paging, string term)
        {
            var pagingResource = new PagingResource<BlocklistResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<BlocklistResource, NzbDrone.Core.Blocklisting.Blocklist>("date", SortDirection.Descending);

            if (term.IsNotNullOrWhiteSpace())
            {
                term = term.Trim();
                pagingSpec.FilterExpressions.Add(b =>
                    b.SourceTitle.Contains(term) ||
                    b.Indexer.Contains(term) ||
                    b.Message.Contains(term) ||
                    b.TorrentInfoHash.Contains(term) ||
                    b.Author.Metadata.Value.Name.Contains(term) ||
                    b.Author.Metadata.Value.SortName.Contains(term) ||
                    b.Author.Metadata.Value.SortNameLastFirst.Contains(term));
            }

            return pagingSpec.ApplyToPage(_blocklistService.Paged, model => BlocklistResourceMapper.MapToResource(model, _formatCalculator));
        }

        [RestDeleteById]
        public void DeleteBlocklist(int id)
        {
            _blocklistService.Delete(id);
        }

        [HttpDelete("bulk")]
        public object Remove([FromBody] BlocklistBulkResource resource)
        {
            _blocklistService.Delete(resource.Ids);

            return new { };
        }
    }
}
