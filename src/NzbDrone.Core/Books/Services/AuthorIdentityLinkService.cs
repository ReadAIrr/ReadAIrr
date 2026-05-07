using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public interface IAuthorIdentityLinkService
    {
        List<AuthorIdentityLink> All();
        List<AuthorIdentityLink> GetByAuthorId(int authorId);
        List<int> GetLinkedAuthorIds(int authorId);
        List<int> GetAuthorIdentityIds(int authorId);
        AuthorIdentityLink Link(AuthorIdentityLink link);
        void Delete(int id);
    }

    public class AuthorIdentityLinkService : IAuthorIdentityLinkService, IHandle<AuthorDeletedEvent>
    {
        private readonly IAuthorIdentityLinkRepository _authorIdentityLinkRepository;
        private readonly IAuthorService _authorService;

        public AuthorIdentityLinkService(IAuthorIdentityLinkRepository authorIdentityLinkRepository,
                                         IAuthorService authorService)
        {
            _authorIdentityLinkRepository = authorIdentityLinkRepository;
            _authorService = authorService;
        }

        public List<AuthorIdentityLink> All()
        {
            return _authorIdentityLinkRepository.All().ToList();
        }

        public List<AuthorIdentityLink> GetByAuthorId(int authorId)
        {
            return GetAuthorIdentityIds(authorId)
                .SelectMany(x => _authorIdentityLinkRepository.GetByAuthorId(x))
                .DistinctBy(x => x.Id)
                .ToList();
        }

        public List<int> GetLinkedAuthorIds(int authorId)
        {
            return GetAuthorIdentityIds(authorId).Where(x => x != authorId).ToList();
        }

        public List<int> GetAuthorIdentityIds(int authorId)
        {
            var links = _authorIdentityLinkRepository.All().ToList();
            var authorIds = new HashSet<int> { authorId };
            var pending = new Queue<int>();
            pending.Enqueue(authorId);

            while (pending.Any())
            {
                var currentAuthorId = pending.Dequeue();
                var connectedAuthorIds = links
                    .Where(x => x.CanonicalAuthorId == currentAuthorId || x.AliasAuthorId == currentAuthorId)
                    .Select(x => x.CanonicalAuthorId == currentAuthorId ? x.AliasAuthorId : x.CanonicalAuthorId);

                foreach (var connectedAuthorId in connectedAuthorIds)
                {
                    if (authorIds.Add(connectedAuthorId))
                    {
                        pending.Enqueue(connectedAuthorId);
                    }
                }
            }

            return authorIds.ToList();
        }

        public AuthorIdentityLink Link(AuthorIdentityLink link)
        {
            if (link.CanonicalAuthorId == link.AliasAuthorId)
            {
                throw new InvalidOperationException("Cannot link an author identity to itself.");
            }

            _authorService.GetAuthor(link.CanonicalAuthorId);
            _authorService.GetAuthor(link.AliasAuthorId);

            link.RelationshipType = string.IsNullOrWhiteSpace(link.RelationshipType) ? "penName" : link.RelationshipType;
            link.DisplayPreference = string.IsNullOrWhiteSpace(link.DisplayPreference) ? "canonical" : link.DisplayPreference;

            var existingLink = _authorIdentityLinkRepository.FindByAuthorPair(link.CanonicalAuthorId, link.AliasAuthorId);

            if (existingLink != null)
            {
                existingLink.CanonicalAuthorId = link.CanonicalAuthorId;
                existingLink.AliasAuthorId = link.AliasAuthorId;
                existingLink.RelationshipType = link.RelationshipType;
                existingLink.DisplayPreference = link.DisplayPreference;

                return _authorIdentityLinkRepository.Update(existingLink);
            }

            return _authorIdentityLinkRepository.Insert(link);
        }

        public void Delete(int id)
        {
            _authorIdentityLinkRepository.Delete(id);
        }

        public void Handle(AuthorDeletedEvent message)
        {
            _authorIdentityLinkRepository.DeleteByAuthorId(message.Author.Id);
        }
    }
}
