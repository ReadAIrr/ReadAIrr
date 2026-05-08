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
  METADATA_MISMATCH: 'metadataMismatch',
  REVIEWED: 'reviewed'
};

const triageFilterLabels = {
  [triageFilterOptions.ALL]: 'All unmapped',
  [triageFilterOptions.NEEDS_REVIEW]: 'Needs review',
  [triageFilterOptions.LOW_CONFIDENCE]: 'Low confidence',
  [triageFilterOptions.NO_CANDIDATE]: 'No candidate',
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
    review.candidate,
    review.candidates
  ].some((value) => valueContainsSearchTerm(value, term));
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

  onTriageFilterChange = (triageFilter) => {
    this.setState({ triageFilter });
  };

  onSearchTermChange = (searchTerm) => {
    this.setState({ searchTerm });
  };

  rowRenderer = ({ key, rowIndex, style }) => {
    const {
      columns,
      deleteUnmappedFile,
      onRetryIdentifyPress,
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
      columns,
      sortKey,
      sortDirection,
      onTableOptionChange,
      onSortPress,
      fetchUnmappedFiles,
      isScanningFolders,
      onAddMissingAuthorsPress,
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
              placeholder="Filter unmapped files"
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
            isPopulated && !error && !!visibleItems.length && scroller &&
              <VirtualTable
                items={visibleItems}
                columns={columns}
                scroller={scroller}
                isSmallScreen={false}
                overscanRowCount={10}
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
  onAddMissingAuthorsPress: PropTypes.func.isRequired,
  onRetryIdentifyPress: PropTypes.func.isRequired
};

export default UnmappedFilesTable;
