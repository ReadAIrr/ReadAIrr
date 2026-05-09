import PropTypes from 'prop-types';
import React, { useState } from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSearchInput from 'Components/Page/Toolbar/PageToolbarSearchInput';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import VirtualTable from 'Components/Table/VirtualTable';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableRow from 'Components/Table/VirtualTableRow';
import { align, icons, kinds, sortDirections } from 'Helpers/Props';
import SeriesIndexFilterMenu from './Menus/SeriesIndexFilterMenu';
import SeriesIndexSortMenu from './Menus/SeriesIndexSortMenu';
import SeriesIndexViewMenu from './Menus/SeriesIndexViewMenu';
import styles from './SeriesIndex.css';

function getCompleteness(series) {
  return series.completeness || {};
}

function getTotalBooks(series) {
  return getCompleteness(series).totalBooks || 0;
}

function getAvailableBooks(series) {
  return getCompleteness(series).availableBooks || 0;
}

function getMissingBooks(series) {
  return getCompleteness(series).missingBooks || 0;
}

function getUnmonitoredBooks(series) {
  return getCompleteness(series).unmonitoredBooks || 0;
}

function getAuthorNames(series) {
  const books = series.books || [];
  const names = books.map((book) => book.authorName).filter(Boolean);

  return [...new Set(names)];
}

function isSeriesMonitored(series) {
  const books = series.books || [];

  return books.length > 0 && books.every((book) => book.monitored);
}

function getProgress(series) {
  const totalBooks = getTotalBooks(series);

  if (!totalBooks) {
    return 0;
  }

  return Math.round((getAvailableBooks(series) / totalBooks) * 100);
}

function getSeriesStatus(series) {
  const missingBooks = getMissingBooks(series);

  if (!getTotalBooks(series)) {
    return 'Empty';
  }

  if (!missingBooks) {
    return 'Complete';
  }

  return `${missingBooks} Missing`;
}

function SeriesAuthors({ series }) {
  const authorNames = getAuthorNames(series);

  if (!authorNames.length) {
    return <span className={styles.muted}>No authors</span>;
  }

  return (
    <span>
      {authorNames.slice(0, 3).join(', ')}
      {
        authorNames.length > 3 &&
          <span className={styles.muted}> +{authorNames.length - 3}</span>
      }
    </span>
  );
}

SeriesAuthors.propTypes = {
  series: PropTypes.object.isRequired
};

function SeriesProgress({ series }) {
  const progress = getProgress(series);

  return (
    <div className={styles.progressWrap}>
      <div className={styles.progressTrack}>
        <div
          className={styles.progressFill}
          style={{ width: `${progress}%` }}
        />
      </div>

      <span className={styles.progressText}>
        {progress}%
      </span>
    </div>
  );
}

SeriesProgress.propTypes = {
  series: PropTypes.object.isRequired
};

function getColumnClassName(name, isHeader = false) {
  const suffix = isHeader ? 'HeaderCell' : 'Cell';

  return styles[`${name}${suffix}`] || styles[isHeader ? 'headerCell' : 'cell'];
}

function SeriesTableRow({ series, columns }) {
  return (
    <>
      {
        columns.map((column) => {
          const {
            name,
            isVisible
          } = column;

          if (!isVisible) {
            return null;
          }

          if (name === 'title') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.titleCell}
              >
                <Link to={`/series/${series.id}`}>
                  {series.title}
                </Link>
                <SeriesProgress series={series} />
              </VirtualTableRowCell>
            );
          }

          if (name === 'authors') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.authorsCell}
              >
                <SeriesAuthors series={series} />
              </VirtualTableRowCell>
            );
          }

          if (name === 'totalBooks') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.numericCell}
              >
                {getTotalBooks(series)}
              </VirtualTableRowCell>
            );
          }

          if (name === 'availableBooks') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.numericCell}
              >
                {getAvailableBooks(series)}
              </VirtualTableRowCell>
            );
          }

          if (name === 'missingBooks') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.numericCell}
              >
                <span className={getMissingBooks(series) ? styles.warningText : undefined}>
                  {getMissingBooks(series)}
                </span>
              </VirtualTableRowCell>
            );
          }

          if (name === 'monitored') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.monitoredCell}
              >
                <Icon
                  name={isSeriesMonitored(series) ? icons.MONITORED : icons.UNMONITORED}
                  title={isSeriesMonitored(series) ? 'Monitored' : 'Unmonitored'}
                />
              </VirtualTableRowCell>
            );
          }

          if (name === 'authorCount') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.numericCell}
              >
                {getAuthorNames(series).length}
              </VirtualTableRowCell>
            );
          }

          if (name === 'unmonitoredBooks') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.numericCell}
              >
                {getUnmonitoredBooks(series)}
              </VirtualTableRowCell>
            );
          }

          if (name === 'actions') {
            return (
              <VirtualTableRowCell
                key={name}
                className={styles.actionsCell}
              >
                <IconButton
                  name={icons.ARROW_RIGHT}
                  title="Open series"
                  to={`/series/${series.id}`}
                />
              </VirtualTableRowCell>
            );
          }

          return null;
        })
      }
    </>
  );
}

SeriesTableRow.propTypes = {
  series: PropTypes.object.isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired
};

function SeriesTableHeader(props) {
  const {
    columns,
    sortKey,
    sortDirection,
    onSortSelect
  } = props;

  return (
    <VirtualTableHeader>
      {
        columns.map((column) => {
          const {
            name,
            label,
            columnLabel,
            isSortable,
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
              isSortable={isSortable}
              sortKey={sortKey}
              sortDirection={sortDirection}
              onSortPress={onSortSelect}
            >
              {columnLabel || label || ''}
            </VirtualTableHeaderCell>
          );
        })
      }
    </VirtualTableHeader>
  );
}

SeriesTableHeader.propTypes = {
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.oneOf(sortDirections.all),
  onSortSelect: PropTypes.func.isRequired
};

function SeriesTableView(props) {
  const {
    items,
    columns,
    sortKey,
    sortDirection,
    isSmallScreen,
    scroller,
    onSortSelect
  } = props;

  const rowRenderer = ({ key, rowIndex, style }) => {
    const series = items[rowIndex];

    return (
      <VirtualTableRow
        key={key}
        style={style}
      >
        <SeriesTableRow
          series={series}
          columns={columns}
        />
      </VirtualTableRow>
    );
  };

  return (
    <VirtualTable
      className={styles.tableContainer}
      items={items}
      columns={columns}
      scroller={scroller}
      isSmallScreen={isSmallScreen}
      rowHeight={64}
      rowRenderer={rowRenderer}
      sortKey={sortKey}
      sortDirection={sortDirection}
      header={
        <SeriesTableHeader
          columns={columns}
          sortKey={sortKey}
          sortDirection={sortDirection}
          onSortSelect={onSortSelect}
        />
      }
    />
  );
}

SeriesTableView.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.oneOf(sortDirections.all),
  isSmallScreen: PropTypes.bool.isRequired,
  scroller: PropTypes.instanceOf(Element).isRequired,
  onSortSelect: PropTypes.func.isRequired
};

function SeriesOverviewView({ items }) {
  return (
    <div className={styles.overviewList}>
      {
        items.map((series) => {
          return (
            <Link
              key={series.id}
              className={styles.overviewItem}
              to={`/series/${series.id}`}
            >
              <div className={styles.overviewMain}>
                <div className={styles.overviewLabel}>
                  Series
                </div>

                <div className={styles.seriesTitle}>
                  {series.title}
                </div>
              </div>

              <div className={styles.overviewAuthors}>
                <div className={styles.overviewLabel}>
                  Authors
                </div>

                <div className={styles.seriesMeta}>
                  <SeriesAuthors series={series} />
                </div>
              </div>

              <div className={styles.overviewStats}>
                <div>
                  <span>{getAvailableBooks(series)}</span>
                  Available
                </div>
                <div>
                  <span>{getMissingBooks(series)}</span>
                  Missing
                </div>
                <div>
                  <span>{getTotalBooks(series)}</span>
                  Books
                </div>
              </div>

              <div className={styles.overviewStatus}>
                <SeriesProgress series={series} />
                <div className={getMissingBooks(series) ? styles.warningText : styles.availableText}>
                  {getSeriesStatus(series)}
                </div>
              </div>
            </Link>
          );
        })
      }
    </div>
  );
}

SeriesOverviewView.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired
};

function SeriesPosterView({ items }) {
  return (
    <div className={styles.posterGrid}>
      {
        items.map((series) => {
          const progress = getProgress(series);

          return (
            <Link
              key={series.id}
              className={styles.posterItem}
              to={`/series/${series.id}`}
            >
              <div className={styles.posterArt}>
                <span>{series.title.charAt(0).toUpperCase()}</span>
              </div>

              <div className={styles.posterBody}>
                <div className={styles.posterTitle}>
                  {series.title}
                </div>

                <div className={styles.seriesMeta}>
                  <SeriesAuthors series={series} />
                </div>

                <SeriesProgress series={series} />

                <div className={styles.posterFooter}>
                  <span>{getAvailableBooks(series)}/{getTotalBooks(series)}</span>
                  <span>{progress}%</span>
                </div>
              </div>
            </Link>
          );
        })
      }
    </div>
  );
}

SeriesPosterView.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired
};

function getViewComponent(view) {
  if (view === 'posters') {
    return SeriesPosterView;
  }

  if (view === 'table') {
    return SeriesTableView;
  }

  return SeriesOverviewView;
}

function SeriesIndex(props) {
  const [scroller, setScroller] = useState(null);
  const {
    isFetching,
    isPopulated,
    error,
    totalItems,
    items,
    columns,
    searchTerm,
    selectedFilterKey,
    filters,
    customFilters,
    sortKey,
    sortDirection,
    view,
    isSmallScreen,
    onSortSelect,
    onFilterSelect,
    onSearchTermChange,
    onViewSelect,
    onTableOptionChange
  } = props;

  const hasNoSeries = !totalItems;
  const isLoaded = !isFetching && !error && isPopulated;
  const ViewComponent = getViewComponent(view);

  return (
    <PageContent>
      <PageToolbar>
        <PageToolbarSection />

        <PageToolbarSection
          alignContent={align.RIGHT}
          collapseButtons={false}
        >
          <PageToolbarSearchInput
            name="seriesIndexSearch"
            value={searchTerm}
            placeholder="Filter series"
            isDisabled={hasNoSeries}
            onChange={onSearchTermChange}
          />

          <PageToolbarSeparator />

          {
            view === 'table' ?
              <TableOptionsModalWrapper
                columns={columns}
                onTableOptionChange={onTableOptionChange}
              >
                <PageToolbarButton
                  label="Options"
                  iconName={icons.TABLE}
                />
              </TableOptionsModalWrapper> :
              null
          }

          {
            view === 'table' ?
              <PageToolbarSeparator /> :
              null
          }

          <SeriesIndexViewMenu
            view={view}
            isDisabled={hasNoSeries}
            onViewSelect={onViewSelect}
          />

          <SeriesIndexSortMenu
            sortKey={sortKey}
            sortDirection={sortDirection}
            isDisabled={hasNoSeries}
            onSortSelect={onSortSelect}
          />

          <SeriesIndexFilterMenu
            selectedFilterKey={selectedFilterKey}
            filters={filters}
            customFilters={customFilters}
            isDisabled={hasNoSeries}
            onFilterSelect={onFilterSelect}
          />
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody registerScroller={setScroller}>
        {
          isFetching && !isPopulated &&
            <LoadingIndicator />
        }

        {
          !isFetching && error &&
            <Alert kind={kinds.DANGER}>
              Unable to load series.
            </Alert>
        }

        {
          isLoaded && !!items.length && (view !== 'table' || scroller) &&
            <ViewComponent
              items={items}
              columns={columns}
              sortKey={sortKey}
              sortDirection={sortDirection}
              isSmallScreen={isSmallScreen}
              scroller={scroller}
              onSortSelect={onSortSelect}
              onTableOptionChange={onTableOptionChange}
            />
        }

        {
          isLoaded && !items.length &&
            <div className={styles.emptyMessage}>
              {
                hasNoSeries ?
                  'No series have been added yet.' :
                  'No series match the current search or filter.'
              }
            </div>
        }
      </PageContentBody>
    </PageContent>
  );
}

SeriesIndex.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  totalItems: PropTypes.number.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  searchTerm: PropTypes.string,
  selectedFilterKey: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
  filters: PropTypes.arrayOf(PropTypes.object).isRequired,
  customFilters: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.oneOf(sortDirections.all),
  view: PropTypes.string.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  onSortSelect: PropTypes.func.isRequired,
  onFilterSelect: PropTypes.func.isRequired,
  onSearchTermChange: PropTypes.func.isRequired,
  onViewSelect: PropTypes.func.isRequired,
  onTableOptionChange: PropTypes.func.isRequired
};

export default SeriesIndex;
