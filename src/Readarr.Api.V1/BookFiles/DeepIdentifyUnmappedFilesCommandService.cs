using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;

namespace Readarr.Api.V1.BookFiles
{
    public class DeepIdentifyUnmappedFilesCommandService : IExecute<DeepIdentifyUnmappedFilesCommand>
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IUnmappedIdentificationSuggestionService _unmappedIdentificationSuggestionService;
        private readonly Logger _logger;

        public DeepIdentifyUnmappedFilesCommandService(IMediaFileService mediaFileService,
                                                       IUnmappedIdentificationSuggestionService unmappedIdentificationSuggestionService,
                                                       Logger logger)
        {
            _mediaFileService = mediaFileService;
            _unmappedIdentificationSuggestionService = unmappedIdentificationSuggestionService;
            _logger = logger;
        }

        public void Execute(DeepIdentifyUnmappedFilesCommand message)
        {
            var bookFileIds = message.BookFileIds?.Distinct().ToList() ?? new List<int>();

            if (!bookFileIds.Any())
            {
                _logger.Info("Deep Identify Audio skipped: no unmapped files were selected");
                return;
            }

            var resources = _mediaFileService.Get(bookFileIds)
                .Where(x => x.EditionId == 0)
                .Select(ToResource)
                .ToList();

            if (!resources.Any())
            {
                _logger.Info("Deep Identify Audio skipped: selected files are no longer unmapped");
                return;
            }

            _logger.Info("Deep Identify Audio queued {0} unmapped file(s)", resources.Count);
            _unmappedIdentificationSuggestionService.QueueDeepIdentifyAudio(resources);

            var result = _unmappedIdentificationSuggestionService.ProcessQueuedDeepIdentifyAudio(resources);
            var captured = result.Count(x => x.Status == "transcriptCaptured");
            var failed = result.Count(x => x.Status == "extractionFailed" || x.Status == "transcriptionFailed" || x.Status == "failed");
            var skipped = result.Count(x => x.Status == "disabled" || x.Status == "skipped");

            _logger.Info("Deep Identify Audio completed for {0} unmapped file(s): {1} transcript captured, {2} failed, {3} skipped or disabled",
                resources.Count,
                captured,
                failed,
                skipped);
        }

        private static BookFileResource ToResource(BookFile bookFile)
        {
            return new BookFileResource
            {
                Id = bookFile.Id,
                Path = bookFile.Path,
                Size = bookFile.Size,
                Modified = bookFile.Modified,
                DateAdded = bookFile.DateAdded,
                Quality = bookFile.Quality
            };
        }
    }
}
