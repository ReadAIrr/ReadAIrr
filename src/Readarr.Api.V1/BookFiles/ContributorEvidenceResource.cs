using System;
using NzbDrone.Core.MediaFiles;

namespace Readarr.Api.V1.BookFiles
{
    public class ContributorEvidenceResource
    {
        public int Id { get; set; }
        public int? BookFileId { get; set; }
        public int? EditionId { get; set; }
        public string ForeignEditionId { get; set; }
        public string Role { get; set; }
        public string DisplayName { get; set; }
        public string NormalizedName { get; set; }
        public string Source { get; set; }
        public int? Confidence { get; set; }
        public string RawValue { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }

    public class ContributorEvidenceUpdateResource
    {
        public int BookFileId { get; set; }
        public string Role { get; set; }
        public string DisplayName { get; set; }
        public int? Confidence { get; set; }
        public string RawValue { get; set; }
    }

    public static class ContributorEvidenceResourceMapper
    {
        public static ContributorEvidenceResource ToResource(this ContributorEvidence model)
        {
            if (model == null)
            {
                return null;
            }

            return new ContributorEvidenceResource
            {
                Id = model.Id,
                BookFileId = model.BookFileId,
                EditionId = model.EditionId,
                ForeignEditionId = model.ForeignEditionId,
                Role = model.Role,
                DisplayName = model.DisplayName,
                NormalizedName = model.NormalizedName,
                Source = model.Source,
                Confidence = model.Confidence,
                RawValue = model.RawValue,
                Created = model.Created,
                Updated = model.Updated
            };
        }
    }
}
