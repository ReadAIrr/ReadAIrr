import PropTypes from 'prop-types';
import React from 'react';
import AuthorNameLink from 'Author/AuthorNameLink';
import BookSearchCellConnector from 'Book/BookSearchCellConnector';
import BookTitleLink from 'Book/BookTitleLink';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './SeriesDetails.css';

const columns = [
  {
    name: 'monitored',
    columnLabel: 'Monitored',
    isVisible: true,
    isModifiable: false
  },
  {
    name: 'position',
    label: 'Number',
    isVisible: true
  },
  {
    name: 'title',
    label: 'Book',
    isVisible: true
  },
  {
    name: 'author',
    label: 'Author',
    isVisible: true
  },
  {
    name: 'status',
    label: 'Status',
    isVisible: true
  },
  {
    name: 'reason',
    label: 'Reason',
    isVisible: true
  },
  {
    name: 'actions',
    columnLabel: 'Actions',
    isVisible: true,
    isModifiable: false
  }
];

function SeriesBookStatus({ book }) {
  if (book.hasFile) {
    return (
      <Label
        title="Available"
        kind={kinds.SUCCESS}
      >
        Available
      </Label>
    );
  }

  if (!book.authorMonitored || !book.monitored) {
    return (
      <Label
        title={translate('NotMonitored')}
        kind={kinds.WARNING}
      >
        {translate('NotMonitored')}
      </Label>
    );
  }

  return (
    <Label
      title={translate('Missing')}
      kind={kinds.DANGER}
    >
      {translate('Missing')}
    </Label>
  );
}

SeriesBookStatus.propTypes = {
  book: PropTypes.object.isRequired
};

function SeriesBookRow(props) {
  const {
    book,
    onMonitorBookPress
  } = props;

  const onMonitorPress = (monitored) => {
    onMonitorBookPress(book.id, monitored);
  };

  return (
    <TableRow>
      <TableRowCell className={styles.monitored}>
        <MonitorToggleButton
          monitored={book.monitored}
          isDisabled={!book.authorMonitored}
          isSaving={false}
          onPress={onMonitorPress}
        />
      </TableRowCell>

      <TableRowCell className={styles.position}>
        {book.position || ''}
      </TableRowCell>

      <TableRowCell className={styles.title}>
        <BookTitleLink
          titleSlug={book.titleSlug}
          title={book.title}
        />
      </TableRowCell>

      <TableRowCell className={styles.author}>
        <AuthorNameLink
          titleSlug={book.authorTitleSlug}
          authorName={book.authorName}
        />
      </TableRowCell>

      <TableRowCell className={styles.status}>
        <SeriesBookStatus book={book} />
      </TableRowCell>

      <TableRowCell className={styles.reason}>
        {book.hasFile ? '' : book.missingReason || ''}
      </TableRowCell>

      <BookSearchCellConnector
        bookId={book.id}
        authorId={book.authorId}
        bookTitle={book.title}
        authorName={book.authorName}
      />
    </TableRow>
  );
}

SeriesBookRow.propTypes = {
  book: PropTypes.object.isRequired,
  onMonitorBookPress: PropTypes.func.isRequired
};

function SeriesDetails(props) {
  const {
    isFetching,
    error,
    item,
    isSearchingSeries,
    onMonitorSeriesPress,
    onMonitorBookPress,
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
            <div className={styles.bookType}>
              <div className={styles.seriesTitle}>
                <MonitorToggleButton
                  size={24}
                  monitored={isMonitored}
                  isDisabled={!books.length}
                  isSaving={false}
                  onPress={onMonitorSeriesPress}
                />

                <div className={styles.header}>
                  <div className={styles.left}>
                    <div>
                      <span className={styles.bookTypeLabel}>
                        {item.title}
                      </span>

                      <span className={styles.bookCount}>
                        ({books.length} Books)
                      </span>
                    </div>
                  </div>

                  <div className={styles.summary}>
                    {completeness.availableBooks || 0}/{completeness.totalBooks || 0} available · {completeness.missingBooks || 0} missing
                  </div>
                </div>

                <SpinnerIconButton
                  className={styles.actionButton}
                  iconClassName={styles.actionButtonIcon}
                  name={icons.SEARCH}
                  size={14}
                  title={translate('SearchForMonitoredBooks')}
                  isSpinning={isSearchingSeries}
                  isDisabled={!searchableBooks.length}
                  onPress={onSearchSeriesPress}
                />

                <IconButton
                  className={styles.actionButton}
                  iconClassName={styles.actionButtonIcon}
                  name={icons.BOOK}
                  size={14}
                  title={translate('BookIndex')}
                  to="/books"
                />
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

              <div className={styles.books}>
                <Table
                  columns={columns}
                  horizontalScroll={true}
                >
                  <TableBody>
                    {
                      books.map((book) => {
                        return (
                          <SeriesBookRow
                            key={book.id}
                            book={book}
                            onMonitorBookPress={onMonitorBookPress}
                          />
                        );
                      })
                    }
                  </TableBody>
                </Table>
              </div>

              <div className={styles.footer}>
                <Icon
                  name={icons.INFO}
                  size={12}
                />
                Missing reasons use the same monitored, author, and file availability rules as the library rows.
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
  isSearchingSeries: PropTypes.bool.isRequired,
  onMonitorSeriesPress: PropTypes.func.isRequired,
  onMonitorBookPress: PropTypes.func.isRequired,
  onSearchSeriesPress: PropTypes.func.isRequired
};

export default SeriesDetails;
