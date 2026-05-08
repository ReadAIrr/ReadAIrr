using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Http;

namespace Readarr.Api.V1.Books
{
    [V1ApiController]
    public class BookController : BookControllerWithSignalR,
        IHandle<BookGrabbedEvent>,
        IHandle<BookEditedEvent>,
        IHandle<BookUpdatedEvent>,
        IHandle<BookDeletedEvent>,
        IHandle<BookImportedEvent>,
        IHandle<TrackImportedEvent>,
        IHandle<BookFileDeletedEvent>
    {
        protected readonly IAuthorService _authorService;
        protected readonly IEditionService _editionService;
        protected readonly IAddBookService _addBookService;
        protected readonly IMediaFileService _mediaFileService;

        public BookController(IAuthorService authorService,
                          IBookService bookService,
                          IAddBookService addBookService,
                          IEditionService editionService,
                          IMediaFileService mediaFileService,
                          ISeriesBookLinkService seriesBookLinkService,
                          IAuthorStatisticsService authorStatisticsService,
                          IMapCoversToLocal coverMapper,
                          IUpgradableSpecification upgradableSpecification,
                          IBroadcastSignalRMessage signalRBroadcaster,
                          QualityProfileExistsValidator qualityProfileExistsValidator,
                          MetadataProfileExistsValidator metadataProfileExistsValidator)

        : base(bookService, seriesBookLinkService, authorStatisticsService, coverMapper, upgradableSpecification, signalRBroadcaster)
        {
            _authorService = authorService;
            _editionService = editionService;
            _addBookService = addBookService;
            _mediaFileService = mediaFileService;

            PostValidator.RuleFor(s => s.ForeignBookId).NotEmpty();
            PostValidator.RuleFor(s => s.Author.QualityProfileId).SetValidator(qualityProfileExistsValidator);
            PostValidator.RuleFor(s => s.Author.MetadataProfileId).SetValidator(metadataProfileExistsValidator);
            PostValidator.RuleFor(s => s.Author.RootFolderPath).IsValidPath().When(s => s.Author.Path.IsNullOrWhiteSpace());
            PostValidator.RuleFor(s => s.Author.ForeignAuthorId).NotEmpty();
        }

        [HttpGet]
        public List<BookResource> GetBooks([FromQuery]int? authorId,
            [FromQuery]List<int> bookIds,
            [FromQuery]string titleSlug,
            [FromQuery]bool includeAllAuthorBooks = false)
        {
            if (!authorId.HasValue && !bookIds.Any() && titleSlug.IsNullOrWhiteSpace())
            {
                var editionTask = Task.Run(() => _editionService.GetAllMonitoredEditions());
                var metadataTask = Task.Run(() => _authorService.GetAllAuthors());
                var books = _bookService.GetAllBooks();

                var editions = editionTask.GetAwaiter().GetResult().GroupBy(x => x.BookId).ToDictionary(x => x.Key, y => y.ToList());

                var authors = metadataTask.GetAwaiter().GetResult().ToDictionary(x => x.AuthorMetadataId);

                foreach (var book in books)
                {
                    book.Author = authors[book.AuthorMetadataId];
                    if (editions.TryGetValue(book.Id, out var bookEditions))
                    {
                        book.Editions = bookEditions;
                    }
                    else
                    {
                        book.Editions = new List<Edition>();
                    }
                }

                return MapToResource(books, false);
            }

            if (authorId.HasValue)
            {
                return GetBooksByAuthor(authorId.Value);
            }

            if (titleSlug.IsNotNullOrWhiteSpace())
            {
                var book = _bookService.FindBySlug(titleSlug);

                if (book == null)
                {
                    return MapToResource(new List<Book>(), false);
                }

                if (includeAllAuthorBooks)
                {
                    return GetBooksByAuthor(book.AuthorId);
                }
                else
                {
                    return MapToResource(new List<Book> { book }, false);
                }
            }

            return MapToResource(_bookService.GetBooks(bookIds), false);
        }

        private List<BookResource> GetBooksByAuthor(int authorId)
        {
            var books = _bookService.GetBooksByAuthor(authorId);

            var author = _authorService.GetAuthor(authorId);
            var editions = _editionService.GetEditionsByAuthor(authorId)
                .GroupBy(x => x.BookId)
                .ToDictionary(x => x.Key, y => y.ToList());

            foreach (var book in books)
            {
                book.Author = author;
                if (editions.TryGetValue(book.Id, out var bookEditions))
                {
                    book.Editions = bookEditions;
                }
                else
                {
                    book.Editions = new List<Edition>();
                }
            }

            return MapToResource(books, false);
        }

        [HttpGet("paged")]
        public PagingResource<BookResource> GetBooksPaged([FromQuery] PagingRequestResource paging, [FromQuery] bool? monitored, [FromQuery] string term)
        {
            var pagingResource = new PagingResource<BookResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<BookResource, Book>("title", SortDirection.Ascending);

            if (monitored.HasValue)
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == monitored.Value);
            }

            AddBookSearchFilter(pagingSpec, term);

            var page = _bookService.Paged(pagingSpec);

            HydrateBooksForList(page.Records);

            return new PagingResource<BookResource>
            {
                Page = page.Page,
                PageSize = page.PageSize,
                SortKey = page.SortKey,
                SortDirection = page.SortDirection,
                TotalRecords = page.TotalRecords,
                Records = MapToResource(page.Records, false, true)
            };
        }

        private void HydrateBooksForList(List<Book> books)
        {
            if (!books.Any())
            {
                return;
            }

            var authors = _authorService.GetAuthorsByMetadataId(books.Select(x => x.AuthorMetadataId).Distinct())
                .ToDictionary(x => x.AuthorMetadataId);

            var editions = _editionService.GetEditionsByBook(books.Select(x => x.Id))
                .Where(x => x.Monitored)
                .GroupBy(x => x.BookId)
                .ToDictionary(x => x.Key, y => y.ToList());

            foreach (var book in books)
            {
                if (authors.TryGetValue(book.AuthorMetadataId, out var author))
                {
                    book.Author = author;
                }

                book.Editions = editions.TryGetValue(book.Id, out var bookEditions) ?
                    bookEditions :
                    new List<Edition>();
            }
        }

        [HttpGet("{id:int}/overview")]
        public object Overview(int id)
        {
            var overview = _editionService.GetEditionsByBook(id).Single(x => x.Monitored).Overview;
            return new
            {
                id,
                overview
            };
        }

        [RestPostById]
        public ActionResult<BookResource> AddBook([FromBody] BookResource bookResource)
        {
            var book = _addBookService.AddBook(bookResource.ToModel());

            return Created(book.Id);
        }

        [RestPutById]
        public ActionResult<BookResource> UpdateBook([FromBody] BookResource bookResource)
        {
            var book = _bookService.GetBook(bookResource.Id);
            var oldMonitoredEdition = book.Editions.Value.SingleOrDefault(x => x.Monitored);

            var model = bookResource.ToModel(book);
            var newMonitoredEdition = model.Editions.Value.SingleOrDefault(x => x.Monitored);

            _bookService.UpdateBook(model);
            _editionService.UpdateMany(model.Editions.Value);
            UpdateFileEditionLinks(oldMonitoredEdition, newMonitoredEdition);

            BroadcastResourceChange(ModelAction.Updated, model.Id);

            return Accepted(model.Id);
        }

        private void UpdateFileEditionLinks(Edition oldMonitoredEdition, Edition newMonitoredEdition)
        {
            if (oldMonitoredEdition == null ||
                newMonitoredEdition == null ||
                oldMonitoredEdition.Id == newMonitoredEdition.Id)
            {
                return;
            }

            var files = _mediaFileService.GetFilesByEdition(oldMonitoredEdition.Id);

            if (!files.Any())
            {
                return;
            }

            foreach (var file in files)
            {
                file.EditionId = newMonitoredEdition.Id;
            }

            _mediaFileService.Update(files);
        }

        [RestDeleteById]
        public void DeleteBook(int id, bool deleteFiles = false, bool addImportListExclusion = false)
        {
            _bookService.DeleteBook(id, deleteFiles, addImportListExclusion);
        }

        [HttpPut("monitor")]
        public IActionResult SetBooksMonitored([FromBody]BooksMonitoredResource resource)
        {
            _bookService.SetMonitored(resource.BookIds, resource.Monitored);

            if (resource.BookIds.Count == 1)
            {
                _bookService.SetBookMonitored(resource.BookIds.First(), resource.Monitored);
            }
            else
            {
                _bookService.SetMonitored(resource.BookIds, resource.Monitored);
            }

            return Accepted(MapToResource(_bookService.GetBooks(resource.BookIds), false));
        }

        [NonAction]
        public void Handle(BookGrabbedEvent message)
        {
            foreach (var book in message.Book.Books)
            {
                var resource = book.ToResource();
                resource.Grabbed = true;

                BroadcastResourceChange(ModelAction.Updated, resource);
            }
        }

        [NonAction]
        public void Handle(BookEditedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, MapToResource(message.Book, true));
        }

        [NonAction]
        public void Handle(BookUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, MapToResource(message.Book, true));
        }

        [NonAction]
        public void Handle(BookDeletedEvent message)
        {
            BroadcastResourceChange(ModelAction.Deleted, message.Book.ToResource());
        }

        [NonAction]
        public void Handle(BookImportedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, MapToResource(message.Book, true));
        }

        [NonAction]
        public void Handle(TrackImportedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, message.BookInfo.Book.ToResource());
        }

        [NonAction]
        public void Handle(BookFileDeletedEvent message)
        {
            if (message.Reason == DeleteMediaFileReason.Upgrade)
            {
                return;
            }

            BroadcastResourceChange(ModelAction.Updated, MapToResource(message.BookFile.Edition.Value.Book.Value, true));
        }
    }
}
