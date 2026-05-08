using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using Readarr.Api.V1.Books;

namespace NzbDrone.Api.Test.Books
{
    [TestFixture]
    public class BookSearchFilterFixture
    {
        [Test]
        public void should_not_add_filter_for_empty_term()
        {
            var spec = SearchFilterAccessor.CreateSpec(" ");

            spec.FilterExpressions.Should().BeEmpty();
        }

        [TestCase("Messiah")]
        [TestCase("dunemessiah")]
        [TestCase("book-123")]
        [TestCase("Frank")]
        [TestCase("Herbert")]
        public void should_match_book_or_author_fields(string term)
        {
            var spec = SearchFilterAccessor.CreateSpec(term);
            var filter = spec.FilterExpressions.Should().ContainSingle().Subject.Compile();

            filter(BuildBook()).Should().BeTrue();
        }

        [Test]
        public void should_not_match_unrelated_term()
        {
            var spec = SearchFilterAccessor.CreateSpec("Foundation");
            var filter = spec.FilterExpressions.Should().ContainSingle().Subject.Compile();

            filter(BuildBook()).Should().BeFalse();
        }

        private static Book BuildBook()
        {
            return new Book
            {
                Title = "Dune Messiah",
                CleanTitle = "dunemessiah",
                ForeignBookId = "book-123",
                ForeignEditionId = "edition-456",
                AuthorMetadata = new AuthorMetadata
                {
                    Name = "Frank Herbert",
                    SortName = "Herbert",
                    SortNameLastFirst = "Herbert, Frank"
                }
            };
        }

        private sealed class SearchFilterAccessor : BookControllerWithSignalR
        {
            private SearchFilterAccessor()
                : base(null, null, null, null, null, null)
            {
            }

            public static PagingSpec<Book> CreateSpec(string term)
            {
                var spec = new PagingSpec<Book>();

                AddBookSearchFilter(spec, term);

                return spec;
            }
        }
    }
}
