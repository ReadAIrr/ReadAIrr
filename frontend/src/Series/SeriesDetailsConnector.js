import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import * as commandNames from 'Commands/commandNames';
import { toggleBooksMonitored } from 'Store/Actions/bookActions';
import { executeCommand } from 'Store/Actions/commandActions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import SeriesDetails from './SeriesDetails';

class SeriesDetailsConnector extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isFetching: true,
      error: null,
      item: null
    };
  }

  componentDidMount() {
    this.fetchSeries();
  }

  componentWillUnmount() {
    if (this._abortRequest) {
      this._abortRequest();
    }
  }

  fetchSeries = () => {
    const {
      request,
      abortRequest
    } = createAjaxRequest({
      url: `/series/${this.props.match.params.id}`,
      dataType: 'json'
    });

    this._abortRequest = abortRequest;

    request.done((item) => {
      this.setState({
        isFetching: false,
        error: null,
        item
      });
    });

    request.fail((xhr) => {
      this.setState({
        isFetching: false,
        error: xhr.aborted ? null : xhr
      });
    });
  };

  onMonitorSeriesPress = (monitored) => {
    const bookIds = this.state.item.books.map((book) => book.id);

    this.props.toggleBooksMonitored({
      bookIds,
      monitored
    });

    this.setState((state) => {
      return {
        item: {
          ...state.item,
          books: state.item.books.map((book) => {
            return {
              ...book,
              monitored
            };
          })
        }
      };
    });
  };

  onSearchSeriesPress = () => {
    const bookIds = this.state.item.books
      .filter((book) => book.monitored && book.authorMonitored)
      .map((book) => book.id);

    this.props.executeCommand({
      name: commandNames.BOOK_SEARCH,
      bookIds
    });
  };

  render() {
    return (
      <SeriesDetails
        {...this.state}
        onMonitorSeriesPress={this.onMonitorSeriesPress}
        onSearchSeriesPress={this.onSearchSeriesPress}
      />
    );
  }
}

SeriesDetailsConnector.propTypes = {
  match: PropTypes.object.isRequired,
  toggleBooksMonitored: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired
};

const mapDispatchToProps = {
  toggleBooksMonitored,
  executeCommand
};

export default connect(null, mapDispatchToProps)(SeriesDetailsConnector);
