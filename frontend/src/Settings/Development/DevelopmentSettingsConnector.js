import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { fetchDevelopmentSettings, saveDevelopmentSettings, setDevelopmentSettingsValue, testDevelopmentMetadataSource, testOpenRouter } from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import DevelopmentSettings from './DevelopmentSettings';

const SECTION = 'development';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.advancedSettings,
    (state) => state.settings[SECTION],
    createSettingsSectionSelector(SECTION),
    (advancedSettings, developmentState, sectionSettings) => {
      return {
        advancedSettings,
        isTestingMetadataSource: developmentState.isTestingMetadataSource,
        metadataSourceTestResult: developmentState.metadataSourceTestResult,
        metadataSourceTestError: developmentState.metadataSourceTestError,
        isTestingOpenRouter: developmentState.isTestingOpenRouter,
        openRouterTestResult: developmentState.openRouterTestResult,
        openRouterTestError: developmentState.openRouterTestError,
        ...sectionSettings
      };
    }
  );
}

const mapDispatchToProps = {
  setDevelopmentSettingsValue,
  saveDevelopmentSettings,
  testDevelopmentMetadataSource,
  testOpenRouter,
  fetchDevelopmentSettings,
  clearPendingChanges
};

class DevelopmentSettingsConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchDevelopmentSettings();
  }

  componentWillUnmount() {
    this.props.clearPendingChanges({ section: `settings.${SECTION}` });
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setDevelopmentSettingsValue({ name, value });
  };

  onSavePress = () => {
    this.props.saveDevelopmentSettings();
  };

  onTestMetadataSourcePress = () => {
    this.props.testDevelopmentMetadataSource();
  };

  onTestOpenRouterPress = () => {
    this.props.testOpenRouter();
  };

  //
  // Render

  render() {
    return (
      <DevelopmentSettings
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
        onTestMetadataSourcePress={this.onTestMetadataSourcePress}
        onTestOpenRouterPress={this.onTestOpenRouterPress}
        {...this.props}
      />
    );
  }
}

DevelopmentSettingsConnector.propTypes = {
  setDevelopmentSettingsValue: PropTypes.func.isRequired,
  saveDevelopmentSettings: PropTypes.func.isRequired,
  testDevelopmentMetadataSource: PropTypes.func.isRequired,
  testOpenRouter: PropTypes.func.isRequired,
  fetchDevelopmentSettings: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(DevelopmentSettingsConnector);
