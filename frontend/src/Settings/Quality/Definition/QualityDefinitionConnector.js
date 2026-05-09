import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { setQualityDefinitionValue } from 'Store/Actions/settingsActions';
import QualityDefinition from './QualityDefinition';

const mapDispatchToProps = {
  setQualityDefinitionValue,
  clearPendingChanges
};

class QualityDefinitionConnector extends Component {

  componentWillUnmount() {
    this.props.clearPendingChanges({ section: 'settings.qualityDefinitions' });
  }

  //
  // Listeners

  onTitleChange = ({ value }) => {
    this.props.setQualityDefinitionValue({ id: this.props.id, name: 'title', value });
  };

  onSizeChange = ({ minSize, maxSize }) => {
    const {
      id,
      minSize: currentMinSize,
      maxSize: currentMaxSize
    } = this.props;

    if (minSize !== currentMinSize) {
      this.props.setQualityDefinitionValue({ id, name: 'minSize', value: minSize });
    }

    if (maxSize !== currentMaxSize) {
      this.props.setQualityDefinitionValue({ id, name: 'maxSize', value: maxSize });
    }
  };

  onTargetSizeChange = (value) => {
    this.props.setQualityDefinitionValue({ id: this.props.id, name: 'targetSize', value });
  };

  onSizePreferenceChange = (value) => {
    this.props.setQualityDefinitionValue({ id: this.props.id, name: 'sizePreference', value });
  };

  onBitratePreferenceChange = (value) => {
    this.props.setQualityDefinitionValue({ id: this.props.id, name: 'bitratePreference', value });
  };

  onEnforceSizeLimitsChange = (value) => {
    this.props.setQualityDefinitionValue({ id: this.props.id, name: 'enforceSizeLimits', value });
  };

  //
  // Render

  render() {
    return (
      <QualityDefinition
        {...this.props}
        onTitleChange={this.onTitleChange}
        onSizeChange={this.onSizeChange}
        onTargetSizeChange={this.onTargetSizeChange}
        onSizePreferenceChange={this.onSizePreferenceChange}
        onBitratePreferenceChange={this.onBitratePreferenceChange}
        onEnforceSizeLimitsChange={this.onEnforceSizeLimitsChange}
      />
    );
  }
}

QualityDefinitionConnector.propTypes = {
  id: PropTypes.number.isRequired,
  minSize: PropTypes.number,
  maxSize: PropTypes.number,
  enforceSizeLimits: PropTypes.bool.isRequired,
  targetSize: PropTypes.number,
  sizePreference: PropTypes.number.isRequired,
  bitratePreference: PropTypes.number.isRequired,
  setQualityDefinitionValue: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(null, mapDispatchToProps)(QualityDefinitionConnector);
