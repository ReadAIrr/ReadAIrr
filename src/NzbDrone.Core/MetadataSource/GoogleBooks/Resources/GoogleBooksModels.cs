using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.GoogleBooks.Resources
{
    public class GoogleBooksSearchResponse
    {
        public string Kind { get; set; }
        public int TotalItems { get; set; }
        public List<GoogleBooksVolume> Items { get; set; }
    }

    public class GoogleBooksVolume
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public string Etag { get; set; }
        public string SelfLink { get; set; }
        public GoogleBooksVolumeInfo VolumeInfo { get; set; }
        public GoogleBooksSaleInfo SaleInfo { get; set; }
        public GoogleBooksAccessInfo AccessInfo { get; set; }
        public GoogleBooksSearchInfo SearchInfo { get; set; }
    }

    public class GoogleBooksVolumeInfo
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public List<string> Authors { get; set; }
        public string Publisher { get; set; }
        public string PublishedDate { get; set; }
        public string Description { get; set; }
        public List<GoogleBooksIndustryIdentifier> IndustryIdentifiers { get; set; }
        public GoogleBooksReadingModes ReadingModes { get; set; }
        public int? PageCount { get; set; }
        public string PrintType { get; set; }
        public List<string> Categories { get; set; }
        public string MaturityRating { get; set; }
        public bool AllowAnonLogging { get; set; }
        public string ContentVersion { get; set; }
        public GoogleBooksPanelizationSummary PanelizationSummary { get; set; }
        public GoogleBooksImageLinks ImageLinks { get; set; }
        public string Language { get; set; }
        public string PreviewLink { get; set; }
        public string InfoLink { get; set; }
        public string CanonicalVolumeLink { get; set; }
        
        // Audiobook-specific fields
        public string Duration { get; set; }
        public double? AverageRating { get; set; }
        public int? RatingsCount { get; set; }
        
        // Custom properties for narrator extraction
        [JsonIgnore]
        public List<string> Narrators { get; set; } = new List<string>();
    }

    public class GoogleBooksIndustryIdentifier
    {
        public string Type { get; set; }
        public string Identifier { get; set; }
    }

    public class GoogleBooksReadingModes
    {
        public bool Text { get; set; }
        public bool Image { get; set; }
    }

    public class GoogleBooksPanelizationSummary
    {
        public bool ContainsEpubBubbles { get; set; }
        public bool ContainsImageBubbles { get; set; }
    }

    public class GoogleBooksImageLinks
    {
        public string SmallThumbnail { get; set; }
        public string Thumbnail { get; set; }
        public string Small { get; set; }
        public string Medium { get; set; }
        public string Large { get; set; }
        public string ExtraLarge { get; set; }
    }

    public class GoogleBooksSaleInfo
    {
        public string Country { get; set; }
        public string Saleability { get; set; }
        public bool IsEbook { get; set; }
        public GoogleBooksListPrice ListPrice { get; set; }
        public GoogleBooksRetailPrice RetailPrice { get; set; }
        public string BuyLink { get; set; }
        public List<GoogleBooksOffer> Offers { get; set; }
    }

    public class GoogleBooksListPrice
    {
        public double Amount { get; set; }
        public string CurrencyCode { get; set; }
        public string AmountInMicros { get; set; }
    }

    public class GoogleBooksRetailPrice
    {
        public double Amount { get; set; }
        public string CurrencyCode { get; set; }
        public string AmountInMicros { get; set; }
    }

    public class GoogleBooksOffer
    {
        public int FinskyOfferType { get; set; }
        public GoogleBooksListPrice ListPrice { get; set; }
        public GoogleBooksRetailPrice RetailPrice { get; set; }
        public bool Giftable { get; set; }
    }

    public class GoogleBooksAccessInfo
    {
        public string Country { get; set; }
        public string Viewability { get; set; }
        public bool Embeddable { get; set; }
        public bool PublicDomain { get; set; }
        public string TextToSpeechPermission { get; set; }
        public GoogleBooksEpub Epub { get; set; }
        public GoogleBooksPdf Pdf { get; set; }
        public string WebReaderLink { get; set; }
        public string AccessViewStatus { get; set; }
        public bool QuoteSharingAllowed { get; set; }
        
        // Audiobook access information
        public GoogleBooksAudiobook Audiobook { get; set; }
    }

    public class GoogleBooksEpub
    {
        public bool IsAvailable { get; set; }
        public string AcsTokenLink { get; set; }
    }

    public class GoogleBooksPdf
    {
        public bool IsAvailable { get; set; }
        public string AcsTokenLink { get; set; }
    }

    public class GoogleBooksAudiobook
    {
        public bool IsAvailable { get; set; }
        public string Duration { get; set; }
        public List<string> Narrators { get; set; }
        public string Format { get; set; }
    }

    public class GoogleBooksSearchInfo
    {
        public string TextSnippet { get; set; }
    }
}
