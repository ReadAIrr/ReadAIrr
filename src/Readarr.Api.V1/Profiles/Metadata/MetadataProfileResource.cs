using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Profiles.Metadata;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Profiles.Metadata
{
    public class MetadataProfileResource : RestResource
    {
        public string Name { get; set; }
        public double MinPopularity { get; set; }
        public bool SkipMissingDate { get; set; }
        public bool SkipMissingIsbn { get; set; }
        public bool SkipPartsAndSets { get; set; }
        public bool SkipSeriesSecondary { get; set; }
        public bool RequireReadable { get; set; }
        public bool RequireAudio { get; set; }
        public bool SkipAnthologies { get; set; }
        public bool SkipCollections { get; set; }
        public bool SkipSerializedParts { get; set; }
        public bool SkipEssays { get; set; }
        public bool SkipShortStories { get; set; }
        public string AllowedLanguages { get; set; }
        public int MinPages { get; set; }
        public List<string> Ignored { get; set; }
    }

    public static class MetadataProfileResourceMapper
    {
        public static MetadataProfileResource ToResource(this MetadataProfile model)
        {
            if (model == null)
            {
                return null;
            }

            return new MetadataProfileResource
            {
                Id = model.Id,
                Name = model.Name,
                MinPopularity = model.MinPopularity,
                SkipMissingDate = model.SkipMissingDate,
                SkipMissingIsbn = model.SkipMissingIsbn,
                SkipPartsAndSets = model.SkipPartsAndSets,
                SkipSeriesSecondary = model.SkipSeriesSecondary,
                RequireReadable = model.RequireReadable,
                RequireAudio = model.RequireAudio,
                SkipAnthologies = model.SkipAnthologies,
                SkipCollections = model.SkipCollections,
                SkipSerializedParts = model.SkipSerializedParts,
                SkipEssays = model.SkipEssays,
                SkipShortStories = model.SkipShortStories,
                AllowedLanguages = model.AllowedLanguages,
                MinPages = model.MinPages,
                Ignored = CleanIgnored(model.Ignored)
            };
        }

        public static MetadataProfile ToModel(this MetadataProfileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new MetadataProfile
            {
                Id = resource.Id,
                Name = resource.Name,
                MinPopularity = resource.MinPopularity,
                SkipMissingDate = resource.SkipMissingDate,
                SkipMissingIsbn = resource.SkipMissingIsbn,
                SkipPartsAndSets = resource.SkipPartsAndSets,
                SkipSeriesSecondary = resource.SkipSeriesSecondary,
                RequireReadable = resource.RequireReadable,
                RequireAudio = resource.RequireAudio,
                SkipAnthologies = resource.SkipAnthologies,
                SkipCollections = resource.SkipCollections,
                SkipSerializedParts = resource.SkipSerializedParts,
                SkipEssays = resource.SkipEssays,
                SkipShortStories = resource.SkipShortStories,
                AllowedLanguages = resource.AllowedLanguages,
                MinPages = resource.MinPages,
                Ignored = CleanIgnored(resource.Ignored)
            };
        }

        private static List<string> CleanIgnored(IEnumerable<string> ignored)
        {
            return (ignored ?? Enumerable.Empty<string>())
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Select(x => x.Trim())
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }

        public static List<MetadataProfileResource> ToResource(this IEnumerable<MetadataProfile> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
