using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Calibre;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Profiles.Metadata
{
    public interface IMetadataProfileService
    {
        MetadataProfile Add(MetadataProfile profile);
        void Update(MetadataProfile profile);
        void Delete(int id);
        List<MetadataProfile> All();
        MetadataProfile Get(int id);
        bool Exists(int id);
        MetadataProfilePreview Preview(MetadataProfile profile);
        string ExplainBookExclusion(Book book, int profileId);
        List<Book> FilterBooks(Author input, int profileId);
    }

    public class MetadataProfileService : IMetadataProfileService, IHandle<ApplicationStartedEvent>
    {
        public const string NONE_PROFILE_NAME = "None";
        public const double NONE_PROFILE_MIN_POPULARITY = 1e10;
        private const int PreviewExampleLimit = 5;

        private static readonly Regex PartOrSetRegex = new Regex(@"(?<from>\d+) of (?<to>\d+)|(?<from>\d+)\s?/\s?(?<to>\d+)|(?<from>\d+)\s?-\s?(?<to>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] AudioTerms = { "audiobook", "audio book", "audio cd", "audible", "mp3", "m4b", "unabridged", "abridged" };
        private static readonly string[] ReadableTerms = { "ebook", "e-book", "epub", "mobi", "kindle", "pdf", "azw", "digital" };
        private static readonly string[] AnthologyTerms = { "anthology" };
        private static readonly string[] CollectionTerms = { "omnibus", "collection", "collected", "box set", "complete works" };
        private static readonly string[] SerializedTerms = { "serialized", "serial", "part", "episode" };
        private static readonly string[] EssayTerms = { "essay", "essays" };
        private static readonly string[] ShortStoryTerms = { "short story", "short stories" };

        private readonly IMetadataProfileRepository _profileRepository;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IImportListFactory _importListFactory;
        private readonly IRootFolderService _rootFolderService;
        private readonly ITermMatcherService _termMatcherService;
        private readonly Logger _logger;

        public MetadataProfileService(IMetadataProfileRepository profileRepository,
                                      IAuthorService authorService,
                                      IBookService bookService,
                                      IEditionService editionService,
                                      IMediaFileService mediaFileService,
                                      IImportListFactory importListFactory,
                                      IRootFolderService rootFolderService,
                                      ITermMatcherService termMatcherService,
                                      Logger logger)
        {
            _profileRepository = profileRepository;
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _mediaFileService = mediaFileService;
            _importListFactory = importListFactory;
            _rootFolderService = rootFolderService;
            _termMatcherService = termMatcherService;
            _logger = logger;
        }

        public MetadataProfile Add(MetadataProfile profile)
        {
            return _profileRepository.Insert(profile);
        }

        public void Update(MetadataProfile profile)
        {
            if (profile.Name == NONE_PROFILE_NAME)
            {
                throw new InvalidOperationException("Not permitted to alter None metadata profile");
            }

            _profileRepository.Update(profile);
        }

        public void Delete(int id)
        {
            var profile = _profileRepository.Get(id);

            if (profile.Name == NONE_PROFILE_NAME ||
                _authorService.GetAllAuthors().Any(c => c.MetadataProfileId == id) ||
                _importListFactory.All().Any(c => c.MetadataProfileId == id) ||
                _rootFolderService.All().Any(c => c.DefaultMetadataProfileId == id))
            {
                throw new MetadataProfileInUseException(profile.Name);
            }

            _profileRepository.Delete(id);
        }

        public List<MetadataProfile> All()
        {
            return _profileRepository.All().ToList();
        }

        public MetadataProfile Get(int id)
        {
            return _profileRepository.Get(id);
        }

        public bool Exists(int id)
        {
            return _profileRepository.Exists(id);
        }

        public MetadataProfilePreview Preview(MetadataProfile profile)
        {
            profile.Ignored = profile.Ignored ?? new List<string>();

            var books = _bookService.GetAllBooks();
            var editions = books.Any() ? _editionService.GetEditionsByBook(books.Select(x => x.Id)) : new List<Edition>();
            var editionsByBook = editions.GroupBy(x => x.BookId).ToDictionary(x => x.Key, x => x.ToList());
            var titles = new HashSet<string>(books.Select(x => x.Title));

            foreach (var book in books)
            {
                if (editionsByBook.TryGetValue(book.Id, out var bookEditions))
                {
                    book.Editions = bookEditions;
                }
                else
                {
                    book.Editions = new List<Edition>();
                }
            }

            var preview = new MetadataProfilePreview
            {
                EvaluatedBooks = books.Count,
                EvaluatedEditions = editions.Count
            };

            AddBookPreviewRule(preview, "minPopularity", "Minimum popularity", "Books below the configured popularity threshold.", books, x => BookAllowedByRating(x, profile) ? null : $"Popularity is below {profile.MinPopularity}");
            AddBookPreviewRule(preview, "skipMissingDate", "Missing release date", "Books without a release date.", books, x => !profile.SkipMissingDate || x.ReleaseDate.HasValue ? null : "Release date is missing");
            AddBookPreviewRule(preview, "skipPartsAndSets", "Parts and sets", "Books that look like multi-work parts or sets.", books, x => !profile.SkipPartsAndSets || !IsPartOrSet(x, GetSeriesLinks(x), titles) ? null : "Looks like a part or set");
            AddBookPreviewRule(preview, "skipSeriesSecondary", "Secondary series", "Books attached only as secondary series entries.", books, x => GetSecondarySeriesReason(x, profile));
            AddBookPreviewRule(preview, "ignored", "Ignored terms", "Book titles matching configured ignored terms.", books, x => GetIgnoredTermReason(x.Title, profile.Ignored));
            AddBookPreviewRule(preview, "skipAnthologies", "Anthologies", "Books with anthology metadata or title terms.", books, x => GetWorkStructureReason(x, profile, AnthologyTerms, profile.SkipAnthologies, "anthology", null, titles));
            AddBookPreviewRule(preview, "skipCollections", "Collections and boxed sets", "Books with collection, omnibus, or box set metadata or title terms.", books, x => GetWorkStructureReason(x, profile, CollectionTerms, profile.SkipCollections, "collection or box set", null, titles));
            AddBookPreviewRule(preview, "skipSerializedParts", "Serialized parts", "Books that look like serialized parts or partial works.", books, x => GetWorkStructureReason(x, profile, SerializedTerms, profile.SkipSerializedParts, "serialized part", GetSeriesLinks(x), titles));
            AddBookPreviewRule(preview, "skipEssays", "Essays", "Books with essay metadata or title terms.", books, x => GetWorkStructureReason(x, profile, EssayTerms, profile.SkipEssays, "essay", null, titles));
            AddBookPreviewRule(preview, "skipShortStories", "Short stories", "Books with short story metadata or title terms.", books, x => GetWorkStructureReason(x, profile, ShortStoryTerms, profile.SkipShortStories, "short story", null, titles));
            AddBookPreviewRule(preview, "minPages", "Minimum pages", "Books where no known edition satisfies the minimum page count.", books, x => GetMinPagesReason(x, profile));

            AddEditionPreviewRule(preview, "requireReadable", "Readable/e-reader compatible", "Editions not marked as e-reader compatible by format metadata.", editions, x => !profile.RequireReadable || IsReadableEdition(x) ? null : "Edition is not marked readable/e-reader compatible");
            AddEditionPreviewRule(preview, "requireAudio", "Audio-compatible", "Editions not marked as audiobook/audio-compatible by format metadata.", editions, x => !profile.RequireAudio || IsAudioEdition(x) ? null : "Edition is not marked audio-compatible");
            AddEditionPreviewRule(preview, "allowedLanguages", "Language filters", "Editions outside the configured language allow-list.", editions, x => GetAllowedLanguageReason(x, profile));
            AddEditionPreviewRule(preview, "skipMissingIsbn", "Missing ISBN/ASIN", "Editions without ISBN13 or ASIN metadata.", editions, x => !profile.SkipMissingIsbn || x.Isbn13.IsNotNullOrWhiteSpace() || x.Asin.IsNotNullOrWhiteSpace() ? null : "ISBN13 and ASIN are missing");
            AddEditionPreviewRule(preview, "ignoredEdition", "Ignored edition terms", "Edition titles matching configured ignored terms.", editions, x => GetIgnoredTermReason(x.Title, profile.Ignored));

            return preview;
        }

        public string ExplainBookExclusion(Book book, int profileId)
        {
            var profile = Get(profileId);
            var titles = new HashSet<string>(new[] { book.Title }.Concat(book.Editions?.Value?.Select(x => x.Title) ?? Enumerable.Empty<string>()));
            var bookReason = GetBookExclusionReason(book, profile, GetSeriesLinks(book), titles);

            if (bookReason.IsNotNullOrWhiteSpace())
            {
                return bookReason;
            }

            var editions = book.Editions?.Value ?? new List<Edition>();
            var editionReasons = editions
                .Select(x => GetEditionExclusionReason(x, profile))
                .Where(x => x.IsNotNullOrWhiteSpace())
                .Distinct()
                .ToList();

            if (editions.Any() && editionReasons.Count == editions.Count)
            {
                return $"All editions excluded: {editionReasons.First()}";
            }

            return GetMinPagesReason(book, profile);
        }

        public List<Book> FilterBooks(Author input, int profileId)
        {
            var seriesLinks = input.Series.Value.SelectMany(x => x.LinkItems.Value)
                .GroupBy(x => x.Book.Value)
                .ToDictionary(x => x.Key, y => y.ToList());

            var dbAuthor = _authorService.FindById(input.ForeignAuthorId);

            var localBooks = new List<Book>();
            if (dbAuthor != null)
            {
                localBooks = _bookService.GetBooksByAuthorMetadataId(dbAuthor.AuthorMetadataId);
                var editions = _editionService.GetEditionsByAuthor(dbAuthor.Id).GroupBy(x => x.BookId).ToDictionary(x => x.Key, y => y.ToList());

                foreach (var book in localBooks)
                {
                    if (editions.TryGetValue(book.Id, out var bookEditions))
                    {
                        book.Editions = bookEditions;
                    }
                    else
                    {
                        book.Editions = new List<Edition>();
                    }
                }
            }

            var localFiles = _mediaFileService.GetFilesByAuthor(dbAuthor?.Id ?? 0);

            return FilterBooks(input.Books.Value, localBooks, localFiles, seriesLinks, profileId);
        }

        private List<Book> FilterBooks(IEnumerable<Book> remoteBooks, List<Book> localBooks, List<BookFile> localFiles, Dictionary<Book, List<SeriesBookLink>> seriesLinks, int metadataProfileId)
        {
            var profile = Get(metadataProfileId);

            _logger.Trace($"Filtering:\n{remoteBooks.Select(x => x.ToString()).Join("\n")}");

            var hash = new HashSet<Book>(remoteBooks);
            var titles = new HashSet<string>(remoteBooks.Select(x => x.Title));

            var localHash = new HashSet<string>(localBooks.Where(x => x.AddOptions.AddType == BookAddType.Manual).Select(x => x.ForeignBookId));
            localHash.UnionWith(localFiles.Select(x => x.Edition.Value.Book.Value.ForeignBookId));

            FilterByPredicate(hash, x => x.ForeignBookId, localHash, profile, BookAllowedByRating, "rating criteria not met");
            FilterByPredicate(hash, x => x.ForeignBookId, localHash, profile, (x, p) => GetBookExclusionReason(x, p, seriesLinks.GetValueOrDefault(x), titles).IsNullOrWhiteSpace(), "book excluded by metadata profile");

            foreach (var book in hash)
            {
                var localEditions = localBooks.SingleOrDefault(x => x.ForeignBookId == book.ForeignBookId)?.Editions.Value ?? new List<Edition>();

                book.Editions = FilterEditions(book.Editions.Value, localEditions, localFiles, profile);
            }

            FilterByPredicate(hash, x => x.ForeignBookId, localHash, profile, (x, p) => x.Editions.Value.Any(e => e.PageCount > p.MinPages) || x.Editions.Value.All(e => e.PageCount == 0), "minimum page count not met");
            FilterByPredicate(hash, x => x.ForeignBookId, localHash, profile, (x, p) => x.Editions.Value.Any(), "all editions filtered out");

            return hash.ToList();
        }

        private List<Edition> FilterEditions(IEnumerable<Edition> editions, List<Edition> localEditions, List<BookFile> localFiles, MetadataProfile profile)
        {
            var allowedLanguages = profile.AllowedLanguages.IsNotNullOrWhiteSpace() ? new HashSet<string>(profile.AllowedLanguages.Trim(',').Split(',').Select(x => x.CanonicalizeLanguage())) : new HashSet<string>();

            var hash = new HashSet<Edition>(editions);

            var localHash = new HashSet<string>(localEditions.Where(x => x.ManualAdd).Select(x => x.ForeignEditionId));
            localHash.UnionWith(localFiles.Select(x => x.Edition.Value.ForeignEditionId));

            FilterByPredicate(hash, x => x.ForeignEditionId, localHash, profile, (x, p) => !allowedLanguages.Any() || allowedLanguages.Contains(x.Language?.CanonicalizeLanguage()), "edition language not allowed");
            FilterByPredicate(hash, x => x.ForeignEditionId, localHash, profile, (x, p) => GetEditionExclusionReason(x, p).IsNullOrWhiteSpace(), "edition excluded by metadata profile");

            return hash.ToList();
        }

        private void FilterByPredicate<T>(HashSet<T> remoteItems, Func<T, string> getId, HashSet<string> localItems, MetadataProfile profile, Func<T, MetadataProfile, bool> bookAllowed, string message)
        {
            var filtered = new HashSet<T>(remoteItems.Where(x => !bookAllowed(x, profile) && !localItems.Contains(getId(x))));
            if (filtered.Any())
            {
                _logger.Trace($"Skipping {filtered.Count} {typeof(T).Name} because {message}:\n{filtered.ConcatToString(x => x.ToString(), "\n")}");
                remoteItems.RemoveWhere(x => filtered.Contains(x));
            }
        }

        private bool BookAllowedByRating(Book b, MetadataProfile p)
        {
            // hack for the 'none' metadata profile
            if (p.MinPopularity == NONE_PROFILE_MIN_POPULARITY)
            {
                return false;
            }

            return (b.Ratings.Popularity >= p.MinPopularity) || b.ReleaseDate > DateTime.UtcNow;
        }

        private bool IsPartOrSet(Book book, List<SeriesBookLink> seriesLinks, HashSet<string> titles)
        {
            if (seriesLinks != null &&
                seriesLinks.Any(x => x.Position.IsNotNullOrWhiteSpace()) &&
                !seriesLinks.Any(s => double.TryParse(s.Position, out _)))
            {
                // No non-empty series entries parse to a number, so all like 1-3 etc.
                return true;
            }

            // Skip things of form Title1 / Title2 when Title1 and Title2 are already in the list
            var bookTitles = new[] { book.Title }.Concat(book.Editions.Value.Select(x => x.Title)).ToList();
            foreach (var title in bookTitles)
            {
                var split = title.Split('/').Select(x => x.Trim()).ToList();
                if (split.Count > 1 && split.All(x => titles.Contains(x)))
                {
                    return true;
                }
            }

            var match = PartOrSetRegex.Match(book.Title);

            if (match.Groups["from"].Success)
            {
                var from = int.Parse(match.Groups["from"].Value);
                return from <= 1800 || from > DateTime.UtcNow.Year;
            }

            return false;
        }

        private bool MatchesTerms(string value, string terms)
        {
            if (terms.IsNullOrWhiteSpace() || value.IsNullOrWhiteSpace())
            {
                return false;
            }

            var split = terms.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var foundTerms = ContainsAny(split, value);

            return foundTerms.Any();
        }

        private List<string> ContainsAny(List<string> terms, string title)
        {
            return terms.Where(t => _termMatcherService.IsMatch(t, title)).ToList();
        }

        private void AddBookPreviewRule(MetadataProfilePreview preview, string key, string label, string description, IEnumerable<Book> books, Func<Book, string> getReason)
        {
            var matches = books
                .Select(x => new
                {
                    Book = x,
                    Reason = getReason(x)
                })
                .Where(x => x.Reason.IsNotNullOrWhiteSpace())
                .ToList();

            preview.Rules.Add(new MetadataProfilePreviewRule
            {
                Key = key,
                Label = label,
                Description = description,
                Count = matches.Count,
                Examples = matches
                    .Take(PreviewExampleLimit)
                    .Select(x => new MetadataProfilePreviewExample
                    {
                        Title = x.Book.Title,
                        Detail = x.Reason
                    })
                    .ToList()
            });
        }

        private void AddEditionPreviewRule(MetadataProfilePreview preview, string key, string label, string description, IEnumerable<Edition> editions, Func<Edition, string> getReason)
        {
            var matches = editions
                .Select(x => new
                {
                    Edition = x,
                    Reason = getReason(x)
                })
                .Where(x => x.Reason.IsNotNullOrWhiteSpace())
                .ToList();

            preview.Rules.Add(new MetadataProfilePreviewRule
            {
                Key = key,
                Label = label,
                Description = description,
                Count = matches.Count,
                Examples = matches
                    .Take(PreviewExampleLimit)
                    .Select(x => new MetadataProfilePreviewExample
                    {
                        Title = x.Edition.Title,
                        Detail = x.Reason
                    })
                    .ToList()
            });
        }

        private bool ShouldSkipWorkStructure(Book book, MetadataProfile profile, List<SeriesBookLink> seriesLinks, HashSet<string> titles)
        {
            return GetWorkStructureReason(book, profile, AnthologyTerms, profile.SkipAnthologies, "anthology", null, titles).IsNotNullOrWhiteSpace() ||
                   GetWorkStructureReason(book, profile, CollectionTerms, profile.SkipCollections, "collection or box set", null, titles).IsNotNullOrWhiteSpace() ||
                   GetWorkStructureReason(book, profile, SerializedTerms, profile.SkipSerializedParts, "serialized part", seriesLinks, titles).IsNotNullOrWhiteSpace() ||
                   GetWorkStructureReason(book, profile, EssayTerms, profile.SkipEssays, "essay", null, titles).IsNotNullOrWhiteSpace() ||
                   GetWorkStructureReason(book, profile, ShortStoryTerms, profile.SkipShortStories, "short story", null, titles).IsNotNullOrWhiteSpace();
        }

        private string GetBookExclusionReason(Book book, MetadataProfile profile, List<SeriesBookLink> seriesLinks, HashSet<string> titles)
        {
            if (!BookAllowedByRating(book, profile))
            {
                return "rating criteria not met";
            }

            if (profile.SkipMissingDate && !book.ReleaseDate.HasValue)
            {
                return "release date is missing";
            }

            if (profile.SkipPartsAndSets && IsPartOrSet(book, seriesLinks, titles))
            {
                return "book is part of a set";
            }

            if (profile.SkipSeriesSecondary && seriesLinks != null && seriesLinks.Any() && !seriesLinks.Any(y => y.IsPrimary))
            {
                return "book is a secondary series item";
            }

            var ignored = GetIgnoredTermReason(book.Title, profile.Ignored);
            if (ignored.IsNotNullOrWhiteSpace())
            {
                return ignored;
            }

            if (ShouldSkipWorkStructure(book, profile, seriesLinks, titles))
            {
                return "work structure excluded by metadata profile";
            }

            return null;
        }

        private string GetEditionExclusionReason(Edition edition, MetadataProfile profile)
        {
            var allowedLanguage = GetAllowedLanguageReason(edition, profile);
            if (allowedLanguage.IsNotNullOrWhiteSpace())
            {
                return allowedLanguage;
            }

            if (profile.RequireReadable && !IsReadableEdition(edition))
            {
                return "edition is not marked readable/e-reader compatible";
            }

            if (profile.RequireAudio && !IsAudioEdition(edition))
            {
                return "edition is not marked audio-compatible";
            }

            if (profile.SkipMissingIsbn && edition.Isbn13.IsNullOrWhiteSpace() && edition.Asin.IsNullOrWhiteSpace())
            {
                return "isbn and asin is missing";
            }

            return GetIgnoredTermReason(edition.Title, profile.Ignored);
        }

        private string GetWorkStructureReason(Book book, MetadataProfile profile, IEnumerable<string> terms, bool enabled, string label, List<SeriesBookLink> seriesLinks, HashSet<string> titles)
        {
            if (!enabled)
            {
                return null;
            }

            if (seriesLinks != null && IsPartOrSet(book, seriesLinks, titles))
            {
                return $"Looks like a {label}";
            }

            var match = GetFirstMatchingTerm(GetBookTextValues(book), terms);
            if (match.IsNotNullOrWhiteSpace())
            {
                return $"Contains '{match}'";
            }

            return null;
        }

        private string GetSecondarySeriesReason(Book book, MetadataProfile profile)
        {
            if (!profile.SkipSeriesSecondary)
            {
                return null;
            }

            var seriesLinks = GetSeriesLinks(book);
            return seriesLinks.Any() && !seriesLinks.Any(x => x.IsPrimary) ? "Only secondary series links found" : null;
        }

        private string GetMinPagesReason(Book book, MetadataProfile profile)
        {
            if (profile.MinPages <= 0)
            {
                return null;
            }

            var editions = book.Editions.Value;
            return editions.Any(x => x.PageCount > 0) && editions.All(x => x.PageCount <= profile.MinPages) ? $"No edition above {profile.MinPages} pages" : null;
        }

        private string GetAllowedLanguageReason(Edition edition, MetadataProfile profile)
        {
            var allowedLanguages = profile.AllowedLanguages.IsNotNullOrWhiteSpace() ? new HashSet<string>(profile.AllowedLanguages.Trim(',').Split(',').Select(x => x.CanonicalizeLanguage())) : new HashSet<string>();

            if (!allowedLanguages.Any() || allowedLanguages.Contains(edition.Language?.CanonicalizeLanguage()))
            {
                return null;
            }

            return edition.Language.IsNullOrWhiteSpace() ? "Language is missing" : $"Language '{edition.Language}' is not allowed";
        }

        private string GetIgnoredTermReason(string title, IEnumerable<string> ignored)
        {
            var match = GetFirstMatchingTerm(new[] { title }, ignored ?? Enumerable.Empty<string>());
            return match.IsNotNullOrWhiteSpace() ? $"Contains ignored term '{match}'" : null;
        }

        private List<SeriesBookLink> GetSeriesLinks(Book book)
        {
            if (book.SeriesLinks == null)
            {
                return new List<SeriesBookLink>();
            }

            return book.SeriesLinks.Value ?? new List<SeriesBookLink>();
        }

        private IEnumerable<string> GetBookTextValues(Book book)
        {
            return new[] { book.Title }
                .Concat(book.Genres ?? Enumerable.Empty<string>())
                .Concat(book.Editions?.Value?.SelectMany(GetEditionTextValues) ?? Enumerable.Empty<string>());
        }

        private IEnumerable<string> GetEditionTextValues(Edition edition)
        {
            return new[] { edition.Title, edition.Format, edition.Disambiguation };
        }

        private bool IsReadableEdition(Edition edition)
        {
            return !IsAudioEdition(edition) && (edition.IsEbook || GetFirstMatchingTerm(GetEditionTextValues(edition), ReadableTerms).IsNotNullOrWhiteSpace());
        }

        private bool IsAudioEdition(Edition edition)
        {
            return GetFirstMatchingTerm(GetEditionTextValues(edition), AudioTerms).IsNotNullOrWhiteSpace();
        }

        private string GetFirstMatchingTerm(IEnumerable<string> values, IEnumerable<string> terms)
        {
            foreach (var term in terms.Where(x => x.IsNotNullOrWhiteSpace()))
            {
                if (values.Any(value => value.IsNotNullOrWhiteSpace() && _termMatcherService.IsMatch(term, value)))
                {
                    return term;
                }
            }

            return null;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            var profiles = All();

            // Name is a unique property
            var emptyProfile = profiles.FirstOrDefault(x => x.Name == NONE_PROFILE_NAME);

            // make sure empty profile exists and is actually empty
            // TODO: reinstate
            if (emptyProfile != null &&
                emptyProfile.MinPopularity == NONE_PROFILE_MIN_POPULARITY)
            {
                return;
            }

            if (!profiles.Any())
            {
                _logger.Info("Setting up standard metadata profile");

                Add(new MetadataProfile
                {
                    Name = "Standard",
                    MinPopularity = 350,
                    SkipMissingDate = true,
                    SkipPartsAndSets = true,
                    AllowedLanguages = "eng, null"
                });
            }

            if (emptyProfile != null)
            {
                // emptyProfile is not the correct empty profile - move it out of the way
                _logger.Info($"Renaming non-empty metadata profile {emptyProfile.Name}");

                var names = profiles.Select(x => x.Name).ToList();

                var i = 1;
                emptyProfile.Name = $"{NONE_PROFILE_NAME}.{i}";

                while (names.Contains(emptyProfile.Name))
                {
                    i++;
                }

                _profileRepository.Update(emptyProfile);
            }

            _logger.Info("Setting up empty metadata profile");

            Add(new MetadataProfile
            {
                Name = NONE_PROFILE_NAME,
                MinPopularity = NONE_PROFILE_MIN_POPULARITY
            });
        }
    }
}
