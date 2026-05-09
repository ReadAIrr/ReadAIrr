using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using Readarr.Api.V1.Series;

namespace NzbDrone.Api.Test.Series
{
    [TestFixture]
    public class SeriesResourceMapperFixture
    {
        [Test]
        public void should_skip_orphaned_series_links()
        {
            var links = new List<SeriesBookLink>
            {
                new SeriesBookLink
                {
                    Id = 1,
                    BookId = 10,
                    SeriesId = 2,
                    SeriesPosition = 1,
                    Position = "1",
                    Book = null
                }
            };

            var resource = new NzbDrone.Core.Books.Series
            {
                Id = 2,
                Title = "Broken Link Series"
            }.ToResource(links, new List<BookFile>());

            resource.Books.Should().BeEmpty();
            resource.Completeness.TotalBooks.Should().Be(0);
        }

        [Test]
        public void should_map_valid_series_links_after_orphaned_links()
        {
            var author = new Author
            {
                Id = 3,
                Monitored = true,
                Metadata = new AuthorMetadata
                {
                    Name = "Alice Writer",
                    TitleSlug = "alice-writer"
                }
            };

            var book = new Book
            {
                Id = 10,
                Title = "Book One",
                TitleSlug = "book-one",
                Monitored = true,
                Author = author,
                Editions = new List<Edition>
                {
                    new Edition { Id = 20 }
                }
            };

            var links = new List<SeriesBookLink>
            {
                new SeriesBookLink
                {
                    Id = 1,
                    BookId = 9,
                    SeriesId = 2,
                    SeriesPosition = 1,
                    Position = "0",
                    Book = null
                },
                new SeriesBookLink
                {
                    Id = 2,
                    BookId = book.Id,
                    SeriesId = 2,
                    SeriesPosition = 2,
                    Position = "1",
                    Book = book
                }
            };

            var resource = new NzbDrone.Core.Books.Series
            {
                Id = 2,
                Title = "Mixed Link Series"
            }.ToResource(links, new List<BookFile>
            {
                new BookFile { EditionId = 20 }
            });

            resource.Books.Should().ContainSingle();
            resource.Books[0].Title.Should().Be("Book One");
            resource.Completeness.TotalBooks.Should().Be(1);
            resource.Completeness.AvailableBooks.Should().Be(1);
        }
    }
}
