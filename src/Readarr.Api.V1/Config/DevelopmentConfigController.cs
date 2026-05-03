using System.Linq;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Validation;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.REST;

namespace Prowlarr.Api.V1.Config
{
    [V1ApiController("config/development")]
    public class DevelopmentConfigController : RestController<DevelopmentConfigResource>
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;

        public DevelopmentConfigController(IConfigFileProvider configFileProvider,
                                IConfigService configService)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;

            SharedValidator.RuleFor(c => c.MetadataSource)
                           .NotEmpty()
                           .WithMessage("Metadata source must be selected");

            SharedValidator.RuleFor(c => c.MetadataSource)
                           .Must(source => MetadataSourceConfig.IsOriginalReadarr(source) || source.IsValidUrl())
                           .WithMessage("Metadata source must be a valid URL or the original Readarr source");
        }

        protected override DevelopmentConfigResource GetResourceById(int id)
        {
            return GetDevelopmentConfig();
        }

        [HttpGet]
        public DevelopmentConfigResource GetDevelopmentConfig()
        {
            var resource = DevelopmentConfigResourceMapper.ToResource(_configFileProvider, _configService);
            resource.Id = 1;

            return resource;
        }

        [RestPutById]
        public ActionResult<DevelopmentConfigResource> SaveDevelopmentConfig(DevelopmentConfigResource resource)
        {
            var dictionary = resource.GetType()
                                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .ToDictionary(prop => prop.Name, prop => prop.GetValue(resource, null));

            _configFileProvider.SaveConfigDictionary(dictionary);
            _configService.SaveConfigDictionary(dictionary);

            return Accepted(resource.Id);
        }
    }
}
