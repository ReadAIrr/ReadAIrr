import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSearchInput from 'Components/Page/Toolbar/PageToolbarSearchInput';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import { align, kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './NarratorEvidenceIndex.css';

const SOURCE_FILTERS = [
  { key: '', label: 'All evidence' },
  { key: 'manual', label: 'Manual' },
  { key: 'aiReview', label: 'AI Review' },
  { key: 'sttTranscript', label: 'STT transcript' }
];

function getSourceLabel(source) {
  switch (source) {
    case 'manual':
      return 'Manual';
    case 'aiReview':
      return 'AI Review';
    case 'sttTranscript':
      return 'STT transcript';
    default:
      return source || 'Unknown';
  }
}

function SourceCounts({ sourceCounts }) {
  return (
    <div className={styles.sourceCounts}>
      {
        (sourceCounts || []).map((item) => {
          return (
            <span
              key={item.source}
              className={styles.sourceCount}
            >
              {getSourceLabel(item.source)}: {item.count}
            </span>
          );
        })
      }
    </div>
  );
}

function NarratorStats({ item }) {
  const stats = [
    `${item.workCount} work${item.workCount === 1 ? '' : 's'}`,
    `${item.matchedBookCount} matched book${item.matchedBookCount === 1 ? '' : 's'}`,
    `${item.unmappedFileCount} unmapped file${item.unmappedFileCount === 1 ? '' : 's'}`,
    `${item.manualEvidenceCount} manual`,
    `${item.reviewEvidenceCount} review`
  ];

  return (
    <div className={styles.narratorStats}>
      {
        stats.map((stat) => {
          return (
            <span
              key={stat}
              className={styles.narratorStat}
            >
              {stat}
            </span>
          );
        })
      }
    </div>
  );
}

NarratorStats.propTypes = {
  item: PropTypes.object.isRequired
};

SourceCounts.propTypes = {
  sourceCounts: PropTypes.arrayOf(PropTypes.object).isRequired
};

function EvidenceExamples({ examples }) {
  if (!examples?.length) {
    return null;
  }

  return (
    <ul className={styles.examples}>
      {
        examples.map((example, index) => {
          const confidence = example.confidence == null ? '' : ` (${example.confidence}%)`;

          return (
            <li key={index}>
              {
                example.bookFileId ?
                  <Link to="/unmapped">
                    {example.path || `Unmapped file ${example.bookFileId}`}
                  </Link> :
                  (example.path || 'Evidence without file context')
              }
              <span className={styles.exampleMeta}>
                {getSourceLabel(example.source)}{confidence}
              </span>
            </li>
          );
        })
      }
    </ul>
  );
}

EvidenceExamples.propTypes = {
  examples: PropTypes.arrayOf(PropTypes.object).isRequired
};

function WorkLink({ item }) {
  const title = item.bookTitle || item.path || 'Evidence without matched book context';

  if (item.bookTitleSlug) {
    return (
      <Link to={`/book/${item.bookTitleSlug}`}>
        {title}
      </Link>
    );
  }

  if (item.bookFileId) {
    return (
      <Link to="/unmapped">
        {title}
      </Link>
    );
  }

  return title;
}

WorkLink.propTypes = {
  item: PropTypes.object.isRequired
};

function NarratorDetailPanel({
  detail,
  isFetching,
  error,
  aliasCanonicalName,
  isSavingAlias,
  aliasError,
  onAliasCanonicalNameChange,
  onSaveAliasPress,
  onDeleteAliasPress
}) {
  if (isFetching) {
    return (
      <div className={styles.detailPanel}>
        <LoadingIndicator />
      </div>
    );
  }

  if (error) {
    return (
      <Alert kind={kinds.DANGER}>
        Unable to load narrator details.
      </Alert>
    );
  }

  if (!detail) {
    return null;
  }

  return (
    <div className={styles.detailPanel}>
      <div className={styles.detailTitle}>
        Known works and review evidence
      </div>

      <div className={styles.identityNote}>
        {detail.reviewOnlyReason}
      </div>

      <NarratorStats item={detail} />

      <div className={styles.aliasPanel}>
        <div className={styles.detailTitle}>
          Standardized name
        </div>

        <div className={styles.identityNote}>
          Link this narrator evidence name to a preferred display name. This only changes narrator evidence grouping and can be reversed.
        </div>

        {
          !!detail.aliases?.length &&
            <ul className={styles.aliasList}>
              {
                detail.aliases.map((alias) => {
                  return (
                    <li key={alias.id}>
                      <span>
                        {alias.aliasName} -> {alias.canonicalName}
                      </span>
                      <Button
                        kind={kinds.DEFAULT}
                        onPress={() => onDeleteAliasPress(alias.id)}
                      >
                        Unlink
                      </Button>
                    </li>
                  );
                })
              }
            </ul>
        }

        <div className={styles.aliasEditor}>
          <TextInput
            name="aliasCanonicalName"
            value={aliasCanonicalName}
            placeholder="Preferred narrator name"
            onChange={onAliasCanonicalNameChange}
          />

          <Button
            kind={kinds.PRIMARY}
            isDisabled={isSavingAlias || !aliasCanonicalName || aliasCanonicalName === detail.displayName}
            onPress={onSaveAliasPress}
          >
            Standardize Current Name
          </Button>
        </div>

        {
          aliasError &&
            <Alert kind={kinds.DANGER}>
              Unable to save narrator alias.
            </Alert>
        }
      </div>

      <div className={styles.workList}>
        {
          (detail.works || []).map((item) => {
            const confidence = item.confidence == null ? 'Unknown confidence' : `${item.confidence}% confidence`;

            return (
              <div
                key={item.evidenceId}
                className={styles.workRow}
              >
                <div className={styles.workTitle}>
                  <WorkLink item={item} />
                </div>

                <div className={styles.workMeta}>
                  {
                    item.authorTitleSlug ?
                      <Link to={`/author/${item.authorTitleSlug}`}>
                        {item.authorName}
                      </Link> :
                      (item.authorName || 'Unknown author')
                  }

                  {
                    item.editionTitle &&
                      <span> - {item.editionTitle}</span>
                  }
                </div>

                {
                  item.path &&
                    <div className={styles.workPath}>
                      {item.path}
                    </div>
                }

                <div className={styles.workMeta}>
                  {getSourceLabel(item.source)} - {confidence}
                </div>
              </div>
            );
          })
        }
      </div>
    </div>
  );
}

NarratorDetailPanel.propTypes = {
  detail: PropTypes.object,
  error: PropTypes.object,
  isFetching: PropTypes.bool.isRequired,
  aliasCanonicalName: PropTypes.string.isRequired,
  isSavingAlias: PropTypes.bool.isRequired,
  aliasError: PropTypes.object,
  onAliasCanonicalNameChange: PropTypes.func.isRequired,
  onSaveAliasPress: PropTypes.func.isRequired,
  onDeleteAliasPress: PropTypes.func.isRequired
};

class NarratorEvidenceIndex extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetching: true,
      error: null,
      items: [],
      searchTerm: '',
      source: '',
      selectedNarrator: null,
      isFetchingDetail: false,
      detailError: null,
      detail: null,
      aliasCanonicalName: '',
      isSavingAlias: false,
      aliasError: null
    };

    this._abortRequest = null;
    this._abortDetailRequest = null;
  }

  componentDidMount() {
    this.fetchNarrators();
  }

  componentWillUnmount() {
    if (this._abortRequest) {
      this._abortRequest();
    }

    if (this._abortDetailRequest) {
      this._abortDetailRequest();
    }
  }

  fetchNarrators = () => {
    const {
      searchTerm,
      source
    } = this.state;

    if (this._abortRequest) {
      this._abortRequest();
    }

    this.setState({
      isFetching: true,
      error: null
    });

    const { request, abortRequest } = createAjaxRequest({
      url: '/narrator',
      data: {
        term: searchTerm,
        source
      }
    });

    this._abortRequest = abortRequest;

    request.done((items) => {
      this.setState({
        isFetching: false,
        error: null,
        items
      });
    });

    request.fail((xhr) => {
      this.setState({
        isFetching: false,
        error: xhr.aborted ? null : xhr
      });
    });
  };

  onSearchTermChange = (searchTerm) => {
    this.setState({ searchTerm }, this.fetchNarrators);
  };

  onSourceFilterPress = (source) => {
    this.setState({
      source,
      selectedNarrator: null,
      detail: null,
      detailError: null
    }, this.fetchNarrators);
  };

  onDetailPress = (normalizedName) => {
    const {
      selectedNarrator,
      source
    } = this.state;

    if (selectedNarrator === normalizedName) {
      this.setState({
        selectedNarrator: null,
        detail: null,
        detailError: null
      });

      return;
    }

    if (this._abortDetailRequest) {
      this._abortDetailRequest();
    }

    this.setState({
      selectedNarrator: normalizedName,
      isFetchingDetail: true,
      detailError: null,
      detail: null
    });

    const { request, abortRequest } = createAjaxRequest({
      url: `/narrator/${normalizedName}`,
      data: {
        source
      }
    });

    this._abortDetailRequest = abortRequest;

    request.done((detail) => {
      this.setState({
        isFetchingDetail: false,
        detailError: null,
        detail,
        aliasCanonicalName: detail.canonicalDisplayName || detail.displayName || ''
      });
    });

    request.fail((xhr) => {
      this.setState({
        isFetchingDetail: false,
        detailError: xhr.aborted ? null : xhr
      });
    });
  };

  onAliasCanonicalNameChange = ({ value }) => {
    this.setState({
      aliasCanonicalName: value,
      aliasError: null
    });
  };

  onSaveAliasPress = () => {
    const {
      aliasCanonicalName,
      detail
    } = this.state;

    if (!detail || !aliasCanonicalName || aliasCanonicalName === detail.displayName) {
      return;
    }

    this.setState({
      isSavingAlias: true,
      aliasError: null
    });

    const { request } = createAjaxRequest({
      url: '/narrator/aliases',
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify({
        canonicalName: aliasCanonicalName,
        aliasName: detail.displayName,
        relationshipType: 'alias',
        displayPreference: 'canonical'
      })
    });

    request.done((alias) => {
      this.setState({
        isSavingAlias: false,
        selectedNarrator: null,
        detail: null
      }, () => {
        this.fetchNarrators();
        this.onDetailPress(alias.canonicalNormalizedName);
      });
    });

    request.fail((xhr) => {
      this.setState({
        isSavingAlias: false,
        aliasError: xhr.aborted ? null : xhr
      });
    });
  };

  onDeleteAliasPress = (aliasId) => {
    const {
      detail
    } = this.state;

    this.setState({
      isSavingAlias: true,
      aliasError: null
    });

    const { request } = createAjaxRequest({
      url: `/narrator/aliases/${aliasId}`,
      method: 'DELETE'
    });

    request.done(() => {
      this.setState({
        isSavingAlias: false,
        selectedNarrator: null,
        detail: null
      }, () => {
        this.fetchNarrators();

        if (detail?.normalizedName) {
          this.onDetailPress(detail.normalizedName);
        }
      });
    });

    request.fail((xhr) => {
      this.setState({
        isSavingAlias: false,
        aliasError: xhr.aborted ? null : xhr
      });
    });
  };

  render() {
    const {
      isFetching,
      error,
      items,
      searchTerm,
      source,
      selectedNarrator,
      isFetchingDetail,
      detailError,
      detail,
      aliasCanonicalName,
      isSavingAlias,
      aliasError
    } = this.state;

    return (
      <PageContent>
        <PageToolbar>
          <PageToolbarSection />

          <PageToolbarSection
            alignContent={align.RIGHT}
            collapseButtons={false}
          >
            <PageToolbarSearchInput
              name="narratorEvidenceSearch"
              value={searchTerm}
              placeholder="Filter narrator evidence"
              isDisabled={isFetching && !items.length}
              onChange={this.onSearchTermChange}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody>
          <Alert kind={kinds.INFO}>
            Narrators are shown from review evidence only. This is not canonical provider-confirmed metadata yet.
          </Alert>

          <div className={styles.filterButtons}>
            {
              SOURCE_FILTERS.map((filter) => {
                return (
                  <Button
                    key={filter.key}
                    kind={source === filter.key ? kinds.PRIMARY : kinds.DEFAULT}
                    onPress={() => this.onSourceFilterPress(filter.key)}
                  >
                    {filter.label}
                  </Button>
                );
              })
            }
          </div>

          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <Alert kind={kinds.DANGER}>
                Unable to load narrator evidence.
              </Alert>
          }

          {
            !isFetching && !error && !items.length &&
              <div className={styles.emptyMessage}>
                No narrator evidence matches the current search or source filter.
              </div>
          }

          {
            !isFetching && !error && !!items.length &&
              <div className={styles.narratorList}>
                {
                  items.map((item) => {
                    return (
                      <div
                        key={item.normalizedName}
                        className={styles.narratorRow}
                      >
                        <div className={styles.narratorHeader}>
                          <div>
                            <div className={styles.narratorName}>
                              {item.displayName}
                            </div>

                            <div className={styles.narratorMeta}>
                              {item.evidenceCount} evidence item{item.evidenceCount === 1 ? '' : 's'}
                            </div>
                          </div>

                          <SourceCounts sourceCounts={item.sourceCounts || []} />
                        </div>

                        <div className={styles.identityNote}>
                          {item.reviewOnlyReason}
                        </div>

                        <NarratorStats item={item} />

                        <EvidenceExamples examples={item.examples || []} />

                        <div className={styles.rowActions}>
                          <Button
                            kind={selectedNarrator === item.normalizedName ? kinds.PRIMARY : kinds.DEFAULT}
                            onPress={() => this.onDetailPress(item.normalizedName)}
                          >
                            {selectedNarrator === item.normalizedName ? 'Hide Details' : 'View Details'}
                          </Button>
                        </div>

                        {
                          selectedNarrator === item.normalizedName &&
                            <NarratorDetailPanel
                              detail={detail}
                              error={detailError}
                              isFetching={isFetchingDetail}
                              aliasCanonicalName={aliasCanonicalName}
                              isSavingAlias={isSavingAlias}
                              aliasError={aliasError}
                              onAliasCanonicalNameChange={this.onAliasCanonicalNameChange}
                              onSaveAliasPress={this.onSaveAliasPress}
                              onDeleteAliasPress={this.onDeleteAliasPress}
                            />
                        }
                      </div>
                    );
                  })
                }
              </div>
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

export default NarratorEvidenceIndex;
