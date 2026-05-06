import PropTypes from 'prop-types';
import React from 'react';
import AuthorNameLink from 'Author/AuthorNameLink';
import BookTitleLink from 'Book/BookTitleLink';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { kinds } from 'Helpers/Props';
import styles from './SeriesDetails.css';

function SeriesDetails(props) {
  const {
    isFetching,
    error,
    item,
    onMonitorSeriesPress,
    onSearchSeriesPress
  } = props;

  const books = item?.books || [];
  const completeness = item?.completeness || {};
  const isMonitored = books.length > 0 && books.every((book) => book.monitored);
  const searchableBooks = books.filter((book) => book.monitored && book.authorMonitored);

  return (
    <PageContent title={item?.title || 'Series'}>
      <PageContentBody>
        {
          isFetching &&
            <LoadingIndicator />
        }

        {
          !isFetching && error &&
            <Alert kind={kinds.DANGER}>
              Unable to load series.
            </Alert>
        }

        {
          !isFetching && item &&
            <div>
              <div className={styles.header}>
                <div>
                  <div className={styles.title}>
                    {item.title}
                  </div>
                  <div className={styles.summary}>
                    {completeness.availableBooks || 0}/{completeness.totalBooks || 0} available · {completeness.missingBooks || 0} missing
                  </div>
                </div>

                <div className={styles.actions}>
                  <MonitorToggleButton
                    monitored={isMonitored}
                    isDisabled={!books.length}
                    onPress={onMonitorSeriesPress}
                  />

                  <Button
                    isDisabled={!searchableBooks.length}
                    onPress={onSearchSeriesPress}
                  >
                    Search Series
                  </Button>
                </div>
              </div>

              <div className={styles.completeness}>
                <div>
                  <span>{completeness.totalBooks || 0}</span>
                  Books
                </div>
                <div>
                  <span>{completeness.availableBooks || 0}</span>
                  Available
                </div>
                <div>
                  <span>{completeness.unmonitoredBooks || 0}</span>
                  Unmonitored Books
                </div>
                <div>
                  <span>{completeness.unmonitoredAuthors || 0}</span>
                  Unmonitored Authors
                </div>
              </div>

              <div className={styles.bookList}>
                {
                  books.map((book) => {
                    return (
                      <div
                        key={book.id}
                        className={styles.bookRow}
                      >
                        <div className={styles.position}>
                          {book.position || ''}
                        </div>

                        <div className={styles.bookMain}>
                          <BookTitleLink
                            titleSlug={book.titleSlug}
                            title={book.title}
                          />
                          <div className={styles.bookMeta}>
                            <AuthorNameLink
                              titleSlug={book.authorTitleSlug}
                              authorName={book.authorName}
                            />
                          </div>
                        </div>

                        <div className={book.hasFile ? styles.available : styles.missing}>
                          {book.hasFile ? 'Available' : 'Missing'}
                        </div>

                        <div className={styles.reason}>
                          {book.missingReason || ''}
                        </div>
                      </div>
                    );
                  })
                }
              </div>
            </div>
        }
      </PageContentBody>
    </PageContent>
  );
}

SeriesDetails.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  item: PropTypes.object,
  onMonitorSeriesPress: PropTypes.func.isRequired,
  onSearchSeriesPress: PropTypes.func.isRequired
};

export default SeriesDetails;
