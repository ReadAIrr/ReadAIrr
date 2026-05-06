import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchMetadataProfileSchema, saveMetadataProfile, setMetadataProfileValue } from 'Store/Actions/settingsActions';
import createProfileInUseSelector from 'Store/Selectors/createProfileInUseSelector';
import createProviderSettingsSelector from 'Store/Selectors/createProviderSettingsSelector';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import EditMetadataProfileModalContent from './EditMetadataProfileModalContent';

function createMapStateToProps() {
  return createSelector(
    createProviderSettingsSelector('metadataProfiles'),
    createProfileInUseSelector('metadataProfileId'),
    (metadataProfile, isInUse) => {
      return {
        ...metadataProfile,
        isInUse
      };
    }
  );
}

const mapDispatchToProps = {
  fetchMetadataProfileSchema,
  setMetadataProfileValue,
  saveMetadataProfile
};

class EditMetadataProfileModalContentConnector extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      dragIndex: null,
      dropIndex: null,
      isPreviewFetching: false,
      previewError: null,
      preview: null
    };
  }

  componentDidMount() {
    if (!this.props.id && !this.props.isPopulated) {
      this.props.fetchMetadataProfileSchema();
    }

    this.schedulePreviewFetch();
  }

  componentDidUpdate(prevProps, prevState) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }

    if (
      prevProps.item !== this.props.item ||
      prevProps.isFetching !== this.props.isFetching ||
      prevProps.isPopulated !== this.props.isPopulated
    ) {
      this.schedulePreviewFetch();
    }
  }

  componentWillUnmount() {
    if (this._previewTimeout) {
      clearTimeout(this._previewTimeout);
    }

    if (this._abortPreviewRequest) {
      this._abortPreviewRequest();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setMetadataProfileValue({ name, value });
  };

  onSavePress = () => {
    this.props.saveMetadataProfile({ id: this.props.id });
  };

  schedulePreviewFetch = () => {
    if (this._previewTimeout) {
      clearTimeout(this._previewTimeout);
    }

    if (this.props.isFetching || !this.props.isPopulated || !this.props.item) {
      return;
    }

    this._previewTimeout = setTimeout(this.fetchPreview, 250);
  };

  getPreviewPayload = () => {
    const {
      item,
      id
    } = this.props;

    return Object.keys(item).reduce((result, key) => {
      const setting = item[key];

      if (setting && Object.prototype.hasOwnProperty.call(setting, 'value')) {
        result[key] = setting.value;
      }

      return result;
    }, { id: id || 0 });
  };

  fetchPreview = () => {
    if (this._abortPreviewRequest) {
      this._abortPreviewRequest();
    }

    const {
      request,
      abortRequest
    } = createAjaxRequest({
      url: '/metadataprofile/preview',
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify(this.getPreviewPayload())
    });

    this._abortPreviewRequest = abortRequest;
    this.setState({
      isPreviewFetching: true,
      previewError: null
    });

    request.done((preview) => {
      this.setState({
        isPreviewFetching: false,
        preview
      });
    }).fail((xhr) => {
      if (xhr.aborted) {
        return;
      }

      this.setState({
        isPreviewFetching: false,
        previewError: xhr
      });
    });
  };

  //
  // Render

  render() {
    return (
      <EditMetadataProfileModalContent
        {...this.state}
        {...this.props}
        onSavePress={this.onSavePress}
        onInputChange={this.onInputChange}
      />
    );
  }
}

EditMetadataProfileModalContentConnector.propTypes = {
  id: PropTypes.number,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  setMetadataProfileValue: PropTypes.func.isRequired,
  fetchMetadataProfileSchema: PropTypes.func.isRequired,
  saveMetadataProfile: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditMetadataProfileModalContentConnector);
