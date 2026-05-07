using System.Linq;
using NzbDrone.Core.Books;

namespace Readarr.Api.V1.Books
{
    public static class BookMissingReason
    {
        public static string Get(Book book, bool hasFile)
        {
            if (hasFile)
            {
                return null;
            }

            if (book.Author?.Value != null && !book.Author.Value.Monitored)
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
