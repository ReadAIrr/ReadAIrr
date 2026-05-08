/* eslint max-params: 0 */
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import withScrollPosition from 'Components/withScrollPosition';
import { fetchBooks } from 'Store/Actions/bookActions';
import { saveBookEditor, setBookFilter, setBookSort, setBookTableOption, setBookView } from 'Store/Actions/bookIndexActions';
import { executeCommand } from 'Store/Actions/commandActions';
import scrollPositions from 'Store/scrollPositions';
import createBookClientSideCollectionItemsSelector from 'Store/Selectors/createBookClientSideCollectionItemsSelector';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import BookIndex from './BookIndex';

function createMapStateToProps() {
  return createSelector(
    createBookClientSideCollectionItemsSelector('bookIndex'),
    createCommandExecutingSelector(commandNames.BULK_REFRESH_AUTHOR),
    createCommandExecutingSelector(commandNames.BULK_REFRESH_BOOK),
    createCommandExecutingSelector(commandNames.RSS_SYNC),
    createCommandExecutingSelector(commandNames.CUTOFF_UNMET_BOOK_SEARCH),
    createCommandExecutingSelector(commandNames.MISSING_BOOK_SEARCH),
    createDimensionsSelector(),
    (
      book,
      isRefreshingAuthorCommand,
      isRefreshingBookCommand,
      isRssSyncExecuting,
      isCutoffBooksSearch,
      isMissingBooksSearch,
      dimensionsState
    ) => {
      const isRefreshingBook = isRefreshingBookCommand || isRefreshingAuthorCommand;
      return {
        ...book,
        isRefreshingBook,
        isRssSyncExecuting,
        isSearching: isCutoffBooksSearch || isMissingBooksSearch,
        isSmallScreen: dimensionsState.isSmallScreen
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onTableOptionChange(payload) {
      dispatch(setBookTableOption(payload));

      if (payload.pageSize) {
        dispatch(fetchBooks({ paged: true, page: 1 }));
      }
    },

    onSortSelect(sortKey) {
      dispatch(setBookSort({ sortKey }));
      dispatch(fetchBooks({ paged: true, page: 1, sortKey }));
    },

    onFilterSelect(selectedFilterKey) {
      dispatch(setBookFilter({ selectedFilterKey }));
      dispatch(fetchBooks({ paged: true, page: 1 }));
    },

    onPageSelect(page) {
      dispatch(fetchBooks({ paged: true, page }));
    },

    dispatchSetBookView(view) {
      dispatch(setBookView({ view }));
    },

    dispatchSaveBookEditor(payload) {
      dispatch(saveBookEditor(payload));
    },

    onRefreshBookPress(items) {
      dispatch(executeCommand({
        name: commandNames.BULK_REFRESH_BOOK,
        bookIds: items
      }));
    },

    onRssSyncPress() {
      dispatch(executeCommand({
        name: commandNames.RSS_SYNC
      }));
    },

    onSearchPress(items) {
      dispatch(executeCommand({
        name: commandNames.BOOK_SEARCH,
        bookIds: items
      }));
    }
  };
}

class BookIndexConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.onPageSelect(this.props.page || 1);
  }

  //
  // Listeners

  onViewSelect = (view) => {
    this.props.dispatchSetBookView(view);
  };

  onSaveSelected = (payload) => {
    this.props.dispatchSaveBookEditor(payload);
  };

  onScroll = ({ scrollTop }) => {
    scrollPositions.bookIndex = scrollTop;
  };

  onFirstPagePress = () => {
    this.props.onPageSelect(1);
  };

  onPreviousPagePress = () => {
    this.props.onPageSelect(Math.max((this.props.page || 1) - 1, 1));
  };

  onNextPagePress = () => {
    this.props.onPageSelect(Math.min((this.props.page || 1) + 1, this.props.totalPages || 1));
  };

  onLastPagePress = () => {
    this.props.onPageSelect(this.props.totalPages || 1);
  };

  //
  // Render

  render() {
    return (
      <BookIndex
        {...this.props}
        onViewSelect={this.onViewSelect}
        onScroll={this.onScroll}
        onSaveSelected={this.onSaveSelected}
        onFirstPagePress={this.onFirstPagePress}
        onPreviousPagePress={this.onPreviousPagePress}
        onNextPagePress={this.onNextPagePress}
        onLastPagePress={this.onLastPagePress}
      />
    );
  }
}

BookIndexConnector.propTypes = {
  page: PropTypes.number,
  totalPages: PropTypes.number,
  isSmallScreen: PropTypes.bool.isRequired,
  view: PropTypes.string.isRequired,
  onPageSelect: PropTypes.func.isRequired,
  dispatchSetBookView: PropTypes.func.isRequired,
  dispatchSaveBookEditor: PropTypes.func.isRequired
};

export default withScrollPosition(
  connect(createMapStateToProps, createMapDispatchToProps)(BookIndexConnector),
  'bookIndex'
);
