using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Profiles.Metadata;
using Readarr.Http;

namespace Readarr.Api.V1.Profiles.Metadata
{
    [V1ApiController("metadataprofile/schema")]
    public class MetadataProfileSchemaController : Controller
    {
        [HttpGet]
        public MetadataProfileResource GetAll()
        {
            var profile = new MetadataProfile
            {
                AllowedLanguages = "eng",
                RequireReadable = false,
                RequireAudio = false,
                SkipAnthologies = false,
                SkipCollections = false,
                SkipSerializedParts = false,
                SkipEssays = false,
                SkipShortStories = false
            };

            return profile.ToResource();
        }
    }
}
