import PropTypes from 'prop-types';
import React, { Component } from 'react';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import { icons } from 'Helpers/Props';
import styles from './PageToolbarSearchInput.css';

class PageToolbarSearchInput extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      value: props.value || ''
    };

    this._debounceTimer = null;
  }

  componentDidUpdate(prevProps) {
    if (prevProps.value !== this.props.value && this.props.value !== this.state.value) {
      this.setState({ value: this.props.value || '' });
    }
  }

  componentWillUnmount() {
    if (this._debounceTimer) {
      clearTimeout(this._debounceTimer);
    }
  }

  //
  // Listeners

  onChange = ({ value }) => {
    this.setState({ value });

    if (this._debounceTimer) {
      clearTimeout(this._debounceTimer);
    }

    this._debounceTimer = setTimeout(() => {
      this.props.onChange(value.trim());
    }, this.props.debounce);
  };

  onClearPress = () => {
    if (this._debounceTimer) {
      clearTimeout(this._debounceTimer);
    }

    this.setState({ value: '' });
    this.props.onChange('');
  };

  //
  // Render

  render() {
    const {
      name,
      placeholder,
      isDisabled
    } = this.props;

    const {
      value
    } = this.state;

    return (
      <div className={styles.searchContainer}>
        <div className={styles.searchIconContainer}>
          <Icon
            name={icons.SEARCH}
            size={14}
          />
        </div>

        <TextInput
          className={styles.searchInput}
          name={name}
          value={value}
          placeholder={placeholder}
          isDisabled={isDisabled}
          onChange={this.onChange}
        />

        <Button
          className={styles.clearButton}
          isDisabled={isDisabled || !value}
          onPress={this.onClearPress}
        >
          <Icon
            name={icons.REMOVE}
            size={14}
          />
        </Button>
      </div>
    );
  }
}

PageToolbarSearchInput.propTypes = {
  name: PropTypes.string.isRequired,
  value: PropTypes.string,
  placeholder: PropTypes.string.isRequired,
  debounce: PropTypes.number,
  isDisabled: PropTypes.bool,
  onChange: PropTypes.func.isRequired
};

PageToolbarSearchInput.defaultProps = {
  value: '',
  debounce: 300,
  isDisabled: false
};

export default PageToolbarSearchInput;
