import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  saveImportList,
  setImportListFieldValue,
  setImportListValue,
  testImportList,
  toggleAdvancedSettings
} from 'Store/Actions/settingsActions';
import createProviderSettingsSelector from 'Store/Selectors/createProviderSettingsSelector';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import EditImportListModalContent from './EditImportListModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.advancedSettings,
    (state) => state.settings.metadataProfiles,
    createProviderSettingsSelector('importLists'),
    (advancedSettings, metadataProfiles, importList) => {
      return {
        advancedSettings,
        showMetadataProfile: metadataProfiles.items.length > 1,
        ...importList
      };
    }
  );
}

const mapDispatchToProps = {
  setImportListValue,
  setImportListFieldValue,
  saveImportList,
  testImportList,
  toggleAdvancedSettings
};

class EditImportListModalContentConnector extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isPreviewFetching: false,
      previewError: null,
      preview: null
    };
  }

  //
  // Lifecycle

  componentDidUpdate(prevProps, prevState) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  componentWillUnmount() {
    if (this._abortPreviewRequest) {
      this._abortPreviewRequest();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setImportListValue({ name, value });
  };

  onFieldChange = ({ name, value }) => {
    this.props.setImportListFieldValue({ name, value });
  };

  onSavePress = () => {
    this.props.saveImportList({ id: this.props.id });
  };

  onTestPress = () => {
    this.props.testImportList({ id: this.props.id });
  };

  onPreviewPress = () => {
    if (this._abortPreviewRequest) {
      this._abortPreviewRequest();
    }

    const {
      request,
      abortRequest
    } = createAjaxRequest({
      url: '/importlist/preview',
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

  getPreviewPayload = () => {
    const {
      item,
      id
    } = this.props;

    return Object.keys(item).reduce((result, key) => {
      const setting = item[key];

      if (key === 'fields') {
        result.fields = setting;
      } else if (setting && Object.prototype.hasOwnProperty.call(setting, 'value')) {
        result[key] = setting.value;
      }

      return result;
    }, { id: id || 0 });
  };

  onAdvancedSettingsPress = () => {
    this.props.toggleAdvancedSettings();
  };

  //
  // Render

  render() {
    return (
      <EditImportListModalContent
        {...this.state}
        {...this.props}
        onSavePress={this.onSavePress}
        onTestPress={this.onTestPress}
        onPreviewPress={this.onPreviewPress}
        onAdvancedSettingsPress={this.onAdvancedSettingsPress}
        onInputChange={this.onInputChange}
        onFieldChange={this.onFieldChange}
      />
    );
  }
}

EditImportListModalContentConnector.propTypes = {
  id: PropTypes.number,
  isFetching: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  item: PropTypes.object.isRequired,
  setImportListValue: PropTypes.func.isRequired,
  setImportListFieldValue: PropTypes.func.isRequired,
  saveImportList: PropTypes.func.isRequired,
  testImportList: PropTypes.func.isRequired,
  toggleAdvancedSettings: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditImportListModalContentConnector);
