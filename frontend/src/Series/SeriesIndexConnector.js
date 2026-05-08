import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearSeries, fetchSeries, setSeriesFilter, setSeriesSearchTerm, setSeriesSort, setSeriesTableOption, setSeriesView } from 'Store/Actions/seriesActions';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import SeriesIndex from './SeriesIndex';

function createMapStateToProps() {
  return createSelector(
    createClientSideCollectionSelector('series', 'series'),
    createDimensionsSelector(),
    (series, dimensions) => {
      return {
        ...series,
        isSmallScreen: dimensions.isSmallScreen
      };
    }
  );
}

function createMapDispatchToProps(dispatch) {
  return {
    fetchSeries(payload) {
      dispatch(fetchSeries(payload));
    },

    clearSeries() {
      dispatch(clearSeries());
    },

    onTableOptionChange(payload) {
      dispatch(setSeriesTableOption(payload));
    },

    onSortSelect(sortKey) {
      dispatch(setSeriesSort({ sortKey }));
    },

    onFilterSelect(selectedFilterKey) {
      dispatch(setSeriesFilter({ selectedFilterKey }));
    },

    onSearchTermChange(searchTerm) {
      dispatch(setSeriesSearchTerm({ searchTerm }));
    },

    onViewSelect(view) {
      dispatch(setSeriesView({ view }));
    }
  };
}

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

export default connect(createMapStateToProps, createMapDispatchToProps)(SeriesIndexConnector);
