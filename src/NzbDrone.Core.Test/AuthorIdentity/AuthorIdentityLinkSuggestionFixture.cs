using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.AuthorIdentity
{
    [TestFixture]
    public class AuthorIdentityLinkSuggestionFixture : CoreTest<AuthorIdentityLinkService>
    {
        [Test]
        public void should_suggest_author_with_matching_persisted_alias()
        {
            var author = GivenAuthor(1, 10, "Alice Writer", "alice", new List<string> { "A. Writer" });
            var candidate = GivenAuthor(2, 20, "A Writer", "awriter");
            var unrelated = GivenAuthor(3, 30, "Someone Else", "someoneelse");

            GivenAuthors(author, candidate, unrelated);
            GivenBooks();
            Mocker.GetMock<IAuthorIdentityLinkRepository>()
                .Setup(x => x.All())
                .Returns(new List<AuthorIdentityLink>().AsQueryable());

            var suggestions = Subject.GetSuggestions(author.Id);

            suggestions.Should().ContainSingle();
            suggestions[0].AuthorId.Should().Be(candidate.Id);
            suggestions[0].ConfidenceLabel.Should().Be("Likely local match");
            suggestions[0].Reasons.Should().Contain(x => x.Kind == "nameVariant");
        }

        [Test]
        public void should_suggest_author_with_shared_foreign_book_id()
        {
            var author = GivenAuthor(1, 10, "Alice Writer", "alice");
            var candidate = GivenAuthor(2, 20, "A. Pen", "apen");

            GivenAuthors(author, candidate);
            GivenBooks(
                new Book { AuthorMetadataId = 10, ForeignBookId = "book-1", Title = "Shared Book" },
                new Book { AuthorMetadataId = 20, ForeignBookId = "book-1", Title = "Shared Book" });
            Mocker.GetMock<IAuthorIdentityLinkRepository>()
                .Setup(x => x.All())
                .Returns(new List<AuthorIdentityLink>().AsQueryable());

            var suggestions = Subject.GetSuggestions(author.Id);

            suggestions.Should().ContainSingle();
            suggestions[0].AuthorId.Should().Be(candidate.Id);
            suggestions[0].ConfidenceLabel.Should().Be("Strong local match");
            suggestions[0].Reasons.Should().Contain(x => x.Kind == "sharedForeignBookId");
        }

        [Test]
        public void should_not_suggest_already_linked_author()
        {
            var author = GivenAuthor(1, 10, "Alice Writer", "alice", new List<string> { "A. Writer" });
            var candidate = GivenAuthor(2, 20, "A Writer", "awriter");

            GivenAuthors(author, candidate);
            GivenBooks();
            Mocker.GetMock<IAuthorIdentityLinkRepository>()
                .Setup(x => x.All())
                .Returns(new List<AuthorIdentityLink>
                {
                    new AuthorIdentityLink { Id = 5, CanonicalAuthorId = 1, AliasAuthorId = 2 }
                }.AsQueryable());

            Subject.GetSuggestions(author.Id).Should().BeEmpty();
        }

        private void GivenAuthors(params Author[] authors)
        {
            Mocker.GetMock<IAuthorService>()
                .Setup(x => x.GetAuthor(It.IsAny<int>()))
                .Returns<int>(id => authors.Single(x => x.Id == id));

            Mocker.GetMock<IAuthorService>()
                .Setup(x => x.GetAllAuthors())
                .Returns(new List<Author>(authors));
        }

        private void GivenBooks(params Book[] books)
        {
            Mocker.GetMock<IBookService>()
                .Setup(x => x.GetAllBooks())
                .Returns(new List<Book>(books));
        }

        private static Author GivenAuthor(int id, int metadataId, string name, string titleSlug, List<string> aliases = null)
        {
            return new Author
            {
                Id = id,
                AuthorMetadataId = metadataId,
                Metadata = new AuthorMetadata
                {
                    Id = metadataId,
                    Name = name,
                    NameLastFirst = name,
                    SortName = name,
                    SortNameLastFirst = name,
                    TitleSlug = titleSlug,
                    Aliases = aliases ?? new List<string>()
                }
            };
        }
    }
}
