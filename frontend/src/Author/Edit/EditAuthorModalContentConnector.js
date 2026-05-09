import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchAuthor, saveAuthor, setAuthorValue } from 'Store/Actions/authorActions';
import createAllAuthorsSelector from 'Store/Selectors/createAllAuthorsSelector';
import createAuthorSelector from 'Store/Selectors/createAuthorSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import EditAuthorModalContent from './EditAuthorModalContent';

function createIsPathChangingSelector() {
  return createSelector(
    (state) => state.authors.pendingChanges,
    createAuthorSelector(),
    (pendingChanges, author) => {
      const path = pendingChanges.path;

      if (path == null) {
        return false;
      }

      return author.path !== path;
    }
  );
}

function createMapStateToProps() {
  return createSelector(
    (state) => state.authors,
    (state) => state.settings.metadataProfiles,
    createAllAuthorsSelector(),
    createAuthorSelector(),
    createIsPathChangingSelector(),
    (authorsState, metadataProfiles, allAuthors, author, isPathChanging) => {
      const {
        isSaving,
        saveError,
        pendingChanges
      } = authorsState;

      const authorSettings = _.pick(author, [
        'monitored',
        'monitorNewItems',
        'qualityProfileId',
        'metadataProfileId',
        'path',
        'tags'
      ]);

      const settings = selectSettings(authorSettings, pendingChanges, saveError);

      return {
        authorName: author.authorName,
        isSaving,
        saveError,
        isPathChanging,
        originalPath: author.path,
        allAuthors: _.orderBy(allAuthors, 'sortNameLastFirst'),
        linkedAuthors: author.linkedAuthors || [],
        identityStatistics: author.identityStatistics || {},
        item: settings.settings,
        showMetadataProfile: metadataProfiles.items.length > 1,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchSetAuthorValue: setAuthorValue,
  dispatchSaveAuthor: saveAuthor,
  dispatchFetchAuthor: fetchAuthor
};

class EditAuthorModalContentConnector extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      identitySuggestions: [],
      isFetchingIdentitySuggestions: false,
      identitySuggestionsError: null
    };
  }

  //
  // Lifecycle

  componentDidMount() {
    this.fetchAuthorIdentitySuggestions();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }

    if (prevProps.authorId !== this.props.authorId) {
      this.fetchAuthorIdentitySuggestions();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetAuthorValue({ name, value });
  };

  onSavePress = (moveFiles) => {
    this.props.dispatchSaveAuthor({
      id: this.props.authorId,
      moveFiles
    });
  };

  onAuthorIdentityLinkChange = () => {
    this.props.dispatchFetchAuthor();
    this.fetchAuthorIdentitySuggestions();

    if (this.props.onAuthorIdentityLinkChange) {
      this.props.onAuthorIdentityLinkChange();
    }
  };

  fetchAuthorIdentitySuggestions = () => {
    if (!this.props.authorId) {
      return;
    }

    this.setState({
      isFetchingIdentitySuggestions: true,
      identitySuggestionsError: null
    });

    const { request } = createAjaxRequest({
      url: `/authoridentitylink/suggestions/${this.props.authorId}`,
      method: 'GET',
      dataType: 'json'
    });

    request.done((identitySuggestions) => {
      this.setState({
        identitySuggestions,
        isFetchingIdentitySuggestions: false,
        identitySuggestionsError: null
      });
    });

    request.fail((xhr) => {
      this.setState({
        identitySuggestions: [],
        isFetchingIdentitySuggestions: false,
        identitySuggestionsError: xhr
      });
    });
  };

  onLinkAuthorPress = (payload) => {
    const { request } = createAjaxRequest({
      url: '/authoridentitylink',
      method: 'POST',
      data: JSON.stringify(payload),
      dataType: 'json'
    });

    request.done(this.onAuthorIdentityLinkChange);
  };

  onUnlinkAuthorPress = (linkId) => {
    const { request } = createAjaxRequest({
      url: `/authoridentitylink/${linkId}`,
      method: 'DELETE'
    });

    request.done(this.onAuthorIdentityLinkChange);
  };

  //
  // Render

  render() {
    return (
      <EditAuthorModalContent
        {...this.props}
        {...this.state}
        onInputChange={this.onInputChange}
        onLinkAuthorPress={this.onLinkAuthorPress}
        onUnlinkAuthorPress={this.onUnlinkAuthorPress}
        onSavePress={this.onSavePress}
        onMoveAuthorPress={this.onMoveAuthorPress}
      />
    );
  }
}

EditAuthorModalContentConnector.propTypes = {
  authorId: PropTypes.number,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  dispatchSetAuthorValue: PropTypes.func.isRequired,
  dispatchSaveAuthor: PropTypes.func.isRequired,
  dispatchFetchAuthor: PropTypes.func.isRequired,
  onAuthorIdentityLinkChange: PropTypes.func,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditAuthorModalContentConnector);
