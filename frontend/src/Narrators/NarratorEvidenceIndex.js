import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
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

class NarratorEvidenceIndex extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetching: true,
      error: null,
      items: [],
      searchTerm: '',
      source: ''
    };

    this._abortRequest = null;
  }

  componentDidMount() {
    this.fetchNarrators();
  }

  componentWillUnmount() {
    if (this._abortRequest) {
      this._abortRequest();
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
    this.setState({ source }, this.fetchNarrators);
  };

  render() {
    const {
      isFetching,
      error,
      items,
      searchTerm,
      source
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

                        <EvidenceExamples examples={item.examples || []} />
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
