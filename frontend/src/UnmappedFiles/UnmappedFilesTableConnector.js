import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import withCurrentPage from 'Components/withCurrentPage';
import { aiReviewUnmappedFiles, clearUnmappedSuggestions, deepIdentifyUnmappedFiles, deleteBookFile, deleteBookFiles, fetchBookFiles, retryUnmappedFiles, setBookFilesSort, setBookFilesTableOption, setUnmappedFilesReviewed } from 'Store/Actions/bookFileActions';
import { executeCommand } from 'Store/Actions/commandActions';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import { registerPagePopulator, unregisterPagePopulator } from 'Utilities/pagePopulator';
import UnmappedFilesTable from './UnmappedFilesTable';

function createMapStateToProps() {
  return createSelector(
    createClientSideCollectionSelector('bookFiles'),
    createCommandExecutingSelector(commandNames.RESCAN_FOLDERS),
    createCommandExecutingSelector(commandNames.DEEP_IDENTIFY_UNMAPPED_FILES),
    createDimensionsSelector(),
    (
      bookFiles,
      isScanningFolders,
      isDeepIdentifyAudioRunning,
      dimensionsState
    ) => {
      // bookFiles could pick up mapped entries via signalR so filter again here
      const {
        items,
        ...otherProps
      } = bookFiles;

      const unmappedFiles = _.filter(items, { bookId: 0 });

      return {
        items: unmappedFiles,
        ...otherProps,
        isScanningFolders,
        isDeepIdentifyAudioRunning,
        isSmallScreen: dimensionsState.isSmallScreen
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onTableOptionChange(payload) {
      dispatch(setBookFilesTableOption(payload));
    },

    onSortPress(sortKey) {
      dispatch(setBookFilesSort({ sortKey }));
    },

    fetchUnmappedFiles() {
      dispatch(fetchBookFiles({ unmapped: true }));
    },

    deleteUnmappedFile(id) {
      dispatch(deleteBookFile({ id }));
    },

    deleteUnmappedFiles(bookFileIds) {
      dispatch(deleteBookFiles({ bookFileIds }));
    },

    onAddMissingAuthorsPress() {
      dispatch(executeCommand({
        name: commandNames.RESCAN_FOLDERS,
        addNewAuthors: true,
        filter: 'matched'
      }));
    },

    onRetryIdentifyPress(bookFileIds) {
      dispatch(retryUnmappedFiles({ bookFileIds }));
    },

    onAiReviewPress(bookFileIds) {
      dispatch(aiReviewUnmappedFiles({ bookFileIds }));
    },

    onDeepIdentifyPress(bookFileIds) {
      dispatch(deepIdentifyUnmappedFiles({ bookFileIds }));
    },

    onClearSuggestionsPress(bookFileIds) {
      dispatch(clearUnmappedSuggestions({ bookFileIds }));
    },

    setUnmappedFilesReviewed(bookFileIds, reviewed) {
      dispatch(setUnmappedFilesReviewed({ bookFileIds, reviewed }));
    }
  };
}

class UnmappedFilesTableConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    registerPagePopulator(this.repopulate, ['bookFileUpdated']);

    this.repopulate();
  }

  componentWillUnmount() {
    unregisterPagePopulator(this.repopulate);
  }

  //
  // Control

  repopulate = () => {
    this.props.fetchUnmappedFiles();
  };

  //
  // Render

  render() {
    return (
      <UnmappedFilesTable
        {...this.props}
      />
    );
  }
}

UnmappedFilesTableConnector.propTypes = {
  isSmallScreen: PropTypes.bool.isRequired,
  isDeepIdentifyAudioRunning: PropTypes.bool.isRequired,
  onSortPress: PropTypes.func.isRequired,
  onTableOptionChange: PropTypes.func.isRequired,
  fetchUnmappedFiles: PropTypes.func.isRequired,
  deleteUnmappedFile: PropTypes.func.isRequired,
  deleteUnmappedFiles: PropTypes.func.isRequired,
  onRetryIdentifyPress: PropTypes.func.isRequired,
  onAiReviewPress: PropTypes.func.isRequired,
  onDeepIdentifyPress: PropTypes.func.isRequired,
  onClearSuggestionsPress: PropTypes.func.isRequired,
  setUnmappedFilesReviewed: PropTypes.func.isRequired
};

export default withCurrentPage(
  connect(createMapStateToProps, createMapDispatchToProps)(UnmappedFilesTableConnector)
);
