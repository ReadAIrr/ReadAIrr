using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Parser;
using NzbDrone.SignalR;
using Readarr.Api.V1.Author;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Books
{
    public abstract class BookControllerWithSignalR : RestControllerWithSignalR<BookResource, Book>
    {
        protected readonly IBookService _bookService;
        protected readonly ISeriesBookLinkService _seriesBookLinkService;
        protected readonly IAuthorStatisticsService _authorStatisticsService;
        protected readonly IUpgradableSpecification _qualityUpgradableSpecification;
        protected readonly IMapCoversToLocal _coverMapper;

        protected BookControllerWithSignalR(IBookService bookService,
                                        ISeriesBookLinkService seriesBookLinkService,
                                        IAuthorStatisticsService authorStatisticsService,
                                        IMapCoversToLocal coverMapper,
                                        IUpgradableSpecification qualityUpgradableSpecification,
                                        IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _bookService = bookService;
            _seriesBookLinkService = seriesBookLinkService;
            _authorStatisticsService = authorStatisticsService;
            _coverMapper = coverMapper;
            _qualityUpgradableSpecification = qualityUpgradableSpecification;
        }

        protected override BookResource GetResourceById(int id)
        {
            var book = _bookService.GetBook(id);
            var resource = MapToResource(book, true);
            return resource;
        }

        protected override BookResource GetResourceByIdForBroadcast(int id)
        {
            var book = _bookService.GetBook(id);
            var resource = MapToResource(book, false);
            return resource;
        }

        protected BookResource MapToResource(Book book, bool includeAuthor)
        {
            var resource = book.ToResource();

            if (includeAuthor)
            {
                var author = book.Author.Value;

                resource.Author = author.ToResource();
            }

            FetchAndLinkBookStatistics(resource);
            LinkMissingReason(book, resource);
            MapCoversToLocal(resource);

            return resource;
        }

        protected List<BookResource> MapToResource(List<Book> books, bool includeAuthor)
        {
            return MapToResource(books, includeAuthor, false);
        }

        protected List<BookResource> MapToResource(List<Book> books, bool includeAuthor, bool pageScopedStatistics)
        {
            var seriesLinks = _seriesBookLinkService.GetLinksByBook(books.Select(x => x.Id).ToList())
                .GroupBy(x => x.BookId)
                .ToDictionary(x => x.Key, y => y.ToList());

            foreach (var book in books)
            {
                if (seriesLinks.TryGetValue(book.Id, out var links))
                {
                    book.SeriesLinks = links;
                }
                else
                {
                    book.SeriesLinks = new List<SeriesBookLink>();
                }
            }

            var result = books.ToResource();

            if (includeAuthor)
            {
                var authorDict = new Dictionary<int, NzbDrone.Core.Books.Author>();
                for (var i = 0; i < books.Count; i++)
                {
                    var book = books[i];
                    var resource = result[i];
                    var author = authorDict.GetValueOrDefault(books[i].AuthorMetadataId) ?? book.Author?.Value;
                    authorDict[author.AuthorMetadataId] = author;

                    resource.Author = author.ToResource();
                }
            }

            var authorStats = pageScopedStatistics ?
                _authorStatisticsService.AuthorStatistics(result.Select(x => x.AuthorId).Distinct()) :
                _authorStatisticsService.AuthorStatistics();

            LinkAuthorStatistics(result, authorStats);
            LinkMissingReasons(books, result);
            MapCoversToLocal(result.ToArray());

            return result;
        }

        protected static void AddBookSearchFilter(PagingSpec<Book> pagingSpec, string term)
        {
            if (term.IsNullOrWhiteSpace())
            {
                return;
            }

            term = term.Trim();
            var cleanTerm = Parser.CleanAuthorName(term);

            if (cleanTerm.IsNullOrWhiteSpace())
            {
                cleanTerm = term;
            }

            pagingSpec.FilterExpressions.Add(v =>
                v.Title.Contains(term) ||
                v.CleanTitle.Contains(cleanTerm) ||
                v.ForeignBookId.Contains(term) ||
                v.AuthorMetadata.Value.Name.Contains(term) ||
                v.AuthorMetadata.Value.SortName.Contains(term) ||
                v.AuthorMetadata.Value.SortNameLastFirst.Contains(term));
        }

        private void FetchAndLinkBookStatistics(BookResource resource)
        {
            LinkAuthorStatistics(resource, _authorStatisticsService.AuthorStatistics(resource.AuthorId));
        }

        private void LinkAuthorStatistics(List<BookResource> resources, List<AuthorStatistics> authorStatistics)
        {
            var bookStatsDict = authorStatistics.SelectMany(x => x.BookStatistics).ToDictionary(x => x.BookId);

            foreach (var book in resources)
            {
                if (bookStatsDict.TryGetValue(book.Id, out var stats))
                {
                    book.Statistics = stats.ToResource();
                }
            }
        }

        private void LinkAuthorStatistics(BookResource resource, AuthorStatistics authorStatistics)
        {
            if (authorStatistics?.BookStatistics != null)
            {
                var dictBookStats = authorStatistics.BookStatistics.ToDictionary(v => v.BookId);

                resource.Statistics = dictBookStats.GetValueOrDefault(resource.Id).ToResource();
            }
        }

        private void LinkMissingReasons(List<Book> books, List<BookResource> resources)
        {
            for (var i = 0; i < books.Count; i++)
            {
                LinkMissingReason(books[i], resources[i]);
            }
        }

        private void LinkMissingReason(Book book, BookResource resource)
        {
            resource.MissingReason = BookMissingReason.Get(book, resource.Statistics?.BookFileCount > 0);
        }

        private void MapCoversToLocal(params BookResource[] books)
        {
            foreach (var bookResource in books)
            {
                _coverMapper.ConvertToLocalUrls(bookResource.Id, MediaCoverEntity.Book, bookResource.Images);
            }
        }
    }
}
