import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import AutoSuggestInput from 'Components/Form/AutoSuggestInput';
import SelectInput from 'Components/Form/SelectInput';
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
  { key: 'providerMetadata', label: 'Provider' },
  { key: 'aiReview', label: 'AI Review' },
  { key: 'sttTranscript', label: 'STT transcript' }
];

const RELATIONSHIP_TYPE_OPTIONS = [
  { key: 'alias', value: 'Alias' }
];

const DISPLAY_PREFERENCE_OPTIONS = [
  { key: 'canonical', value: 'Preferred name' },
  { key: 'alias', value: 'Evidence name' },
  { key: 'both', value: 'Both' }
];

function normalizeName(value) {
  return (value || '').toLowerCase().replace(/[^a-z0-9]+/g, '');
}

function getSourceLabel(source) {
  switch (source) {
    case 'manual':
      return 'Manual';
    case 'providerMetadata':
      return 'Provider';
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

function BucketCounts({ bucketCounts }) {
  if (!bucketCounts?.length) {
    return null;
  }

  return (
    <div className={styles.narratorStats}>
      {
        bucketCounts.filter((item) => item.count > 0).map((item) => {
          return (
            <span
              key={item.bucket}
              className={styles.narratorStat}
            >
              {item.label}: {item.count}
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

BucketCounts.propTypes = {
  bucketCounts: PropTypes.arrayOf(PropTypes.object)
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
  aliasCanonicalNormalizedName,
  aliasRelationshipType,
  aliasDisplayPreference,
  aliasSuggestions,
  aliasValidationMessage,
  isSavingAlias,
  aliasError,
  onAliasCanonicalNameChange,
  onAliasSuggestionsFetchRequested,
  onAliasSuggestionsClearRequested,
  onAliasSuggestionSelected,
  onAliasRelationshipTypeChange,
  onAliasDisplayPreferenceChange,
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
      <BucketCounts bucketCounts={detail.bucketCounts} />

      <div className={styles.aliasPanel}>
        <div className={styles.detailTitle}>
          Standardized name
        </div>

        <div className={styles.identityNote}>
          Link this narrator evidence name to an existing preferred narrator identity. This only changes narrator evidence grouping and can be reversed.
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
          <AutoSuggestInput
            name="aliasCanonicalName"
            value={aliasCanonicalName}
            placeholder="Search existing narrator identity"
            suggestions={aliasSuggestions}
            getSuggestionValue={(item) => item.displayName}
            renderSuggestion={(item) => {
              return (
                <div className={styles.aliasSuggestion}>
                  <div>{item.displayName}</div>
                  <div className={styles.aliasSuggestionMeta}>
                    {item.evidenceCount} evidence item{item.evidenceCount === 1 ? '' : 's'} · {item.workCount} work{item.workCount === 1 ? '' : 's'}
                  </div>
                </div>
              );
            }}
            onChange={onAliasCanonicalNameChange}
            onInputBlur={() => {}}
            onSuggestionsFetchRequested={onAliasSuggestionsFetchRequested}
            onSuggestionsClearRequested={onAliasSuggestionsClearRequested}
            onSuggestionSelected={onAliasSuggestionSelected}
          />

          <SelectInput
            name="aliasRelationshipType"
            value={aliasRelationshipType}
            values={RELATIONSHIP_TYPE_OPTIONS}
            onChange={onAliasRelationshipTypeChange}
          />

          <SelectInput
            name="aliasDisplayPreference"
            value={aliasDisplayPreference}
            values={DISPLAY_PREFERENCE_OPTIONS}
            onChange={onAliasDisplayPreferenceChange}
          />

          <Button
            kind={kinds.PRIMARY}
            isDisabled={isSavingAlias || !aliasCanonicalNormalizedName}
            onPress={onSaveAliasPress}
          >
            Link to Selected Narrator
          </Button>
        </div>

        {
          aliasValidationMessage &&
            <Alert kind={kinds.WARNING}>
              {aliasValidationMessage}
            </Alert>
        }

        {
          aliasError &&
            <Alert kind={kinds.DANGER}>
              {aliasError.responseJSON?.message || aliasError.responseText || 'Unable to save narrator alias.'}
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
  aliasCanonicalNormalizedName: PropTypes.string.isRequired,
  aliasRelationshipType: PropTypes.string.isRequired,
  aliasDisplayPreference: PropTypes.string.isRequired,
  aliasSuggestions: PropTypes.arrayOf(PropTypes.object).isRequired,
  aliasValidationMessage: PropTypes.string,
  isSavingAlias: PropTypes.bool.isRequired,
  aliasError: PropTypes.object,
  onAliasCanonicalNameChange: PropTypes.func.isRequired,
  onAliasSuggestionsFetchRequested: PropTypes.func.isRequired,
  onAliasSuggestionsClearRequested: PropTypes.func.isRequired,
  onAliasSuggestionSelected: PropTypes.func.isRequired,
  onAliasRelationshipTypeChange: PropTypes.func.isRequired,
  onAliasDisplayPreferenceChange: PropTypes.func.isRequired,
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
      page: 1,
      pageSize: 50,
      totalRecords: 0,
      narratorOptions: [],
      searchTerm: '',
      source: '',
      selectedNarrator: null,
      isFetchingDetail: false,
      detailError: null,
      detail: null,
      aliasCanonicalName: '',
      aliasCanonicalNormalizedName: '',
      aliasRelationshipType: 'alias',
      aliasDisplayPreference: 'canonical',
      aliasSuggestions: [],
      aliasValidationMessage: '',
      isSavingAlias: false,
      aliasError: null
    };

    this._abortRequest = null;
    this._abortDetailRequest = null;
  }

  componentDidMount() {
    this.fetchNarrators();
    this.fetchNarratorOptions();
  }

  componentWillUnmount() {
    if (this._abortRequest) {
      this._abortRequest();
    }

    if (this._abortDetailRequest) {
      this._abortDetailRequest();
    }
  }

  fetchNarrators = (page = 1) => {
    const {
      searchTerm,
      source,
      pageSize
    } = this.state;

    if (this._abortRequest) {
      this._abortRequest();
    }

    this.setState({
      isFetching: true,
      error: null
    });

    const { request, abortRequest } = createAjaxRequest({
      url: '/narrator/paged',
      data: {
        page,
        pageSize,
        term: searchTerm,
        source
      }
    });

    this._abortRequest = abortRequest;

    request.done((response) => {
      const records = response.records || [];

      this.setState({
        isFetching: false,
        error: null,
        items: page === 1 ? records : this.state.items.concat(records),
        page: response.page || page,
        pageSize: response.pageSize || pageSize,
        totalRecords: response.totalRecords || records.length
      });
    });

    request.fail((xhr) => {
      this.setState({
        isFetching: false,
        error: xhr.aborted ? null : xhr
      });
    });
  };

  fetchNarratorOptions = () => {
    const { request } = createAjaxRequest({
      url: '/narrator'
    });

    request.done((narratorOptions) => {
      this.setState({
        narratorOptions
      });
    });
  };

  onSearchTermChange = (searchTerm) => {
    this.setState({
      searchTerm,
      page: 1,
      totalRecords: 0
    }, () => this.fetchNarrators(1));
  };

  onSourceFilterPress = (source) => {
    this.setState({
      source,
      page: 1,
      totalRecords: 0,
      selectedNarrator: null,
      detail: null,
      detailError: null
    }, () => this.fetchNarrators(1));
  };

  onLoadMorePress = () => {
    this.fetchNarrators(this.state.page + 1);
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
        aliasCanonicalName: '',
        aliasCanonicalNormalizedName: '',
        aliasRelationshipType: 'alias',
        aliasDisplayPreference: 'canonical',
        aliasSuggestions: [],
        aliasValidationMessage: '',
        aliasError: null
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
    const {
      aliasCanonicalNormalizedName,
      narratorOptions
    } = this.state;

    const selected = narratorOptions.find((item) => item.normalizedName === aliasCanonicalNormalizedName);

    this.setState({
      aliasCanonicalName: value,
      aliasCanonicalNormalizedName: selected?.displayName === value ? aliasCanonicalNormalizedName : '',
      aliasValidationMessage: '',
      aliasError: null
    });
  };

  onAliasSuggestionsFetchRequested = ({ value }) => {
    const {
      detail,
      narratorOptions
    } = this.state;

    const normalizedValue = normalizeName(value);
    const currentNormalizedName = detail?.normalizedName;

    const aliasSuggestions = narratorOptions
      .filter((item) => {
        return item.normalizedName !== currentNormalizedName &&
          item.displayName !== detail?.displayName &&
          (
            !normalizedValue ||
            normalizeName(item.displayName).includes(normalizedValue) ||
            normalizeName(item.canonicalDisplayName).includes(normalizedValue)
          );
      })
      .slice(0, 12);

    this.setState({ aliasSuggestions });
  };

  onAliasSuggestionsClearRequested = () => {
    this.setState({ aliasSuggestions: [] });
  };

  onAliasSuggestionSelected = (event, { suggestion }) => {
    this.setState({
      aliasCanonicalName: suggestion.displayName,
      aliasCanonicalNormalizedName: suggestion.normalizedName,
      aliasValidationMessage: '',
      aliasError: null
    });
  };

  onAliasRelationshipTypeChange = ({ value }) => {
    this.setState({ aliasRelationshipType: value });
  };

  onAliasDisplayPreferenceChange = ({ value }) => {
    this.setState({ aliasDisplayPreference: value });
  };

  onSaveAliasPress = () => {
    const {
      aliasCanonicalName,
      aliasCanonicalNormalizedName,
      aliasRelationshipType,
      aliasDisplayPreference,
      detail
    } = this.state;

    if (!detail) {
      return;
    }

    if (!aliasCanonicalNormalizedName) {
      this.setState({
        aliasValidationMessage: 'Select an existing narrator from the picker before linking.'
      });
      return;
    }

    if (aliasCanonicalNormalizedName === detail.normalizedName) {
      this.setState({
        aliasValidationMessage: 'A narrator identity cannot be linked to itself.'
      });
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
        relationshipType: aliasRelationshipType,
        displayPreference: aliasDisplayPreference
      })
    });

    request.done((alias) => {
      this.setState({
        isSavingAlias: false,
        selectedNarrator: null,
        detail: null
      }, () => {
        this.fetchNarrators();
        this.fetchNarratorOptions();
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
        this.fetchNarratorOptions();

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
      totalRecords,
      searchTerm,
      source,
      selectedNarrator,
      isFetchingDetail,
      detailError,
      detail,
      aliasCanonicalName,
      aliasCanonicalNormalizedName,
      aliasRelationshipType,
      aliasDisplayPreference,
      aliasSuggestions,
      aliasValidationMessage,
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
                        <BucketCounts bucketCounts={item.bucketCounts} />

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
                              aliasCanonicalNormalizedName={aliasCanonicalNormalizedName}
                              aliasRelationshipType={aliasRelationshipType}
                              aliasDisplayPreference={aliasDisplayPreference}
                              aliasSuggestions={aliasSuggestions}
                              aliasValidationMessage={aliasValidationMessage}
                              isSavingAlias={isSavingAlias}
                              aliasError={aliasError}
                              onAliasCanonicalNameChange={this.onAliasCanonicalNameChange}
                              onAliasSuggestionsFetchRequested={this.onAliasSuggestionsFetchRequested}
                              onAliasSuggestionsClearRequested={this.onAliasSuggestionsClearRequested}
                              onAliasSuggestionSelected={this.onAliasSuggestionSelected}
                              onAliasRelationshipTypeChange={this.onAliasRelationshipTypeChange}
                              onAliasDisplayPreferenceChange={this.onAliasDisplayPreferenceChange}
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

          {
            !isFetching && !error && items.length < totalRecords &&
              <div className={styles.loadMore}>
                <Button onPress={this.onLoadMorePress}>
                  Load more narrators ({items.length} of {totalRecords})
                </Button>
              </div>
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

export default NarratorEvidenceIndex;
