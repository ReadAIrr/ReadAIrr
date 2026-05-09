import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import SelectInput from 'Components/Form/SelectInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import SelectedMenuItem from 'Components/Menu/SelectedMenuItem';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import VirtualTable from 'Components/Table/VirtualTable';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableRow from 'Components/Table/VirtualTableRow';
import VirtualTableSelectAllHeaderCell from 'Components/Table/VirtualTableSelectAllHeaderCell';
import { align, icons, kinds, scrollDirections } from 'Helpers/Props';
import SelectAuthorModal from 'InteractiveImport/Author/SelectAuthorModal';
import SelectBookModal from 'InteractiveImport/Book/SelectBookModal';
import ConfirmImportModal from 'InteractiveImport/Confirmation/ConfirmImportModal';
import SelectEditionModal from 'InteractiveImport/Edition/SelectEditionModal';
import SelectIndexerFlagsModal from 'InteractiveImport/IndexerFlags/SelectIndexerFlagsModal';
import SelectQualityModal from 'InteractiveImport/Quality/SelectQualityModal';
import SelectReleaseGroupModal from 'InteractiveImport/ReleaseGroup/SelectReleaseGroupModal';
import { buildAddSearchLinks } from 'UnmappedFiles/unmappedAddSearchUtils';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import InteractiveImportRow, { VirtualTableRowCellButton } from './InteractiveImportRow';
import styles from './InteractiveImportModalContent.css';

const COLUMNS = [
  {
    name: 'path',
    label: 'Path',
    isSortable: true,
    isVisible: true
  },
  {
    name: 'author',
    label: 'Author',
    isSortable: true,
    isVisible: true
  },
  {
    name: 'book',
    label: 'Book',
    isVisible: true
  },
  {
    name: 'releaseGroup',
    label: 'Release Group',
    isVisible: true
  },
  {
    name: 'quality',
    label: 'Quality',
    isSortable: true,
    isVisible: true
  },
  {
    name: 'size',
    label: 'Size',
    isSortable: true,
    isVisible: true
  },
  {
    name: 'customFormats',
    label: React.createElement(Icon, {
      name: icons.INTERACTIVE,
      title: () => translate('CustomFormat')
    }),
    isSortable: true,
    isVisible: true
  },
  {
    name: 'indexerFlags',
    label: React.createElement(Icon, {
      name: icons.FLAG,
      title: () => translate('IndexerFlags')
    }),
    isSortable: true,
    isVisible: true
  },
  {
    name: 'rejections',
    label: React.createElement(Icon, {
      name: icons.DANGER,
      kind: kinds.DANGER,
      title: () => translate('Rejections')
    }),
    isSortable: true,
    isVisible: true
  }
];

const filterExistingFilesOptions = {
  ALL: 'all',
  NEW: 'new'
};

const importModeOptions = [
  { key: 'chooseImportMode', value: () => translate('ChooseImportMethod'), disabled: true },
  { key: 'move', value: () => translate('MoveFiles') },
  { key: 'copy', value: () => translate('HardlinkCopyFiles') }
];

const SELECT = 'select';
const AUTHOR = 'author';
const BOOK = 'book';
const EDITION = 'edition';
const RELEASE_GROUP = 'releaseGroup';
const QUALITY = 'quality';
const INDEXER_FLAGS = 'indexerFlags';

const replaceExistingFilesOptions = {
  COMBINE: 'combine',
  DELETE: 'delete'
};

function getNarratorEvidence(contributorEvidence, source) {
  return (contributorEvidence || []).find((item) => {
    return item.role === 'narrator' &&
      item.displayName &&
      (!source || item.source === source);
  });
}

function hasNarratorEvidence(contributorEvidence, displayName) {
  const normalized = displayName.trim().toLowerCase();

  return (contributorEvidence || []).some((item) => {
    return item.role === 'narrator' &&
      item.displayName &&
      item.displayName.trim().toLowerCase() === normalized;
  });
}

function isValidImportItem(item) {
  return !!(
    item.author &&
    item.book &&
    item.foreignEditionId &&
    item.quality &&
    item.size > 0
  );
}

function getColumnClassName(name) {
  return styles[`${name}HeaderCell`] || styles.headerCell;
}

function InteractiveImportTableHeader(props) {
  const {
    columns,
    allSelected,
    allUnselected,
    sortKey,
    sortDirection,
    onSortPress,
    onSelectAllChange
  } = props;

  return (
    <VirtualTableHeader>
      <VirtualTableSelectAllHeaderCell
        allSelected={allSelected}
        allUnselected={allUnselected}
        onSelectAllChange={onSelectAllChange}
      />

      {
        columns.map((column) => {
          const {
            name,
            label,
            isVisible,
            isSortable
          } = column;

          if (!isVisible) {
            return null;
          }

          return (
            <VirtualTableHeaderCell
              key={name}
              name={name}
              className={getColumnClassName(name)}
              isSortable={isSortable}
              sortKey={sortKey}
              sortDirection={sortDirection}
              onSortPress={onSortPress}
            >
              {label}
            </VirtualTableHeaderCell>
          );
        })
      }
    </VirtualTableHeader>
  );
}

InteractiveImportTableHeader.propTypes = {
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  allSelected: PropTypes.bool.isRequired,
  allUnselected: PropTypes.bool.isRequired,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.string,
  onSortPress: PropTypes.func.isRequired,
  onSelectAllChange: PropTypes.func.isRequired
};

class InteractiveImportModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      allSelected: false,
      allUnselected: false,
      lastToggled: null,
      selectedState: {},
      invalidRowsSelected: [],
      selectModalOpen: null,
      booksImported: [],
      isConfirmImportModalOpen: false,
      inconsistentBookReleases: false,
      acceptedNarratorEvidenceKey: null,
      scroller: null
    };
  }

  componentDidMount() {
    this.setSelectedState();
    this.applyAcceptedNarratorEvidence();
  }

  componentDidUpdate(prevProps) {
    const selectedIds = this.getSelectedIds();
    const selectedItems = _.filter(this.props.items, (x) => _.includes(selectedIds, x.id));

    const inconsistent = _(selectedItems)
      .map((x) => ({ bookId: x.book ? x.book.id : 0, foreignEditionId: x.foreignEditionId }))
      .groupBy('bookId')
      .mapValues((book) => _(book).groupBy((x) => x.foreignEditionId).values().value().length)
      .values()
      .some((x) => x !== undefined && x > 1);

    if (inconsistent !== this.state.inconsistentBookReleases) {
      this.setState({ inconsistentBookReleases: inconsistent });
    }

    if (
      prevProps.acceptedSuggestion !== this.props.acceptedSuggestion ||
      prevProps.acceptedPath !== this.props.acceptedPath ||
      prevProps.items !== this.props.items
    ) {
      this.applyAcceptedNarratorEvidence();
    }

    if (prevProps.items !== this.props.items) {
      this.setSelectedState();
    }
  }

  //
  // Control

  getSelectedIds = () => {
    return getSelectedIds(this.state.selectedState);
  };

  setScrollerRef = (ref) => {
    this.setState({ scroller: ref });
  };

  setSelectedState = () => {
    this.setState((state, props) => {
      const selectedState = {};

      props.items.forEach((item) => {
        selectedState[item.id] = state.selectedState.hasOwnProperty(item.id) ?
          state.selectedState[item.id] :
          isValidImportItem(item);
      });

      const selectedIds = getSelectedIds(selectedState);
      const selectedCount = selectedIds.length;
      const totalCount = Object.keys(selectedState).length;

      return {
        selectedState,
        allSelected: totalCount > 0 && selectedCount === totalCount,
        allUnselected: selectedCount === 0,
        invalidRowsSelected: props.items
          .filter((item) => selectedState[item.id] && !isValidImportItem(item))
          .map((item) => item.id)
      };
    });
  };

  applyAcceptedNarratorEvidence = () => {
    const {
      acceptedSuggestion,
      acceptedPath,
      items,
      onSetContributorEvidencePress
    } = this.props;

    const displayName = acceptedSuggestion?.narrator?.trim();

    if (!displayName || !acceptedPath) {
      return;
    }

    const acceptedItem = items.find((item) => item.path === acceptedPath);
    const review = acceptedItem?.review || {};
    const bookFileId = review.bookFileId;
    const contributorEvidence = review.contributorEvidence || [];
    const key = `${acceptedPath}|${bookFileId || 0}|${displayName}`;

    if (
      !acceptedItem ||
      !bookFileId ||
      !review.canEditContributorEvidence ||
      this.state.acceptedNarratorEvidenceKey === key ||
      getNarratorEvidence(contributorEvidence, 'manual') ||
      hasNarratorEvidence(contributorEvidence, displayName)
    ) {
      return;
    }

    this.setState({ acceptedNarratorEvidenceKey: key });

    onSetContributorEvidencePress({
      id: acceptedItem.id,
      bookFileId,
      role: 'narrator',
      displayName
    });
  };

  //
  // Listeners

  onSelectAllChange = ({ value }) => {
    this.setState(selectAll(this.state.selectedState, value));
  };

  onSelectedChange = ({ id, value, shiftKey = false }) => {
    this.setState((state) => {
      return toggleSelected(state, this.props.items, id, value, shiftKey);
    });
  };

  onValidRowChange = (id, isValid) => {
    this.setState((state, props) => {
      // make sure to exclude any invalidRows that are no longer present in props
      const diff = _.difference(state.invalidRowsSelected, _.map(props.items, 'id'));
      const currentInvalid = _.difference(state.invalidRowsSelected, diff);
      const newstate = isValid ? _.without(currentInvalid, id) : _.union(currentInvalid, [id]);
      return { invalidRowsSelected: newstate };
    });
  };

  onImportSelectedPress = () => {
    if (!this.props.replaceExistingFiles) {
      this.onConfirmImportPress();
      return;
    }

    // potentially deleting files
    const selectedIds = this.getSelectedIds();
    const booksImported = _(this.props.items)
      .filter((x) => _.includes(selectedIds, x.id))
      .keyBy((x) => x.book.id)
      .map((x) => x.book)
      .value();

    console.log(booksImported);

    this.setState({
      booksImported,
      isConfirmImportModalOpen: true
    });
  };

  onConfirmImportPress = () => {
    const {
      downloadId,
      showImportMode,
      importMode,
      onImportSelectedPress
    } = this.props;

    const selected = this.getSelectedIds();
    const finalImportMode = downloadId || !showImportMode ? 'auto' : importMode;

    onImportSelectedPress(selected, finalImportMode);
  };

  onFilterExistingFilesChange = (value) => {
    this.props.onFilterExistingFilesChange(value !== filterExistingFilesOptions.ALL);
  };

  onReplaceExistingFilesChange = (value) => {
    this.props.onReplaceExistingFilesChange(value === replaceExistingFilesOptions.DELETE);
  };

  onPreviousPagePress = () => {
    this.props.onPageChange(Math.max(this.props.page - 1, 1));
  };

  onNextPagePress = () => {
    this.props.onPageChange(this.props.page + 1);
  };

  onImportModeChange = ({ value }) => {
    this.props.onImportModeChange(value);
  };

  onSelectModalSelect = ({ value }) => {
    this.setState({ selectModalOpen: value });
  };

  onClearBookMappingPress = () => {
    const selectedIds = this.getSelectedIds();

    selectedIds.forEach((id) => {
      this.props.updateInteractiveImportItem({
        id,
        rejections: []
      });
    });
  };

  onGetBookMappingPress = () => {
    this.props.saveInteractiveImportItem({ ids: this.getSelectedIds() });
  };

  onIgnoreSelectedPress = () => {
    const selectedIds = this.getSelectedIds();

    this.props.removeInteractiveImportItems({ ids: selectedIds });
    this.setState(selectAll(this.state.selectedState, false));
  };

  onOpenManualMatchPress = () => {
    const selectedIds = this.getSelectedIds();
    const selectedItem = selectedIds.length ? _.find(this.props.items, { id: selectedIds[0] }) : null;

    this.setState({
      selectModalOpen: selectedItem?.author ? BOOK : AUTHOR
    });
  };

  onSelectModalClose = () => {
    this.setState({ selectModalOpen: null });
  };

  onConfirmImportModalClose = () => {
    this.setState({ isConfirmImportModalOpen: false });
  };

  rowRenderer = ({ key, rowIndex, style }) => {
    const {
      allowAuthorChange,
      items,
      isSaving,
      onSetContributorEvidencePress
    } = this.props;

    const item = items[rowIndex];
    const columns = this._columns || this.getColumns();

    return (
      <InteractiveImportRow
        key={key}
        style={style}
        component={VirtualTableRow}
        rowCellComponent={VirtualTableRowCell}
        rowCellButtonComponent={VirtualTableRowCellButton}
        selectCellComponent={VirtualTableSelectCell}
        selectOnMount={false}
        isSelected={this.state.selectedState[item.id]}
        isSaving={isSaving}
        {...item}
        allowAuthorChange={allowAuthorChange}
        columns={columns}
        onSelectedChange={this.onSelectedChange}
        onValidRowChange={this.onValidRowChange}
        onSetContributorEvidencePress={onSetContributorEvidencePress}
      />
    );
  };

  getColumns = () => {
    const allColumns = _.cloneDeep(COLUMNS);
    const showIndexerFlags = this.props.items.some((item) => item.indexerFlags);

    if (!showIndexerFlags) {
      const indexerFlagsColumn = allColumns.find((c) => c.name === 'indexerFlags');

      if (indexerFlagsColumn) {
        indexerFlagsColumn.isVisible = false;
      }
    }

    return allColumns;
  };

  //
  // Render

  render() {
    const {
      downloadId,
      allowAuthorChange,
      showFilterExistingFiles,
      showReplaceExistingFiles,
      showImportMode,
      filterExistingFiles,
      replaceExistingFiles,
      title,
      folder,
      isFetching,
      isPopulated,
      isSaving,
      error,
      items,
      page,
      pageSize,
      totalRecords,
      isPaged,
      sortKey,
      sortDirection,
      importMode,
      interactiveImportErrorMessage,
      acceptedSuggestion,
      acceptedPath,
      onSortPress,
      onModalClose
    } = this.props;

    const {
      allSelected,
      allUnselected,
      invalidRowsSelected,
      selectModalOpen,
      booksImported,
      isConfirmImportModalOpen,
      inconsistentBookReleases,
      scroller
    } = this.state;

    const acceptedItem = acceptedPath ? items.find((item) => item.path === acceptedPath) : null;
    const acceptedReview = acceptedItem?.review;
    const acceptedAddLinks = buildAddSearchLinks(acceptedReview, acceptedSuggestion, acceptedItem?.contributorEvidence);
    const acceptedAddAuthorUrl = acceptedAddLinks.addAuthorUrl;
    const acceptedAddBookUrl = acceptedAddLinks.addBookUrl;
    const columns = this.getColumns();
    this._columns = columns;

    const selectedIds = this.getSelectedIds();
    const selectedItem = selectedIds.length ? _.find(items, { id: selectedIds[0] }) : null;
    const importIdsByBook = _.chain(items).filter((x) => x.book).groupBy((x) => x.book.id).mapValues((x) => x.map((y) => y.id)).value();
    const editions = _.chain(items).filter((x) => x.book).keyBy((x) => x.book.id).mapValues((x) => ({ matchedEditionId: x.foreignEditionId, book: x.book })).values().value();
    const errorMessage = getErrorMessage(error, 'Unable to load manual import items');
    const totalPages = pageSize > 0 ? Math.max(Math.ceil(totalRecords / pageSize), 1) : 1;
    const isFirstPage = page <= 1;
    const isLastPage = page >= totalPages;

    const bulkSelectOptions = [
      { key: SELECT, value: translate('SelectDropdown'), disabled: true },
      { key: BOOK, value: translate('SelectBook') },
      { key: EDITION, value: translate('SelectEdition') },
      { key: QUALITY, value: translate('SelectQuality') },
      { key: RELEASE_GROUP, value: translate('SelectReleaseGroup') },
      { key: INDEXER_FLAGS, value: translate('SelectIndexerFlags') }
    ];

    if (allowAuthorChange) {
      bulkSelectOptions.splice(1, 0, {
        key: AUTHOR,
        value: 'Select Author'
      });
    }

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Manual Import - {title || folder}
        </ModalHeader>

        <ModalBody
          scrollDirection={scrollDirections.VERTICAL}
          registerScroller={this.setScrollerRef}
        >
          <div className={styles.filterContainer}>
            {
              showFilterExistingFiles &&
                <Menu alignMenu={align.RIGHT}>
                  <MenuButton>
                    <Icon
                      name={icons.FILTER}
                      size={22}
                    />

                    <div className={styles.filterText}>
                      {
                        filterExistingFiles ? 'Unmapped Files Only' : 'All Files'
                      }
                    </div>
                  </MenuButton>

                  <MenuContent>
                    <SelectedMenuItem
                      name={filterExistingFilesOptions.ALL}
                      isSelected={!filterExistingFiles}
                      onPress={this.onFilterExistingFilesChange}
                    >
                      All Files
                    </SelectedMenuItem>

                    <SelectedMenuItem
                      name={filterExistingFilesOptions.NEW}
                      isSelected={filterExistingFiles}
                      onPress={this.onFilterExistingFilesChange}
                    >
                      Unmapped Files Only
                    </SelectedMenuItem>
                  </MenuContent>
                </Menu>
            }
            {
              showReplaceExistingFiles &&
                <Menu alignMenu={align.RIGHT}>
                  <MenuButton>
                    <Icon
                      name={icons.CLONE}
                      size={22}
                    />

                    <div className={styles.filterText}>
                      {
                        replaceExistingFiles ? 'Existing files will be deleted' : 'Combine with existing files'
                      }
                    </div>
                  </MenuButton>

                  <MenuContent>
                    <SelectedMenuItem
                      name={replaceExistingFilesOptions.COMBINE}
                      isSelected={!replaceExistingFiles}
                      onPress={this.onReplaceExistingFilesChange}
                    >
                      Combine With Existing Files
                    </SelectedMenuItem>

                    <SelectedMenuItem
                      name={replaceExistingFilesOptions.DELETE}
                      isSelected={replaceExistingFiles}
                      onPress={this.onReplaceExistingFilesChange}
                    >
                      Replace Existing Files
                    </SelectedMenuItem>
                  </MenuContent>
                </Menu>
            }
          </div>

          {
            acceptedSuggestion &&
              <div className={styles.acceptedSuggestion}>
                <div className={styles.acceptedSuggestionTitle}>
                  Accepted Deep Identify suggestion
                </div>

                <div className={styles.acceptedSuggestionDetail}>
                  {
                    [
                      acceptedSuggestion.likelyAuthor,
                      acceptedSuggestion.likelyBook,
                      acceptedSuggestion.likelyEdition,
                      acceptedSuggestion.narrator && `Narrator: ${acceptedSuggestion.narrator}`,
                      acceptedPath && `Path: ${acceptedPath}`
                    ].filter(Boolean).join(' - ')
                  }
                </div>

                {
                  (acceptedAddAuthorUrl || acceptedAddBookUrl) &&
                    <div className={styles.acceptedSuggestionActions}>
                      {
                        acceptedAddAuthorUrl &&
                          <Button
                            to={acceptedAddAuthorUrl}
                            kind={kinds.DEFAULT}
                          >
                            Add Author
                          </Button>
                      }

                      {
                        acceptedAddBookUrl &&
                          <Button
                            to={acceptedAddBookUrl}
                            kind={kinds.DEFAULT}
                          >
                            Add Book
                          </Button>
                      }
                    </div>
                }
              </div>
          }

          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            error &&
              <div>{errorMessage}</div>
          }

          {
            isPaged && isPopulated && !isFetching &&
              <div className={styles.pageNotice}>
                <span>
                  Showing manual import review page {page} of {totalPages} ({items.length} of {totalRecords} files). Select all, ignore, retry, and import actions apply only to this loaded page.
                </span>

                <div className={styles.pageButtons}>
                  <Button
                    isDisabled={isFirstPage}
                    onPress={this.onPreviousPagePress}
                  >
                    Previous
                  </Button>

                  <Button
                    isDisabled={isLastPage}
                    onPress={this.onNextPagePress}
                  >
                    Next
                  </Button>
                </div>
              </div>
          }

          {
            isPopulated && !!items.length && !isFetching && scroller &&
              <VirtualTable
                className={styles.tableContainer}
                items={items}
                columns={columns}
                scroller={scroller}
                isSmallScreen={false}
                rowHeight={54}
                rowRenderer={this.rowRenderer}
                allSelected={allSelected}
                allUnselected={allUnselected}
                sortKey={sortKey}
                sortDirection={sortDirection}
                header={
                  <InteractiveImportTableHeader
                    columns={columns}
                    allSelected={allSelected}
                    allUnselected={allUnselected}
                    sortKey={sortKey}
                    sortDirection={sortDirection}
                    onSortPress={onSortPress}
                    onSelectAllChange={this.onSelectAllChange}
                  />
                }
              />
          }

          {
            isPopulated && !items.length && !isFetching &&
              'No book files were found in the selected folder'
          }
        </ModalBody>

        <ModalFooter className={styles.footer}>
          <div className={styles.leftButtons}>
            {
              !downloadId && showImportMode ?
                <SelectInput
                  className={styles.importMode}
                  name="importMode"
                  value={importMode}
                  values={importModeOptions}
                  onChange={this.onImportModeChange}
                /> :
                null
            }

            <SelectInput
              className={styles.bulkSelect}
              name="select"
              value={SELECT}
              values={bulkSelectOptions}
              isDisabled={!selectedIds.length}
              onChange={this.onSelectModalSelect}
            />

            <Button
              isDisabled={isSaving || !selectedIds.length}
              onPress={this.onIgnoreSelectedPress}
            >
              Ignore
            </Button>

            <Button
              isDisabled={isSaving || !selectedIds.length}
              onPress={this.onGetBookMappingPress}
            >
              Retry Identify
            </Button>

            <Button
              isDisabled={isSaving || !selectedIds.length}
              onPress={this.onOpenManualMatchPress}
            >
              Manual Match
            </Button>
          </div>

          <div className={styles.rightButtons}>
            <Button onPress={onModalClose}>
              Cancel
            </Button>

            {
              interactiveImportErrorMessage &&
                <span className={styles.errorMessage}>{interactiveImportErrorMessage}</span>
            }

            <Button
              kind={kinds.SUCCESS}
              isDisabled={isSaving || !selectedIds.length || !!invalidRowsSelected.length || inconsistentBookReleases}
              onPress={this.onImportSelectedPress}
            >
              Import
            </Button>
          </div>
        </ModalFooter>

        <SelectAuthorModal
          isOpen={selectModalOpen === AUTHOR}
          ids={selectedIds}
          onModalClose={this.onSelectModalClose}
        />

        <SelectBookModal
          isOpen={selectModalOpen === BOOK}
          ids={selectedIds}
          authorId={selectedItem && selectedItem.author && selectedItem.author.id}
          onModalClose={this.onSelectModalClose}
        />

        <SelectEditionModal
          isOpen={selectModalOpen === EDITION}
          importIdsByBook={importIdsByBook}
          books={editions}
          onModalClose={this.onSelectModalClose}
        />

        <SelectReleaseGroupModal
          isOpen={selectModalOpen === RELEASE_GROUP}
          ids={selectedIds}
          releaseGroup=""
          onModalClose={this.onSelectModalClose}
        />

        <SelectQualityModal
          isOpen={selectModalOpen === QUALITY}
          ids={selectedIds}
          qualityId={0}
          proper={false}
          real={false}
          onModalClose={this.onSelectModalClose}
        />

        <SelectIndexerFlagsModal
          isOpen={selectModalOpen === INDEXER_FLAGS}
          ids={selectedIds}
          indexerFlags={0}
          onModalClose={this.onSelectModalClose}
        />

        <ConfirmImportModal
          isOpen={isConfirmImportModalOpen}
          books={booksImported}
          onModalClose={this.onConfirmImportModalClose}
          onConfirmImportPress={this.onConfirmImportPress}
        />

      </ModalContent>
    );
  }
}

InteractiveImportModalContent.propTypes = {
  downloadId: PropTypes.string,
  allowAuthorChange: PropTypes.bool.isRequired,
  showImportMode: PropTypes.bool.isRequired,
  showFilterExistingFiles: PropTypes.bool.isRequired,
  showReplaceExistingFiles: PropTypes.bool.isRequired,
  filterExistingFiles: PropTypes.bool.isRequired,
  replaceExistingFiles: PropTypes.bool.isRequired,
  importMode: PropTypes.string.isRequired,
  title: PropTypes.string,
  folder: PropTypes.string,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  page: PropTypes.number,
  pageSize: PropTypes.number,
  totalRecords: PropTypes.number,
  isPaged: PropTypes.bool,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.string,
  interactiveImportErrorMessage: PropTypes.string,
  acceptedSuggestion: PropTypes.object,
  acceptedPath: PropTypes.string,
  onSortPress: PropTypes.func.isRequired,
  onFilterExistingFilesChange: PropTypes.func.isRequired,
  onReplaceExistingFilesChange: PropTypes.func.isRequired,
  onPageChange: PropTypes.func.isRequired,
  onImportModeChange: PropTypes.func.isRequired,
  onImportSelectedPress: PropTypes.func.isRequired,
  saveInteractiveImportItem: PropTypes.func.isRequired,
  removeInteractiveImportItems: PropTypes.func.isRequired,
  updateInteractiveImportItem: PropTypes.func.isRequired,
  onSetContributorEvidencePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

InteractiveImportModalContent.defaultProps = {
  allowAuthorChange: true,
  showFilterExistingFiles: false,
  showReplaceExistingFiles: false,
  showImportMode: true,
  importMode: 'move',
  page: 1,
  pageSize: 500,
  totalRecords: 0,
  isPaged: false
};

export default InteractiveImportModalContent;
