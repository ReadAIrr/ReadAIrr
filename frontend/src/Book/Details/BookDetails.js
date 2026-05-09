import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
import AuthorHistoryTable from 'Author/History/AuthorHistoryTable';
import DeleteBookModal from 'Book/Delete/DeleteBookModal';
import EditBookModalConnector from 'Book/Edit/EditBookModalConnector';
import BookFileEditorTable from 'BookFile/Editor/BookFileEditorTable';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import SwipeHeaderConnector from 'Components/Swipe/SwipeHeaderConnector';
import { icons } from 'Helpers/Props';
import InteractiveSearchFilterMenuConnector from 'InteractiveSearch/InteractiveSearchFilterMenuConnector';
import InteractiveSearchTable from 'InteractiveSearch/InteractiveSearchTable';
import OrganizePreviewModalConnector from 'Organize/OrganizePreviewModalConnector';
import RetagPreviewModalConnector from 'Retag/RetagPreviewModalConnector';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import BookDetailsHeaderConnector from './BookDetailsHeaderConnector';
import styles from './BookDetails.css';

function getFieldStatusLabel(status) {
  switch (status) {
    case 'confirmed':
      return 'Confirmed';
    case 'conflicting':
      return 'Conflict';
    case 'provider-only':
      return 'Provider only';
    case 'local-only':
      return 'Local only';
    case 'low-confidence':
      return 'Low confidence';
    case 'needs-review':
      return 'Needs review';
    case 'available':
      return 'Available';
    case 'unavailable':
      return 'Unavailable';
    case 'notAvailable':
      return 'Not available';
    case 'notEvaluated':
      return 'Not evaluated';
    case 'disabled':
      return 'Disabled';
    default:
      return 'Missing';
  }
}

function getGroupedMetadataFields(fields = []) {
  return fields.reduce((acc, field) => {
    const section = field.section || 'other';
    const sectionLabel = field.sectionLabel || 'Other metadata';
    const existing = acc.find((item) => item.section === section);

    if (existing) {
      existing.fields.push(field);
    } else {
      acc.push({
        section,
        sectionLabel,
        fields: [field]
      });
    }

    return acc;
  }, []);
}

function renderMetadataFieldValue(field, value, emptyValue) {
  const displayValue = value || emptyValue;

  if (field.field !== 'narrators' || !value) {
    return displayValue;
  }

  const names = value.split(';').map((item) => item.trim()).filter(Boolean);

  if (!names.length) {
    return displayValue;
  }

  return names.map((name, index) => {
    return (
      <React.Fragment key={name}>
        {
          index > 0 &&
            '; '
        }

        <Link to={`/narrators?term=${encodeURIComponent(name)}`}>
          {name}
        </Link>
      </React.Fragment>
    );
  });
}

function MetadataComparisonDrillInModal({ comparison, isOpen, onModalClose }) {
  if (!comparison) {
    return null;
  }

  const groups = getGroupedMetadataFields(comparison.fields || []);
  const statusCounts = comparison.statusCounts || [];
  const confidenceScoring = comparison.confidenceScoring || {};
  const confidenceSources = confidenceScoring.sources || [];

  return (
    <Modal
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Metadata confidence review
        </ModalHeader>

        <ModalBody>
          <div className={styles.metadataDrillInSummary}>
            <div>
              <div className={styles.metadataDrillInTitle}>{comparison.title}</div>
              <div className={styles.metadataComparisonSummary}>{comparison.summary}</div>
              <div className={styles.metadataComparisonNotice}>
                Provider: {comparison.metadataSource || 'Not configured'} - {comparison.providerStatus}
              </div>
            </div>

            <div className={styles.metadataStatusCounts}>
              {
                statusCounts.map((statusCount) => {
                  return (
                    <div
                      key={statusCount.status}
                      className={styles.metadataStatusCount}
                    >
                      <span>{statusCount.label}</span>
                      <b>{statusCount.count}</b>
                    </div>
                  );
                })
              }
            </div>
          </div>

          <div className={styles.metadataComparisonNotice}>
            This is a read-only curation view. It does not update metadata, switch editions, add authors or books, import files, move files, rename, retag, monitor, search, download, change provider settings, or call AI decisioning.
          </div>

          <div className={styles.metadataConfidenceScoring}>
            <div className={styles.metadataDrillInSectionTitle}>
              Source confidence posture
            </div>

            <div className={styles.metadataConfidenceSummary}>
              <b>{confidenceScoring.fieldConfidenceScore || 0}% passive field confidence</b>
              <span>
                {confidenceScoring.summary}
                {' '}
                {confidenceScoring.totalComparableFields || 0} field{confidenceScoring.totalComparableFields === 1 ? '' : 's'} included in scoring.
              </span>
            </div>

            <div className={styles.metadataConfidenceSourceGrid}>
              {
                confidenceSources.map((source) => {
                  return (
                    <div
                      key={`${source.sourceType}-${source.role}`}
                      className={styles.metadataConfidenceSource}
                    >
                      <div className={styles.metadataComparisonField}>
                        <span className={styles.metadataComparisonLabel}>{source.sourceLabel}</span>
                        <span className={styles.metadataComparisonStatus}>{getFieldStatusLabel(source.status)}</span>
                      </div>

                      <div className={styles.metadataConfidenceSourceMeta}>
                        {source.role} - {source.weight}% review weight
                      </div>

                      <div className={styles.metadataComparisonExplanation}>
                        {source.explanation}
                      </div>
                    </div>
                  );
                })
              }
            </div>
          </div>

          {
            groups.map((group) => {
              return (
                <div
                  key={group.section}
                  className={styles.metadataDrillInSection}
                >
                  <div className={styles.metadataDrillInSectionTitle}>
                    {group.sectionLabel}
                  </div>

                  {
                    group.fields.map((field) => {
                      return (
                        <div
                          key={field.field}
                          className={styles.metadataDrillInField}
                        >
                          <div className={styles.metadataComparisonField}>
                            <span className={styles.metadataComparisonLabel}>{field.label}</span>
                            <span className={styles.metadataComparisonStatus}>{getFieldStatusLabel(field.status)}</span>
                          </div>

                          <div className={styles.metadataDrillInValues}>
                            <div>
                              <span>Local</span>
                              <p>{renderMetadataFieldValue(field, field.localValue, 'Not set')}</p>
                            </div>

                            <div>
                              <span>Provider</span>
                              <p>{renderMetadataFieldValue(field, field.providerValue, 'Not available')}</p>
                            </div>

                            <div>
                              <span>Evidence</span>
                              <p>{field.evidenceValue || field.source || 'No separate evidence'}</p>
                            </div>
                          </div>

                          <div className={styles.metadataComparisonExplanation}>
                            {field.explanation}
                          </div>

                          <div className={styles.metadataDrillInHint}>
                            {field.actionHint}
                            {
                              !field.includedInScoring &&
                                ' This row is status context only and is not included in the passive score.'
                            }
                          </div>
                        </div>
                      );
                    })
                  }
                </div>
              );
            })
          }
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>
            Close
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

MetadataComparisonDrillInModal.propTypes = {
  comparison: PropTypes.object,
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired
};

function MetadataComparisonPanel({ comparison, isFetching, error, onOpenDrillIn }) {
  if (isFetching) {
    return (
      <div className={styles.metadataComparison}>
        <LoadingIndicator />
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.metadataComparison}>
        <div className={styles.metadataComparisonTitle}>Metadata confidence review</div>
        <div className={styles.metadataComparisonNotice}>
          Unable to load review-only metadata comparison.
        </div>
      </div>
    );
  }

  if (!comparison) {
    return null;
  }

  const fields = (comparison.fields || []).filter((field) => field.status !== 'confirmed').slice(0, 8);
  const visibleFields = fields.length ? fields : (comparison.fields || []).slice(0, 5);

  return (
    <div className={styles.metadataComparison}>
      <div className={styles.metadataComparisonHeader}>
        <div>
          <div className={styles.metadataComparisonTitle}>Metadata confidence review</div>
          <div className={styles.metadataComparisonSummary}>
            {comparison.summary}
          </div>
        </div>

        <div className={styles.metadataComparisonBadge}>
          Review only
        </div>
      </div>

      <div className={styles.metadataStatusCountsCompact}>
        {
          (comparison.statusCounts || []).map((statusCount) => {
            return (
              <span key={statusCount.status}>
                <b>{statusCount.count}</b> {statusCount.label}
              </span>
            );
          })
        }
      </div>

      <div className={styles.metadataComparisonNotice}>
        Opening this comparison does not update metadata, change editions, import files, rename, retag, search, monitor, or call AI providers.
      </div>

      <div className={styles.metadataComparisonGrid}>
        {
          visibleFields.map((field) => {
            return (
              <div
                key={field.field}
                className={styles.metadataComparisonRow}
              >
                <div className={styles.metadataComparisonField}>
                  <span className={styles.metadataComparisonLabel}>{field.label}</span>
                  <span className={styles.metadataComparisonStatus}>{getFieldStatusLabel(field.status)}</span>
                </div>
                <div className={styles.metadataComparisonValues}>
                  <div><b>Local:</b> {field.localValue || 'Not set'}</div>
                  <div><b>Provider:</b> {field.providerValue || 'Not available'}</div>
                  {
                    field.evidenceValue &&
                      <div><b>Evidence:</b> {field.evidenceValue}</div>
                  }
                </div>
                <div className={styles.metadataComparisonExplanation}>
                  {field.explanation}
                </div>
              </div>
            );
          })
        }
      </div>

      <div className={styles.metadataComparisonActions}>
        <Button onPress={onOpenDrillIn}>
          Review Details
        </Button>
      </div>
    </div>
  );
}

MetadataComparisonPanel.propTypes = {
  comparison: PropTypes.object,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  onOpenDrillIn: PropTypes.func.isRequired
};

class BookDetails extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isOrganizeModalOpen: false,
      isRetagModalOpen: false,
      isEditBookModalOpen: false,
      isDeleteBookModalOpen: false,
      selectedTabIndex: 0,
      metadataComparison: null,
      isFetchingMetadataComparison: false,
      metadataComparisonError: null,
      isMetadataDrillInModalOpen: false
    };
  }

  componentDidMount() {
    this.fetchMetadataComparison();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.id !== this.props.id) {
      this.fetchMetadataComparison();
    }
  }

  componentWillUnmount() {
    if (this._abortMetadataComparisonRequest) {
      this._abortMetadataComparisonRequest();
    }
  }

  fetchMetadataComparison = () => {
    const { id } = this.props;

    if (!id) {
      return;
    }

    if (this._abortMetadataComparisonRequest) {
      this._abortMetadataComparisonRequest();
    }

    this.setState({
      isFetchingMetadataComparison: true,
      metadataComparisonError: null
    });

    const { request, abortRequest } = createAjaxRequest({
      url: `/metadata/compare/book/${id}`
    });

    this._abortMetadataComparisonRequest = abortRequest;

    request.done((metadataComparison) => {
      this.setState({
        metadataComparison,
        isFetchingMetadataComparison: false,
        metadataComparisonError: null
      });
    });

    request.fail((xhr) => {
      if (xhr.aborted) {
        return;
      }

      this.setState({
        metadataComparison: null,
        isFetchingMetadataComparison: false,
        metadataComparisonError: xhr
      });
    });
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

  onEditBookPress = () => {
    this.setState({ isEditBookModalOpen: true });
  };

  onEditBookModalClose = () => {
    this.setState({ isEditBookModalOpen: false });
  };

  onDeleteBookPress = () => {
    this.setState({
      isEditBookModalOpen: false,
      isDeleteBookModalOpen: true
    });
  };

  onDeleteBookModalClose = () => {
    this.setState({ isDeleteBookModalOpen: false });
  };

  onMetadataDrillInPress = () => {
    this.setState({ isMetadataDrillInModalOpen: true });
  };

  onMetadataDrillInModalClose = () => {
    this.setState({ isMetadataDrillInModalOpen: false });
  };

  onTabSelect = (index, lastIndex) => {
    this.setState({ selectedTabIndex: index });
  };

  //
  // Render

  render() {
    const {
      id,
      title,
      isRefreshing,
      isFetching,
      isPopulated,
      bookFilesError,
      hasBookFiles,
      author,
      previousBook,
      nextBook,
      isSearching,
      onRefreshPress,
      onSearchPress,
      statistics = {}
    } = this.props;

    const {
      bookFileCount = 0
    } = statistics;

    const {
      isOrganizeModalOpen,
      isRetagModalOpen,
      isEditBookModalOpen,
      isDeleteBookModalOpen,
      selectedTabIndex,
      metadataComparison,
      isFetchingMetadataComparison,
      metadataComparisonError,
      isMetadataDrillInModalOpen
    } = this.state;

    return (
      <PageContent title={title}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('Refresh')}
              iconName={icons.REFRESH}
              spinningName={icons.REFRESH}
              title={translate('RefreshInformation')}
              isSpinning={isRefreshing}
              onPress={onRefreshPress}
            />

            <PageToolbarButton
              label={translate('SearchBook')}
              iconName={icons.SEARCH}
              isSpinning={isSearching}
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

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('Edit')}
              iconName={icons.EDIT}
              onPress={this.onEditBookPress}
            />

            <PageToolbarButton
              label={translate('Delete')}
              iconName={icons.DELETE}
              onPress={this.onDeleteBookPress}
            />

          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody innerClassName={styles.innerContentBody}>
          <SwipeHeaderConnector
            className={styles.header}
            nextLink={`/book/${nextBook.titleSlug}`}
            nextComponent={(width) => (
              <BookDetailsHeaderConnector
                bookId={nextBook.id}
                author={author}
                width={width}
              />
            )}
            prevLink={`/book/${previousBook.titleSlug}`}
            prevComponent={(width) => (
              <BookDetailsHeaderConnector
                bookId={previousBook.id}
                author={author}
                width={width}
              />
            )}
            currentComponent={(width) => (
              <BookDetailsHeaderConnector
                bookId={id}
                author={author}
                width={width}
              />
            )}
          >
            <div className={styles.bookNavigationButtons}>
              <IconButton
                className={styles.bookNavigationButton}
                name={icons.ARROW_LEFT}
                size={30}
                title={translate('GoToInterp', [previousBook.title])}
                to={`/book/${previousBook.titleSlug}`}
              />

              <IconButton
                className={styles.bookUpButton}
                name={icons.ARROW_UP}
                size={30}
                title={translate('GoToInterp', [author.authorName])}
                to={`/author/${author.titleSlug}`}
              />

              <IconButton
                className={styles.bookNavigationButton}
                name={icons.ARROW_RIGHT}
                size={30}
                title={translate('GoToInterp', [nextBook.title])}
                to={`/book/${nextBook.titleSlug}`}
              />
            </div>
          </SwipeHeaderConnector>

          <div className={styles.contentContainer}>
            <MetadataComparisonPanel
              comparison={metadataComparison}
              isFetching={isFetchingMetadataComparison}
              error={metadataComparisonError}
              onOpenDrillIn={this.onMetadataDrillInPress}
            />

            {
              !isPopulated && !bookFilesError &&
                <LoadingIndicator />
            }

            {
              !isFetching && bookFilesError &&
                <div>
                  {translate('LoadingBookFilesFailed')}
                </div>
            }

            <Tabs selectedIndex={this.state.tabIndex} onSelect={this.onTabSelect}>
              <TabList
                className={styles.tabList}
              >
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
                  selectedTabIndex === 1 &&
                    <div className={styles.filterIcon}>
                      <InteractiveSearchFilterMenuConnector
                        type="book"
                      />
                    </div>
                }

              </TabList>

              <TabPanel>
                <AuthorHistoryTable
                  authorId={author.id}
                  bookId={id}
                />
              </TabPanel>

              <TabPanel>
                <InteractiveSearchTable
                  bookId={id}
                  type="book"
                />
              </TabPanel>

              <TabPanel>
                <BookFileEditorTable
                  authorId={author.id}
                  bookId={id}
                />
              </TabPanel>
            </Tabs>
          </div>

          <OrganizePreviewModalConnector
            isOpen={isOrganizeModalOpen}
            authorId={author.id}
            bookId={id}
            onModalClose={this.onOrganizeModalClose}
          />

          <RetagPreviewModalConnector
            isOpen={isRetagModalOpen}
            authorId={author.id}
            bookId={id}
            onModalClose={this.onRetagModalClose}
          />

          <EditBookModalConnector
            isOpen={isEditBookModalOpen}
            bookId={id}
            authorId={author.id}
            onModalClose={this.onEditBookModalClose}
            onDeleteAuthorPress={this.onDeleteBookPress}
          />

          <DeleteBookModal
            isOpen={isDeleteBookModalOpen}
            bookId={id}
            authorSlug={author.titleSlug}
            onModalClose={this.onDeleteBookModalClose}
          />

          <MetadataComparisonDrillInModal
            comparison={metadataComparison}
            isOpen={isMetadataDrillInModalOpen}
            onModalClose={this.onMetadataDrillInModalClose}
          />

        </PageContentBody>
      </PageContent>
    );
  }
}

BookDetails.propTypes = {
  id: PropTypes.number.isRequired,
  titleSlug: PropTypes.string.isRequired,
  title: PropTypes.string.isRequired,
  seriesTitle: PropTypes.string.isRequired,
  pageCount: PropTypes.number,
  overview: PropTypes.string,
  releaseDate: PropTypes.string.isRequired,
  ratings: PropTypes.object.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  links: PropTypes.arrayOf(PropTypes.object).isRequired,
  statistics: PropTypes.object.isRequired,
  monitored: PropTypes.bool.isRequired,
  shortDateFormat: PropTypes.string.isRequired,
  isSaving: PropTypes.bool.isRequired,
  isRefreshing: PropTypes.bool,
  isSearching: PropTypes.bool,
  isFetching: PropTypes.bool,
  isPopulated: PropTypes.bool,
  bookFilesError: PropTypes.object,
  hasBookFiles: PropTypes.bool.isRequired,
  author: PropTypes.object,
  previousBook: PropTypes.object,
  nextBook: PropTypes.object,
  isSmallScreen: PropTypes.bool.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired,
  onRefreshPress: PropTypes.func,
  onSearchPress: PropTypes.func.isRequired
};

BookDetails.defaultProps = {
  isSaving: false
};

export default BookDetails;
