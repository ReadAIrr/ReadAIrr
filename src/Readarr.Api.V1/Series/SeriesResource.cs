using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Series
{
    public class SeriesResource : RestResource
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<SeriesBookLinkResource> Links { get; set; }
        public List<SeriesBookResource> Books { get; set; }
        public SeriesCompletenessResource Completeness { get; set; }
    }

    public static class SeriesResourceMapper
    {
        public static SeriesResource ToResource(this NzbDrone.Core.Books.Series model, List<SeriesBookLink> links = null, List<BookFile> files = null)
        {
            if (model == null)
            {
                return null;
            }

            links ??= model.LinkItems.Value;
            files ??= new List<BookFile>();

            return new SeriesResource
            {
                Id = model.Id,
                Title = model.Title,
                Description = model.Description,
                Links = links.ToResource(),
                Books = links.ToBookResource(files),
                Completeness = links.ToCompletenessResource(files)
            };
        }

        public static List<SeriesResource> ToResource(this IEnumerable<NzbDrone.Core.Books.Series> models)
        {
            return models?.Select(x => x.ToResource()).ToList();
        }

        public static List<SeriesResource> ToResource(this IEnumerable<NzbDrone.Core.Books.Series> models, Dictionary<int, List<SeriesBookLink>> links, List<BookFile> files)
        {
            return models?.Select(x => ToResource(x, links.GetValueOrDefault(x.Id) ?? new List<SeriesBookLink>(), files)).ToList();
        }
    }

    public class SeriesBookResource : RestResource
    {
        public string Title { get; set; }
        public string TitleSlug { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorTitleSlug { get; set; }
        public string Position { get; set; }
        public int SeriesPosition { get; set; }
        public bool Monitored { get; set; }
        public bool AuthorMonitored { get; set; }
        public bool HasFile { get; set; }
        public string MissingReason { get; set; }
    }

    public class SeriesCompletenessResource
    {
        public int TotalBooks { get; set; }
        public int AvailableBooks { get; set; }
        public int MissingBooks { get; set; }
        public int UnmonitoredBooks { get; set; }
        public int UnmonitoredAuthors { get; set; }
    }

    public static class SeriesBookResourceMapper
    {
        public static List<SeriesBookResource> ToBookResource(this IEnumerable<SeriesBookLink> links, List<BookFile> files)
        {
            var fileEditionIds = files.Select(x => x.EditionId).ToHashSet();

            return links
                .OrderBy(x => x.SeriesPosition)
                .ThenBy(x => x.Position)
                .Select(x => ToBookResource(x, fileEditionIds))
                .ToList();
        }

        public static SeriesCompletenessResource ToCompletenessResource(this IEnumerable<SeriesBookLink> links, List<BookFile> files)
        {
            var books = links.ToBookResource(files);

            return new SeriesCompletenessResource
            {
                TotalBooks = books.Count,
                AvailableBooks = books.Count(x => x.HasFile),
                MissingBooks = books.Count(x => !x.HasFile),
                UnmonitoredBooks = books.Count(x => !x.Monitored),
                UnmonitoredAuthors = books.Count(x => !x.AuthorMonitored)
            };
        }

        private static SeriesBookResource ToBookResource(SeriesBookLink link, HashSet<int> fileEditionIds)
        {
            var book = link.Book.Value;
            var author = book.Author.Value;
            var editions = book.Editions.Value;
            var hasFile = editions.Any(x => fileEditionIds.Contains(x.Id));

            return new SeriesBookResource
            {
                Id = book.Id,
                Title = book.Title,
                TitleSlug = book.TitleSlug,
                AuthorId = author.Id,
                AuthorName = author.Name,
                AuthorTitleSlug = author.Metadata.Value.TitleSlug,
                Position = link.Position,
                SeriesPosition = link.SeriesPosition,
                Monitored = book.Monitored,
                AuthorMonitored = author.Monitored,
                HasFile = hasFile,
                MissingReason = GetMissingReason(book, author, hasFile)
            };
        }

        private static string GetMissingReason(Book book, NzbDrone.Core.Books.Author author, bool hasFile)
        {
            if (hasFile)
            {
                return null;
            }

            if (!author.Monitored)
            {
                return "Missing because the author is not monitored.";
            }

            if (!book.Monitored)
            {
                return "Missing because the book is not monitored.";
            }

            if (book.Editions?.Value != null && !book.AnyEditionOk && !book.Editions.Value.Any(x => x.Monitored))
            {
                return "Missing because no edition is monitored.";
            }

            return "Missing because no imported file exists for the monitored book or edition.";
        }
    }
}
