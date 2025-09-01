using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Services;
using Readarr.Http;
using Readarr.Http.Extensions;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Books
{
    [V1ApiController]
    public class AudiobookValidationController : RestController<AudiobookValidationResource>
    {
        private readonly IAudiobookDurationValidationService _validationService;
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly IEditionService _editionService;
        private readonly Logger _logger;

        public AudiobookValidationController(
            IAudiobookDurationValidationService validationService,
            IBookService bookService,
            IAuthorService authorService,
            IEditionService editionService,
            Logger logger)
        {
            _validationService = validationService;
            _bookService = bookService;
            _authorService = authorService;
            _editionService = editionService;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AudiobookValidationResource>> GetValidationResult(int id)
        {
            var edition = _editionService.GetEdition(id);
            if (edition == null)
            {
                return NotFound();
            }

            var result = await _validationService.ValidateAudiobookCompleteness(edition);
            return Ok(result.ToResource());
        }

        [HttpPost("validate")]
        public async Task<ActionResult<AudiobookValidationResource>> ValidateEdition([FromBody] ValidateEditionRequest request)
        {
            var edition = _editionService.GetEdition(request.EditionId);
            if (edition == null)
            {
                return NotFound($"Edition with ID {request.EditionId} not found");
            }

            var result = await _validationService.ValidateAudiobookCompleteness(edition);
            return Ok(result.ToResource());
        }

        [HttpPost("validate-batch")]
        public async Task<ActionResult<List<AudiobookValidationResource>>> ValidateMultipleEditions([FromBody] ValidateMultipleEditionsRequest request)
        {
            var editions = new List<Edition>();
            
            foreach (var editionId in request.EditionIds)
            {
                var edition = _editionService.GetEdition(editionId);
                if (edition != null)
                {
                    editions.Add(edition);
                }
            }

            if (!editions.Any())
            {
                return BadRequest("No valid editions found for the provided IDs");
            }

            var results = await _validationService.ValidateMultipleAudiobooks(editions);
            return Ok(results.Select(r => r.ToResource()).ToList());
        }

        [HttpGet("author/{authorId:int}/health-check")]
        public async Task<ActionResult<AudiobookHealthCheckResource>> GetAuthorHealthCheck(int authorId)
        {
            var author = _authorService.GetAuthor(authorId);
            if (author == null)
            {
                return NotFound($"Author with ID {authorId} not found");
            }

            var healthCheck = await _validationService.PerformHealthCheck(author);
            return Ok(healthCheck.ToResource());
        }

        [HttpGet("book/{bookId:int}/validate")]
        public async Task<ActionResult<List<AudiobookValidationResource>>> ValidateBook(int bookId)
        {
            var book = _bookService.GetBook(bookId);
            if (book == null)
            {
                return NotFound($"Book with ID {bookId} not found");
            }

            var results = await _validationService.ValidateMultipleAudiobooks(book.Editions.Value);
            return Ok(results.Select(r => r.ToResource()).ToList());
        }

        [HttpGet("summary")]
        public async Task<ActionResult<ValidationSummaryResource>> GetValidationSummary([FromQuery] int? authorId = null)
        {
            try
            {
                var authors = authorId.HasValue 
                    ? new List<Author> { _authorService.GetAuthor(authorId.Value) }.Where(a => a != null).ToList()
                    : _authorService.GetAllAuthors();

                var summary = new ValidationSummaryResource
                {
                    GeneratedAt = DateTime.UtcNow,
                    AuthorHealthChecks = new List<AudiobookHealthCheckResource>()
                };

                foreach (var author in authors)
                {
                    try
                    {
                        var healthCheck = await _validationService.PerformHealthCheck(author);
                        summary.AuthorHealthChecks.Add(healthCheck.ToResource());
                        
                        // Add small delay to respect API rate limits
                        await Task.Delay(50);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Error performing health check for author {AuthorId}", author.Id);
                    }
                }

                // Calculate overall statistics
                summary.TotalAuthors = summary.AuthorHealthChecks.Count;
                summary.TotalEditions = summary.AuthorHealthChecks.Sum(hc => hc.TotalEditions);
                summary.ValidEditions = summary.AuthorHealthChecks.Sum(hc => hc.ValidEditions);
                summary.InvalidEditions = summary.AuthorHealthChecks.Sum(hc => hc.InvalidEditions);
                summary.SkippedEditions = summary.AuthorHealthChecks.Sum(hc => hc.SkippedEditions);

                if (summary.TotalEditions > 0)
                {
                    summary.OverallValidationPercentage = (double)summary.ValidEditions / (summary.TotalEditions - summary.SkippedEditions) * 100;
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating validation summary");
                return StatusCode(500, new { error = "Error generating validation summary" });
            }
        }
    }

    public class ValidateEditionRequest
    {
        public int EditionId { get; set; }
    }

    public class ValidateMultipleEditionsRequest
    {
        public List<int> EditionIds { get; set; } = new List<int>();
    }

    public class AudiobookValidationResource : RestResource
    {
        public int EditionId { get; set; }
        public string EditionTitle { get; set; }
        public bool IsValid { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string LocalDuration { get; set; }
        public string ExpectedDuration { get; set; }
        public string DurationDifference { get; set; }
        public double? DifferencePercentage { get; set; }
        public DateTime ValidationDate { get; set; }
    }

    public class AudiobookHealthCheckResource : RestResource
    {
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public DateTime CheckDate { get; set; }
        public int TotalEditions { get; set; }
        public int ValidEditions { get; set; }
        public int InvalidEditions { get; set; }
        public int SkippedEditions { get; set; }
        public string OverallHealth { get; set; }
        public List<AudiobookValidationResource> ValidationResults { get; set; } = new List<AudiobookValidationResource>();
        public string Error { get; set; }
    }

    public class ValidationSummaryResource
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalAuthors { get; set; }
        public int TotalEditions { get; set; }
        public int ValidEditions { get; set; }
        public int InvalidEditions { get; set; }
        public int SkippedEditions { get; set; }
        public double OverallValidationPercentage { get; set; }
        public List<AudiobookHealthCheckResource> AuthorHealthChecks { get; set; } = new List<AudiobookHealthCheckResource>();
    }
}

public static class AudiobookValidationResourceMapper
{
    public static AudiobookValidationResource ToResource(this NzbDrone.Core.Books.Services.AudiobookValidationResult model)
    {
        return new AudiobookValidationResource
        {
            Id = model.Edition.Id,
            EditionId = model.Edition.Id,
            EditionTitle = model.Edition.Title,
            IsValid = model.IsValid,
            Status = model.Status.ToString(),
            Message = model.Message,
            LocalDuration = model.LocalDuration?.ToString(@"h\:mm\:ss"),
            ExpectedDuration = model.ExpectedDuration?.ToString(@"h\:mm\:ss"),
            DurationDifference = model.DurationDifference?.ToString(@"h\:mm\:ss"),
            DifferencePercentage = model.DifferencePercentage,
            ValidationDate = model.ValidationDate
        };
    }

    public static AudiobookHealthCheckResource ToResource(this NzbDrone.Core.Books.Services.AudiobookHealthCheckResult model)
    {
        return new AudiobookHealthCheckResource
        {
            Id = model.Author.Id,
            AuthorId = model.Author.Id,
            AuthorName = model.Author.Name,
            CheckDate = model.CheckDate,
            TotalEditions = model.TotalEditions,
            ValidEditions = model.ValidEditions,
            InvalidEditions = model.InvalidEditions,
            SkippedEditions = model.SkippedEditions,
            OverallHealth = model.OverallHealth.ToString(),
            ValidationResults = model.ValidationResults.Select(vr => vr.ToResource()).ToList(),
            Error = model.Error
        };
    }
}
