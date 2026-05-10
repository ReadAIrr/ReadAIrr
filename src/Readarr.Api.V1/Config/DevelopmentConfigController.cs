using System.Linq;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Validation;
using NzbDrone.Http.REST.Attributes;
using Readarr.Api.V1.BookFiles;
using Readarr.Http;
using Readarr.Http.REST;

namespace Prowlarr.Api.V1.Config
{
    [V1ApiController("config/development")]
    public class DevelopmentConfigController : RestController<DevelopmentConfigResource>
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IMetadataSourceHealthService _metadataSourceHealthService;
        private readonly IUnmappedIdentificationSuggestionService _unmappedIdentificationSuggestionService;

        public DevelopmentConfigController(IConfigFileProvider configFileProvider,
                                IConfigService configService,
                                IMetadataSourceHealthService metadataSourceHealthService,
                                IUnmappedIdentificationSuggestionService unmappedIdentificationSuggestionService)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _metadataSourceHealthService = metadataSourceHealthService;
            _unmappedIdentificationSuggestionService = unmappedIdentificationSuggestionService;

            SharedValidator.RuleFor(c => c.MetadataSource)
                           .NotEmpty()
                           .WithMessage("Metadata source must be selected");

            SharedValidator.RuleFor(c => c.MetadataSource)
                           .Must(source => MetadataSourceConfig.IsOriginalReadarr(source) || source.IsValidUrl())
                           .WithMessage("Metadata source must be a valid URL or the original Readarr source");

            SharedValidator.RuleFor(c => c.MinimumBookMatchSimilarity)
                           .InclusiveBetween(50, 100)
                           .WithMessage("Minimum match similarity must be between 50 and 100");

            SharedValidator.RuleFor(c => c.OpenRouterBaseUrl)
                           .Must(baseUrl => baseUrl.IsNullOrWhiteSpace() || baseUrl.IsValidUrl())
                           .WithMessage("OpenRouter base URL must be a valid URL");

            SharedValidator.RuleFor(c => c.OpenRouterTimeout)
                           .InclusiveBetween(5, 120)
                           .WithMessage("OpenRouter timeout must be between 5 and 120 seconds");

            SharedValidator.RuleFor(c => c.OpenRouterMaxFileContext)
                           .InclusiveBetween(1, 20)
                           .WithMessage("OpenRouter file context limit must be between 1 and 20 files");

            SharedValidator.RuleFor(c => c.SpeechToTextBaseUrl)
                           .Must(baseUrl => baseUrl.IsNullOrWhiteSpace() || baseUrl.IsValidUrl())
                           .WithMessage("Speech-to-text base URL must be a valid URL");

            SharedValidator.RuleFor(c => c.SpeechToTextIntroSeconds)
                           .InclusiveBetween(5, 120)
                           .WithMessage("Speech-to-text intro window must be between 5 and 120 seconds");
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
        public ActionResult<DevelopmentConfigResource> SaveDevelopmentConfig([FromBody] DevelopmentConfigResource resource)
        {
            if (resource.OpenRouterApiKey == DevelopmentConfigResourceMapper.RedactedSecret)
            {
                resource.OpenRouterApiKey = _configService.OpenRouterApiKey;
            }

            if (resource.SpeechToTextApiKey == DevelopmentConfigResourceMapper.RedactedSecret)
            {
                resource.SpeechToTextApiKey = _configService.SpeechToTextApiKey;
            }

            var dictionary = resource.GetType()
                                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .ToDictionary(prop => prop.Name, prop => prop.GetValue(resource, null));

            _configFileProvider.SaveConfigDictionary(dictionary);
            _configService.SaveConfigDictionary(dictionary);

            return Accepted(resource.Id);
        }

        [HttpPost("openrouter/test")]
        [Consumes("application/json")]
        public OpenRouterConfigTestResource TestOpenRouterConfig([FromBody] OpenRouterConfigTestResource resource)
        {
            return _unmappedIdentificationSuggestionService.Test(resource);
        }

        [HttpPost("test")]
        [Consumes("application/json")]
        public DevelopmentConfigTestResource TestDevelopmentConfig([FromBody] DevelopmentConfigTestResource resource)
        {
            var metadataSource = resource.MetadataSource;

            if (metadataSource.IsNullOrWhiteSpace())
            {
                metadataSource = MetadataSourceConfig.GoodreadsHosted;
            }

            if (!MetadataSourceConfig.IsOriginalReadarr(metadataSource) && !metadataSource.IsValidUrl())
            {
                return new DevelopmentConfigTestResource
                {
                    MetadataSource = metadataSource,
                    IsHealthy = false,
                    Message = "Metadata source must be a valid URL or the original Readarr source"
                };
            }

            var result = _metadataSourceHealthService.Test(metadataSource);

            return new DevelopmentConfigTestResource
            {
                MetadataSource = result.MetadataSource,
                IsHealthy = result.IsHealthy,
                Message = result.Message,
                Detail = result.Detail,
                StatusCode = result.StatusCode,
                ResponseTimeMs = result.ResponseTimeMs
            };
        }
    }
}
