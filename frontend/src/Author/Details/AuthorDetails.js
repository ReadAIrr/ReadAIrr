import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
import DeleteAuthorModal from 'Author/Delete/DeleteAuthorModal';
import EditAuthorModalConnector from 'Author/Edit/EditAuthorModalConnector';
import AuthorHistoryTable from 'Author/History/AuthorHistoryTable';
import MonitoringOptionsModal from 'Author/MonitoringOptions/MonitoringOptionsModal';
import BookEditorFooter from 'Book/Editor/BookEditorFooter';
import BookFileEditorTable from 'BookFile/Editor/BookFileEditorTable';
import Alert from 'Components/Alert';
import SelectInput from 'Components/Form/SelectInput';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import SwipeHeaderConnector from 'Components/Swipe/SwipeHeaderConnector';
import { align, icons, kinds } from 'Helpers/Props';
import InteractiveSearchFilterMenuConnector from 'InteractiveSearch/InteractiveSearchFilterMenuConnector';
import InteractiveSearchTable from 'InteractiveSearch/InteractiveSearchTable';
import OrganizePreviewModalConnector from 'Organize/OrganizePreviewModalConnector';
import RetagPreviewModalConnector from 'Retag/RetagPreviewModalConnector';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import InteractiveImportModal from '../../InteractiveImport/InteractiveImportModal';
import AuthorDetailsHeaderConnector from './AuthorDetailsHeaderConnector';
import AuthorDetailsSeasonConnector from './AuthorDetailsSeasonConnector';
import AuthorDetailsSeriesConnector from './AuthorDetailsSeriesConnector';
import styles from './AuthorDetails.css';

function getExpandedState(newState) {
  return {
    allExpanded: newState.allSelected,
    allCollapsed: newState.allUnselected,
    expandedState: newState.selectedState
  };
}

const bookMonitorFilterOptions = [
  { key: 'all', value: () => translate('All') },
  { key: 'monitored', value: () => translate('Monitored') },
  { key: 'unmonitored', value: () => translate('Unmonitored') }
];

const relationshipTypeOptions = [
  { key: 'penName', value: 'Pen name' },
  { key: 'coauthor', value: 'Coauthor' },
  { key: 'alias', value: 'Alias' }
];

const displayPreferenceOptions = [
  { key: 'canonical', value: 'Canonical' },
  { key: 'alias', value: 'Alias' },
  { key: 'both', value: 'Both' }
];

const relationshipTypeLabels = {
  penName: 'Pen name',
  coauthor: 'Coauthor',
  alias: 'Alias'
};

class AuthorDetails extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isOrganizeModalOpen: false,
      isRetagModalOpen: false,
      isEditAuthorModalOpen: false,
      isDeleteAuthorModalOpen: false,
      isInteractiveImportModalOpen: false,
      isMonitorOptionsModalOpen: false,
      isEditorActive: false,
      allExpanded: false,
      allCollapsed: false,
      expandedState: {},
      allSelected: false,
      allUnselected: false,
      lastToggled: null,
      selectedState: {},
      selectedTabIndex: 0,
      bookMonitorFilter: 'all',
      linkAuthorId: '',
      linkRelationshipType: 'penName',
      linkDisplayPreference: 'canonical'
    };
  }

  //
  // Control

  setSelectedState = (items) => {
    const {
      selectedState
    } = this.state;

    const newSelectedState = {};

    items.forEach((item) => {
      const isItemSelected = selectedState[item.id];

      if (isItemSelected) {
        newSelectedState[item.id] = isItemSelected;
      } else {
        newSelectedState[item.id] = false;
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
  };

  getSelectedIds = () => {
    return getSelectedIds(this.state.selectedState);
  };

  //
  // Listeners

  onOrganizePress = () => {
    this.setState({ isOrganizeModalOpen: true });
  };

  onOrganizeModalClose = () => {
    this.setState({ isOrganizeModalOpen: false });
  };

  onRetagPress = () => {
    this.setState({ isRetagModalOpen: true });
  };

  onRetagModalClose = () => {
    this.setState({ isRetagModalOpen: false });
  };

  onInteractiveImportPress = () => {
    this.setState({ isInteractiveImportModalOpen: true });
  };

  onInteractiveImportModalClose = () => {
    this.setState({ isInteractiveImportModalOpen: false });
  };

  onEditAuthorPress = () => {
    this.setState({ isEditAuthorModalOpen: true });
  };

  onEditAuthorModalClose = () => {
    this.setState({ isEditAuthorModalOpen: false });
  };

  onDeleteAuthorPress = () => {
    this.setState({
      isEditAuthorModalOpen: false,
      isDeleteAuthorModalOpen: true
    });
  };

  onDeleteAuthorModalClose = () => {
    this.setState({ isDeleteAuthorModalOpen: false });
  };

  onMonitorOptionsPress = () => {
    this.setState({ isMonitorOptionsModalOpen: true });
  };

  onMonitorOptionsClose = () => {
    this.setState({ isMonitorOptionsModalOpen: false });
  };

  onBookEditorTogglePress = () => {
    this.setState({ isEditorActive: !this.state.isEditorActive });
  };

  onExpandAllPress = () => {
    const {
      allExpanded,
      expandedState
    } = this.state;

    this.setState(getExpandedState(selectAll(expandedState, !allExpanded)));
  };

  onExpandPress = (bookId, isExpanded) => {
    this.setState((state) => {
      const convertedState = {
        allSelected: state.allExpanded,
        allUnselected: state.allCollapsed,
        selectedState: state.expandedState
      };

      const newState = toggleSelected(convertedState, [], bookId, isExpanded, false);

      return getExpandedState(newState);
    });
  };

  onSelectAllChange = ({ value }) => {
    this.setState(selectAll(this.state.selectedState, value));
  };

  onSelectAllPress = () => {
    this.onSelectAllChange({ value: !this.state.allSelected });
  };

  onSelectedChange = (items, id, value, shiftKey = false) => {
    this.setState((state) => {
      return toggleSelected(state, items, id, value, shiftKey);
    });
  };

  onSaveSelected = (changes) => {
    this.props.onSaveSelected({
      bookIds: this.getSelectedIds(),
      ...changes
    });
  };

  onTabSelect = (index, lastIndex) => {
    this.setState({ selectedTabIndex: index });
  };

  onBookMonitorFilterChange = ({ value }) => {
    this.setState({ bookMonitorFilter: value });
  };

  onLinkedAuthorSelectChange = ({ value }) => {
    this.setState({ linkAuthorId: value });
  };

  onLinkRelationshipTypeChange = ({ value }) => {
    this.setState({ linkRelationshipType: value });
  };

  onLinkDisplayPreferenceChange = ({ value }) => {
    this.setState({ linkDisplayPreference: value });
  };

  onLinkAuthorPress = (selectedLinkAuthorId) => {
    const {
      id,
      onLinkAuthorPress
    } = this.props;

    const {
      linkAuthorId,
      linkRelationshipType,
      linkDisplayPreference
    } = this.state;

    const aliasAuthorId = selectedLinkAuthorId || linkAuthorId;

    if (!aliasAuthorId) {
      return;
    }

    onLinkAuthorPress({
      canonicalAuthorId: id,
      aliasAuthorId: parseInt(aliasAuthorId),
      relationshipType: linkRelationshipType,
      displayPreference: linkDisplayPreference
    });

    this.setState({ linkAuthorId: '' });
  };

  //
  // Render

  render() {
    const {
      id,
      authorName,
      path,
      monitored,
      isRefreshing,
      isSearching,
      isFetching,
      isPopulated,
      booksError,
      bookFilesError,
      hasBooks,
      hasMonitoredBooks,
      hasSeries,
      series,
      hasBookFiles,
      allAuthors = [],
      linkedAuthors = [],
      identityStatistics = {},
      previousAuthor,
      nextAuthor,
      onRefreshPress,
      onSearchPress,
      onUnlinkAuthorPress,
      isSaving,
      saveError,
      isDeleting,
      deleteError,
      statistics = {}
    } = this.props;

    const {
      bookFileCount = 0,
      totalBookCount = 0
    } = statistics;

    const {
      isOrganizeModalOpen,
      isRetagModalOpen,
      isEditAuthorModalOpen,
      isDeleteAuthorModalOpen,
      isInteractiveImportModalOpen,
      isMonitorOptionsModalOpen,
      isEditorActive,
      allSelected,
      selectedState,
      allExpanded,
      allCollapsed,
      expandedState,
      selectedTabIndex,
      bookMonitorFilter,
      linkAuthorId,
      linkRelationshipType,
      linkDisplayPreference
    } = this.state;

    let expandIcon = icons.EXPAND_INDETERMINATE;

    if (allExpanded) {
      expandIcon = icons.COLLAPSE;
    } else if (allCollapsed) {
      expandIcon = icons.EXPAND;
    }

    const selectedBookIds = this.getSelectedIds();
    const linkedAuthorIds = linkedAuthors.map((linkedAuthor) => linkedAuthor.id);
    const linkableAuthors = allAuthors.filter((author) => {
      return author.id !== id && linkedAuthorIds.indexOf(author.id) === -1;
    });
    const selectedLinkAuthorId = linkAuthorId || linkableAuthors[0]?.id || '';

    return (
      <PageContent title={authorName}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('RefreshAndScan')}
              iconName={icons.REFRESH}
              spinningName={icons.REFRESH}
              title={translate('RefreshInformationAndScanDisk')}
              isSpinning={isRefreshing}
              onPress={onRefreshPress}
            />

            <PageToolbarButton
              label={translate('SearchMonitored')}
              iconName={icons.SEARCH}
              isDisabled={!monitored || !hasMonitoredBooks || !hasBooks}
              isSpinning={isSearching}
              title={hasMonitoredBooks ? undefined : translate('HasMonitoredBooksNoMonitoredBooksForThisAuthor')}
              onPress={onSearchPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('PreviewRename')}
              iconName={icons.ORGANIZE}
              isDisabled={!hasBookFiles}
              onPress={this.onOrganizePress}
            />

            <PageToolbarButton
              label={translate('PreviewRetag')}
              iconName={icons.RETAG}
              isDisabled={!hasBookFiles}
              onPress={this.onRetagPress}
            />

            <PageToolbarButton
              label={translate('ManualImport')}
              iconName={icons.INTERACTIVE}
              onPress={this.onInteractiveImportPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('BookMonitoring')}
              iconName={icons.MONITORED}
              onPress={this.onMonitorOptionsPress}
            />

            <PageToolbarButton
              label={translate('Edit')}
              iconName={icons.EDIT}
              onPress={this.onEditAuthorPress}
            />

            <PageToolbarButton
              label={translate('Delete')}
              iconName={icons.DELETE}
              onPress={this.onDeleteAuthorPress}
            />

            <PageToolbarSeparator />

            {
              isEditorActive ?
                <PageToolbarButton
                  label={translate('BookList')}
                  iconName={icons.AUTHOR_CONTINUING}
                  onPress={this.onBookEditorTogglePress}
                /> :
                <PageToolbarButton
                  label={translate('BookEditor')}
                  iconName={icons.EDIT}
                  onPress={this.onBookEditorTogglePress}
                />
            }

            {
              isEditorActive ?
                <PageToolbarButton
                  label={allSelected ? translate('UnselectAll') : translate('SelectAll')}
                  iconName={icons.CHECK_SQUARE}
                  onPress={this.onSelectAllPress}
                /> :
                null
            }

          </PageToolbarSection>

          <PageToolbarSection alignContent={align.RIGHT}>
            <SelectInput
              name="bookMonitorFilter"
              value={bookMonitorFilter}
              values={bookMonitorFilterOptions}
              onChange={this.onBookMonitorFilterChange}
            />

            <PageToolbarButton
              label={allExpanded ? translate('AllExpandedCollapseAll') : translate('AllExpandedExpandAll')}
              iconName={expandIcon}
              onPress={this.onExpandAllPress}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody innerClassName={styles.innerContentBody}>
          <SwipeHeaderConnector
            className={styles.header}
            nextLink={`/author/${nextAuthor.titleSlug}`}
            nextComponent={(width) => <AuthorDetailsHeaderConnector authorId={nextAuthor.id} width={width} />}
            prevLink={`/author/${previousAuthor.titleSlug}`}
            prevComponent={(width) => <AuthorDetailsHeaderConnector authorId={previousAuthor.id} width={width} />}
            currentComponent={(width) => <AuthorDetailsHeaderConnector authorId={id} width={width} />}
          >
            <div className={styles.authorNavigationButtons}>
              <IconButton
                className={styles.authorNavigationButton}
                name={icons.ARROW_LEFT}
                size={30}
                title={translate('GoToInterp', [previousAuthor.authorName])}
                to={`/author/${previousAuthor.titleSlug}`}
              />

              <IconButton
                className={styles.authorUpButton}
                name={icons.ARROW_UP}
                size={30}
                title={translate('GoToAuthorListing')}
                to={'/'}
              />

              <IconButton
                className={styles.authorNavigationButton}
                name={icons.ARROW_RIGHT}
                size={30}
                title={translate('GoToInterp', [nextAuthor.authorName])}
                to={`/author/${nextAuthor.titleSlug}`}
              />
            </div>
          </SwipeHeaderConnector>

          <div className={styles.contentContainer}>
            {
              !isPopulated && !booksError && !bookFilesError ?
                <LoadingIndicator /> :
                null
            }

            {
              !isFetching && booksError ?
                <Alert kind={kinds.DANGER}>
                  {translate('LoadingBooksFailed')}
                </Alert> :
                null
            }

            {
              !isFetching && bookFilesError ?
                <Alert kind={kinds.DANGER}>
                  {translate('LoadingBookFilesFailed')}
                </Alert> :
                null
            }

            <div className={styles.linkedAuthorsPanel}>
              <div className={styles.linkedAuthorsHeader}>
                <div>
                  <div className={styles.linkedAuthorsTitle}>
                    Linked Authors
                  </div>

                  <div className={styles.linkedAuthorsSummary}>
                    {identityStatistics.availableBookCount || 0}/{identityStatistics.bookCount || 0} available across {linkedAuthors.length + 1} identities
                  </div>
                </div>
              </div>

              {
                linkedAuthors.length > 0 &&
                  <div className={styles.linkedAuthorsList}>
                    {
                      linkedAuthors.map((linkedAuthor) => {
                        return (
                          <div
                            key={linkedAuthor.id}
                            className={styles.linkedAuthorRow}
                          >
                            <Link to={`/author/${linkedAuthor.titleSlug}`}>
                              {linkedAuthor.authorName}
                            </Link>

                            <span>
                              {relationshipTypeLabels[linkedAuthor.relationshipType] || linkedAuthor.relationshipType || 'Linked'}
                            </span>

                            <IconButton
                              name={icons.REMOVE}
                              title="Unlink author"
                              onPress={() => onUnlinkAuthorPress(linkedAuthor.linkId)}
                            />
                          </div>
                        );
                      })
                    }
                  </div>
              }

              <div className={styles.linkAuthorControls}>
                <SelectInput
                  name="linkAuthorId"
                  value={selectedLinkAuthorId}
                  values={linkableAuthors.map((author) => {
                    return {
                      key: author.id,
                      value: author.authorName
                    };
                  })}
                  isDisabled={!linkableAuthors.length}
                  onChange={this.onLinkedAuthorSelectChange}
                />

                <SelectInput
                  name="linkRelationshipType"
                  value={linkRelationshipType}
                  values={relationshipTypeOptions}
                  onChange={this.onLinkRelationshipTypeChange}
                />

                <SelectInput
                  name="linkDisplayPreference"
                  value={linkDisplayPreference}
                  values={displayPreferenceOptions}
                  onChange={this.onLinkDisplayPreferenceChange}
                />

                <Button
                  isDisabled={!selectedLinkAuthorId}
                  onPress={() => this.onLinkAuthorPress(selectedLinkAuthorId)}
                >
                  Link
                </Button>
              </div>
            </div>

            {
              isPopulated &&
                <Tabs selectedIndex={selectedTabIndex} onSelect={this.onTabSelect}>
                  <TabList
                    className={styles.tabList}
                  >
                    <Tab
                      className={styles.tab}
                      selectedClassName={styles.selectedTab}
                    >
                      {translate('BooksTotal', [totalBookCount])}
                    </Tab>

                    <Tab
                      className={styles.tab}
                      selectedClassName={styles.selectedTab}
                    >
                      {translate('SeriesTotal', [series.length])}
                    </Tab>

                    <Tab
                      className={styles.tab}
                      selectedClassName={styles.selectedTab}
                    >
                      {translate('History')}
                    </Tab>

                    <Tab
                      className={styles.tab}
                      selectedClassName={styles.selectedTab}
                    >
                      {translate('Search')}
                    </Tab>

                    <Tab
                      className={styles.tab}
                      selectedClassName={styles.selectedTab}
                    >
                      {translate('FilesTotal', [bookFileCount])}
                    </Tab>

                    {
                      selectedTabIndex === 3 &&
                        <div className={styles.filterIcon}>
                          <InteractiveSearchFilterMenuConnector
                            type="author"
                          />
                        </div>
                    }
                  </TabList>

                  <TabPanel>
                    <AuthorDetailsSeasonConnector
                      authorId={id}
                      isExpanded={true}
                      selectedState={selectedState}
                      onExpandPress={this.onExpandPress}
                      setSelectedState={this.setSelectedState}
                      onSelectedChange={this.onSelectedChange}
                      isEditorActive={isEditorActive}
                      bookMonitorFilter={bookMonitorFilter}
                    />
                  </TabPanel>

                  <TabPanel>
                    {
                      isPopulated && hasSeries &&
                        <div>
                          {
                            series.map((item) => {
                              return (
                                <AuthorDetailsSeriesConnector
                                  key={item.id}
                                  seriesId={item.id}
                                  authorId={id}
                                  isExpanded={expandedState[item.id]}
                                  onExpandPress={this.onExpandPress}
                                  bookMonitorFilter={bookMonitorFilter}
                                />
                              );
                            })
                          }
                        </div>
                    }
                  </TabPanel>

                  <TabPanel>
                    <AuthorHistoryTable
                      authorId={id}
                    />
                  </TabPanel>

                  <TabPanel>
                    <InteractiveSearchTable
                      type="author"
                      authorId={id}
                    />
                  </TabPanel>

                  <TabPanel>
                    <BookFileEditorTable
                      authorId={id}
                    />
                  </TabPanel>
                </Tabs>
            }
          </div>

          <div className={styles.metadataMessage}>
            {translate('TooManyBooks')}
            <Link to='/settings/profiles'> {translate('MetadataProfile')} </Link>
            or manually
            <Link to={`/add/search?term=${encodeURIComponent(authorName)}`}> {translate('Search')} </Link>
            for new items!
          </div>

          <OrganizePreviewModalConnector
            isOpen={isOrganizeModalOpen}
            authorId={id}
            onModalClose={this.onOrganizeModalClose}
          />

          <RetagPreviewModalConnector
            isOpen={isRetagModalOpen}
            authorId={id}
            onModalClose={this.onRetagModalClose}
          />

          <EditAuthorModalConnector
            isOpen={isEditAuthorModalOpen}
            authorId={id}
            onModalClose={this.onEditAuthorModalClose}
            onDeleteAuthorPress={this.onDeleteAuthorPress}
          />

          <DeleteAuthorModal
            isOpen={isDeleteAuthorModalOpen}
            authorId={id}
            onModalClose={this.onDeleteAuthorModalClose}
          />

          <InteractiveImportModal
            isOpen={isInteractiveImportModalOpen}
            authorId={id}
            folder={path}
            allowAuthorChange={false}
            showFilterExistingFiles={true}
            showImportMode={false}
            onModalClose={this.onInteractiveImportModalClose}
          />

          <MonitoringOptionsModal
            isOpen={isMonitorOptionsModalOpen}
            authorId={id}
            onModalClose={this.onMonitorOptionsClose}
          />
        </PageContentBody>

        {
          isEditorActive &&
            <BookEditorFooter
              bookIds={selectedBookIds}
              selectedCount={selectedBookIds.length}
              isSaving={isSaving}
              saveError={saveError}
              isDeleting={isDeleting}
              deleteError={deleteError}
              onSaveSelected={this.onSaveSelected}
            />
        }
      </PageContent>
    );
  }
}

AuthorDetails.propTypes = {
  id: PropTypes.number.isRequired,
  authorName: PropTypes.string.isRequired,
  ratings: PropTypes.object.isRequired,
  path: PropTypes.string.isRequired,
  statistics: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.number.isRequired,
  monitored: PropTypes.bool.isRequired,
  status: PropTypes.string.isRequired,
  overview: PropTypes.string,
  links: PropTypes.arrayOf(PropTypes.object).isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  alternateTitles: PropTypes.arrayOf(PropTypes.string).isRequired,
  tags: PropTypes.arrayOf(PropTypes.number).isRequired,
  isRefreshing: PropTypes.bool.isRequired,
  isSearching: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  booksError: PropTypes.object,
  bookFilesError: PropTypes.object,
  hasBooks: PropTypes.bool.isRequired,
  hasMonitoredBooks: PropTypes.bool.isRequired,
  hasSeries: PropTypes.bool.isRequired,
  series: PropTypes.arrayOf(PropTypes.object).isRequired,
  hasBookFiles: PropTypes.bool.isRequired,
  allAuthors: PropTypes.arrayOf(PropTypes.object).isRequired,
  linkedAuthors: PropTypes.arrayOf(PropTypes.object),
  identityStatistics: PropTypes.object,
  previousAuthor: PropTypes.object.isRequired,
  nextAuthor: PropTypes.object.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired,
  onRefreshPress: PropTypes.func.isRequired,
  onSearchPress: PropTypes.func.isRequired,
  onLinkAuthorPress: PropTypes.func.isRequired,
  onUnlinkAuthorPress: PropTypes.func.isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  isDeleting: PropTypes.bool.isRequired,
  deleteError: PropTypes.object,
  onSaveSelected: PropTypes.func.isRequired
};

AuthorDetails.defaultProps = {
  statistics: {},
  tags: []
};

export default AuthorDetails;
