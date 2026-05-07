import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import * as commandNames from 'Commands/commandNames';
import { toggleBooksMonitored } from 'Store/Actions/bookActions';
import { executeCommand } from 'Store/Actions/commandActions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import SeriesDetails from './SeriesDetails';

const AUTHOR_NOT_MONITORED_REASON = 'Missing because the author is not monitored.';
const BOOK_NOT_MONITORED_REASON = 'Missing because the book is not monitored.';
const DEFAULT_MISSING_REASON = 'Missing because no imported file exists for the monitored book or edition.';

function getMissingReason(book) {
  if (book.hasFile) {
    return null;
  }

  if (!book.authorMonitored) {
    return AUTHOR_NOT_MONITORED_REASON;
  }

  if (!book.monitored) {
    return BOOK_NOT_MONITORED_REASON;
  }

  if (book.missingReason === BOOK_NOT_MONITORED_REASON) {
    return DEFAULT_MISSING_REASON;
  }

  return book.missingReason || DEFAULT_MISSING_REASON;
}

function getCompleteness(books) {
  return {
    totalBooks: books.length,
    availableBooks: books.filter((book) => book.hasFile).length,
    missingBooks: books.filter((book) => !book.hasFile).length,
    unmonitoredBooks: books.filter((book) => !book.monitored).length,
    unmonitoredAuthors: books.filter((book) => !book.authorMonitored).length
  };
}

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
    const item = this.state.item;

    if (!item) {
      return;
    }

    const bookIds = item.books.map((book) => book.id);

    this.props.toggleBooksMonitored({
      bookIds,
      monitored
    });

    this.setState((state) => {
      const books = state.item.books.map((book) => {
        const updatedBook = {
          ...book,
          monitored
        };

        return {
          ...updatedBook,
          missingReason: getMissingReason(updatedBook)
        };
      });

      return {
        item: {
          ...state.item,
          books,
          completeness: getCompleteness(books)
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
