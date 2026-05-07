using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Books
{
    public interface ISeriesService
    {
        Series FindById(string foreignSeriesId);
        List<Series> FindById(List<string> foreignSeriesId);
        Series Get(int id);
        List<Series> All();
        List<Series> GetByAuthorMetadataId(int authorMetadataId);
        List<Series> GetByAuthorId(int authorId);
        List<Series> GetByAuthorIds(IEnumerable<int> authorIds);
        void Delete(int seriesId);
        void InsertMany(IList<Series> series);
        void UpdateMany(IList<Series> series);
    }

    public class SeriesService : ISeriesService
    {
        private readonly ISeriesRepository _seriesRepository;

        public SeriesService(ISeriesRepository seriesRepository)
        {
            _seriesRepository = seriesRepository;
        }

        public Series FindById(string foreignSeriesId)
        {
            return _seriesRepository.FindById(foreignSeriesId);
        }

        public List<Series> FindById(List<string> foreignSeriesId)
        {
            return _seriesRepository.FindById(foreignSeriesId);
        }

        public Series Get(int id)
        {
            return _seriesRepository.Get(id);
        }

        public List<Series> All()
        {
            return _seriesRepository.All().ToList();
        }

        public List<Series> GetByAuthorMetadataId(int authorMetadataId)
        {
            return _seriesRepository.GetByAuthorMetadataId(authorMetadataId);
        }

        public List<Series> GetByAuthorId(int authorId)
        {
            return _seriesRepository.GetByAuthorId(authorId);
        }

        public List<Series> GetByAuthorIds(IEnumerable<int> authorIds)
        {
            return _seriesRepository.GetByAuthorIds(authorIds);
        }

        public void Delete(int seriesId)
        {
            _seriesRepository.Delete(seriesId);
        }

        public void InsertMany(IList<Series> series)
        {
            _seriesRepository.InsertMany(series);
        }

        public void UpdateMany(IList<Series> series)
        {
            _seriesRepository.UpdateMany(series);
        }
    }
}
