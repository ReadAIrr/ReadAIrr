using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
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
        List<AuthorIdentityLinkSuggestion> GetSuggestions(int authorId);
        AuthorIdentityLink Link(AuthorIdentityLink link);
        void Delete(int id);
    }

    public class AuthorIdentityLinkService : IAuthorIdentityLinkService, IHandle<AuthorDeletedEvent>
    {
        private readonly IAuthorIdentityLinkRepository _authorIdentityLinkRepository;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;

        public AuthorIdentityLinkService(IAuthorIdentityLinkRepository authorIdentityLinkRepository,
                                         IAuthorService authorService,
                                         IBookService bookService)
        {
            _authorIdentityLinkRepository = authorIdentityLinkRepository;
            _authorService = authorService;
            _bookService = bookService;
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

        public List<AuthorIdentityLinkSuggestion> GetSuggestions(int authorId)
        {
            var author = _authorService.GetAuthor(authorId);
            var allAuthors = _authorService.GetAllAuthors();
            var linkedAuthorIds = GetAuthorIdentityIds(authorId).ToHashSet();
            var allBooks = _bookService.GetAllBooks();
            var authorBooks = allBooks.Where(x => x.AuthorMetadataId == author.AuthorMetadataId).ToList();
            var authorForeignBookIds = authorBooks.Select(x => x.ForeignBookId).Where(x => x.IsNotNullOrWhiteSpace()).ToHashSet();
            var authorBookTitles = authorBooks.Select(x => NormalizeIdentityText(x.Title)).Where(x => x.IsNotNullOrWhiteSpace()).ToHashSet();
            var authorNameVariants = GetAuthorNameVariants(author).ToHashSet();

            return allAuthors
                .Where(x => x.Id != authorId && !linkedAuthorIds.Contains(x.Id))
                .Select(candidate => BuildSuggestion(candidate, authorNameVariants, authorForeignBookIds, authorBookTitles, allBooks))
                .Where(x => x != null)
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.AuthorName)
                .Take(8)
                .ToList();
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

        private static AuthorIdentityLinkSuggestion BuildSuggestion(Author candidate, HashSet<string> authorNameVariants, HashSet<string> authorForeignBookIds, HashSet<string> authorBookTitles, List<Book> allBooks)
        {
            var reasons = new List<AuthorIdentityLinkSuggestionReason>();
            var candidateNameVariants = GetAuthorNameVariants(candidate).ToHashSet();
            var sharedNameVariant = authorNameVariants.Intersect(candidateNameVariants).FirstOrDefault();

            if (sharedNameVariant.IsNotNullOrWhiteSpace())
            {
                reasons.Add(new AuthorIdentityLinkSuggestionReason
                {
                    Kind = "nameVariant",
                    Label = "Same normalized name",
                    Detail = "Author names, sort names, or persisted aliases normalize to the same value."
                });
            }

            var candidateBooks = allBooks.Where(x => x.AuthorMetadataId == candidate.AuthorMetadataId).ToList();
            var sharedForeignBookIds = candidateBooks
                .Where(x => x.ForeignBookId.IsNotNullOrWhiteSpace() && authorForeignBookIds.Contains(x.ForeignBookId))
                .Select(x => x.Title)
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Distinct()
                .Take(3)
                .ToList();

            if (sharedForeignBookIds.Any())
            {
                reasons.Add(new AuthorIdentityLinkSuggestionReason
                {
                    Kind = "sharedForeignBookId",
                    Label = "Same book metadata id",
                    Detail = $"Both identities have local books with the same metadata id, including {sharedForeignBookIds.ConcatToString("; ")}."
                });
            }

            var sharedTitles = candidateBooks
                .Where(x => authorBookTitles.Contains(NormalizeIdentityText(x.Title)))
                .Select(x => x.Title)
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Distinct()
                .Take(3)
                .ToList();

            if (sharedTitles.Count >= 2)
            {
                reasons.Add(new AuthorIdentityLinkSuggestionReason
                {
                    Kind = "sharedTitles",
                    Label = "Repeated title overlap",
                    Detail = $"Multiple local book titles overlap, including {sharedTitles.ConcatToString("; ")}."
                });
            }

            if (!reasons.Any())
            {
                return null;
            }

            var confidence = reasons.Any(x => x.Kind == "sharedForeignBookId") ? 95 :
                reasons.Any(x => x.Kind == "nameVariant") ? 85 :
                70;

            if (reasons.Count > 1)
            {
                confidence = Math.Min(98, confidence + 5);
            }

            return new AuthorIdentityLinkSuggestion
            {
                AuthorId = candidate.Id,
                AuthorName = candidate.Name,
                TitleSlug = candidate.Metadata.Value.TitleSlug,
                RelationshipType = "penName",
                DisplayPreference = "canonical",
                Confidence = confidence,
                ConfidenceLabel = confidence >= 90 ? "Strong local match" :
                    confidence >= 80 ? "Likely local match" :
                    "Possible local match",
                Reasons = reasons
            };
        }

        private static IEnumerable<string> GetAuthorNameVariants(Author author)
        {
            return new[]
                {
                    author.Metadata.Value.Name,
                    author.Metadata.Value.NameLastFirst,
                    author.Metadata.Value.SortName,
                    author.Metadata.Value.SortNameLastFirst
                }
                .Concat(author.Metadata.Value.Aliases ?? new List<string>())
                .Select(NormalizeIdentityText)
                .Where(x => x.IsNotNullOrWhiteSpace() && x.Length > 2);
        }

        private static string NormalizeIdentityText(string value)
        {
            return value.IsNullOrWhiteSpace() ? string.Empty : string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        }
    }
}
