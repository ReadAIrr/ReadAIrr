import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddNewAuthorSearchResultConnector from './Author/AddNewAuthorSearchResultConnector';
import AddNewBookSearchResultConnector from './Book/AddNewBookSearchResultConnector';
import styles from './AddNewItem.css';

class AddNewItem extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      term: props.term || '',
      isFetching: false
    };
  }

  componentDidMount() {
    const term = this.state.term;

    if (term) {
      this.props.onSearchChange(term);
    }
  }

  componentDidUpdate(prevProps) {
    const {
      term,
      isFetching
    } = this.props;

    if (term && term !== prevProps.term) {
      this.setState({
        term,
        isFetching: true
      });
      this.props.onSearchChange(term);
    } else if (isFetching !== prevProps.isFetching) {
      this.setState({
        isFetching
      });
    }
  }

  //
  // Listeners

  onSearchInputChange = ({ value }) => {
    const hasValue = !!value.trim();

    this.setState({ term: value, isFetching: hasValue }, () => {
      if (hasValue) {
        this.props.onSearchChange(value);
      } else {
        this.props.onClearSearch();
      }
    });
  };

  onClearSearchPress = () => {
    this.setState({ term: '' });
    this.props.onClearSearch();
  };

  //
  // Render

  render() {
    const {
      error,
      items,
      hasExistingAuthors
    } = this.props;

    const term = this.state.term;
    const isFetching = this.state.isFetching;

    return (
      <PageContent title={translate('AddNewItem')}>
        <PageContentBody>
          <div className={styles.searchContainer}>
            <div className={styles.searchIconContainer}>
              <Icon
                name={icons.SEARCH}
                size={20}
              />
            </div>

            <TextInput
              className={styles.searchInput}
              name="searchBox"
              value={term}
              placeholder={translate('SearchBoxPlaceHolder')}
              autoFocus={true}
              onChange={this.onSearchInputChange}
            />

            <Button
              className={styles.clearLookupButton}
              onPress={this.onClearSearchPress}
            >
              <Icon
                name={icons.REMOVE}
                size={20}
              />
            </Button>
          </div>

          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && !!error ?
              <div className={styles.message}>
                <div className={styles.helpText}>
                  {translate('FailedLoadingSearchResults')}
                </div>

                <Alert kind={kinds.WARNING}>{getErrorMessage(error)}</Alert>

                <div>
                  <Link to="https://wiki.servarr.com/readarr/troubleshooting#invalid-response-received-from-metadata-api">
                    {translate('WhySearchesCouldBeFailing')}
                  </Link>
                </div>
              </div> : null
          }

          {
            !isFetching && !error && !!items.length &&
              <div className={styles.searchResults}>
                {
                  items.map((item) => {
                    if (item.author) {
                      const author = item.author;
                      return (
                        <AddNewAuthorSearchResultConnector
                          key={item.id}
                          {...author}
                        />
                      );
                    } else if (item.book) {
                      const book = item.book;
                      return (
                        <AddNewBookSearchResultConnector
                          key={item.id}
                          isExistingBook={'id' in book && book.id !== 0}
                          isExistingAuthor={'id' in book.author && book.author.id !== 0}
                          {...book}
                        />
                      );
                    }
                    return null;
                  })
                }
              </div>
          }

          {
            !isFetching && !error && !items.length && !!term &&
              <div className={styles.message}>
                <div className={styles.noResults}>
                  {translate('CouldntFindAnyResultsForTerm', [term])}
                </div>
                <div>
                  You can also search using the
                  <Link to="https://goodreads.com"> Goodreads ID </Link>
                  of a book (e.g. edition:656), work (e.g. work:4912783) or author (e.g. author:128382), the isbn (e.g. isbn:067003469X) or the asin (e.g. asin:B00JCDK5ME)
                </div>
              </div>
          }

          {
            term ?
              null :
              <div className={styles.message}>
                <div className={styles.helpText}>
                  {translate('ItsEasyToAddANewAuthorOrBookJustStartTypingTheNameOfTheItemYouWantToAdd')}
                </div>
                <div>
                  You can also search using the
                  <Link to="https://goodreads.com"> Goodreads ID </Link>
                  of a book (e.g. edition:656), work (e.g. work:4912783) or author (e.g. author:128382), the isbn (e.g. isbn:067003469X) or the asin (e.g. asin:B00JCDK5ME)
                </div>
              </div>
          }

          {
            !term && !hasExistingAuthors ?
              <div className={styles.message}>
                <div className={styles.noAuthorsText}>
                  You haven't added any authors yet, do you want to add an existing library location (Root Folder) and update?
                </div>
                <div>
                  <Button
                    to="/settings/mediamanagement"
                    kind={kinds.PRIMARY}
                  >
                    {translate('AddRootFolder')}
                  </Button>
                </div>
              </div> :
              null
          }

          <div />
        </PageContentBody>
      </PageContent>
    );
  }
}

AddNewItem.propTypes = {
  term: PropTypes.string,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  hasExistingAuthors: PropTypes.bool.isRequired,
  onSearchChange: PropTypes.func.isRequired,
  onClearSearch: PropTypes.func.isRequired
};

export default AddNewItem;
