using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using Readarr.Http;

namespace Readarr.Api.V1.Author
{
    [V1ApiController]
    public class AuthorIdentityLinkController : Controller
    {
        private readonly IAuthorIdentityLinkService _authorIdentityLinkService;
        private readonly IAuthorService _authorService;

        public AuthorIdentityLinkController(IAuthorIdentityLinkService authorIdentityLinkService,
                                            IAuthorService authorService)
        {
            _authorIdentityLinkService = authorIdentityLinkService;
            _authorService = authorService;
        }

        [HttpGet]
        public List<AuthorIdentityLinkResource> GetAuthorIdentityLinks(int? authorId)
        {
            var links = authorId.HasValue ? _authorIdentityLinkService.GetByAuthorId(authorId.Value) : _authorIdentityLinkService.All();
            var authorIds = links.SelectMany(x => new[] { x.CanonicalAuthorId, x.AliasAuthorId }).Distinct();
            var authors = _authorService.GetAuthors(authorIds).ToDictionary(x => x.Id);

            return links.ToResource(authors);
        }

        [HttpPost]
        public ActionResult<AuthorIdentityLinkResource> LinkAuthorIdentity([FromBody] AuthorIdentityLinkResource resource)
        {
            var link = _authorIdentityLinkService.Link(resource.ToModel());
            var authors = _authorService.GetAuthors(new[] { link.CanonicalAuthorId, link.AliasAuthorId }).ToDictionary(x => x.Id);

            return Created($"/api/v1/authoridentitylink/{link.Id}", link.ToResource(authors));
        }

        [HttpDelete("{id:int}")]
        public void UnlinkAuthorIdentity(int id)
        {
            _authorIdentityLinkService.Delete(id);
        }
    }
}
