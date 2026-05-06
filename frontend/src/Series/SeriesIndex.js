import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { kinds } from 'Helpers/Props';
import styles from './SeriesIndex.css';

function SeriesIndex(props) {
  const {
    isFetching,
    error,
    items
  } = props;

  return (
    <PageContent title="Series">
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
          !isFetching && !error &&
            <div className={styles.seriesGrid}>
              {
                items.map((series) => {
                  const completeness = series.completeness || {};
                  const totalBooks = completeness.totalBooks || 0;
                  const availableBooks = completeness.availableBooks || 0;
                  const missingBooks = completeness.missingBooks || 0;

                  return (
                    <Link
                      key={series.id}
                      className={styles.seriesItem}
                      to={`/series/${series.id}`}
                    >
                      <div className={styles.seriesTitle}>
                        {series.title}
                      </div>

                      <div className={styles.seriesMeta}>
                        {availableBooks}/{totalBooks} available
                        {
                          missingBooks > 0 &&
                            <span> · {missingBooks} missing</span>
                        }
                      </div>
                    </Link>
                  );
                })
              }
            </div>
        }
      </PageContentBody>
    </PageContent>
  );
}

SeriesIndex.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired
};

export default SeriesIndex;
