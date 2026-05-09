import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchStatus } from 'Store/Actions/systemActions';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import About from './About';

function createMapStateToProps() {
  return createSelector(
    (state) => state.system.status,
    createUISettingsSelector(),
    (status, uiSettings) => {
      return {
        ...status.item,
        timeFormat: uiSettings.timeFormat,
        longDateFormat: uiSettings.longDateFormat
      };
    }
  );
}

const mapDispatchToProps = {
  fetchStatus
};

class AboutConnector extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetchingMetadataServiceStatus: false,
      metadataServiceStatus: null,
      metadataServiceStatusError: null
    };
  }

  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchStatus();
    this.fetchMetadataServiceStatus();
  }

  fetchMetadataServiceStatus = () => {
    this.setState({
      isFetchingMetadataServiceStatus: true,
      metadataServiceStatusError: null
    });

    const { request } = createAjaxRequest({
      url: '/system/metadata',
      method: 'GET',
      dataType: 'json'
    });

    request.done((data) => {
      this.setState({
        isFetchingMetadataServiceStatus: false,
        metadataServiceStatus: data,
        metadataServiceStatusError: null
      });
    });

    request.fail((xhr) => {
      this.setState({
        isFetchingMetadataServiceStatus: false,
        metadataServiceStatusError: xhr
      });
    });
  };

  //
  // Render

  render() {
    return (
      <About
        {...this.props}
        {...this.state}
      />
    );
  }
}

AboutConnector.propTypes = {
  fetchStatus: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AboutConnector);
