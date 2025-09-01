using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.GoogleBooks;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Config
{
    [V1ApiController]
    public class GoogleBooksController : RestController<GoogleBooksConfigResource>
    {
        private readonly IConfigService _configService;
        private readonly IGoogleBooksProxy _googleBooksProxy;
        private readonly Logger _logger;

        public GoogleBooksController(IConfigService configService, IGoogleBooksProxy googleBooksProxy, Logger logger)
        {
            _configService = configService;
            _googleBooksProxy = googleBooksProxy;
            _logger = logger;
        }

        [HttpGet]
        public GoogleBooksConfigResource GetConfig()
        {
            return new GoogleBooksConfigResource
            {
                Id = 1,
                ApiKey = _configService.GoogleBooksApiKey,
                Enabled = _configService.EnableGoogleBooksMetadata,
                IsConfigured = !string.IsNullOrEmpty(_configService.GoogleBooksApiKey),
                SetupCompleted = !string.IsNullOrEmpty(_configService.GoogleBooksApiKey) && _configService.EnableGoogleBooksMetadata
            };
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<GoogleBooksConfigResource>> UpdateConfig(int id, GoogleBooksConfigResource resource)
        {
            var config = GetConfig();
            
            // Validate API key if changed
            if (!string.IsNullOrEmpty(resource.ApiKey) && resource.ApiKey != config.ApiKey)
            {
                var isValid = await _googleBooksProxy.ValidateApiKey(resource.ApiKey);
                if (!isValid)
                {
                    return BadRequest(new { error = "Invalid Google Books API key. Please check your key and try again." });
                }
            }

            _configService.GoogleBooksApiKey = resource.ApiKey ?? "";
            _configService.EnableGoogleBooksMetadata = resource.Enabled && !string.IsNullOrEmpty(resource.ApiKey);

            return GetConfig();
        }

        [HttpPost("validate")]
        public async Task<ActionResult<GoogleBooksValidationResult>> ValidateApiKey(GoogleBooksValidationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.ApiKey))
                {
                    return Ok(new GoogleBooksValidationResult 
                    { 
                        IsValid = false, 
                        Error = "API key is required" 
                    });
                }

                if (request.ApiKey.Length < 30)
                {
                    return Ok(new GoogleBooksValidationResult 
                    { 
                        IsValid = false, 
                        Error = "API key appears to be invalid (too short). Google API keys are typically 39 characters long." 
                    });
                }

                // Check for common API key format issues
                if (!request.ApiKey.StartsWith("AIza"))
                {
                    return Ok(new GoogleBooksValidationResult 
                    { 
                        IsValid = false, 
                        Error = "API key format appears incorrect. Google Books API keys should start with 'AIza'." 
                    });
                }

                var isValid = await _googleBooksProxy.ValidateApiKey(request.ApiKey);
                
                if (isValid)
                {
                    // Test with a known book to ensure full functionality
                    var testResult = await _googleBooksProxy.GetAudioBookMetadata("9780545010221"); // Harry Potter ISBN
                    
                    return Ok(new GoogleBooksValidationResult 
                    { 
                        IsValid = true,
                        Message = "API key validated successfully!",
                        TestBookFound = testResult != null,
                        TestBookTitle = testResult?.Title
                    });
                }
                else
                {
                    return Ok(new GoogleBooksValidationResult 
                    { 
                        IsValid = false, 
                        Error = "Invalid API key. Please verify your Google Books API key is correct and has the Books API enabled." 
                    });
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("403"))
            {
                _logger.Warn(ex, "Google Books API returned 403 Forbidden");
                return Ok(new GoogleBooksValidationResult 
                { 
                    IsValid = false, 
                    Error = "Access denied. Please ensure your API key is valid and the Google Books API is enabled for your project." 
                });
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                _logger.Warn(ex, "Google Books API rate limit exceeded");
                return Ok(new GoogleBooksValidationResult 
                { 
                    IsValid = false, 
                    Error = "Rate limit exceeded. Please wait a moment and try again." 
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.Warn(ex, "HTTP error validating Google Books API key");
                return Ok(new GoogleBooksValidationResult 
                { 
                    IsValid = false, 
                    Error = "Network error: Unable to connect to Google Books API. Please check your internet connection." 
                });
            }
            catch (TaskCanceledException ex)
            {
                _logger.Warn(ex, "Timeout validating Google Books API key");
                return Ok(new GoogleBooksValidationResult 
                { 
                    IsValid = false, 
                    Error = "Request timeout: Google Books API is not responding. Please try again later." 
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error validating Google Books API key");
                return Ok(new GoogleBooksValidationResult 
                { 
                    IsValid = false, 
                    Error = "Validation failed: " + ex.Message 
                });
            }
        }

        [HttpGet("setup-url")]
        public ActionResult<GoogleBooksSetupUrlResponse> GetSetupUrl([FromQuery] string userEmail = "")
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            var projectName = $"readairr-books-{timestamp}";
            
            var setupUrl = $"https://console.cloud.google.com/projectcreate?" +
                          $"name={Uri.EscapeDataString(projectName)}&" +
                          $"organizationId=&" +
                          $"billingAccount=&" +
                          $"folder=";

            var enableApiUrl = $"https://console.cloud.google.com/apis/library/books.googleapis.com";
            
            var createKeyUrl = "https://console.cloud.google.com/apis/credentials";

            return Ok(new GoogleBooksSetupUrlResponse
            {
                ProjectName = projectName,
                ProjectCreationUrl = setupUrl,
                EnableApiUrl = enableApiUrl,
                CreateApiKeyUrl = createKeyUrl,
                Instructions = new[]
                {
                    $"Click 'Create Project' to create '{projectName}'",
                    "Wait for project creation to complete (30-60 seconds)",
                    "Enable the Google Books API for your project",
                    "Create an API key in the Credentials section",
                    "Copy the API key and paste it into ReadAIrr"
                }
            });
        }

        [HttpGet("status")]
        public async Task<ActionResult<GoogleBooksStatusResponse>> GetStatus()
        {
            var config = GetConfig();
            
            if (!config.IsConfigured)
            {
                return Ok(new GoogleBooksStatusResponse
                {
                    Status = "not_configured",
                    Message = "Google Books API is not configured",
                    Enabled = false
                });
            }

            try
            {
                var isValid = await _googleBooksProxy.ValidateApiKey(config.ApiKey);
                
                return Ok(new GoogleBooksStatusResponse
                {
                    Status = isValid ? "active" : "invalid_key",
                    Message = isValid ? "Google Books API is active and working" : "API key is invalid",
                    Enabled = config.Enabled,
                    LastValidated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking Google Books API status");
                return Ok(new GoogleBooksStatusResponse
                {
                    Status = "error",
                    Message = "Unable to check API status",
                    Enabled = config.Enabled
                });
            }
        }

        [HttpPost("test")]
        public async Task<ActionResult<GoogleBooksTestResponse>> TestApiIntegration([FromQuery] string isbn = "", [FromQuery] string title = "")
        {
            if (string.IsNullOrEmpty(_configService.GoogleBooksApiKey))
            {
                return BadRequest(new GoogleBooksTestResponse
                {
                    Success = false,
                    Error = "Google Books API key is not configured"
                });
            }

            try
            {
                var testIsbn = !string.IsNullOrEmpty(isbn) ? isbn : "9780545010221"; // Default to Harry Potter
                var result = await _googleBooksProxy.GetAudioBookMetadata(testIsbn);
                
                if (result != null)
                {
                    return Ok(new GoogleBooksTestResponse
                    {
                        Success = true,
                        BookFound = true,
                        Title = result.Title,
                        Authors = result.Authors,
                        Duration = result.Duration?.ToString(@"hh\:mm\:ss"),
                        Narrators = result.Narrators,
                        Message = "Successfully retrieved audiobook metadata!"
                    });
                }
                else
                {
                    return Ok(new GoogleBooksTestResponse
                    {
                        Success = true,
                        BookFound = false,
                        Message = "API is working, but no audiobook data found for the requested ISBN"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error testing Google Books API integration");
                return Ok(new GoogleBooksTestResponse
                {
                    Success = false,
                    Error = "Error testing API: " + ex.Message
                });
            }
        }
    }

    public class GoogleBooksConfigResource : RestResource
    {
        public string ApiKey { get; set; }
        public bool Enabled { get; set; }
        public bool IsConfigured { get; set; }
        public bool SetupCompleted { get; set; }
    }

    public class GoogleBooksValidationRequest
    {
        public string ApiKey { get; set; }
    }

    public class GoogleBooksValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public bool? TestBookFound { get; set; }
        public string TestBookTitle { get; set; }
    }

    public class GoogleBooksSetupUrlResponse
    {
        public string ProjectName { get; set; }
        public string ProjectCreationUrl { get; set; }
        public string EnableApiUrl { get; set; }
        public string CreateApiKeyUrl { get; set; }
        public string[] Instructions { get; set; }
    }

    public class GoogleBooksStatusResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public bool Enabled { get; set; }
        public DateTime? LastValidated { get; set; }
    }

    public class GoogleBooksTestResponse
    {
        public bool Success { get; set; }
        public bool BookFound { get; set; }
        public string Title { get; set; }
        public System.Collections.Generic.List<string> Authors { get; set; }
        public string Duration { get; set; }
        public System.Collections.Generic.List<string> Narrators { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }
}
