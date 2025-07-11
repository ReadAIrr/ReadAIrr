import PropTypes from 'prop-types';
import React, { Component } from 'react';
import TextTruncate from 'react-text-truncate';
import AuthorPoster from 'Author/AuthorPoster';
import CheckInput from 'Components/Form/CheckInput';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import AddAuthorOptionsForm from '../Common/AddAuthorOptionsForm.js';
import styles from './AddNewAuthorModalContent.css';

class AddNewAuthorModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      searchForMissingBooks: false
    };
  }

  //
  // Listeners

  onSearchForMissingBooksChange = ({ value }) => {
    this.setState({ searchForMissingBooks: value });
  };

  onAddAuthorPress = () => {
    this.props.onAddAuthorPress(this.state.searchForMissingBooks);
  };

  //
  // Render

  render() {
    const {
      authorName,
      disambiguation,
      overview,
      images,
      isAdding,
      isSmallScreen,
      onModalClose,
      ...otherProps
    } = this.props;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {translate('AddNewAuthor')}
        </ModalHeader>

        <ModalBody>
          <div className={styles.container}>
            {
              isSmallScreen ?
                null:
                <div className={styles.poster}>
                  <AuthorPoster
                    className={styles.poster}
                    images={images}
                    size={250}
                  />
                </div>
            }

            <div className={styles.info}>
              <div className={styles.name}>
                {authorName}
              </div>

              {
                !!disambiguation &&
                  <span className={styles.disambiguation}>({disambiguation})</span>
              }

              {
                overview ?
                  <div className={styles.overview}>
                    <TextTruncate
                      truncateText="…"
                      line={8}
                      text={overview}
                    />
                  </div> :
                  null
              }

              <AddAuthorOptionsForm
                includeNoneMetadataProfile={false}
                {...otherProps}
              />

            </div>
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          <label className={styles.searchForMissingBooksLabelContainer}>
            <span className={styles.searchForMissingBooksLabel}>
              Start search for missing books
            </span>

            <CheckInput
              containerClassName={styles.searchForMissingBooksContainer}
              className={styles.searchForMissingBooksInput}
              name="searchForMissingBooks"
              value={this.state.searchForMissingBooks}
              onChange={this.onSearchForMissingBooksChange}
            />
          </label>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.SUCCESS}
            isSpinning={isAdding}
            onPress={this.onAddAuthorPress}
          >
            Add {authorName}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddNewAuthorModalContent.propTypes = {
  authorName: PropTypes.string.isRequired,
  disambiguation: PropTypes.string,
  overview: PropTypes.string,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  isSmallScreen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onAddAuthorPress: PropTypes.func.isRequired
};

export default AddNewAuthorModalContent;
