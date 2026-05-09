import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import SelectedMenuItem from 'Components/Menu/SelectedMenuItem';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSearchInput from 'Components/Page/Toolbar/PageToolbarSearchInput';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import TablePager from 'Components/Table/TablePager';
import VirtualTable from 'Components/Table/VirtualTable';
import VirtualTableRow from 'Components/Table/VirtualTableRow';
import { align, icons, kinds, sortDirections } from 'Helpers/Props';
import InteractiveImportModal from 'InteractiveImport/InteractiveImportModal';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import UnmappedFilesTableHeader from './UnmappedFilesTableHeader';
import UnmappedFilesTableRow from './UnmappedFilesTableRow';

const triageFilterOptions = {
  ALL: 'all',
  NEEDS_REVIEW: 'needsReview',
  LOW_CONFIDENCE: 'lowConfidence',
  NO_CANDIDATE: 'noCandidate',
  NO_EDITION: 'noEdition',
  METADATA_MISMATCH: 'metadataMismatch',
  REVIEWED: 'reviewed'
};

const triageFilterLabels = {
  [triageFilterOptions.ALL]: 'All unmapped',
  [triageFilterOptions.NEEDS_REVIEW]: 'Needs review',
  [triageFilterOptions.LOW_CONFIDENCE]: 'Low confidence',
  [triageFilterOptions.NO_CANDIDATE]: 'No candidate',
  [triageFilterOptions.NO_EDITION]: 'No edition',
  [triageFilterOptions.METADATA_MISMATCH]: 'Metadata mismatch',
  [triageFilterOptions.REVIEWED]: 'Reviewed'
};

function valueContainsSearchTerm(value, term) {
  if (value == null) {
    return false;
  }

  if (Array.isArray(value)) {
    return value.some((item) => valueContainsSearchTerm(item, term));
  }

  if (typeof value === 'object') {
    return Object.keys(value).some((key) => valueContainsSearchTerm(value[key], term));
  }

  return value.toString().toLowerCase().includes(term);
}

function getFileName(path) {
  if (!path) {
    return '';
  }

  return path.substring(Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\')) + 1);
}

function matchesSearchTerm(item, searchTerm) {
  const term = searchTerm.trim().toLowerCase();

  if (!term) {
    return true;
  }

  const path = item.path || item.relativePath;
  const review = item.review || {};

  return [
    path,
    getFileName(path),
    item.relativePath,
    item.fileName,
    item.parsedBookInfo,
    item.quality,
    item.language,
    review.status,
    review.reason,
    review.reasons,
    review.rejectionReasons,
    review.suggestions,
    review.candidate,
    review.candidates
  ].some((value) => valueContainsSearchTerm(value, term));
}

function getDeepIdentifySuggestion(item) {
  return item.review?.suggestions?.find((suggestion) => suggestion.type === 'deepAudio');
}

function getDeepIdentifyStatus(item) {
  const suggestion = getDeepIdentifySuggestion(item);

  if (!suggestion && item.isReprocessing) {
    return 'queued';
  }

  return suggestion?.stage || suggestion?.status;
}

function getDeepIdentifySummary(items, isDeepIdentifyAudioRunning) {
  const summary = {
    queued: 0,
    running: 0,
    complete: 0,
    failed: 0,
    skipped: 0
  };

  items.forEach((item) => {
    const status = getDeepIdentifyStatus(item);

    if (!status) {
      return;
    }

    if (status === 'queued') {
      summary.queued++;
      return;
    }

    if (status === 'extractingIntro' ||
        status === 'introClipReady' ||
        status === 'sendingToProvider' ||
        status === 'waitingForTranscription' ||
        status === 'parsingTranscript' ||
        item.isReprocessing) {
      summary.running++;
      return;
    }

    if (status === 'transcriptCaptured' || status === 'suggested' || status === 'providerReady') {
      summary.complete++;
      return;
    }

    if (status === 'skipped') {
      summary.skipped++;
      return;
    }

    if (status === 'disabled' || status === 'extractionFailed' || status === 'transcriptionFailed' || status === 'failed' || status === 'unavailable') {
      summary.failed++;
    }
  });

  const total = summary.queued + summary.running + summary.complete + summary.failed + summary.skipped;

  if (!total && !isDeepIdentifyAudioRunning) {
    return null;
  }

  return summary;
}

function formatDeepIdentifySummary(summary, isDeepIdentifyAudioRunning) {
  const parts = [
    summary.queued && `${summary.queued} queued`,
    summary.running && `${summary.running} running`,
    summary.complete && `${summary.complete} complete`,
    summary.failed && `${summary.failed} failed/disabled`,
    summary.skipped && `${summary.skipped} skipped`
  ].filter(Boolean);

  if (!parts.length && isDeepIdentifyAudioRunning) {
    return 'Deep Identify Audio is running. Refreshing unmapped file status while the background task progresses.';
  }

  return `Deep Identify Audio: ${parts.join(', ')}. Row status updates persist after refresh.`;
}

class UnmappedFilesTable extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._visibleItems = [];

    this.state = {
      scroller: null,
      allSelected: false,
      allUnselected: false,
      lastToggled: null,
      selectedState: {},
      searchTerm: '',
      triageFilter: triageFilterOptions.NEEDS_REVIEW,
      isManualMatchModalOpen: false,
      manualMatchFolder: null
    };
  }

  componentDidMount() {
    this.setSelectedState();
  }

  componentDidUpdate(prevProps) {
    const {
      items,
      pageSize,
      sortKey,
      sortDirection,
      isDeleting,
      deleteError
    } = this.props;

    if (sortKey !== prevProps.sortKey ||
      sortDirection !== prevProps.sortDirection ||
      hasDifferentItemsOrOrder(prevProps.items, items)
    ) {
      this.setSelectedState();
    }

    if (pageSize !== prevProps.pageSize) {
      this.props.fetchUnmappedFiles(1);
    }

    const hasFinishedDeleting = prevProps.isDeleting &&
                                !isDeleting &&
                                !deleteError;

    if (hasFinishedDeleting) {
      this.onSelectAllChange({ value: false });
    }
  }

  //
  // Control

  setScrollerRef = (ref) => {
    this.setState({ scroller: ref });
  };

  getSelectedIds = () => {
    if (this.state.allUnselected) {
      return [];
    }
    return getSelectedIds(this.state.selectedState);
  };

  setSelectedState() {
    const {
      items
    } = this.props;

    const {
      selectedState
    } = this.state;

    const newSelectedState = {};

    items.forEach((file) => {
      const isItemSelected = selectedState[file.id];

      if (isItemSelected) {
        newSelectedState[file.id] = isItemSelected;
      } else {
        newSelectedState[file.id] = false;
      }
    });

    const selectedCount = getSelectedIds(newSelectedState).length;
    const newStateCount = Object.keys(newSelectedState).length;
    let isAllSelected = false;
    let isAllUnselected = false;

    if (selectedCount === 0) {
      isAllUnselected = true;
    } else if (selectedCount === newStateCount) {
      isAllSelected = true;
    }

    this.setState({ selectedState: newSelectedState, allSelected: isAllSelected, allUnselected: isAllUnselected });
  }

  onSelectAllChange = ({ value }) => {
    this.setState(selectAll(this.state.selectedState, value));
  };

  onSelectAllPress = () => {
    this.onSelectAllChange({ value: !this.state.allSelected });
  };

  onSelectedChange = ({ id, value, shiftKey = false }) => {
    this.setState((state) => {
      return toggleSelected(state, this.props.items, id, value, shiftKey);
    });
  };

  onDeleteUnmappedFilesPress = () => {
    const selectedIds = this.getSelectedIds();

    this.props.deleteUnmappedFiles(selectedIds);
  };

  getSelectedItems = () => {
    const selectedIds = this.getSelectedIds();

    return this.props.items.filter((item) => {
      return selectedIds.indexOf(item.id) > -1;
    });
  };

  getVisibleItems = () => {
    const {
      searchTerm,
      triageFilter
    } = this.state;

    return this.props.items.filter((item) => {
      if (!matchesSearchTerm(item, searchTerm)) {
        return false;
      }

      const status = item.review?.status;
      const isReviewed = item.reviewed || status === 'reviewed';

      if (triageFilter === triageFilterOptions.ALL) {
        return true;
      }

      if (triageFilter === triageFilterOptions.NEEDS_REVIEW) {
        return !isReviewed;
      }

      if (triageFilter === triageFilterOptions.REVIEWED) {
        return isReviewed;
      }

      return status === triageFilter;
    });
  };

  getSelectedFolder = () => {
    const selectedItem = this.getSelectedItems()[0];

    if (!selectedItem) {
      return null;
    }

    const {
      path
    } = selectedItem;

    return path.substring(0, Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\')));
  };

  onIgnoreSelectedPress = () => {
    const selectedIds = this.getSelectedIds();

    this.props.setUnmappedFilesReviewed(selectedIds, true);
    this.setState(selectAll(this.state.selectedState, false));
  };

  onKeepReviewSelectedPress = () => {
    const selectedIds = this.getSelectedIds();

    this.props.setUnmappedFilesReviewed(selectedIds, false);
    this.setState(selectAll(this.state.selectedState, false));
  };

  onOpenManualMatchPress = () => {
    this.setState({
      isManualMatchModalOpen: true,
      manualMatchFolder: this.getSelectedFolder()
    });
  };

  onManualMatchModalClose = () => {
    this.setState({
      isManualMatchModalOpen: false,
      manualMatchFolder: null
    });

    this.props.fetchUnmappedFiles();
  };

  onRetryIdentifyPress = () => {
    this.props.onRetryIdentifyPress(this.getSelectedIds());
  };

  onAiReviewPress = () => {
    this.props.onAiReviewPress(this.getSelectedIds());
  };

  onDeepIdentifyPress = () => {
    this.props.onDeepIdentifyPress(this.getSelectedIds());
  };

  onClearSuggestionsPress = () => {
    this.props.onClearSuggestionsPress(this.getSelectedIds());
  };

  onTriageFilterChange = (triageFilter) => {
    this.setState({ triageFilter });
  };

  onSearchTermChange = (searchTerm) => {
    this.setState({ searchTerm });
  };

  onFirstPagePress = () => {
    this.props.fetchUnmappedFiles(1);
  };

  onPreviousPagePress = () => {
    this.props.fetchUnmappedFiles(Math.max(this.props.page - 1, 1));
  };

  onNextPagePress = () => {
    this.props.fetchUnmappedFiles(Math.min(this.props.page + 1, this.props.totalPages));
  };

  onLastPagePress = () => {
    this.props.fetchUnmappedFiles(this.props.totalPages);
  };

  onPageSelect = (page) => {
    this.props.fetchUnmappedFiles(page);
  };

  rowRenderer = ({ key, rowIndex, style }) => {
    const {
      columns,
      deleteUnmappedFile,
      onRetryIdentifyPress,
      onAiReviewPress,
      onDeepIdentifyPress,
      onClearSuggestionsPress,
      onSetContributorEvidencePress,
      setUnmappedFilesReviewed
    } = this.props;

    const {
      selectedState
    } = this.state;

    const item = this._visibleItems[rowIndex];

    return (
      <VirtualTableRow
        key={key}
        style={style}
      >
        <UnmappedFilesTableRow
          key={item.id}
          columns={columns}
          isSelected={selectedState[item.id]}
          onSelectedChange={this.onSelectedChange}
          deleteUnmappedFile={deleteUnmappedFile}
          retryUnmappedFile={onRetryIdentifyPress}
          aiReviewUnmappedFile={onAiReviewPress}
          deepIdentifyUnmappedFile={onDeepIdentifyPress}
          clearUnmappedSuggestions={onClearSuggestionsPress}
          setContributorEvidence={onSetContributorEvidencePress}
          setUnmappedFileReviewed={setUnmappedFilesReviewed}
          {...item}
        />
      </VirtualTableRow>
    );
  };

  render() {

    const {
      isFetching,
      isPopulated,
      isDeleting,
      isSaving,
      error,
      items,
      page,
      totalPages,
      totalRecords,
      columns,
      sortKey,
      sortDirection,
      onTableOptionChange,
      onSortPress,
      fetchUnmappedFiles,
      isScanningFolders,
      isDeepIdentifyAudioRunning,
      onAddMissingAuthorsPress,
      onAiReviewPress,
      onDeepIdentifyPress,
      onClearSuggestionsPress,
      onSetContributorEvidencePress,
      ...otherProps
    } = this.props;

    const {
      scroller,
      allSelected,
      allUnselected,
      selectedState,
      isManualMatchModalOpen,
      manualMatchFolder,
      searchTerm,
      triageFilter
    } = this.state;

    const selectedTrackFileIds = this.getSelectedIds();
    const visibleItems = this.getVisibleItems();
    const deepIdentifySummary = getDeepIdentifySummary(items, isDeepIdentifyAudioRunning);
    this._visibleItems = visibleItems;

    return (
      <PageContent title={translate('UnmappedFiles')}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('AddMissing')}
              iconName={icons.ADD_MISSING_AUTHORS}
              isDisabled={isPopulated && !error && !items.length}
              isSpinning={isScanningFolders}
              onPress={onAddMissingAuthorsPress}
            />
            <PageToolbarButton
              label="Ignore Selected"
              iconName={icons.IGNORE}
              isDisabled={selectedTrackFileIds.length === 0}
              onPress={this.onIgnoreSelectedPress}
            />
            <PageToolbarButton
              label="Keep Reviewing"
              iconName={icons.RESTORE}
              isDisabled={selectedTrackFileIds.length === 0}
              onPress={this.onKeepReviewSelectedPress}
            />
            <PageToolbarButton
              label="Retry Identify"
              iconName={icons.REFRESH}
              isDisabled={selectedTrackFileIds.length === 0}
              isSpinning={isSaving}
              onPress={this.onRetryIdentifyPress}
            />
            <PageToolbarButton
              label="Open Manual Match"
              iconName={icons.INTERACTIVE}
              isDisabled={selectedTrackFileIds.length === 0}
              onPress={this.onOpenManualMatchPress}
            />
            <PageToolbarButton
              label="AI Review"
              iconName={icons.QUICK}
              isDisabled={selectedTrackFileIds.length === 0}
              isSpinning={isSaving}
              onPress={this.onAiReviewPress}
            />
            <PageToolbarButton
              label="Deep Identify Audio"
              iconName={icons.TRACK_FILE}
              isDisabled={selectedTrackFileIds.length === 0}
              isSpinning={isSaving || isDeepIdentifyAudioRunning}
              onPress={this.onDeepIdentifyPress}
            />
            <PageToolbarButton
              label="Clear Suggestions"
              iconName={icons.CLEAR}
              isDisabled={selectedTrackFileIds.length === 0}
              isSpinning={isSaving}
              onPress={this.onClearSuggestionsPress}
            />
            <PageToolbarButton
              label={translate('DeleteSelected')}
              iconName={icons.DELETE}
              isDisabled={selectedTrackFileIds.length === 0}
              isSpinning={isDeleting}
              onPress={this.onDeleteUnmappedFilesPress}
            />
          </PageToolbarSection>

          <PageToolbarSection alignContent={align.RIGHT}>
            <PageToolbarSearchInput
              name="unmappedFilesSearch"
              value={searchTerm}
              placeholder="Filter loaded unmapped page"
              onChange={this.onSearchTermChange}
            />

            <PageToolbarSeparator />

            <Menu alignMenu={align.RIGHT}>
              <MenuButton>
                <Icon
                  name={icons.FILTER}
                  size={22}
                />

                <div>
                  {triageFilterLabels[triageFilter]}
                </div>
              </MenuButton>

              <MenuContent>
                {
                  Object.keys(triageFilterLabels).map((filter) => {
                    return (
                      <SelectedMenuItem
                        key={filter}
                        name={filter}
                        isSelected={triageFilter === filter}
                        onPress={this.onTriageFilterChange}
                      >
                        {triageFilterLabels[filter]}
                      </SelectedMenuItem>
                    );
                  })
                }
              </MenuContent>
            </Menu>

            <PageToolbarButton
              label={translate('Refresh')}
              iconName={icons.REFRESH}
              isSpinning={isFetching}
              onPress={fetchUnmappedFiles}
            />

            <TableOptionsModalWrapper
              {...otherProps}
              columns={columns}
              onTableOptionChange={onTableOptionChange}
            >
              <PageToolbarButton
                label={translate('Options')}
                iconName={icons.TABLE}
              />
            </TableOptionsModalWrapper>

          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody
          registerScroller={this.setScrollerRef}
        >
          {
            isFetching && !isPopulated &&
              <LoadingIndicator />
          }

          {
            isPopulated && !error && !visibleItems.length &&
              <Alert kind={kinds.INFO}>
                {
                  items.length ?
                    'No unmapped files match the current search or filter.' :
                    'Success! My work is done, all files on disk are matched to known books.'
                }
              </Alert>
          }

          {
            isPopulated && !error && !!items.length &&
              <Alert kind={kinds.INFO}>
                This page shows {items.length} loaded unmapped files out of {totalRecords}. Select all, search, filter, sorting, and row actions apply only to this loaded page.
              </Alert>
          }

          {
            isPopulated && !error && deepIdentifySummary &&
              <Alert kind={isDeepIdentifyAudioRunning || deepIdentifySummary.running || deepIdentifySummary.queued ? kinds.INFO : kinds.SUCCESS}>
                {formatDeepIdentifySummary(deepIdentifySummary, isDeepIdentifyAudioRunning)}
              </Alert>
          }

          {
            isPopulated && !error && !!visibleItems.length && scroller &&
              <VirtualTable
                items={visibleItems}
                columns={columns}
                scroller={scroller}
                isSmallScreen={false}
                overscanRowCount={10}
                rowHeight={54}
                rowRenderer={this.rowRenderer}
                header={
                  <UnmappedFilesTableHeader
                    columns={columns}
                    sortKey={sortKey}
                    sortDirection={sortDirection}
                    onTableOptionChange={onTableOptionChange}
                    onSortPress={onSortPress}
                    allSelected={allSelected}
                    allUnselected={allUnselected}
                    onSelectAllChange={this.onSelectAllChange}
                  />
                }
                selectedState={selectedState}
                sortKey={sortKey}
                sortDirection={sortDirection}
              />
          }

          {
            isPopulated && !error && totalPages > 1 &&
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
          }

          <InteractiveImportModal
            isOpen={isManualMatchModalOpen}
            folder={manualMatchFolder}
            showFilterExistingFiles={true}
            filterExistingFiles={false}
            showImportMode={false}
            showReplaceExistingFiles={false}
            replaceExistingFiles={false}
            onModalClose={this.onManualMatchModalClose}
          />
        </PageContentBody>
      </PageContent>
    );
  }
}

UnmappedFilesTable.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  isDeleting: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  deleteError: PropTypes.object,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  page: PropTypes.number.isRequired,
  pageSize: PropTypes.number.isRequired,
  totalPages: PropTypes.number.isRequired,
  totalRecords: PropTypes.number.isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.oneOf(sortDirections.all),
  onTableOptionChange: PropTypes.func.isRequired,
  onSortPress: PropTypes.func.isRequired,
  fetchUnmappedFiles: PropTypes.func.isRequired,
  deleteUnmappedFile: PropTypes.func.isRequired,
  deleteUnmappedFiles: PropTypes.func.isRequired,
  setUnmappedFilesReviewed: PropTypes.func.isRequired,
  isScanningFolders: PropTypes.bool.isRequired,
  isDeepIdentifyAudioRunning: PropTypes.bool.isRequired,
  onAddMissingAuthorsPress: PropTypes.func.isRequired,
  onRetryIdentifyPress: PropTypes.func.isRequired,
  onAiReviewPress: PropTypes.func.isRequired,
  onDeepIdentifyPress: PropTypes.func.isRequired,
  onClearSuggestionsPress: PropTypes.func.isRequired,
  onSetContributorEvidencePress: PropTypes.func.isRequired
};

export default UnmappedFilesTable;
