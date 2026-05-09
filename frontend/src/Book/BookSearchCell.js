import PropTypes from 'prop-types';
import React, { Component } from 'react';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons } from 'Helpers/Props';
import BookInteractiveSearchModalConnector from './Search/BookInteractiveSearchModalConnector';
import styles from './BookSearchCell.css';

class BookSearchCell extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isDetailsModalOpen: false
    };
  }

  //
  // Listeners

  onManualSearchPress = () => {
    this.setState({ isDetailsModalOpen: true });
  };

  onDetailsModalClose = () => {
    this.setState({ isDetailsModalOpen: false });
  };

  //
  // Render

  render() {
    const {
      bookId,
      bookTitle,
      authorName,
      className,
      component: Cell,
      isSearching,
      onSearchPress,
      ...otherProps
    } = this.props;

    return (
      <Cell className={className}>
        <SpinnerIconButton
          name={icons.SEARCH}
          isSpinning={isSearching}
          onPress={onSearchPress}
        />

        <IconButton
          name={icons.INTERACTIVE}
          onPress={this.onManualSearchPress}
        />

        <BookInteractiveSearchModalConnector
          isOpen={this.state.isDetailsModalOpen}
          bookId={bookId}
          bookTitle={bookTitle}
          authorName={authorName}
          onModalClose={this.onDetailsModalClose}
          {...otherProps}
        />

      </Cell>
    );
  }
}

BookSearchCell.propTypes = {
  bookId: PropTypes.number.isRequired,
  authorId: PropTypes.number.isRequired,
  bookTitle: PropTypes.string.isRequired,
  authorName: PropTypes.string.isRequired,
  className: PropTypes.string,
  component: PropTypes.elementType,
  isSearching: PropTypes.bool.isRequired,
  onSearchPress: PropTypes.func.isRequired
};

BookSearchCell.defaultProps = {
  className: styles.BookSearchCell,
  component: TableRowCell
};

export default BookSearchCell;
