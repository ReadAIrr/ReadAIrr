import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSearchInput from 'Components/Page/Toolbar/PageToolbarSearchInput';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TablePager from 'Components/Table/TablePager';
import { align, icons, kinds, sortDirections } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import ActivityOverviewRow from './ActivityOverviewRow';

const columns = [
  { name: 'time', label: 'Time', isVisible: true, isSortable: true },
  { name: 'category', label: 'Category', isVisible: true, isSortable: true },
  { name: 'level', label: 'Level', isVisible: true, isSortable: true },
  { name: 'status', label: 'Status', isVisible: true, isSortable: true },
  { name: 'source', label: 'Source', isVisible: true, isSortable: false },
  { name: 'title', label: 'Title', isVisible: true, isSortable: true },
  { name: 'message', label: 'Message', isVisible: true, isSortable: false }
];

const filters = [
  { key: 'all', label: 'All' },
  { key: 'history', label: 'History' },
  { key: 'command', label: 'Commands' },
  { key: 'queue', label: 'Queue' },
  { key: 'health', label: 'Health' },
  { key: 'log', label: 'Warnings' }
];

class ActivityOverview extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetching: false,
      isPopulated: false,
      error: null,
      records: [],
      includedSources: [],
      deferredSources: [],
      safetyNote: '',
      page: 1,
      pageSize: 20,
      totalRecords: 0,
      sortKey: 'time',
      sortDirection: sortDirections.DESCENDING,
      selectedFilterKey: 'all',
      searchTerm: ''
    };
  }

  componentDidMount() {
    this.fetchActivity();
  }

  fetchActivity = () => {
    const {
      page,
      pageSize,
      sortKey,
      sortDirection,
      selectedFilterKey,
      searchTerm
    } = this.state;

    this.setState({ isFetching: true });

    const data = {
      page,
      pageSize,
      sortKey,
      sortDirection
    };

    if (selectedFilterKey !== 'all') {
      data.category = selectedFilterKey;
    }

    if (searchTerm) {
      data.term = searchTerm;
    }

    const promise = createAjaxRequest({
      url: '/activity',
      data
    }).request;

    promise.done((response) => {
      this.setState({
        isFetching: false,
        isPopulated: true,
        error: null,
        records: response.records || [],
        includedSources: response.includedSources || [],
        deferredSources: response.deferredSources || [],
        safetyNote: response.safetyNote || '',
        page: response.page,
        pageSize: response.pageSize,
        totalRecords: response.totalRecords,
        sortKey: response.sortKey,
        sortDirection: response.sortDirection
      });
    });

    promise.fail((xhr) => {
      this.setState({
        isFetching: false,
        isPopulated: false,
        error: xhr
      });
    });
  };

  onRefreshPress = () => {
    this.fetchActivity();
  };

  onFirstPagePress = () => {
    this.setState({ page: 1 }, this.fetchActivity);
  };

  onPreviousPagePress = () => {
    this.setState(({ page }) => ({ page: Math.max(1, page - 1) }), this.fetchActivity);
  };

  onNextPagePress = () => {
    this.setState(({ page }) => ({ page: page + 1 }), this.fetchActivity);
  };

  onLastPagePress = () => {
    const totalPages = this.getTotalPages();
    this.setState({ page: totalPages }, this.fetchActivity);
  };

  onPageSelect = (page) => {
    this.setState({ page }, this.fetchActivity);
  };

  onSortPress = (sortKey) => {
    this.setState((state) => {
      const sortDirection = state.sortKey === sortKey && state.sortDirection === sortDirections.ASCENDING ?
        sortDirections.DESCENDING :
        sortDirections.ASCENDING;

      return {
        page: 1,
        sortKey,
        sortDirection
      };
    }, this.fetchActivity);
  };

  onFilterSelect = (selectedFilterKey) => {
    this.setState({ page: 1, selectedFilterKey }, this.fetchActivity);
  };

  onSearchTermChange = (searchTerm) => {
    this.setState({ page: 1, searchTerm }, this.fetchActivity);
  };

  getTotalPages() {
    const {
      pageSize,
      totalRecords
    } = this.state;

    return Math.max(1, Math.ceil(totalRecords / pageSize));
  }

  render() {
    const {
      isFetching,
      isPopulated,
      error,
      records,
      includedSources,
      deferredSources,
      safetyNote,
      page,
      totalRecords,
      sortKey,
      sortDirection,
      selectedFilterKey,
      searchTerm
    } = this.state;
    const totalPages = this.getTotalPages();

    return (
      <PageContent title="Activity">
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('Refresh')}
              iconName={icons.REFRESH}
              isSpinning={isFetching}
              onPress={this.onRefreshPress}
            />
          </PageToolbarSection>

          <PageToolbarSection alignContent={align.RIGHT}>
            <PageToolbarSearchInput
              name="activitySearch"
              value={searchTerm}
              placeholder="Filter activity"
              onChange={this.onSearchTermChange}
            />

            <PageToolbarSeparator />

            <FilterMenu
              alignMenu={align.RIGHT}
              selectedFilterKey={selectedFilterKey}
              filters={filters}
              customFilters={[]}
              onFilterSelect={this.onFilterSelect}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody>
          {
            isFetching && !isPopulated &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <Alert kind={kinds.DANGER}>
                Unable to load activity.
              </Alert>
          }

          {
            isPopulated && safetyNote &&
              <Alert kind={kinds.INFO}>
                {safetyNote}
              </Alert>
          }

          {
            isPopulated && !error && !records.length &&
              <Alert kind={kinds.INFO}>
                {
                  searchTerm || selectedFilterKey !== 'all' ?
                    'No activity matches the current filters.' :
                    'No recent activity was found.'
                }
              </Alert>
          }

          {
            isPopulated && !error && !!records.length &&
              <div>
                <Table
                  columns={columns}
                  sortKey={sortKey}
                  sortDirection={sortDirection}
                  onSortPress={this.onSortPress}
                >
                  <TableBody>
                    {
                      records.map((item) => {
                        return (
                          <ActivityOverviewRow
                            key={item.id}
                            columns={columns}
                            {...item}
                          />
                        );
                      })
                    }
                  </TableBody>
                </Table>

                <TablePager
                  page={page}
                  totalPages={totalPages}
                  totalRecords={totalRecords}
                  isFetching={isFetching}
                  onFirstPagePress={this.onFirstPagePress}
                  onPreviousPagePress={this.onPreviousPagePress}
                  onNextPagePress={this.onNextPagePress}
                  onLastPagePress={this.onLastPagePress}
                  onPageSelect={this.onPageSelect}
                />
              </div>
          }

          {
            isPopulated && (includedSources.length > 0 || deferredSources.length > 0) &&
              <Alert kind={kinds.INFO}>
                Included: {includedSources.join(', ')}. Deferred: {deferredSources.join(', ')}.
              </Alert>
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

ActivityOverview.propTypes = {
  location: PropTypes.object
};

export default ActivityOverview;
