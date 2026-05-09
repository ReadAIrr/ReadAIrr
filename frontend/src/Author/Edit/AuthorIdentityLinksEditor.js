import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import SelectInput from 'Components/Form/SelectInput';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { icons, kinds } from 'Helpers/Props';
import styles from './AuthorIdentityLinksEditor.css';

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

class AuthorIdentityLinksEditor extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      linkAuthorId: '',
      linkRelationshipType: 'penName',
      linkDisplayPreference: 'canonical'
    };
  }

  //
  // Listeners

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
      authorId,
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
      canonicalAuthorId: authorId,
      aliasAuthorId: parseInt(aliasAuthorId),
      relationshipType: linkRelationshipType,
      displayPreference: linkDisplayPreference
    });

    this.setState({ linkAuthorId: '' });
  };

  onLinkSuggestionPress = (suggestion) => {
    this.props.onLinkAuthorPress({
      canonicalAuthorId: this.props.authorId,
      aliasAuthorId: suggestion.authorId,
      relationshipType: suggestion.relationshipType || 'penName',
      displayPreference: suggestion.displayPreference || 'canonical'
    });
  };

  //
  // Render

  renderSuggestions() {
    const {
      identitySuggestions,
      isFetchingIdentitySuggestions,
      identitySuggestionsError
    } = this.props;

    if (isFetchingIdentitySuggestions) {
      return (
        <div className={styles.suggestions}>
          <LoadingIndicator />
        </div>
      );
    }

    if (identitySuggestionsError) {
      return (
        <Alert kind={kinds.WARNING}>
          Unable to load author identity suggestions.
        </Alert>
      );
    }

    return (
      <div className={styles.suggestions}>
        <div className={styles.suggestionsTitle}>
          Suggested links
        </div>

        {
          identitySuggestions.length ?
            <div className={styles.suggestionList}>
              {
                identitySuggestions.map((suggestion) => {
                  return (
                    <div
                      key={suggestion.authorId}
                      className={styles.suggestionRow}
                    >
                      <div className={styles.suggestionMain}>
                        <Link to={`/author/${suggestion.titleSlug}`}>
                          {suggestion.authorName}
                        </Link>

                        <span className={styles.suggestionConfidence}>
                          {suggestion.confidenceLabel} · {suggestion.confidence}%
                        </span>
                      </div>

                      <div className={styles.suggestionReasons}>
                        {
                          (suggestion.reasons || []).map((reason) => {
                            return (
                              <div
                                key={reason.kind}
                                className={styles.suggestionReason}
                              >
                                <b>{reason.label}</b>: {reason.detail}
                              </div>
                            );
                          })
                        }
                      </div>

                      <Button onPress={() => this.onLinkSuggestionPress(suggestion)}>
                        Link
                      </Button>
                    </div>
                  );
                })
              }
            </div> :
            <div className={styles.empty}>
              No local pen-name suggestions found.
            </div>
        }
      </div>
    );
  }

  render() {
    const {
      authorId,
      allAuthors,
      linkedAuthors,
      identityStatistics,
      onUnlinkAuthorPress
    } = this.props;

    const {
      linkAuthorId,
      linkRelationshipType,
      linkDisplayPreference
    } = this.state;

    const linkedAuthorIds = linkedAuthors.map((linkedAuthor) => linkedAuthor.id);
    const linkableAuthors = allAuthors.filter((author) => {
      return author.id !== authorId && linkedAuthorIds.indexOf(author.id) === -1;
    });

    const selectedLinkAuthorId = linkAuthorId || linkableAuthors[0]?.id || '';

    return (
      <FieldSet legend="Linked Authors">
        <div className={styles.summary}>
          {identityStatistics.availableBookCount || 0}/{identityStatistics.bookCount || 0} available across {linkedAuthors.length + 1} identities
        </div>

        {
          linkedAuthors.length > 0 ?
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
            </div> :
            <div className={styles.empty}>
              No linked authors
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

        {this.renderSuggestions()}
      </FieldSet>
    );
  }
}

AuthorIdentityLinksEditor.propTypes = {
  authorId: PropTypes.number.isRequired,
  allAuthors: PropTypes.arrayOf(PropTypes.object).isRequired,
  linkedAuthors: PropTypes.arrayOf(PropTypes.object).isRequired,
  identityStatistics: PropTypes.object.isRequired,
  identitySuggestions: PropTypes.arrayOf(PropTypes.object).isRequired,
  isFetchingIdentitySuggestions: PropTypes.bool.isRequired,
  identitySuggestionsError: PropTypes.object,
  onLinkAuthorPress: PropTypes.func.isRequired,
  onUnlinkAuthorPress: PropTypes.func.isRequired
};

export default AuthorIdentityLinksEditor;
