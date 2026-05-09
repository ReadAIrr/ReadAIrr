import PropTypes from 'prop-types';
import React, { useState } from 'react';
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
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTable from 'Components/Table/VirtualTable';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableRow from 'Components/Table/VirtualTableRow';
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

function getColumnClassName(name, isHeader = false) {
  const suffix = isHeader ? 'HeaderCell' : '';

  return styles[`${name}${suffix}`] || styles.cell;
}

function SeriesBookTableHeader() {
  return (
    <VirtualTableHeader>
      {
        columns.map((column) => {
          const {
            name,
            label,
            columnLabel,
            isVisible
          } = column;

          if (!isVisible) {
            return null;
          }

          return (
            <VirtualTableHeaderCell
              key={name}
              name={name}
              className={getColumnClassName(name, true)}
            >
              {columnLabel || label || ''}
            </VirtualTableHeaderCell>
          );
        })
      }
    </VirtualTableHeader>
  );
}

function SeriesBookRow(props) {
  const {
    book,
    onMonitorBookPress
  } = props;

  const onMonitorPress = (monitored) => {
    onMonitorBookPress(book.id, monitored);
  };

  return (
    <>
      <VirtualTableRowCell className={styles.monitored}>
        <MonitorToggleButton
          monitored={book.monitored}
          isDisabled={!book.authorMonitored}
          isSaving={false}
          onPress={onMonitorPress}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.position}>
        {book.position || ''}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.title}>
        <BookTitleLink
          titleSlug={book.titleSlug}
          title={book.title}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.author}>
        <AuthorNameLink
          titleSlug={book.authorTitleSlug}
          authorName={book.authorName}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.status}>
        <SeriesBookStatus book={book} />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.reason}>
        {book.hasFile ? '' : book.missingReason || ''}
      </VirtualTableRowCell>

      <BookSearchCellConnector
        className={styles.actions}
        component={VirtualTableRowCell}
        bookId={book.id}
        authorId={book.authorId}
        bookTitle={book.title}
        authorName={book.authorName}
      />
    </>
  );
}

SeriesBookRow.propTypes = {
  book: PropTypes.object.isRequired,
  onMonitorBookPress: PropTypes.func.isRequired
};

function SeriesDetails(props) {
  const [scroller, setScroller] = useState(null);
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
  const rowRenderer = ({ key, rowIndex, style }) => {
    const book = books[rowIndex];

    return (
      <VirtualTableRow
        key={key}
        style={style}
      >
        <SeriesBookRow
          book={book}
          onMonitorBookPress={onMonitorBookPress}
        />
      </VirtualTableRow>
    );
  };

  return (
    <PageContent title={item?.title || 'Series'}>
      <PageContentBody registerScroller={setScroller}>
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

              {
                scroller &&
                  <div className={styles.books}>
                    <VirtualTable
                      className={styles.tableContainer}
                      items={books}
                      scroller={scroller}
                      isSmallScreen={false}
                      rowHeight={42}
                      rowRenderer={rowRenderer}
                      header={<SeriesBookTableHeader />}
                    />
                  </div>
              }

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
