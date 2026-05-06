import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearSeries, fetchSeries } from 'Store/Actions/seriesActions';
import SeriesIndex from './SeriesIndex';

function createMapStateToProps() {
  return createSelector(
    (state) => state.series,
    (series) => {
      return {
        ...series,
        items: [...series.items].sort((a, b) => a.title.localeCompare(b.title))
      };
    }
  );
}

const mapDispatchToProps = {
  fetchSeries,
  clearSeries
};

class SeriesIndexConnector extends Component {
  componentDidMount() {
    this.props.fetchSeries();
  }

  componentWillUnmount() {
    this.props.clearSeries();
  }

  render() {
    return (
      <SeriesIndex
        {...this.props}
      />
    );
  }
}

SeriesIndexConnector.propTypes = {
  fetchSeries: PropTypes.func.isRequired,
  clearSeries: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(SeriesIndexConnector);
