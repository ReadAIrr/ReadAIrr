using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Author
{
    public class AuthorIdentityLinkResource : RestResource
    {
        public int CanonicalAuthorId { get; set; }
        public string CanonicalAuthorName { get; set; }
        public string CanonicalAuthorTitleSlug { get; set; }
        public int AliasAuthorId { get; set; }
        public string AliasAuthorName { get; set; }
        public string AliasAuthorTitleSlug { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
    }

    public class LinkedAuthorResource
    {
        public int Id { get; set; }
        public string AuthorName { get; set; }
        public string TitleSlug { get; set; }
        public string RelationshipType { get; set; }
        public string DisplayPreference { get; set; }
        public bool IsCanonical { get; set; }
        public int LinkId { get; set; }
    }

    public static class AuthorIdentityLinkResourceMapper
    {
        public static AuthorIdentityLinkResource ToResource(this AuthorIdentityLink model, Dictionary<int, NzbDrone.Core.Books.Author> authors)
        {
            if (model == null)
            {
                return null;
            }

            var canonicalAuthor = authors.GetValueOrDefault(model.CanonicalAuthorId);
            var aliasAuthor = authors.GetValueOrDefault(model.AliasAuthorId);

            return new AuthorIdentityLinkResource
            {
                Id = model.Id,
                CanonicalAuthorId = model.CanonicalAuthorId,
                CanonicalAuthorName = canonicalAuthor?.Name,
                CanonicalAuthorTitleSlug = canonicalAuthor?.Metadata.Value.TitleSlug,
                AliasAuthorId = model.AliasAuthorId,
                AliasAuthorName = aliasAuthor?.Name,
                AliasAuthorTitleSlug = aliasAuthor?.Metadata.Value.TitleSlug,
                RelationshipType = model.RelationshipType,
                DisplayPreference = model.DisplayPreference
            };
        }

        public static List<AuthorIdentityLinkResource> ToResource(this IEnumerable<AuthorIdentityLink> models, Dictionary<int, NzbDrone.Core.Books.Author> authors)
        {
            return models.Select(x => x.ToResource(authors)).ToList();
        }

        public static AuthorIdentityLink ToModel(this AuthorIdentityLinkResource resource)
        {
            return new AuthorIdentityLink
            {
                Id = resource.Id,
                CanonicalAuthorId = resource.CanonicalAuthorId,
                AliasAuthorId = resource.AliasAuthorId,
                RelationshipType = resource.RelationshipType,
                DisplayPreference = resource.DisplayPreference
            };
        }
    }
}
