import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
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

class UnmappedFilesTable extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      scroller: null,
      allSelected: false,
      allUnselected: false,
      lastToggled: null,
      selectedState: {},
      ignoredIds: [],
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
      ignoredIds
    } = this.state;

    return this.props.items.filter((item) => {
      return ignoredIds.indexOf(item.id) === -1;
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

  getSelectedFolders = () => {
    return [...new Set(this.getSelectedItems().map((item) => {
      const {
        path
      } = item;

      return path.substring(0, Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\')));
    }))];
  };

  onIgnoreSelectedPress = () => {
    const selectedIds = this.getSelectedIds();

    this.setState((state) => {
      return {
        ignoredIds: [...new Set([...state.ignoredIds, ...selectedIds])],
        ...selectAll(state.selectedState, false)
      };
    });
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
    this.props.onRetryIdentifyPress(this.getSelectedFolders());
  };

  rowRenderer = ({ key, rowIndex, style }) => {
    const {
      columns,
      deleteUnmappedFile
    } = this.props;

    const {
      selectedState
    } = this.state;

    const item = this.getVisibleItems()[rowIndex];

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
      error,
      items,
      columns,
      sortKey,
      sortDirection,
      onTableOptionChange,
      onSortPress,
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
      manualMatchFolder
    } = this.state;

    const selectedTrackFileIds = this.getSelectedIds();
    const visibleItems = this.getVisibleItems();

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
              label="Retry Identify"
              iconName={icons.REFRESH}
              isDisabled={selectedTrackFileIds.length === 0}
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
                Success! My work is done, all files on disk are matched to known books.
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
  isScanningFolders: PropTypes.bool.isRequired,
  onAddMissingAuthorsPress: PropTypes.func.isRequired,
  onRetryIdentifyPress: PropTypes.func.isRequired
};

export default UnmappedFilesTable;
