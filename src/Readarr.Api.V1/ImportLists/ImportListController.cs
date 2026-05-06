using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using Readarr.Http;

namespace Readarr.Api.V1.ImportLists
{
    [V1ApiController]
    public class ImportListController : ProviderControllerBase<ImportListResource, ImportListBulkResource, IImportList, ImportListDefinition>
    {
        public static readonly ImportListResourceMapper ResourceMapper = new ();
        public static readonly ImportListBulkResourceMapper BulkResourceMapper = new ();
        private readonly IImportListPreviewService _importListPreviewService;

        public ImportListController(IImportListFactory importListFactory,
                                    IImportListPreviewService importListPreviewService,
                                    QualityProfileExistsValidator qualityProfileExistsValidator,
                                    MetadataProfileExistsValidator metadataProfileExistsValidator)
            : base(importListFactory, "importlist", ResourceMapper, BulkResourceMapper)
        {
            _importListPreviewService = importListPreviewService;

            Http.Validation.RuleBuilderExtensions.ValidId(SharedValidator.RuleFor(s => s.QualityProfileId));
            Http.Validation.RuleBuilderExtensions.ValidId(SharedValidator.RuleFor(s => s.MetadataProfileId));

            SharedValidator.RuleFor(c => c.RootFolderPath).IsValidPath();
            SharedValidator.RuleFor(c => c.QualityProfileId).SetValidator(qualityProfileExistsValidator);
            SharedValidator.RuleFor(c => c.MetadataProfileId).SetValidator(metadataProfileExistsValidator);
        }

        [Microsoft.AspNetCore.Mvc.HttpPost("preview")]
        public ImportListPreview Preview([Microsoft.AspNetCore.Mvc.FromBody] ImportListResource resource)
        {
            return _importListPreviewService.Preview(ResourceMapper.ToModel(resource));
        }
    }
}
